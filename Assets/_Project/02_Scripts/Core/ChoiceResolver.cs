namespace Roadnest.Core
{
    public enum ChoiceStatus : byte { Hidden, Disabled, Available }

    /// <summary>선택 결과. Success가 false면 아무 일도 일어나지 않은 것.</summary>
    /// <summary>Result of a pick. Success false means nothing happened.</summary>
    public readonly struct ChoiceResult
    {
        public readonly bool Success;
        public readonly int OutcomeIndex;
        public readonly OutcomeDef Outcome;

        public ChoiceResult(int outcomeIndex, OutcomeDef outcome)
        {
            Success = true; OutcomeIndex = outcomeIndex; Outcome = outcome;
        }
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
            run.Apply(outcome.Effects);
            return new ChoiceResult(idx, outcome);
        }
    }
}