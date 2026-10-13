namespace Roadnest.Core
{
    /// <summary>
    /// 시드 기반 난수 (SplitMix64). 정수 연산만 써서 기기와 무관하게 결과가 같다.
    /// Seeded RNG (SplitMix64). Integer math only, so results match on every device.
    /// 값 타입이므로 복사하지 말고 로컬 변수 하나로 이어서 쓸 것.
    /// Value type: keep one local instance, do not copy mid-sequence.
    /// </summary>
    public struct SeededRandom
    {
        const ulong Golden = 0x9E3779B97F4A7C15UL;
        ulong _state;

        public SeededRandom(ulong seed) { _state = seed; }

        // (런 시드, Day, 슬롯, 용도)마다 독립된 스트림 / independent stream per (seed, day, slot, purpose)
        public static SeededRandom Derive(ulong runSeed, int day, int slot, RngPurpose purpose)
        {
            ulong h = Mix(runSeed);
            h = Mix(h ^ (uint)day);
            h = Mix(h ^ (uint)slot);
            h = Mix(h ^ (uint)purpose);
            return new SeededRandom(h);
        }

        public ulong Next()
        {
            _state += Golden;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        // min 이상 max 미만 / [min, max)
        public int Range(int minInclusive, int maxExclusive)
        {
            int span = maxExclusive - minInclusive;
            if (span <= 1) return minInclusive;
            return minInclusive + (int)(((Next() >> 32) * (ulong)span) >> 32);
        }

        public bool Chance(int percent) => Range(0, 100) < percent;

        static ulong Mix(ulong x)
        {
            var r = new SeededRandom(x);
            return r.Next();
        }
    }
}