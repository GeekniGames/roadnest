namespace Roadnest.Core
{
    public readonly struct Effect
    {
        public readonly StatType Stat;
        public readonly int Amount;
        public Effect(StatType stat, int amount) { Stat = stat; Amount = amount; }
    }

    public readonly struct Requirement
    {
        public readonly StatType Stat;
        public readonly CompareOp Op;
        public readonly int Value;

        public Requirement(StatType stat, CompareOp op, int value)
        {
            Stat = stat; Op = op; Value = value;
        }

        public bool IsMet(int current)
        {
            switch (Op)
            {
                case CompareOp.Gte: return current >= Value;
                case CompareOp.Gt: return current > Value;
                case CompareOp.Lte: return current <= Value;
                case CompareOp.Lt: return current < Value;
                default: return current == Value;
            }
        }
    }

    public sealed class OutcomeDef
    {
        public readonly ProbKind Kind;
        public readonly int Percent;        // rest는 남은 값으로 확정됨 / rest is resolved to the remainder
        public readonly int CumulativeEnd;  // 굴림 0~99가 이 값 미만이면 당첨 / a 0-99 roll below this wins
        public readonly string TextKey;
        public readonly Effect[] Effects;
        public readonly ChainLinkType LinkType;
        public readonly int LinkIndex;      // 후속 이벤트 Index, 없으면 -1 / follow-up event index, -1 = none
        public readonly int LinkPercent;
        public readonly int LinkWeight;

        public OutcomeDef(ProbKind kind, int percent, int cumulativeEnd, string textKey, Effect[] effects,
                          ChainLinkType linkType = ChainLinkType.None, int linkIndex = -1,
                          int linkPercent = 0, int linkWeight = 0)
        {
            Kind = kind; Percent = percent; CumulativeEnd = cumulativeEnd;
            TextKey = textKey; Effects = effects;
            LinkType = linkType; LinkIndex = linkIndex; LinkPercent = linkPercent; LinkWeight = linkWeight;
        }
    }

    public sealed class ChoiceDef
    {
        public readonly string Id;
        public readonly string TextKey;
        public readonly string IconKey;     // 없으면 null 또는 빈 문자열 / null or empty when absent
        public readonly bool Hidden;
        public readonly bool Special;
        public readonly Requirement[] Requirements;
        public readonly Effect[] Cost;
        public readonly OutcomeDef[] Outcomes;

        public ChoiceDef(string id, string textKey, string iconKey, bool hidden, Requirement[] requirements,
                         Effect[] cost, OutcomeDef[] outcomes, bool special = false)
        {
            Id = id; TextKey = textKey; IconKey = iconKey; Hidden = hidden; Special = special;
            Requirements = requirements; Cost = cost; Outcomes = outcomes;
        }
    }

    public sealed class EventDef
    {
        public readonly int Index;
        public readonly string Id;
        public readonly string TitleKey;
        public readonly string TextKey;
        public readonly TriggerType Trigger;
        public readonly int DayMin;
        public readonly int DayMax;
        public readonly WeightMods Mods;
        public readonly int[] TagIds;
        public readonly ChoiceDef[] Choices;
        public readonly int ModeMask;       // 1 << MoveMode
        public readonly bool ChainOnly;
        public readonly int SlotMin;        // 1부터, 0 = 제한 없음 / 1-based, 0 = no limit
        public readonly int SlotMax;

        public EventDef(int index, string id, string titleKey, string textKey, TriggerType trigger,
                        int dayMin, int dayMax, WeightMods mods, int[] tagIds, ChoiceDef[] choices,
                        int modeMask = 0, bool chainOnly = false, int slotMin = 0, int slotMax = 0)
        {
            Index = index; Id = id; TitleKey = titleKey; TextKey = textKey; Trigger = trigger;
            DayMin = dayMin; DayMax = dayMax; Mods = mods; TagIds = tagIds; Choices = choices;
            ModeMask = modeMask; ChainOnly = chainOnly; SlotMin = slotMin; SlotMax = slotMax;
        }

        public bool IsInDayRange(int day) => day >= DayMin && (DayMax == 0 || day <= DayMax);

        public bool HasMode(MoveMode mode) => (ModeMask & (1 << (int)mode)) != 0;

        // slotOrder는 0부터 / slotOrder is 0-based
        public bool IsInSlotRange(int slotOrder)
        {
            int n = slotOrder + 1;
            return n >= SlotMin && (SlotMax == 0 || n <= SlotMax);
        }
    }
}