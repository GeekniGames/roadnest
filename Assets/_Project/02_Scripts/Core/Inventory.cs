using System;

namespace Roadnest.Core
{
    // 인벤토리에 들어가는 모든 종류. 앞 3개는 ResourceType, 뒤 3개는 ItemType과 같은 순서
    // Every kind the inventory holds. First 3 match ResourceType, last 3 match ItemType
    public enum ItemKind : byte { Fuel, Food, Water, RepairTool, RepairKitLow, RepairKitHigh }

    /// <summary>
    /// 칸 단위 보관함. 칸마다 (종류, 개수)를 갖고, 비면 사라지며 뒤 칸이 당겨진다. Unity 의존 없음.
    /// Cell-based storage. Each cell holds (kind, count); empty cells vanish and later ones shift forward. No Unity dependency.
    /// </summary>
    public sealed class Inventory
    {
        public const int KindCount = 6;
        public const int MaxSlots = 30;         // 업그레이드 상한 / upgrade ceiling
        public const int ItemKindOffset = 3;    // ItemType -> ItemKind

        readonly byte[] _kind = new byte[MaxSlots];
        readonly int[] _count = new int[MaxSlots];
        readonly int[] _total = new int[KindCount];
        int _used;

        public int SlotCount { get; private set; }
        public int StackCap { get; private set; }
        public int UsedSlots => _used;

        // 연산 하나당 최대 한 번 / at most once per operation
        public event Action Changed;

        public Inventory(int slotCount, int stackCap)
        {
            SlotCount = slotCount < 1 ? 1 : (slotCount > MaxSlots ? MaxSlots : slotCount);
            StackCap = stackCap < 1 ? 1 : stackCap;
        }

        public ItemKind KindAt(int cell) => (ItemKind)_kind[cell];
        public int CountAt(int cell) => _count[cell];
        public int Get(ItemKind kind) => _total[(int)kind];

        // 덜 찬 칸(앞쪽부터) -> 뒤에 새 칸. 실제로 들어간 개수를 반환
        // Fill partial cells (earliest first), then new cells at the end. Returns the amount that fit
        public int Add(ItemKind kind, int amount)
        {
            if (amount <= 0) return 0;
            int k = (int)kind, left = amount;

            for (int i = 0; i < _used && left > 0; i++)
            {
                if (_kind[i] != k) continue;
                int room = StackCap - _count[i];
                if (room <= 0) continue;
                int put = left < room ? left : room;
                _count[i] += put;
                left -= put;
            }
            while (left > 0 && _used < SlotCount)
            {
                int put = left < StackCap ? left : StackCap;
                _kind[_used] = (byte)k;
                _count[_used] = put;
                _used++;
                left -= put;
            }

            int added = amount - left;
            if (added > 0)
            {
                _total[k] += added;
                Changed?.Invoke();
            }
            return added;
        }

        // 같은 종류의 맨 뒤 칸부터 뺀다. 빈 칸은 제거되고 뒤 칸이 당겨진다. 실제로 뺀 개수 반환
        // Take from the last cell of that kind first. Emptied cells are removed and later ones shift. Returns the amount removed
        public int Remove(ItemKind kind, int amount)
        {
            if (amount <= 0) return 0;
            int k = (int)kind, left = amount;

            for (int i = _used - 1; i >= 0 && left > 0; i--)
            {
                if (_kind[i] != k) continue;
                int take = left < _count[i] ? left : _count[i];
                _count[i] -= take;
                left -= take;
                if (_count[i] == 0) RemoveCellAt(i);
            }

            int removed = amount - left;
            if (removed > 0)
            {
                _total[k] -= removed;
                Changed?.Invoke();
            }
            return removed;
        }

        // 한 칸 최대치 업그레이드. 같은 종류는 앞 칸으로 합쳐지고 빈 칸은 없어진다 (올리는 것만 가능)
        // Stack-cap upgrade. Same kinds merge into earlier cells and emptied cells disappear (increase only)
        public void SetStackCap(int cap)
        {
            if (cap <= StackCap) return;
            StackCap = cap;

            for (int i = 0; i < _used; i++)
            {
                if (_count[i] == 0) continue;
                for (int j = i + 1; j < _used && _count[i] < StackCap; j++)
                {
                    if (_kind[j] != _kind[i] || _count[j] == 0) continue;
                    int room = StackCap - _count[i];
                    int move = _count[j] < room ? _count[j] : room;
                    _count[i] += move;
                    _count[j] -= move;
                }
            }

            int w = 0;
            for (int r = 0; r < _used; r++)
            {
                if (_count[r] == 0) continue;
                if (w != r) { _kind[w] = _kind[r]; _count[w] = _count[r]; }
                w++;
            }
            for (int r = w; r < _used; r++) { _kind[r] = 0; _count[r] = 0; }
            _used = w;
            Changed?.Invoke();
        }

        // 칸 수 업그레이드. 빈 칸만 늘어난다 / slot upgrade, only adds empty cells
        public void SetSlotCount(int count)
        {
            if (count <= SlotCount) return;
            SlotCount = count > MaxSlots ? MaxSlots : count;
            Changed?.Invoke();
        }

        // 화면용 사본 만들기. 이벤트는 호출하지 않는다 / for display copies; raises no event
        public void CopyFrom(Inventory src)
        {
            SlotCount = src.SlotCount;
            StackCap = src.StackCap;
            _used = src._used;
            Array.Copy(src._kind, _kind, MaxSlots);
            Array.Copy(src._count, _count, MaxSlots);
            Array.Copy(src._total, _total, KindCount);
        }

        void RemoveCellAt(int i)
        {
            int tail = _used - i - 1;
            if (tail > 0)
            {
                Array.Copy(_kind, i + 1, _kind, i, tail);
                Array.Copy(_count, i + 1, _count, i, tail);
            }
            _used--;
            _kind[_used] = 0;
            _count[_used] = 0;
        }
    }
}