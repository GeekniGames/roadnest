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
            for (int i = 0; i < fx.Length; i++)
            {
                int got = run.Apply(fx[i]);
                int shown = fx[i].Amount > 0 ? got : fx[i].Amount;
                if (i == 0) a0 = shown;
                else if (i == 1) a1 = shown;
                else a2 = shown;
            }
            return new ChoiceResult(idx, outcome, a0, a1, a2);
        }
    }
}