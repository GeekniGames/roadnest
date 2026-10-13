namespace Roadnest.Core
{
    public enum MoveMode : byte { Depart, Settle }

    public enum SlotType : byte
    {
        Minor, ResourceExplore, Danger, Major,
        MaintenanceRecovery, NearbyExplore, Night
    }

    public enum TriggerType : byte { Telegraphed, Sudden }

    // Fuel/Food/Water는 ResourceType과 순서가 같음 / first three match ResourceType order
    public enum StatType : byte { Fuel, Food, Water, Durability, HpAll }

    public enum CompareOp : byte { Gte, Gt, Lte, Lt, Eq }

    public enum ProbKind : byte { Fixed, Rest }

    public enum RngPurpose : byte { EventPick = 1, Outcome = 2, Misc = 3 }

    public enum EndReason : byte { None, AllDead, VehicleDestroyed }
}