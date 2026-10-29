using System;
using Roadnest.Core;

namespace Roadnest.Tests
{
    public enum BotStrategy { Random, DepartOnly, SettleOnly, Weighing }

    public readonly struct BotRunResult
    {
        public readonly int SurvivedDays;   // 죽은 날은 제외 (Day - 1) / excludes the day of death
        public readonly EndReason Reason;
        public readonly bool Deadlock;
        public readonly bool TimedOut;

        public BotRunResult(int days, EndReason reason, bool deadlock, bool timedOut)
        {
            SurvivedDays = days; Reason = reason; Deadlock = deadlock; TimedOut = timedOut;
        }
    }

    public sealed class BotStats
    {
        public int Runs, AllDead, VehicleDestroyed, Deadlocks, TimedOut;
        public double AverageDays;
        public int MedianDays, MaxDays;
    }

    /// <summary>
    /// 화면 없이 DayFlow만으로 한 판을 끝까지 자동 플레이한다. 개발용 도구라 테스트 어셈블리에 둔다.
    /// Plays a whole run through DayFlow alone, no view. Dev tool, so it lives in the test assembly.
    /// </summary>
    public static class BotSimulator
    {
        const int MaxDays = 200;        // 무한 루프 방지 / infinite-loop guard
        const int ModeStream = 100;     // 모드 결정용 난수 줄기 번호 / RNG stream id for the mode decision

        // 따지는 봇의 점수 가중치 (0.5 단위를 피하려고 2배 정수). 임의 값이며 조정 대상
        // Weighing bot score weights (doubled to stay integer). Arbitrary, meant to be tuned
        const int WFuel = 6, WFood = 4, WWater = 4, WDurability = 2, WHpPerChar = 3, WItem = 2;
        const int ReserveFuel = 2;      // 연료가 이보다 적으면 정착 / settle below this fuel
        const int LowDurability = 20;   // 내구도가 이 이하면 정착 / settle at or below this durability

        public static BotStats Simulate(EventTable table, RunRules rules, BotStrategy strategy, int runs)
        {
            var stats = new BotStats { Runs = runs };
            var days = new int[runs];
            long total = 0;

            for (int i = 0; i < runs; i++)
            {
                BotRunResult r = PlayRun(table, rules, strategy, (ulong)(i + 1));
                days[i] = r.SurvivedDays;
                total += r.SurvivedDays;

                if (r.Deadlock) stats.Deadlocks++;
                else if (r.TimedOut) stats.TimedOut++;
                else if (r.Reason == EndReason.AllDead) stats.AllDead++;
                else if (r.Reason == EndReason.VehicleDestroyed) stats.VehicleDestroyed++;
            }

            if (runs > 0)
            {
                Array.Sort(days);
                stats.AverageDays = (double)total / runs;
                stats.MedianDays = days[runs / 2];
                stats.MaxDays = days[runs - 1];
            }
            return stats;
        }

        public static BotRunResult PlayRun(EventTable table, RunRules rules, BotStrategy strategy, ulong seed)
        {
            var run = RunState.CreateDefault(rules);
            var flow = new DayFlow(run, table, seed);

            while (flow.Phase != DayPhase.Over)
            {
                if (run.Day > MaxDays) return new BotRunResult(MaxDays, EndReason.None, false, true);

                if (flow.Phase == DayPhase.ChooseMode)
                {
                    flow.ChooseMode(DecideMode(flow, strategy, seed));
                }
                else if (flow.Phase == DayPhase.Event)
                {
                    int pick = PickChoice(flow, strategy, seed);
                    if (pick < 0) return new BotRunResult(run.Day - 1, EndReason.None, true, false);
                    flow.Choose(pick);
                }
                else if (flow.Phase == DayPhase.Distribution)
                {
                    GiveRations(flow);
                    flow.ConfirmDistribution();
                }
                else
                {
                    flow.Maintain(MaintenanceChoice.Pass);
                }
            }
            return new BotRunResult(run.Day - 1, run.EndReason, false, false);
        }

        // 연료가 없으면 어떤 전략이든 정착 / with no fuel every strategy has to settle
        static MoveMode DecideMode(DayFlow flow, BotStrategy strategy, ulong seed)
        {
            if (!flow.CanDepart) return MoveMode.Settle;

            switch (strategy)
            {
                case BotStrategy.DepartOnly: return MoveMode.Depart;
                case BotStrategy.SettleOnly: return MoveMode.Settle;
                case BotStrategy.Weighing:
                    return flow.Run.Resources.Get(ResourceType.Fuel) >= ReserveFuel && flow.Run.Durability > LowDurability
                        ? MoveMode.Depart : MoveMode.Settle;
                default:
                    SeededRandom rng = SeededRandom.Derive(seed, flow.Run.Day, ModeStream, RngPurpose.Misc);
                    return rng.Chance(50) ? MoveMode.Depart : MoveMode.Settle;
            }
        }

        // 고를 수 있는 선택지가 없으면 -1 (데드락) / -1 when nothing is pickable (deadlock)
        static int PickChoice(DayFlow flow, BotStrategy strategy, ulong seed)
        {
            ChoiceDef[] list = flow.CurrentEvent.Choices;
            int available = 0;
            for (int i = 0; i < list.Length; i++)
                if (flow.GetChoiceStatus(i) == ChoiceStatus.Available) available++;
            if (available == 0) return -1;

            if (strategy == BotStrategy.Weighing) return BestScored(flow, list);

            SeededRandom rng = SeededRandom.Derive(seed, flow.Run.Day, flow.SlotIndex, RngPurpose.Misc);
            int k = rng.Range(0, available);
            for (int i = 0; i < list.Length; i++)
            {
                if (flow.GetChoiceStatus(i) != ChoiceStatus.Available) continue;
                if (k == 0) return i;
                k--;
            }
            return -1;
        }

        static int BestScored(DayFlow flow, ChoiceDef[] list)
        {
            int best = -1;
            long bestScore = long.MinValue;
            int alive = flow.Run.AliveCount;

            for (int i = 0; i < list.Length; i++)
            {
                if (flow.GetChoiceStatus(i) != ChoiceStatus.Available) continue;
                long score = Score(list[i], alive);
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        // 기대 손익 x100. 비용은 확정, 결과는 확률 가중 / expected value x100: cost is certain, outcomes weighted by chance
        static long Score(ChoiceDef c, int alive)
        {
            long score = 0;
            for (int i = 0; i < c.Cost.Length; i++) score += 100L * Value(c.Cost[i], alive);
            for (int i = 0; i < c.Outcomes.Length; i++)
            {
                OutcomeDef o = c.Outcomes[i];
                long sum = 0;
                for (int j = 0; j < o.Effects.Length; j++) sum += Value(o.Effects[j], alive);
                score += o.Percent * sum;
            }
            return score;
        }

        static int Value(Effect e, int alive)
        {
            switch (e.Stat)
            {
                case StatType.Fuel: return e.Amount * WFuel;
                case StatType.Food: return e.Amount * WFood;
                case StatType.Water: return e.Amount * WWater;
                case StatType.Durability: return e.Amount * WDurability;
                case StatType.HpAll: return e.Amount * WHpPerChar * alive;
                default: return e.Amount * WItem;   // 나머지 아이템 / all other items
            }
        }

        // 살아 있는 캐릭터에게 순서대로, 재고가 있는 만큼 나눠 준다 / feed living characters in order while stock lasts
        static void GiveRations(DayFlow flow)
        {
            for (int i = 0; i < flow.Run.Characters.Length; i++)
            {
                flow.SetRation(i, ResourceType.Food, true);
                flow.SetRation(i, ResourceType.Water, true);
            }
        }
    }
}