namespace Roadnest.Core
{
    /// <summary>
    /// 한 판의 규칙 수치. 기본값은 기획서의 임시 수치. Unity 의존 없음.
    /// Rule numbers for one run. Defaults follow the design doc (temporary). No Unity dependency.
    /// </summary>
    public sealed class RunRules
    {
        public int StartFuel = 5, StartFood = 5, StartWater = 5;
        public int InventorySlots = 10;     // 기본 칸 수 / base cell count
        public int StackCap = 10;           // 한 칸 최대 개수 / max per cell
        public int DepartFuelCost = 1;

        public int MaxDurability = 100;         // 시작값 = 최대값 / start = max
        public int DepartDurabilityLoss = 5;
        public int SettleDurabilityGain = 5;    // 정착 선택 즉시 수리 / repair applied the moment settle is picked
        public int RepairToolAmount = 3;        // 수리 아이템별 수리량 / repair amount per item
        public int RepairKitLowAmount = 10;
        public int RepairKitHighAmount = 25;

        public int MaxCharacterHp = 100;        // 시작값 = 최대값 / start = max
        public int HpLossMissingOne = 15;
        public int HpLossMissingBoth = 35;

        public int EventsPerDay = 5;


        public int GetRepairAmount(ItemType item)
        {
            switch (item)
            {
                case ItemType.RepairTool: return RepairToolAmount;
                case ItemType.RepairKitLow: return RepairKitLowAmount;
                default: return RepairKitHighAmount;
            }
        }
    }
}