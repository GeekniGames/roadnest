namespace Roadnest.Core
{
    /// <summary>
    /// 한 판의 상태. 자원, 내구도, 캐릭터 HP를 관리하고 효과를 즉시 적용하며 종료를 판정한다.
    /// State of one run. Holds resources, durability, character HP; applies effects and checks the end.
    /// </summary>
    public sealed class RunState
    {
        public readonly RunRules Rules;
        public readonly ResourceStore Resources;
        public readonly CharacterState[] Characters;

        public int Day { get; private set; } = 1;
        public int ConsecutiveSettles { get; private set; }
        public int Durability { get; private set; }
        public int AliveCount { get; private set; }
        public EndReason EndReason { get; private set; }
        public bool IsOver => EndReason != EndReason.None;

        public RunState(RunRules rules, ResourceStore resources, CharacterState[] characters)
        {
            Rules = rules;
            Resources = resources;
            Characters = characters;
            Resources.Reset(rules.StartFuel, rules.StartFood, rules.StartWater);
            Durability = rules.MaxDurability;
            RecountAlive();
        }

        // 임시 시작 캐릭터 1명. 나중에 데이터로 교체 / temporary single starting character, to be data-driven later
        public static RunState CreateDefault(RunRules rules, ResourceStore resources)
        {
            var chars = new[] { new CharacterState("char_mechanic", "mechanic", rules.MaxCharacterHp) };
            return new RunState(rules, resources, chars);
        }

        public void AdvanceDay() => Day++;

        // 출발이면 연속 정착 0, 정착이면 +1 / depart resets the settle streak, settle adds 1
        public void RegisterMove(MoveMode mode) => ConsecutiveSettles = mode == MoveMode.Settle ? ConsecutiveSettles + 1 : 0;

        // 캐릭터 한 명의 HP 감소. 죽은 캐릭터는 무시 / damage one character; dead ones ignored
        public void DamageCharacter(int index, int amount)
        {
            CharacterState c = Characters[index];
            if (!c.IsAlive) return;
            c.Hp = Clamp(c.Hp - amount, 0, c.MaxHp);
            RecountAlive();
            if (AliveCount == 0) End(EndReason.AllDead);
        }

        // 효과는 전부 적용하고, 종료 사유는 가장 먼저 발생한 것만 기록 / all effects apply; first end reason wins
        public void Apply(Effect e)
        {
            if (e.Stat <= StatType.Water) ApplyResource((ResourceType)(int)e.Stat, e.Amount);
            else if (e.Stat == StatType.Durability) ApplyDurability(e.Amount);
            else ApplyHpAll(e.Amount);
        }

        public void Apply(Effect[] effects)
        {
            for (int i = 0; i < effects.Length; i++) Apply(effects[i]);
        }

        // 비용을 낼 수 있는가. 자원만 부족 판정 / can the cost be paid; only resources can run short
        public bool CanPay(Effect[] cost)
        {
            for (int i = 0; i < cost.Length; i++)
            {
                Effect c = cost[i];
                if (c.Stat <= StatType.Water && Resources.Get((ResourceType)(int)c.Stat) + c.Amount < 0)
                    return false;
            }
            return true;
        }

        public bool MeetsRequirements(Requirement[] reqs)
        {
            for (int i = 0; i < reqs.Length; i++)
            {
                if (!reqs[i].IsMet(GetStat(reqs[i].Stat))) return false;
            }
            return true;
        }

        // hp_all은 조건에 쓸 수 없고 EventTable이 검증한다 / hp_all is rejected in requirements by EventTable
        public int GetStat(StatType stat)
        {
            if (stat <= StatType.Water) return Resources.Get((ResourceType)(int)stat);
            return stat == StatType.Durability ? Durability : 0;
        }

        void ApplyResource(ResourceType type, int amount)
        {
            int cur = Resources.Get(type);
            int delta = Clamp(cur + amount, 0, Rules.ResourceCap) - cur;
            if (delta != 0) Resources.Add(type, delta);
        }

        void ApplyDurability(int amount)
        {
            Durability = Clamp(Durability + amount, 0, Rules.MaxDurability);
            if (Durability == 0) End(EndReason.VehicleDestroyed);
        }

        // 죽은 캐릭터는 회복되지 않는다 / dead characters are never healed
        void ApplyHpAll(int amount)
        {
            for (int i = 0; i < Characters.Length; i++)
            {
                CharacterState c = Characters[i];
                if (!c.IsAlive) continue;
                c.Hp = Clamp(c.Hp + amount, 0, c.MaxHp);
            }
            RecountAlive();
            if (AliveCount == 0) End(EndReason.AllDead);
        }

        void RecountAlive()
        {
            int n = 0;
            for (int i = 0; i < Characters.Length; i++)
                if (Characters[i].IsAlive) n++;
            AliveCount = n;
        }

        void End(EndReason reason)
        {
            if (EndReason == EndReason.None) EndReason = reason;
        }

        static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }
}