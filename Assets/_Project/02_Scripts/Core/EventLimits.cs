namespace Roadnest.Core
{
    /// <summary>
    /// UI 칸 수에 맞춘 데이터 한도. 넘으면 로드 때 막는다.
    /// Data limits matching the UI's fixed slots; exceeding them fails at load.
    /// </summary>
    public static class EventLimits
    {
        public const int MaxNormalChoices = 4;
        public const int MaxSpecialChoices = 1;
        public const int MaxChoices = MaxNormalChoices + MaxSpecialChoices;
        public const int MaxCostEffects = 3;
        public const int MaxOutcomes = 2;
        public const int MaxOutcomeEffects = 3;
        public const int MaxChainStages = 4;        // 연계 최대 단계 / max chain stages
        public const int MaxPendingChains = 32;     // 다른날 대기 최대 수 / max next-day pending
        public const int MaxChainWeight = 1000;
        public const int MaxBoosts = 8;             // 동시에 쓰는 보정 태그 수 / distinct boost tags
        public const int MaxBoostMul = 10;          // 보정 최대 배수 / max boost multiplier
        public const int MaxBoostDays = 7;          // 보정 최대 일수 / max boost days
    }
}