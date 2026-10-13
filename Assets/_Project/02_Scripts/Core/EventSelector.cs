using System;

namespace Roadnest.Core
{
    /// <summary>
    /// 슬롯에 맞는 이벤트를 가중치로 뽑는다. 호출 시 할당 없음.
    /// Picks an event for a slot by weight. No allocation per call.
    /// 같은 날 나온 이벤트는 제외하고, 후보가 없으면 제외 없이 다시 뽑고, 그래도 없으면 폴백.
    /// Excludes events already seen today; if none remain, retries without exclusion; then falls back.
    /// </summary>
    public sealed class EventSelector
    {
        const int StreakStepPct = 30;
        const int StreakMaxSteps = 4;
        const int DayStepPct = 5;

        readonly EventTable _table;
        readonly bool[] _usedToday;

        public EventSelector(EventTable table)
        {
            _table = table;
            _usedToday = new bool[table.Events.Length];
        }

        public void BeginDay() => Array.Clear(_usedToday, 0, _usedToday.Length);

        // 기본 가중치 x 연속 정착 % x 날짜 %. 소수 없이 정수만 쓰고, 모든 이벤트가 같은 배율(x10000)
        // base weight x streak % x day %. Integers only; every event shares the same x10000 scale
        public static long EffectiveWeight(int baseWeight, WeightMods mods, int day, int settleStreak)
        {
            int streakPct = 100;
            if ((mods & WeightMods.SettleStreak) != 0)
            {
                int steps = settleStreak - 1;
                if (steps < 0) steps = 0;
                if (steps > StreakMaxSteps) steps = StreakMaxSteps;
                streakPct += StreakStepPct * steps;
            }

            int dayPct = 100;
            if ((mods & WeightMods.Day) != 0) dayPct += DayStepPct * Math.Max(day - 1, 0);

            return (long)baseWeight * streakPct * dayPct;
        }

        // slotOrder는 하루 안에서의 순서(0부터) / position within the day, 0-based
        public EventDef Pick(ulong runSeed, int day, int slotOrder, MoveMode mode, SlotType slot, int settleStreak)
        {
            SlotCandidate[] list = _table.GetCandidates(mode, slot);
            SeededRandom rng = SeededRandom.Derive(runSeed, day, slotOrder, RngPurpose.EventPick);

            EventDef picked = Roll(list, day, settleStreak, true, ref rng);
            if (picked == null) picked = Roll(list, day, settleStreak, false, ref rng);
            if (picked == null) return _table.Fallback;

            _usedToday[picked.Index] = true;
            return picked;
        }

        EventDef Roll(SlotCandidate[] list, int day, int streak, bool excludeUsed, ref SeededRandom rng)
        {
            long total = 0;
            for (int i = 0; i < list.Length; i++)
            {
                if (!Eligible(list[i].Def, day, excludeUsed)) continue;
                total += EffectiveWeight(list[i].Weight, list[i].Def.Mods, day, streak);
            }
            if (total <= 0) return null;

            long r = rng.RangeLong(0, total);
            for (int i = 0; i < list.Length; i++)
            {
                if (!Eligible(list[i].Def, day, excludeUsed)) continue;
                r -= EffectiveWeight(list[i].Weight, list[i].Def.Mods, day, streak);
                if (r < 0) return list[i].Def;
            }
            return null;
        }

        bool Eligible(EventDef def, int day, bool excludeUsed)
            => def.IsInDayRange(day) && !(excludeUsed && _usedToday[def.Index]);
    }
}