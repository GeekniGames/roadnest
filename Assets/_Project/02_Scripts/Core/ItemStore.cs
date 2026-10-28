namespace Roadnest.Core
{
    // 수리 아이템 3종. 종류를 늘릴 땐 ItemKind와 TypeCount를 같이 맞춘다
    // Three repair items. When adding a type, update ItemKind and TypeCount together
    public enum ItemType : byte { RepairTool, RepairKitLow, RepairKitHigh }

    /// <summary>
    /// 아이템을 Inventory 위에서 읽고 쓰는 창구. Unity 의존 없음.
    /// Front for items on top of Inventory. No Unity dependency.
    /// </summary>
    public sealed class ItemStore
    {
        public const int TypeCount = 3;

        readonly Inventory _inv;

        public ItemStore(Inventory inv) { _inv = inv; }

        static ItemKind Kind(ItemType t) => (ItemKind)((int)t + Inventory.ItemKindOffset);

        public int Get(ItemType type) => _inv.Get(Kind(type));

        // 실제로 적용된 변화량 반환 / returns the applied delta
        public int Add(ItemType type, int amount)
            => amount >= 0 ? _inv.Add(Kind(type), amount) : -_inv.Remove(Kind(type), -amount);

        public bool TryUse(ItemType type) => _inv.Remove(Kind(type), 1) == 1;

        public bool HasAny()
        {
            for (int i = 0; i < TypeCount; i++)
                if (_inv.Get((ItemKind)(i + Inventory.ItemKindOffset)) > 0) return true;
            return false;
        }
    }
}