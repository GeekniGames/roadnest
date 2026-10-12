using System;

namespace Roadnest.Core
{
    public enum ResourceType : byte { Fuel, Food, Water }

    /// <summary>
    /// 자원 값 보관. 화면과 무관한 순수 C#.
    /// Holds resource values. Pure C#, no view dependency.
    /// </summary>
    public sealed class ResourceStore
    {
        public const int Count = 3;

        readonly int[] _value = new int[Count];
        readonly int _cap;

        // (종류, 이전 값, 새 값) / (type, old value, new value)
        public event Action<ResourceType, int, int> Changed;

        public ResourceStore(int cap) { _cap = cap; }

        public int Get(ResourceType t) => _value[(int)t];
        public bool Has(ResourceType t, int amount) => _value[(int)t] >= amount;

        public void Reset(int fuel, int food, int water)
        {
            Set(ResourceType.Fuel, fuel);
            Set(ResourceType.Food, food);
            Set(ResourceType.Water, water);
        }

        // 실제로 적용된 변화량 반환 (상한/0 클램프). / Returns the applied delta (clamped).
        public int Add(ResourceType t, int delta)
        {
            int i = (int)t, old = _value[i];
            Set(t, (int)Math.Clamp((long)old + delta, 0, _cap));
            return _value[i] - old;
        }

        public bool TryConsume(ResourceType t, int amount)
        {
            if (amount < 0 || _value[(int)t] < amount) return false;
            Set(t, _value[(int)t] - amount);
            return true;
        }

        void Set(ResourceType t, int v)
        {
            int i = (int)t, old = _value[i];
            v = Math.Clamp(v, 0, _cap);
            if (v == old) return;
            _value[i] = v;
            Changed?.Invoke(t, old, v);
        }
    }
}