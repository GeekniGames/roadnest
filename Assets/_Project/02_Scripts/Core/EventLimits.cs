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
    }
}