using System;

namespace Roadnest.Core
{
    public enum DayPhase : byte { ChooseMode, Event, Distribution, Over }

    /// <summary>
    /// 하루 진행 상태 머신: 모드 선택 → 이벤트 슬롯 → 분배/정산 → 다음 날. 화면과 무관한 순수 C#.
    /// Day state machine: pick mode → event slots → distribution/settlement → next day. Pure C#, no view.
    /// </summary>
    public sealed class DayFlow
    {
        readonly EventTable _table;
        readonly EventSelector _selector;
        readonly ulong _seed;
        readonly bool[] _food;
        readonly bool[] _water;
        SlotType[] _slots;
        int _foodGiven;
        int _waterGiven;

        public RunState Run { get; }
        public DayPhase Phase { get; private set; }
        public MoveMode Mode { get; private set; }
        public int SlotIndex { get; private set; }
        public int SlotCount => _slots.Length;
        public EventDef CurrentEvent { get; private set; }
        public bool CanDepart => Run.Resources.Get(ResourceType.Fuel) >= Run.Rules.DepartFuelCost;

        public DayFlow(RunState run, EventTable table, ulong runSeed)
        {
            int perDay = run.Rules.EventsPerDay;
            if (table.GetDaySlots(MoveMode.Depart).Length != perDay || table.GetDaySlots(MoveMode.Settle).Length != perDay)
                throw new ArgumentException("daySlots 개수와 EventsPerDay가 다름 / daySlots length differs from EventsPerDay");

            Run = run;
            _table = table;
            _seed = runSeed;
            _selector = new EventSelector(table);
            _slots = table.GetDaySlots(MoveMode.Depart);
            _food = new bool[run.Characters.Length];
            _water = new bool[run.Characters.Length];
            Phase = run.IsOver ? DayPhase.Over : DayPhase.ChooseMode;
        }

        // 출발/정착 선택. 연료가 없으면 출발 불가 / pick depart or settle; depart needs fuel
        public bool ChooseMode(MoveMode mode)
        {
            if (Phase != DayPhase.ChooseMode) return false;
            if (mode == MoveMode.Depart && !CanDepart) return false;

            RunRules rules = Run.Rules;
            Mode = mode;
            _slots = _table.GetDaySlots(mode);
            Run.RegisterMove(mode);

            if (mode == MoveMode.Depart)
            {
                Run.Apply(new Effect(StatType.Fuel, -rules.DepartFuelCost));
                Run.Apply(new Effect(StatType.Durability, -rules.DepartDurabilityLoss));
            }
            else
            {
                Run.Apply(new Effect(StatType.Durability, rules.SettleDurabilityGain));
            }

            if (Run.IsOver) { Phase = DayPhase.Over; return true; }

            _selector.BeginDay();
            SlotIndex = 0;
            Phase = DayPhase.Event;
            CurrentEvent = PickCurrent();
            return true;
        }

        public ChoiceStatus GetChoiceStatus(int choiceIndex)
            => Phase == DayPhase.Event
                ? ChoiceResolver.GetStatus(Run, CurrentEvent.Choices[choiceIndex])
                : ChoiceStatus.Disabled;

        // 선택 가능한 선택지가 하나라도 있는가 / is at least one choice playable
        public bool HasPlayableChoice()
        {
            if (Phase != DayPhase.Event) return false;
            ChoiceDef[] list = CurrentEvent.Choices;
            for (int i = 0; i < list.Length; i++)
                if (ChoiceResolver.GetStatus(Run, list[i]) == ChoiceStatus.Available) return true;
            return false;
        }

        // 선택 후 다음 슬롯으로 넘어간다. 반환값은 방금 선택의 결과
        // Moves to the next slot after the pick; returns the result of the pick just made
        public ChoiceResult Choose(int choiceIndex)
        {
            if (Phase != DayPhase.Event || (uint)choiceIndex >= (uint)CurrentEvent.Choices.Length) return default;

            ChoiceResult r = ChoiceResolver.Resolve(Run, CurrentEvent.Choices[choiceIndex], _seed, Run.Day, SlotIndex);
            if (!r.Success) return r;

            if (Run.IsOver)
            {
                Phase = DayPhase.Over;
                CurrentEvent = null;
                return r;
            }

            SlotIndex++;
            if (SlotIndex >= _slots.Length)
            {
                Phase = DayPhase.Distribution;
                CurrentEvent = null;
            }
            else CurrentEvent = PickCurrent();
            return r;
        }

        // 캐릭터에게 식량/물 1개를 주거나 회수. 재고보다 많이 줄 수 없다
        // Give or take back 1 food/water for a character; cannot give more than the stock
        public bool SetRation(int charIndex, ResourceType type, bool give)
        {
            if (Phase != DayPhase.Distribution || type == ResourceType.Fuel) return false;
            if ((uint)charIndex >= (uint)_food.Length || !Run.Characters[charIndex].IsAlive) return false;

            bool isFood = type == ResourceType.Food;
            bool[] arr = isFood ? _food : _water;
            if (arr[charIndex] == give) return true;

            int given = isFood ? _foodGiven : _waterGiven;
            if (give && given >= Run.Resources.Get(type)) return false;

            arr[charIndex] = give;
            if (isFood) _foodGiven += give ? 1 : -1;
            else _waterGiven += give ? 1 : -1;
            return true;
        }

        public bool GetRation(int charIndex, ResourceType type)
            => type == ResourceType.Food ? _food[charIndex] : _water[charIndex];

        // 현재 분배 상태 기준으로 이 캐릭터가 확정 시 잃을 HP. 미리보기와 확정이 같은 값을 쓴다
        // HP this character would lose on confirm with the current rations; preview and confirm share it
        public int GetHpLoss(int charIndex)
        {
            if (Phase != DayPhase.Distribution || (uint)charIndex >= (uint)_food.Length) return 0;
            if (!Run.Characters[charIndex].IsAlive) return 0;

            bool f = _food[charIndex], w = _water[charIndex];
            if (f && w) return 0;
            RunRules rules = Run.Rules;
            return (!f && !w) ? rules.HpLossMissingBoth : rules.HpLossMissingOne;
        }

        // 분배 확정 → 소비 → HP 감소 → 생존 확인 → 수리 → Day+1
        // Confirm: consume → HP loss → survival check → repair → Day+1
        public bool ConfirmDistribution()
        {
            if (Phase != DayPhase.Distribution) return false;
            RunRules rules = Run.Rules;

            if (_foodGiven > 0) Run.Apply(new Effect(StatType.Food, -_foodGiven));
            if (_waterGiven > 0) Run.Apply(new Effect(StatType.Water, -_waterGiven));

            for (int i = 0; i < Run.Characters.Length; i++)
            {
                int loss = GetHpLoss(i);
                if (loss > 0) Run.DamageCharacter(i, loss);
            }

            Array.Clear(_food, 0, _food.Length);
            Array.Clear(_water, 0, _water.Length);
            _foodGiven = 0;
            _waterGiven = 0;

            // 종료 시 Day 유지, 수리 없음 / on game over: keep Day, skip repair
            if (Run.IsOver) { Phase = DayPhase.Over; return true; }

            int repair = Mode == MoveMode.Settle ? rules.SettleRepairAmount : rules.RepairAmount;
            Run.Apply(new Effect(StatType.Durability, repair));
            Run.AdvanceDay();
            Phase = DayPhase.ChooseMode;
            return true;
        }

        EventDef PickCurrent()
            => _selector.Pick(_seed, Run.Day, SlotIndex, Mode, _slots[SlotIndex], Run.ConsecutiveSettles);
    }
}