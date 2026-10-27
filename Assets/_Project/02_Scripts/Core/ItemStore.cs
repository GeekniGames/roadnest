namespace Roadnest.Core
{
    // 수리 아이템 3종. 종류를 늘릴 땐 값을 뒤에 추가하고 TypeCount를 맞춘다
    // Three repair items. To add a type, append a value and update TypeCount
    public enum ItemType : byte { RepairTool, RepairKitLow, RepairKitHigh }

    /// <summary>
    /// 아이템 종류별 보유 개수. Unity 의존 없음.
    /// Item counts per type. No Unity dependency.
    /// </summary>
    public sealed class ItemStore
    {
        public const int TypeCount = 3;     // ItemType 값의 개수 / number of ItemType values

        readonly int[] _counts = new int[TypeCount];
        readonly int _cap;

        public ItemStore(int cap) { _cap = cap; }

        public int Get(ItemType type) => _counts[(int)type];

        // 0~cap 범위로 자른다 / clamped to 0..cap
        public void Add(ItemType type, int amount)
        {
            int v = _counts[(int)type] + amount;
            _counts[(int)type] = v < 0 ? 0 : (v > _cap ? _cap : v);
        }

        public bool TryUse(ItemType type)
        {
            if (_counts[(int)type] <= 0) return false;
            _counts[(int)type]--;
            return true;
        }

        public bool HasAny()
        {
            for (int i = 0; i < _counts.Length; i++)
                if (_counts[i] > 0) return true;
            return false;
        }
    }
}