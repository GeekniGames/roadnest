namespace Roadnest.Core
{
    public enum ResourceType : byte { Fuel, Food, Water }

    /// <summary>
    /// 자원 3종을 Inventory 위에서 읽고 쓰는 창구. 값은 Inventory가 갖는다.
    /// Front for the three resources on top of Inventory. The values live in Inventory.
    /// </summary>
    public sealed class ResourceStore
    {
        public const int Count = 3;

        readonly Inventory _inv;

        public ResourceStore(Inventory inv) { _inv = inv; }

        static ItemKind Kind(ResourceType t) => (ItemKind)(int)t;

        public int Get(ResourceType t) => _inv.Get(Kind(t));
        public bool Has(ResourceType t, int amount) => _inv.Get(Kind(t)) >= amount;

        // 실제로 적용된 변화량 반환. 공간이 부족하면 덜 들어간다 / returns the applied delta; less if space runs out
        public int Add(ResourceType t, int delta)
            => delta >= 0 ? _inv.Add(Kind(t), delta) : -_inv.Remove(Kind(t), -delta);

        public bool TryConsume(ResourceType t, int amount)
        {
            if (amount < 0 || _inv.Get(Kind(t)) < amount) return false;
            _inv.Remove(Kind(t), amount);
            return true;
        }
    }
}