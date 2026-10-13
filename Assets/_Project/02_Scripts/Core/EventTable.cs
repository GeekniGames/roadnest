using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roadnest.Core
{
    public readonly struct SlotCandidate
    {
        public readonly EventDef Def;
        public readonly int Weight;
        public SlotCandidate(EventDef def, int weight) { Def = def; Weight = weight; }
    }

    /// <summary>
    /// 이벤트 테이블. 시작 시 JSON을 한 번 읽어 검증하고, 모드x슬롯별 후보를 미리 만든다.
    /// Event table. Parses and validates JSON once at boot and prebuilds per mode x slot candidates.
    /// 이후 조회는 할당이 없다. / Lookups after loading allocate nothing.
    /// </summary>
    public sealed class EventTable
    {
        static readonly int ModeCount = Enum.GetValues(typeof(MoveMode)).Length;
        static readonly int SlotCount = Enum.GetValues(typeof(SlotType)).Length;

        public EventDef[] Events { get; }
        public EventDef Fallback { get; }

        readonly SlotCandidate[][] _candidates;
        readonly Dictionary<string, int> _tagIds;
        readonly Dictionary<string, EventDef> _byId;

        EventTable(EventDef[] events, EventDef fallback, SlotCandidate[][] candidates,
                   Dictionary<string, int> tagIds, Dictionary<string, EventDef> byId)
        {
            Events = events; Fallback = fallback; _candidates = candidates; _tagIds = tagIds; _byId = byId;
        }

        public SlotCandidate[] GetCandidates(MoveMode mode, SlotType slot)
            => _candidates[BucketIndex(mode, slot)];

        public int TagId(string tag) => _tagIds.TryGetValue(tag, out int id) ? id : -1;

        public bool TryGetEvent(string id, out EventDef def) => _byId.TryGetValue(id, out def);

        // 오류가 하나라도 있으면 null / returns null if any error was found
        public static EventTable FromJson(string json, List<string> errors)
        {
            if (string.IsNullOrEmpty(json))
            {
                errors.Add("JSON이 비어 있음 / JSON is empty");
                return null;
            }

            EventTableDto dto;
            try { dto = JsonUtility.FromJson<EventTableDto>(json); }
            catch (ArgumentException ex)
            {
                errors.Add("JSON 형식 오류 / invalid JSON: " + ex.Message);
                return null;
            }
            return Build(dto, errors);
        }

        public static EventTable Build(EventTableDto dto, List<string> errors)
        {
            int errorStart = errors.Count;
            if (dto == null || dto.events == null || dto.events.Length == 0)
            {
                errors.Add("이벤트 목록이 비어 있음 / event list is empty");
                return null;
            }

            var tagIds = new Dictionary<string, int>();
            var byId = new Dictionary<string, EventDef>();
            var defs = new List<EventDef>(dto.events.Length);
            var buckets = new List<SlotCandidate>[ModeCount * SlotCount];

            for (int i = 0; i < dto.events.Length; i++)
            {
                EventDto e = dto.events[i];
                if (string.IsNullOrEmpty(e.id))
                {
                    errors.Add("events[" + i + "]: id 없음 / missing id");
                    continue;
                }
                if (byId.ContainsKey(e.id))
                {
                    errors.Add(e.id + ": id 중복 / duplicate id");
                    continue;
                }

                EventDef def = BuildEvent(e, defs.Count, tagIds, errors);
                defs.Add(def);
                byId.Add(e.id, def);
                AddCandidates(e, def, e.id == dto.fallback, buckets, errors);
            }

            EventDef fallback = null;
            if (string.IsNullOrEmpty(dto.fallback))
                errors.Add("fallback 이벤트 ID가 없음 / missing fallback event id");
            else if (!byId.TryGetValue(dto.fallback, out fallback))
                errors.Add("fallback 이벤트를 찾을 수 없음 '" + dto.fallback + "' / fallback event not found");

            if (errors.Count > errorStart) return null;

            var candidates = new SlotCandidate[buckets.Length][];
            for (int i = 0; i < buckets.Length; i++)
                candidates[i] = buckets[i] == null ? Array.Empty<SlotCandidate>() : buckets[i].ToArray();

            return new EventTable(defs.ToArray(), fallback, candidates, tagIds, byId);
        }

        static int BucketIndex(MoveMode mode, SlotType slot) => (int)mode * SlotCount + (int)slot;

        static EventDef BuildEvent(EventDto e, int index, Dictionary<string, int> tagIds, List<string> errors)
        {
            string id = e.id;

            if (string.IsNullOrEmpty(e.titleKey)) errors.Add(id + ": titleKey 없음 / missing titleKey");
            if (string.IsNullOrEmpty(e.textKey)) errors.Add(id + ": textKey 없음 / missing textKey");

            TriggerType trigger;
            if (!TryParseEnum(e.trigger, out trigger))
                errors.Add(id + ": trigger 값 오류 '" + e.trigger + "' / invalid trigger");

            if (e.dayMin < 0 || e.dayMax < 0 || (e.dayMax != 0 && e.dayMax < e.dayMin))
                errors.Add(id + ": dayMin/dayMax 범위 오류 / invalid day range");

            WeightMods mods = ParseWeightMods(e.weightMods, id, errors);

            int tagCount = Len(e.tags);
            int[] tagArr = tagCount == 0 ? Array.Empty<int>() : new int[tagCount];
            for (int t = 0; t < tagCount; t++)
            {
                string tag = e.tags[t];
                if (string.IsNullOrEmpty(tag))
                {
                    errors.Add(id + ": 빈 태그 / empty tag");
                    continue;
                }
                if (!tagIds.TryGetValue(tag, out int tagId))
                {
                    tagId = tagIds.Count;
                    tagIds.Add(tag, tagId);
                }
                tagArr[t] = tagId;
            }

            int choiceCount = Len(e.choices);
            var choices = new ChoiceDef[choiceCount];
            int visible = 0;
            for (int c = 0; c < choiceCount; c++)
            {
                choices[c] = BuildChoice(e.choices[c], id, c, errors);
                if (!choices[c].Hidden) visible++;
            }
            if (visible < 2 || visible > 3)
                errors.Add(id + ": 보이는 선택지는 2~3개여야 함 (현재 " + visible + ") / visible choices must be 2-3");

            return new EventDef(index, id, e.titleKey, e.textKey, trigger, e.dayMin, e.dayMax, mods, tagArr, choices);
        }

        static WeightMods ParseWeightMods(string[] src, string id, List<string> errors)
        {
            WeightMods mods = WeightMods.None;
            int n = Len(src);
            for (int i = 0; i < n; i++)
            {
                switch (src[i])
                {
                    case "settle_streak": mods |= WeightMods.SettleStreak; break;
                    case "day": mods |= WeightMods.Day; break;
                    default:
                        errors.Add(id + ": 알 수 없는 weightMods '" + src[i] + "' / unknown weightMods");
                        break;
                }
            }
            return mods;
        }

        static ChoiceDef BuildChoice(ChoiceDto c, string eventId, int index, List<string> errors)
        {
            string where = eventId + ".choices[" + index + "]";
            if (string.IsNullOrEmpty(c.id)) errors.Add(where + ": id 없음 / missing id");
            if (string.IsNullOrEmpty(c.textKey)) errors.Add(where + ": textKey 없음 / missing textKey");

            Requirement[] reqs = BuildRequirements(c.requirements, where, errors);
            Effect[] cost = BuildEffects(c.cost, where + ".cost", errors);
            for (int i = 0; i < cost.Length; i++)
            {
                if (cost[i].Amount >= 0)
                    errors.Add(where + ".cost[" + i + "]: 비용은 음수여야 함 / cost must be negative");
            }

            OutcomeDef[] outcomes = BuildOutcomes(c.outcomes, where, errors);
            return new ChoiceDef(c.id, c.textKey, c.hidden, reqs, cost, outcomes);
        }

        static OutcomeDef[] BuildOutcomes(OutcomeDto[] src, string where, List<string> errors)
        {
            int n = Len(src);
            if (n == 0)
            {
                errors.Add(where + ": outcomes 없음 / no outcomes");
                return Array.Empty<OutcomeDef>();
            }

            var kinds = new ProbKind[n];
            int sumFixed = 0;
            int restCount = 0;

            for (int i = 0; i < n; i++)
            {
                string w = where + ".outcomes[" + i + "]";
                if (!TryParseEnum(src[i].kind, out kinds[i]))
                    errors.Add(w + ": kind 값 오류 '" + src[i].kind + "' / invalid kind");
                if (string.IsNullOrEmpty(src[i].textKey))
                    errors.Add(w + ": textKey 없음 / missing textKey");

                if (kinds[i] == ProbKind.Fixed)
                {
                    if (src[i].percent < 1 || src[i].percent > 100)
                        errors.Add(w + ": percent는 1~100 / percent must be 1-100");
                    sumFixed += src[i].percent;
                }
                else restCount++;
            }

            if (restCount > 1) errors.Add(where + ": rest는 하나만 가능 / only one rest allowed");
            if (sumFixed > 100) errors.Add(where + ": 확률 합이 100 초과 (" + sumFixed + ") / percent sum over 100");
            if (restCount == 0 && sumFixed != 100)
                errors.Add(where + ": rest가 없으면 합이 100이어야 함 (" + sumFixed + ") / sum must be 100 without rest");
            if (restCount == 1 && sumFixed >= 100)
                errors.Add(where + ": rest 확률이 0이 됨 / rest would get 0%");

            var result = new OutcomeDef[n];
            int cumulative = 0;
            for (int i = 0; i < n; i++)
            {
                int pct = kinds[i] == ProbKind.Rest ? 100 - sumFixed : src[i].percent;
                cumulative += pct;
                Effect[] fx = BuildEffects(src[i].effects, where + ".outcomes[" + i + "].effects", errors);
                result[i] = new OutcomeDef(kinds[i], pct, cumulative, src[i].textKey, fx);
            }
            return result;
        }

        static Effect[] BuildEffects(EffectDto[] src, string where, List<string> errors)
        {
            int n = Len(src);
            if (n == 0) return Array.Empty<Effect>();

            var arr = new Effect[n];
            for (int i = 0; i < n; i++)
            {
                StatType stat;
                if (!TryParseStat(src[i].stat, out stat))
                    errors.Add(where + "[" + i + "]: 알 수 없는 stat '" + src[i].stat + "' / unknown stat");
                if (src[i].amount == 0)
                    errors.Add(where + "[" + i + "]: amount가 0 / amount is 0");
                arr[i] = new Effect(stat, src[i].amount);
            }
            return arr;
        }

        static Requirement[] BuildRequirements(RequirementDto[] src, string where, List<string> errors)
        {
            int n = Len(src);
            if (n == 0) return Array.Empty<Requirement>();

            var arr = new Requirement[n];
            for (int i = 0; i < n; i++)
            {
                StatType stat;
                CompareOp op;
                if (!TryParseStat(src[i].stat, out stat))
                    errors.Add(where + ".requirements[" + i + "]: 알 수 없는 stat '" + src[i].stat + "' / unknown stat");
                if (stat == StatType.HpAll)
                    errors.Add(where + ".requirements[" + i + "]: hp_all은 조건에 쓸 수 없음 / hp_all is not allowed in requirements");
                if (!TryParseOp(src[i].op, out op))
                    errors.Add(where + ".requirements[" + i + "]: 알 수 없는 op '" + src[i].op + "' / unknown op");
                arr[i] = new Requirement(stat, op, src[i].value);
            }
            return arr;
        }

        // 폴백 이벤트는 mode/slot 없이도 허용 / the fallback event may omit modes and slots
        static void AddCandidates(EventDto e, EventDef def, bool isFallback,
                                  List<SlotCandidate>[] buckets, List<string> errors)
        {
            int modeCount = Len(e.modes);
            int slotCount = Len(e.slots);
            if (!isFallback)
            {
                if (modeCount == 0) errors.Add(e.id + ": modes 없음 / missing modes");
                if (slotCount == 0) errors.Add(e.id + ": slots 없음 / missing slots");
            }

            for (int m = 0; m < modeCount; m++)
            {
                MoveMode mode;
                if (!TryParseEnum(e.modes[m], out mode))
                {
                    errors.Add(e.id + ": mode 값 오류 '" + e.modes[m] + "' / invalid mode");
                    continue;
                }

                for (int s = 0; s < slotCount; s++)
                {
                    SlotWeightDto sw = e.slots[s];
                    SlotType slot;
                    if (!TryParseEnum(sw.slot, out slot))
                    {
                        if (m == 0) errors.Add(e.id + ": slot 값 오류 '" + sw.slot + "' / invalid slot");
                        continue;
                    }
                    if (sw.weight <= 0)
                    {
                        if (m == 0) errors.Add(e.id + ": slot '" + sw.slot + "' weight는 1 이상 / weight must be >= 1");
                        continue;
                    }

                    int key = BucketIndex(mode, slot);
                    if (buckets[key] == null) buckets[key] = new List<SlotCandidate>();
                    buckets[key].Add(new SlotCandidate(def, sw.weight));
                }
            }
        }

        static int Len<T>(T[] a) => a == null ? 0 : a.Length;

        static bool TryParseEnum<T>(string s, out T value) where T : struct, Enum
        {
            value = default;
            if (string.IsNullOrEmpty(s) || char.IsDigit(s[0])) return false;
            return Enum.TryParse(s, true, out value) && Enum.IsDefined(typeof(T), value);
        }

        static bool TryParseStat(string s, out StatType stat)
        {
            switch (s)
            {
                case "fuel": stat = StatType.Fuel; return true;
                case "food": stat = StatType.Food; return true;
                case "water": stat = StatType.Water; return true;
                case "durability": stat = StatType.Durability; return true;
                case "hp_all": stat = StatType.HpAll; return true;
                default: stat = StatType.Fuel; return false;
            }
        }

        static bool TryParseOp(string s, out CompareOp op)
        {
            switch (s)
            {
                case "gte": op = CompareOp.Gte; return true;
                case "gt": op = CompareOp.Gt; return true;
                case "lte": op = CompareOp.Lte; return true;
                case "lt": op = CompareOp.Lt; return true;
                case "eq": op = CompareOp.Eq; return true;
                default: op = CompareOp.Eq; return false;
            }
        }
    }
}