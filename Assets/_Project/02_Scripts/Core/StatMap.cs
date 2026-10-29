namespace Roadnest.Core
{
    /// <summary>
    /// StatType과 인벤토리 종류, JSON 이름을 잇는 표. Unity 의존 없음.
    /// Maps StatType to inventory kinds and JSON names. No Unity dependency.
    /// </summary>
    public static class StatMap
    {
        public const int Count = 32;

        // StatType 순서와 같다 / same order as StatType
        static readonly string[] JsonNames =
        {
            "fuel", "food", "water", "durability", "hp_all",
            "repair_tool", "repair_kit_low", "repair_kit_high",
            "bandage", "first_aid", "medical_kit",
            "crowbar", "axe", "shovel", "bolt_cutter", "flashlight", "rope",
            "wood_scrap", "steel_plate", "machine_part", "scrap_metal",
            "cloth_scrap", "alcohol",
            "purification_tablet", "flare", "smoke_grenade", "radio", "map_fragment",
            "gun", "fire_extinguisher", "binoculars", "megaphone"
        };

        // 인벤토리에 쌓이는 값인가 (자원 + 아이템). 내구도/HP는 아님
        // True for values kept in the inventory (resources + items); durability and HP are not
        public static bool IsStored(StatType s) => s <= StatType.Water || s >= StatType.RepairTool;

        public static ItemKind ToKind(StatType s) => (ItemKind)(s <= StatType.Water ? (int)s : (int)s - 2);

        public static string NameOf(StatType s) => JsonNames[(int)s];

        // 로드 때만 호출 / called at load time only
        public static bool TryParse(string name, out StatType stat)
        {
            for (int i = 0; i < JsonNames.Length; i++)
            {
                if (JsonNames[i] != name) continue;
                stat = (StatType)i;
                return true;
            }
            stat = StatType.Fuel;
            return false;
        }
    }
}