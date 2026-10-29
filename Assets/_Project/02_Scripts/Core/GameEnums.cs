namespace Roadnest.Core
{
    public enum MoveMode : byte { Depart, Settle }

    public enum SlotType : byte
    {
        Minor, ResourceExplore, Danger, Major,
        MaintenanceRecovery, NearbyExplore, Night
    }

    public enum TriggerType : byte { Telegraphed, Sudden }

    // 앞 5개는 기존 순서 유지, 뒤는 ItemKind의 RepairTool부터 같은 순서 / first 5 keep their order, the rest follow ItemKind from RepairTool
    public enum StatType : byte
    {
        Fuel, Food, Water, Durability, HpAll,
        RepairTool, RepairKitLow, RepairKitHigh,
        Bandage, FirstAid, MedicalKit,
        Crowbar, Axe, Shovel, BoltCutter, Flashlight, Rope,
        WoodScrap, SteelPlate, MachinePart, ScrapMetal,
        ClothScrap, Alcohol,
        PurificationTablet, Flare, SmokeGrenade, Radio, MapFragment,
        Gun, FireExtinguisher, Binoculars, Megaphone
    }

    public enum CompareOp : byte { Gte, Gt, Lte, Lt, Eq }

    public enum ProbKind : byte { Fixed, Rest }

    public enum RngPurpose : byte { EventPick = 1, Outcome = 2, Misc = 3, ChainSameDay = 4, ChainNextDay = 5 }

    public enum ChainLinkType : byte { None, SameDay, NextDay }

    public enum EndReason : byte { None, AllDead, VehicleDestroyed }

    [System.Flags]
    public enum WeightMods : byte { None = 0, SettleStreak = 1, Day = 2 }
}