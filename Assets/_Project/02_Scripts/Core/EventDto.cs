using System;

namespace Roadnest.Core
{
    // 필드 이름은 JSON 키와 정확히 같아야 한다 / field names must match JSON keys exactly
    [Serializable] public class DaySlotsDto { public string mode; public string[] slots; }
    [Serializable] public class EventTableDto { public int version; public string fallback; public DaySlotsDto[] daySlots; public EventDto[] events; }

    [Serializable]
    public class EventDto
    {
        public string id;
        public string titleKey;
        public string textKey;
        public string trigger;          // telegraphed | sudden
        public string[] modes;          // depart | settle
        public SlotWeightDto[] slots;
        public int dayMin;              // 0 = 제한 없음 / no limit
        public int dayMax;              // 0 = 제한 없음 / no limit
        public string[] tags;
        public string[] weightMods;     // settle_streak | day
        public bool chainOnly;          // 연계 전용, 일반 랜덤 제외 / chain-only, excluded from normal picks
        public int slotMin;             // 1~5, 0 = 제한 없음 / 0 = no limit
        public int slotMax;             // 1~5, 0 = 제한 없음 / 0 = no limit
        public ChoiceDto[] choices;
    }

    [Serializable] public class SlotWeightDto { public string slot; public int weight; }

    [Serializable]
    public class ChoiceDto
    {
        public string id;
        public string textKey;
        public string iconKey;
        public bool special;
        public bool hidden;
        public RequirementDto[] requirements;
        public EffectDto[] cost;        // 선택하면 항상 지불 / always paid when chosen
        public OutcomeDto[] outcomes;
    }

    [Serializable] public class RequirementDto { public string stat; public string op; public int value; }

    [Serializable]
    public class OutcomeDto
    {
        public string kind;             // fixed | rest
        public int percent;             // fixed일 때만 사용 / used by fixed only
        public string textKey;
        public EffectDto[] effects;
        public string next;             // 후속 이벤트 id, 비우면 없음 / follow-up event id, empty = none
        public string nextType;         // same_day | next_day
        public int nextPercent;         // 1~100
        public int nextWeight;          // next_day만, 0 = 1 / next_day only, 0 means 1
    }

    [Serializable] public class EffectDto { public string stat; public int amount; }
}