namespace Roadnest.Core
{
    /// <summary>
    /// 연계 진행 상태. 같은날 연계는 1개, 다른날 연계는 이벤트당 1개만 대기. 고정 배열이라 할당 없음.
    /// Chain progress. One same-day link; next-day pending is unique per event. Fixed arrays, no allocation.
    /// </summary>
    public sealed class ChainState
    {
        public readonly int NextDayPermille;    // 1000분율 / per-mille chance per slot

        readonly int[] _event = new int[EventLimits.MaxPendingChains];
        readonly int[] _weight = new int[EventLimits.MaxPendingChains];
        readonly int[] _day = new int[EventLimits.MaxPendingChains];

        public int SameDayEvent { get; private set; } = -1;
        public int SameDayPercent { get; private set; }
        public int PendingCount { get; private set; }

        public ChainState(int nextDayPermille) { NextDayPermille = nextDayPermille; }

        public void SetSameDay(int eventIndex, int percent)
        {
            SameDayEvent = eventIndex;
            SameDayPercent = percent;
        }

        // 꺼내면서 비운다. 없으면 -1 / returns and clears; -1 if none
        public int TakeSameDay(out int percent)
        {
            int e = SameDayEvent;
            percent = SameDayPercent;
            SameDayEvent = -1;
            SameDayPercent = 0;
            return e;
        }

        public void ClearSameDay() { SameDayEvent = -1; SameDayPercent = 0; }

        // 이미 대기 중이면 유지하고 false / keeps the existing one and returns false
        public bool AddPending(int eventIndex, int weight, int day)
        {
            for (int i = 0; i < PendingCount; i++)
                if (_event[i] == eventIndex) return false;
            if (PendingCount >= _event.Length) return false;

            _event[PendingCount] = eventIndex;
            _weight[PendingCount] = weight;
            _day[PendingCount] = day;
            PendingCount++;
            return true;
        }

        public int EventAt(int i) => _event[i];
        public int WeightAt(int i) => _weight[i];
        public int DayAt(int i) => _day[i];

        // 순서를 유지하며 제거 / removes while keeping order
        public void RemoveAt(int i)
        {
            for (int k = i + 1; k < PendingCount; k++)
            {
                _event[k - 1] = _event[k];
                _weight[k - 1] = _weight[k];
                _day[k - 1] = _day[k];
            }
            PendingCount--;
        }
    }
}