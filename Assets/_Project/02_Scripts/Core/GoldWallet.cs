using System;

namespace Roadnest.Core
{
    /// <summary>
    /// 골드 지갑. 한 판(RunState)과 따로 남는 재화라 인벤토리 밖에 둔다. Unity 의존 없음.
    /// Gold wallet. Outlives a run, so it lives outside the inventory. No Unity dependency.
    /// </summary>
    public sealed class GoldWallet
    {
        public const int MaxGold = 9999999;     // 7자리 / 7 digits

        public int Amount { get; private set; }
        public event Action Changed;

        public GoldWallet(int amount = 0) { Amount = Clamp(amount); }

        // 실제로 적용된 변화량 반환. 0과 MaxGold에서 잘린다 / returns the applied delta, clamped to 0..MaxGold
        public int Add(int amount)
        {
            int next = Clamp((long)Amount + amount);
            int applied = next - Amount;
            if (applied == 0) return 0;

            Amount = next;
            Changed?.Invoke();
            return applied;
        }

        public void Set(int amount)
        {
            int next = Clamp(amount);
            if (next == Amount) return;

            Amount = next;
            Changed?.Invoke();
        }

        static int Clamp(long v) => v < 0 ? 0 : (v > MaxGold ? MaxGold : (int)v);
    }
}