using System;

namespace Roadnest.Core
{
    /// <summary>
    /// 슬롯에 맞는 이벤트를 가중치로 뽑는다. 호출 시 할당 없음.
    /// Picks an event for a slot by weight. No allocation per call.
    /// 연계(같은날 → 다른날) 먼저, 없으면 일반 후보. 같은 날 나온 이벤트는 제외하고, 없으면 제외 없이, 그래도 없으면 폴백.
    /// Chains first (same-day, then next-day), else normal candidates. Excludes events seen today, then retries, then falls back.
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

        public EventDef Pick(ulong runSeed, int day, int slotOrder, MoveMode mode, SlotType slot, int settleStreak)
            => Pick(runSeed, day, slotOrder, mode, slot, settleStreak, null);

        // slotOrder는 하루 안에서의 순서(0부터) / position within the day, 0-based
        public EventDef Pick(ulong runSeed, int day, int slotOrder, MoveMode mode, SlotType slot,
                             int settleStreak, ChainState chains)
        {
            if (chains != null)
            {
                EventDef chained = PickChained(runSeed, day, slotOrder, mode, chains);
                if (chained != null)
                {
                    _usedToday[chained.Index] = true;
                    return chained;
                }
            }

            SlotCandidate[] list = _table.GetCandidates(mode, slot);
            SeededRandom rng = SeededRandom.Derive(runSeed, day, slotOrder, RngPurpose.EventPick);

            EventDef picked = Roll(list, day, slotOrder, settleStreak, true, ref rng);
            if (picked == null) picked = Roll(list, day, slotOrder, settleStreak, false, ref rng);
            if (picked == null) return _table.Fallback;

            _usedToday[picked.Index] = true;
            return picked;
        }

        EventDef PickChained(ulong runSeed, int day, int slotOrder, MoveMode mode, ChainState chains)
        {
            // 같은날 연계: 이 슬롯에서 한 번만 굴리고 끝 / same-day: one roll at this slot, then gone
            int sameIdx = chains.TakeSameDay(out int percent);
            if (sameIdx >= 0)
            {
                SeededRandom sr = SeededRandom.Derive(runSeed, day, slotOrder, RngPurpose.ChainSameDay);
                if (sr.Range(0, 100) < percent) return _table.Events[sameIdx];
            }

            if (chains.PendingCount == 0 || chains.NextDayPermille <= 0) return null;

            long total = 0;
            for (int i = 0; i < chains.PendingCount; i++)
                if (PendingEligible(chains, i, day, slotOrder, mode)) total += chains.WeightAt(i);
            if (total <= 0) return null;

            SeededRandom rng = SeededRandom.Derive(runSeed, day, slotOrder, RngPurpose.ChainNextDay);
            if (rng.Range(0, 1000) >= chains.NextDayPermille) return null;

            long r = rng.RangeLong(0, total);
            for (int i = 0; i < chains.PendingCount; i++)
            {
                if (!PendingEligible(chains, i, day, slotOrder, mode)) continue;
                r -= chains.WeightAt(i);
                if (r < 0)
                {
                    EventDef def = _table.Events[chains.EventAt(i)];
                    chains.RemoveAt(i);
                    return def;
                }
            }
            return null;
        }

        // 만들어진 날에는 나오지 않는다(다음 날부터) / not eligible on the day it was created
        bool PendingEligible(ChainState chains, int i, int day, int slotOrder, MoveMode mode)
        {
            if (chains.DayAt(i) >= day) return false;
            EventDef d = _table.Events[chains.EventAt(i)];
            return d.HasMode(mode) && d.IsInDayRange(day) && d.IsInSlotRange(slotOrder);
        }

        EventDef Roll(SlotCandidate[] list, int day, int slotOrder, int streak, bool excludeUsed, ref SeededRandom rng)
        {
            long total = 0;
            for (int i = 0; i < list.Length; i++)
            {
                if (!Eligible(list[i].Def, day, slotOrder, excludeUsed)) continue;
                total += EffectiveWeight(list[i].Weight, list[i].Def.Mods, day, streak);
            }
            if (total <= 0) return null;

            long r = rng.RangeLong(0, total);
            for (int i = 0; i < list.Length; i++)
            {
                if (!Eligible(list[i].Def, day, slotOrder, excludeUsed)) continue;
                r -= EffectiveWeight(list[i].Weight, list[i].Def.Mods, day, streak);
                if (r < 0) return list[i].Def;
            }
            return null;
        }

        bool Eligible(EventDef def, int day, int slotOrder, bool excludeUsed)
            => def.IsInDayRange(day) && def.IsInSlotRange(slotOrder) && !(excludeUsed && _usedToday[def.Index]);
    }
}