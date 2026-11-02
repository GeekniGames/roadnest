namespace Roadnest.Core
{
    public enum ChoiceStatus : byte { Hidden, Disabled, Available }

    /// <summary>선택 결과. Success가 false면 아무 일도 일어나지 않은 것.</summary>
    /// <summary>Result of a pick. Success false means nothing happened.</summary>
    public readonly struct ChoiceResult
    {
        // 결과 효과 한도(EventLimits.MaxOutcomeEffects)와 맞춰야 한다 / must cover EventLimits.MaxOutcomeEffects
        public const int AppliedSlots = 3;

        public readonly bool Success;
        public readonly int OutcomeIndex;
        public readonly OutcomeDef Outcome;
        readonly int _a0, _a1, _a2;

        public ChoiceResult(int outcomeIndex, OutcomeDef outcome, int a0, int a1, int a2)
        {
            Success = true; OutcomeIndex = outcome == null ? outcomeIndex : outcomeIndex; Outcome = outcome;
            _a0 = a0; _a1 = a1; _a2 = a2;
        }

        // i번째 효과로 화면에 보여줄 수치. 자원 획득은 실제 들어간 양, 나머지는 계획값
        // Amount to display for effect i: actual amount for resource gains, planned otherwise
        public int GetApplied(int i) => i == 0 ? _a0 : (i == 1 ? _a1 : _a2);
    }

    /// <summary>
    /// 선택지 상태 판정과 해결(비용 지불, 확률 굴림, 효과 적용). 호출 시 할당 없음.
    /// Choice status check and resolution (pay cost, roll, apply effects). No allocation per call.
    /// </summary>
    public static class ChoiceResolver
    {
        const int NotPicked = -2;

        // hidden은 조건을 만족해야 등장, 비용 부족은 보이되 선택 불가
        // hidden appears only when requirements are met; unaffordable stays visible but disabled
        public static ChoiceStatus GetStatus(RunState run, ChoiceDef choice)
        {
            bool reqOk = run.MeetsRequirements(choice.Requirements);
            if (choice.Hidden && !reqOk) return ChoiceStatus.Hidden;
            if (!reqOk || !run.CanPay(choice.Cost)) return ChoiceStatus.Disabled;
            return ChoiceStatus.Available;
        }

        // slotOrder는 EventSelector.Pick에 쓴 값과 같게 / use the same slotOrder passed to EventSelector.Pick
        public static ChoiceResult Resolve(RunState run, ChoiceDef choice, ulong runSeed, int day, int slotOrder)
        {
            if (run.IsOver || GetStatus(run, choice) != ChoiceStatus.Available) return default;

            run.Apply(choice.Cost);

            SeededRandom rng = SeededRandom.Derive(runSeed, day, slotOrder, RngPurpose.Outcome);
            int roll = rng.Range(0, 100);

            OutcomeDef[] outcomes = choice.Outcomes;
            int idx = 0;
            while (idx < outcomes.Length - 1 && roll >= outcomes[idx].CumulativeEnd) idx++;

            OutcomeDef outcome = outcomes[idx];
            Effect[] fx = outcome.Effects;
            int a0 = 0, a1 = 0, a2 = 0;
            int hpTarget = NotPicked;   // hp_one은 한 결과 안에서 같은 1명에게 몰린다 / hp_one effects in one outcome share a target
            for (int i = 0; i < fx.Length; i++)
            {
                Effect e = fx[i];
                int shown = e.Amount;
                if (e.Stat == StatType.HpOne)
                {
                    if (hpTarget == NotPicked) hpTarget = PickHpTarget(run, runSeed, day, slotOrder);
                    if (hpTarget >= 0) run.ChangeHp(hpTarget, e.Amount);
                }
                else
                {
                    int got = run.Apply(e);
                    if (e.Amount > 0) shown = got;
                }

                if (i == 0) a0 = shown;
                else if (i == 1) a1 = shown;
                else a2 = shown;
            }
            return new ChoiceResult(idx, outcome, a0, a1, a2);
        }

        // 살아 있는 캐릭터 중 무작위 1명, 없으면 -1 / random living character, -1 if none
        static int PickHpTarget(RunState run, ulong runSeed, int day, int slotOrder)
        {
            if (run.AliveCount == 0) return -1;
            SeededRandom r = SeededRandom.Derive(runSeed, day, slotOrder, RngPurpose.HpTarget);
            return run.NthAlive(r.Range(0, run.AliveCount));
        }
    }
}