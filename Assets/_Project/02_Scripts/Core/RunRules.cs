namespace Roadnest.Core
{
    /// <summary>
    /// 한 판의 규칙 수치. 기본값은 기획서의 임시 수치. Unity 의존 없음.
    /// Rule numbers for one run. Defaults follow the design doc (temporary). No Unity dependency.
    /// </summary>
    public sealed class RunRules
    {
        public int StartFuel = 5, StartFood = 5, StartWater = 5;
        public int ResourceCap = 99;
        public int DepartFuelCost = 1;

        public int MaxDurability = 100;         // 시작값 = 최대값 / start = max
        public int DepartDurabilityLoss = 5;
        public int SettleDurabilityGain = 3;
        public int RepairAmount = 10;
        public int SettleRepairAmount = 15;

        public int MaxCharacterHp = 100;        // 시작값 = 최대값 / start = max
        public int HpLossMissingOne = 15;
        public int HpLossMissingBoth = 35;

        public int EventsPerDay = 5;
    }
}