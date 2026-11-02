namespace Roadnest.Core
{
    /// <summary>
    /// 태그별 등장 확률 보정. 같은 태그는 한 칸을 쓰고, 배수는 큰 쪽, 기간은 늘어나는 쪽을 따른다. 고정 배열이라 할당 없음.
    /// Per-tag chance boost. One slot per tag; the larger multiplier and the later end day win. Fixed arrays, no allocation.
    /// </summary>
    public sealed class BoostState
    {
        readonly int[] _tag = new int[EventLimits.MaxBoosts];
        readonly int[] _mul = new int[EventLimits.MaxBoosts];
        readonly int[] _lastDay = new int[EventLimits.MaxBoosts];   // 이 날까지 유효 / valid through this day

        public int Count { get; private set; }

        // days는 today를 포함한 일수 / days counts today
        public bool Add(int tagId, int mul, int today, int days)
        {
            int last = today + days - 1;

            for (int i = 0; i < Count; i++)
            {
                if (_tag[i] != tagId) continue;
                if (_lastDay[i] < today) { _mul[i] = mul; _lastDay[i] = last; }
                else
                {
                    if (mul > _mul[i]) _mul[i] = mul;
                    if (last > _lastDay[i]) _lastDay[i] = last;
                }
                return true;
            }

            // 끝난 칸 재사용 / reuse an expired slot
            for (int i = 0; i < Count; i++)
            {
                if (_lastDay[i] >= today) continue;
                _tag[i] = tagId; _mul[i] = mul; _lastDay[i] = last;
                return true;
            }

            if (Count >= _tag.Length) return false;
            _tag[Count] = tagId; _mul[Count] = mul; _lastDay[Count] = last;
            Count++;
            return true;
        }

        // 이벤트 태그들에 걸린 배수 중 가장 큰 값, 없으면 1 / largest multiplier hitting the event's tags, 1 if none
        public int MultiplierFor(int[] tagIds, int day)
        {
            int best = 1;
            for (int i = 0; i < Count; i++)
            {
                if (_lastDay[i] < day || _mul[i] <= best) continue;
                for (int t = 0; t < tagIds.Length; t++)
                {
                    if (tagIds[t] != _tag[i]) continue;
                    best = _mul[i];
                    break;
                }
            }
            return best;
        }
    }
}