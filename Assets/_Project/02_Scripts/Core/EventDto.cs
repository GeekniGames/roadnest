using System;

namespace Roadnest.Core
{
    // 필드 이름은 JSON 키와 정확히 같아야 한다 / field names must match JSON keys exactly
    [Serializable] public class EventTableDto { public int version; public EventDto[] events; }

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
        public ChoiceDto[] choices;
    }

    [Serializable] public class SlotWeightDto { public string slot; public int weight; }

    [Serializable]
    public class ChoiceDto
    {
        public string id;
        public string textKey;
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
    }

    [Serializable] public class EffectDto { public string stat; public int amount; }
}