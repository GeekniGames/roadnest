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
        readonly SlotType[][] _daySlots;

        EventTable(EventDef[] events, EventDef fallback, SlotCandidate[][] candidates,
                   Dictionary<string, int> tagIds, Dictionary<string, EventDef> byId, SlotType[][] daySlots)
        {
            Events = events; Fallback = fallback; _candidates = candidates; _tagIds = tagIds; _byId = byId;
            _daySlots = daySlots;
        }

        public SlotCandidate[] GetCandidates(MoveMode mode, SlotType slot)
            => _candidates[BucketIndex(mode, slot)];

        public SlotType[] GetDaySlots(MoveMode mode) => _daySlots[(int)mode];

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

            // 연계 대상이 뒤에 정의돼도 Index를 알 수 있게 먼저 번호를 매긴다
            // number events first so a chain target defined later still has its Index
            var indexById = new Dictionary<string, int>();
            for (int i = 0; i < dto.events.Length; i++)
            {
                string pid = dto.events[i].id;
                if (!string.IsNullOrEmpty(pid) && !indexById.ContainsKey(pid)) indexById.Add(pid, indexById.Count);
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
                if (e.chainOnly && e.id == dto.fallback)
                    errors.Add(e.id + ": fallback은 chainOnly일 수 없음 / fallback cannot be chainOnly");

                EventDef def = BuildEvent(e, defs.Count, tagIds, indexById, errors);
                defs.Add(def);
                byId.Add(e.id, def);
                AddCandidates(e, def, e.id == dto.fallback, buckets, errors);
            }

            EventDef fallback = null;
            if (string.IsNullOrEmpty(dto.fallback))
                errors.Add("fallback 이벤트 ID가 없음 / missing fallback event id");
            else if (!byId.TryGetValue(dto.fallback, out fallback))
                errors.Add("fallback 이벤트를 찾을 수 없음 '" + dto.fallback + "' / fallback event not found");

            SlotType[][] daySlots = ParseDaySlots(dto.daySlots, errors);
            ValidateChains(defs, daySlots, errors);
            if (errors.Count > errorStart) return null;

            var candidates = new SlotCandidate[buckets.Length][];
            for (int i = 0; i < buckets.Length; i++)
                candidates[i] = buckets[i] == null ? Array.Empty<SlotCandidate>() : buckets[i].ToArray();

            return new EventTable(defs.ToArray(), fallback, candidates, tagIds, byId, daySlots);
        }

        // 모드마다 슬롯 목록이 하나씩 있어야 한다 / every mode needs exactly one slot list
        static SlotType[][] ParseDaySlots(DaySlotsDto[] src, List<string> errors)
        {
            var result = new SlotType[ModeCount][];
            int n = Len(src);
            for (int i = 0; i < n; i++)
            {
                MoveMode mode;
                if (!TryParseEnum(src[i].mode, out mode))
                {
                    errors.Add("daySlots[" + i + "]: mode 값 오류 '" + src[i].mode + "' / invalid mode");
                    continue;
                }
                if (result[(int)mode] != null)
                {
                    errors.Add("daySlots: mode 중복 '" + src[i].mode + "' / duplicate mode");
                    continue;
                }

                int count = Len(src[i].slots);
                if (count == 0)
                {
                    errors.Add("daySlots[" + i + "]: slots가 비어 있음 / slots is empty");
                    continue;
                }

                var arr = new SlotType[count];
                for (int s = 0; s < count; s++)
                {
                    if (!TryParseEnum(src[i].slots[s], out arr[s]))
                        errors.Add("daySlots[" + i + "].slots[" + s + "]: 알 수 없는 슬롯 '" + src[i].slots[s] + "' / unknown slot");
                }
                result[(int)mode] = arr;
            }

            for (int m = 0; m < ModeCount; m++)
                if (result[m] == null) errors.Add("daySlots: " + (MoveMode)m + " 누락 / missing mode");
            return result;
        }

        static int BucketIndex(MoveMode mode, SlotType slot) => (int)mode * SlotCount + (int)slot;

        static EventDef BuildEvent(EventDto e, int index, Dictionary<string, int> tagIds,
                                   Dictionary<string, int> indexById, List<string> errors)
        {
            string id = e.id;

            if (string.IsNullOrEmpty(e.titleKey)) errors.Add(id + ": titleKey 없음 / missing titleKey");
            if (string.IsNullOrEmpty(e.textKey)) errors.Add(id + ": textKey 없음 / missing textKey");

            TriggerType trigger;
            if (!TryParseEnum(e.trigger, out trigger))
                errors.Add(id + ": trigger 값 오류 '" + e.trigger + "' / invalid trigger");

            if (e.dayMin < 0 || e.dayMax < 0 || (e.dayMax != 0 && e.dayMax < e.dayMin))
                errors.Add(id + ": dayMin/dayMax 범위 오류 / invalid day range");

            if (e.slotMin < 0 || e.slotMax < 0 || (e.slotMax != 0 && e.slotMax < e.slotMin))
                errors.Add(id + ": slotMin/slotMax 범위 오류 / invalid slot range");

            // 잘못된 mode 값은 AddCandidates가 보고한다 / invalid modes are reported by AddCandidates
            int modeMask = 0;
            int modeLen = Len(e.modes);
            for (int m = 0; m < modeLen; m++)
            {
                MoveMode mm;
                if (TryParseEnum(e.modes[m], out mm)) modeMask |= 1 << (int)mm;
            }

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
            int normal = 0;
            int special = 0;
            int visible = 0;
            int free = 0;
            for (int c = 0; c < choiceCount; c++)
            {
                ChoiceDef ch = BuildChoice(e.choices[c], id, c, indexById, errors);
                choices[c] = ch;
                if (ch.Special) { special++; continue; }
                normal++;
                if (ch.Hidden) continue;
                visible++;
                if (ch.Cost.Length == 0 && ch.Requirements.Length == 0) free++;
            }
            if (visible < 2 || visible > EventLimits.MaxNormalChoices)
                errors.Add(id + ": 보이는 선택지는 2~" + EventLimits.MaxNormalChoices + "개여야 함 (현재 " + visible + ") / visible choices (excluding special) must be 2-" + EventLimits.MaxNormalChoices);
            // 항상 고를 수 있는 선택지가 없으면 플레이어가 멈춘다. 특별 선택지는 조건부라 제외 / without an always-pickable choice the player gets stuck. special choices are conditional and do not count
            if (free == 0)
                errors.Add(id + ": 비용·조건 없는 보이는 선택지가 1개 이상 필요 / needs at least one visible choice with no cost or requirements (free choice)");
            if (normal > EventLimits.MaxNormalChoices)
                errors.Add(id + ": 일반 선택지는 최대 " + EventLimits.MaxNormalChoices + "개 (현재 " + normal + ") / at most " + EventLimits.MaxNormalChoices + " normal choices");
            if (special > EventLimits.MaxSpecialChoices)
                errors.Add(id + ": special 선택지는 최대 " + EventLimits.MaxSpecialChoices + "개 (현재 " + special + ") / at most " + EventLimits.MaxSpecialChoices + " special choices");
            if (choiceCount > EventLimits.MaxChoices)
                errors.Add(id + ": 선택지는 최대 " + EventLimits.MaxChoices + "개 (현재 " + choiceCount + ") / at most " + EventLimits.MaxChoices + " choices");

            return new EventDef(index, id, e.titleKey, e.textKey, trigger, e.dayMin, e.dayMax, mods, tagArr, choices,
                                modeMask, e.chainOnly, e.slotMin, e.slotMax);
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

        static ChoiceDef BuildChoice(ChoiceDto c, string eventId, int index,
                                     Dictionary<string, int> indexById, List<string> errors)
        {
            string where = eventId + ".choices[" + index + "]";
            if (string.IsNullOrEmpty(c.id)) errors.Add(where + ": id 없음 / missing id");
            if (string.IsNullOrEmpty(c.textKey)) errors.Add(where + ": textKey 없음 / missing textKey");

            Requirement[] reqs = BuildRequirements(c.requirements, where, errors);
            Effect[] cost = BuildEffects(c.cost, where + ".cost", errors);
            if (cost.Length > EventLimits.MaxCostEffects)
                errors.Add(where + ".cost: 비용은 최대 " + EventLimits.MaxCostEffects + "개 / at most " + EventLimits.MaxCostEffects + " cost effect");
            for (int i = 0; i < cost.Length; i++)
            {
                for (int j = 0; j < i; j++)
                    if (cost[i].Stat == cost[j].Stat)
                        errors.Add(where + ".cost[" + i + "]: 같은 stat 중복 / duplicate stat in cost");
            }

            OutcomeDef[] outcomes = BuildOutcomes(c.outcomes, where, indexById, errors);
            return new ChoiceDef(c.id, c.textKey, c.iconKey, c.hidden, reqs, cost, outcomes, c.special);
        }

        static OutcomeDef[] BuildOutcomes(OutcomeDto[] src, string where, Dictionary<string, int> indexById, List<string> errors)
        {
            int n = Len(src);
            if (n == 0)
            {
                errors.Add(where + ": outcomes 없음 / no outcomes");
                return Array.Empty<OutcomeDef>();
            }

            if (n > EventLimits.MaxOutcomes)
                errors.Add(where + ": 결과는 최대 " + EventLimits.MaxOutcomes + "개 (현재 " + n + ") / at most " + EventLimits.MaxOutcomes + " outcomes");

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
                string w = where + ".outcomes[" + i + "]";
                int pct = kinds[i] == ProbKind.Rest ? 100 - sumFixed : src[i].percent;
                cumulative += pct;
                Effect[] fx = BuildEffects(src[i].effects, w + ".effects", errors);
                if (fx.Length > EventLimits.MaxOutcomeEffects)
                    errors.Add(w + ".effects: 효과는 최대 " + EventLimits.MaxOutcomeEffects + "개 / at most " + EventLimits.MaxOutcomeEffects + " effects per outcome");

                ChainLinkType linkType;
                int linkIndex, linkPercent, linkWeight;
                ParseLink(src[i], w, indexById, errors, out linkType, out linkIndex, out linkPercent, out linkWeight);
                result[i] = new OutcomeDef(kinds[i], pct, cumulative, src[i].textKey, fx,
                                           linkType, linkIndex, linkPercent, linkWeight);
            }
            return result;
        }

        static void ParseLink(OutcomeDto o, string where, Dictionary<string, int> indexById, List<string> errors,
                              out ChainLinkType type, out int index, out int percent, out int weight)
        {
            type = ChainLinkType.None; index = -1; percent = 0; weight = 0;

            if (string.IsNullOrEmpty(o.next))
            {
                if (!string.IsNullOrEmpty(o.nextType) || o.nextPercent != 0 || o.nextWeight != 0)
                    errors.Add(where + ": next 없이 연계 설정이 있음 / chain settings without next");
                return;
            }

            if (!indexById.TryGetValue(o.next, out index))
            {
                index = -1;
                errors.Add(where + ": 연계 대상 없음 '" + o.next + "' / chain target not found");
                return;
            }

            if (o.nextType == "same_day") type = ChainLinkType.SameDay;
            else if (o.nextType == "next_day") type = ChainLinkType.NextDay;
            else
            {
                index = -1;
                errors.Add(where + ": nextType은 same_day 또는 next_day / nextType must be same_day or next_day");
                return;
            }

            percent = o.nextPercent;
            if (percent < 1 || percent > 100)
                errors.Add(where + ": nextPercent는 1~100 / nextPercent must be 1-100");

            if (type == ChainLinkType.NextDay)
            {
                weight = o.nextWeight == 0 ? 1 : o.nextWeight;
                if (weight < 1 || weight > EventLimits.MaxChainWeight)
                    errors.Add(where + ": nextWeight는 1~" + EventLimits.MaxChainWeight + " / nextWeight must be 1-" + EventLimits.MaxChainWeight);
            }
            else if (o.nextWeight != 0)
                errors.Add(where + ": same_day에는 nextWeight를 쓰지 않음 / nextWeight is for next_day only");
        }

        // 연계 전체 검증: 대상, 모드, 순환, 단계 수, 하루 5슬롯 초과
        // whole-chain checks: targets, modes, cycles, stage count, day overflow
        static void ValidateChains(List<EventDef> defs, SlotType[][] daySlots, List<string> errors)
        {
            int perDay = int.MaxValue;
            for (int m = 0; m < ModeCount; m++)
            {
                if (daySlots[m] == null) return;
                if (daySlots[m].Length < perDay) perDay = daySlots[m].Length;
            }

            int n = defs.Count;
            var nextDayIn = new bool[n];
            int nextDayTargets = 0;

            for (int i = 0; i < n; i++)
            {
                EventDef d = defs[i];
                if (d.SlotMin > perDay || d.SlotMax > perDay)
                    errors.Add(d.Id + ": slotMin/slotMax가 하루 슬롯 수(" + perDay + ")를 넘음 / slot range exceeds slots per day");

                for (int c = 0; c < d.Choices.Length; c++)
                {
                    OutcomeDef[] os = d.Choices[c].Outcomes;
                    for (int k = 0; k < os.Length; k++)
                    {
                        OutcomeDef o = os[k];
                        if (o.LinkType == ChainLinkType.None) continue;

                        EventDef t = defs[o.LinkIndex];
                        if (!t.ChainOnly)
                            errors.Add(d.Id + ": 연계 대상 '" + t.Id + "'은 chainOnly여야 함 / chain target must be chainOnly");

                        if (o.LinkType == ChainLinkType.SameDay)
                        {
                            if ((d.ModeMask & ~t.ModeMask) != 0)
                                errors.Add(d.Id + ": 같은날 연계 대상 '" + t.Id + "'의 modes가 시작 이벤트 modes를 모두 포함해야 함 / same-day chain target must cover all modes of the starter");
                        }
                        else if (!nextDayIn[o.LinkIndex])
                        {
                            nextDayIn[o.LinkIndex] = true;
                            nextDayTargets++;
                        }
                    }
                }
            }

            if (nextDayTargets > EventLimits.MaxPendingChains)
                errors.Add("다른날 연계 대상이 너무 많음 (" + nextDayTargets + ") / too many next-day chain targets, max " + EventLimits.MaxPendingChains);

            bool cycle;
            int[] sameDepth = ChainDepth(defs, true, out cycle);
            int[] allDepth = ChainDepth(defs, false, out cycle);
            if (cycle)
            {
                errors.Add("연계가 순환함 / chain cycle detected");
                return;
            }

            for (int i = 0; i < n; i++)
            {
                EventDef d = defs[i];
                if (allDepth[i] > EventLimits.MaxChainStages)
                    errors.Add(d.Id + ": 연계는 최대 " + EventLimits.MaxChainStages + "단계 (현재 " + allDepth[i] + ") / chain stages over " + EventLimits.MaxChainStages);

                // 연계 중간 단계가 아닌 시작점만 슬롯 범위를 검사 / only entry points need a slot-range check
                int sd = sameDepth[i];
                if (sd > 1 && (!d.ChainOnly || nextDayIn[i]))
                {
                    int effMax = d.SlotMax == 0 ? perDay : d.SlotMax;
                    if (effMax + sd - 1 > perDay)
                        errors.Add(d.Id + ": 같은날 " + sd + "단계 연계가 하루 " + perDay + "슬롯을 넘을 수 있음. slotMax를 " + (perDay - sd + 1) + " 이하로 / same-day chain of " + sd + " stages can overflow the day; set slotMax <= " + (perDay - sd + 1));
                }
            }
        }

        // 각 이벤트에서 시작했을 때의 연계 길이(자기 포함). sameOnly면 같은날 연계만 센다
        // chain length starting at each event (itself included); sameOnly counts same-day links only
        static int[] ChainDepth(List<EventDef> defs, bool sameOnly, out bool cycle)
        {
            int n = defs.Count;
            var depth = new int[n];
            var state = new byte[n];
            cycle = false;
            for (int i = 0; i < n; i++) Visit(defs, i, sameOnly, depth, state, ref cycle);
            return depth;
        }

        static int Visit(List<EventDef> defs, int i, bool sameOnly, int[] depth, byte[] state, ref bool cycle)
        {
            if (state[i] == 2) return depth[i];
            if (state[i] == 1) { cycle = true; return 1; }

            state[i] = 1;
            int best = 0;
            ChoiceDef[] cs = defs[i].Choices;
            for (int c = 0; c < cs.Length; c++)
            {
                OutcomeDef[] os = cs[c].Outcomes;
                for (int k = 0; k < os.Length; k++)
                {
                    OutcomeDef o = os[k];
                    if (o.LinkType == ChainLinkType.None) continue;
                    if (sameOnly && o.LinkType != ChainLinkType.SameDay) continue;
                    int len = Visit(defs, o.LinkIndex, sameOnly, depth, state, ref cycle);
                    if (len > best) best = len;
                }
            }
            state[i] = 2;
            depth[i] = best + 1;
            return depth[i];
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

        // 폴백은 mode/slot 없이도 허용, 연계 전용은 slot 없이도 허용하며 후보 목록에는 넣지 않는다
        // the fallback may omit modes and slots; chain-only may omit slots and never enters the candidate lists
        static void AddCandidates(EventDto e, EventDef def, bool isFallback,
                                  List<SlotCandidate>[] buckets, List<string> errors)
        {
            int modeCount = Len(e.modes);
            int slotCount = Len(e.slots);
            if (!isFallback)
            {
                if (modeCount == 0) errors.Add(e.id + ": modes 없음 / missing modes");
                if (slotCount == 0 && !e.chainOnly) errors.Add(e.id + ": slots 없음 / missing slots");
            }

            for (int m = 0; m < modeCount; m++)
            {
                MoveMode mode;
                if (!TryParseEnum(e.modes[m], out mode))
                {
                    errors.Add(e.id + ": mode 값 오류 '" + e.modes[m] + "' / invalid mode");
                    continue;
                }
                if (e.chainOnly) continue;

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

        static bool TryParseStat(string s, out StatType stat) => StatMap.TryParse(s, out stat);

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