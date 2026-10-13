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

        public OutcomeDef(ProbKind kind, int percent, int cumulativeEnd, string textKey, Effect[] effects)
        {
            Kind = kind; Percent = percent; CumulativeEnd = cumulativeEnd;
            TextKey = textKey; Effects = effects;
        }
    }

    public sealed class ChoiceDef
    {
        public readonly string Id;
        public readonly string TextKey;
        public readonly bool Hidden;
        public readonly Requirement[] Requirements;
        public readonly Effect[] Cost;
        public readonly OutcomeDef[] Outcomes;

        public ChoiceDef(string id, string textKey, bool hidden, Requirement[] requirements,
                         Effect[] cost, OutcomeDef[] outcomes)
        {
            Id = id; TextKey = textKey; Hidden = hidden;
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

        public EventDef(int index, string id, string titleKey, string textKey, TriggerType trigger,
                        int dayMin, int dayMax, WeightMods mods, int[] tagIds, ChoiceDef[] choices)
        {
            Index = index; Id = id; TitleKey = titleKey; TextKey = textKey; Trigger = trigger;
            DayMin = dayMin; DayMax = dayMax; Mods = mods; TagIds = tagIds; Choices = choices;
        }

        public bool IsInDayRange(int day) => day >= DayMin && (DayMax == 0 || day <= DayMax);
    }
}