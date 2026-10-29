using System.Collections.Generic;
using NUnit.Framework;
using Roadnest.Core;
using UnityEditor;
using UnityEngine;

namespace Roadnest.Tests
{
    public class SeededRandomTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new SeededRandom(123);
            var b = new SeededRandom(123);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.Next(), b.Next());
        }

        [Test]
        public void Derive_SameInput_SameStream()
        {
            var a = SeededRandom.Derive(7, 3, 2, RngPurpose.Outcome);
            var b = SeededRandom.Derive(7, 3, 2, RngPurpose.Outcome);
            Assert.AreEqual(a.Next(), b.Next());
        }

        [Test]
        public void Derive_DifferentSlot_DifferentStream()
        {
            var a = SeededRandom.Derive(7, 3, 1, RngPurpose.Outcome);
            var b = SeededRandom.Derive(7, 3, 2, RngPurpose.Outcome);
            Assert.AreNotEqual(a.Next(), b.Next());
        }

        [Test]
        public void Range_StaysInBounds()
        {
            var r = new SeededRandom(99);
            for (int i = 0; i < 10000; i++)
            {
                int v = r.Range(0, 100);
                Assert.IsTrue(v >= 0 && v < 100);
            }
        }

        [Test]
        public void RangeLong_StaysInBounds()
        {
            var r = new SeededRandom(5);
            for (int i = 0; i < 10000; i++)
            {
                long v = r.RangeLong(0, 1234567);
                Assert.IsTrue(v >= 0 && v < 1234567);
            }
        }
    }

    public class EventTableTests
    {
        const string JsonPath = "Assets/_Project/Tests/events_test.json";

        static EventTable LoadSample(List<string> errors)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
            Assert.IsNotNull(asset, "events.json을 찾을 수 없음: " + JsonPath);
            return EventTable.FromJson(asset.text, errors);
        }

        static bool Contains(SlotCandidate[] list, string id)
        {
            for (int i = 0; i < list.Length; i++)
                if (list[i].Def.Id == id) return true;
            return false;
        }

        static bool HasError(List<string> errors, string part)
        {
            for (int i = 0; i < errors.Count; i++)
                if (errors[i].Contains(part)) return true;
            return false;
        }

        static EventTableDto MakeDto()
        {
            return new EventTableDto
            {
                version = 1,
                fallback = "EVT_FB",
                daySlots = TestData.DaySlots(),
                events = new[] { MakeEvent("EVT_T"), MakeFallback() }
            };
        }

        static EventDto MakeEvent(string id)
        {
            return new EventDto
            {
                id = id,
                titleKey = "t",
                textKey = "x",
                trigger = "telegraphed",
                modes = new[] { "depart" },
                slots = new[] { new SlotWeightDto { slot = "Minor", weight = 10 } },
                choices = new[] { MakeChoice("A"), MakeChoice("B") }
            };
        }

        static EventDto MakeFallback()
        {
            return new EventDto
            {
                id = "EVT_FB",
                titleKey = "t",
                textKey = "x",
                trigger = "telegraphed",
                choices = new[] { MakeChoice("FA"), MakeChoice("FB") }
            };
        }

        static ChoiceDto MakeChoice(string id)
        {
            return new ChoiceDto
            {
                id = id,
                textKey = id,
                outcomes = new[] { new OutcomeDto { kind = "rest", textKey = "o" } }
            };
        }

        [Test]
        public void Sample_LoadsWithoutErrors()
        {
            var errors = new List<string>();
            var table = LoadSample(errors);
            Assert.IsNotNull(table, string.Join("\n", errors));
            Assert.AreEqual(0, errors.Count);
            Assert.AreEqual(18, table.Events.Length);
        }

        [Test]
        public void Candidates_FilteredByModeAndSlot()
        {
            var table = LoadSample(new List<string>());
            Assert.IsTrue(Contains(table.GetCandidates(MoveMode.Depart, SlotType.Minor), "EVT_FALLEN_TREE"));
            Assert.IsFalse(Contains(table.GetCandidates(MoveMode.Settle, SlotType.Minor), "EVT_FALLEN_TREE"));
            Assert.IsTrue(Contains(table.GetCandidates(MoveMode.Settle, SlotType.Minor), "EVT_TRADER"));
            Assert.Greater(table.GetCandidates(MoveMode.Settle, SlotType.MaintenanceRecovery).Length, 0);
        }

        [Test]
        public void Sample_WeightModsTagsAndFallbackParsed()
        {
            var table = LoadSample(new List<string>());
            EventDef def;
            Assert.IsTrue(table.TryGetEvent("EVT_ZOMBIE_RAID", out def));
            Assert.AreEqual(WeightMods.SettleStreak | WeightMods.Day, def.Mods);
            Assert.GreaterOrEqual(table.TagId("zombie_raid"), 0);
            Assert.AreEqual("EVT_QUIET_DAY", table.Fallback.Id);
        }

        [Test]
        public void Outcomes_RestIsResolvedToRemainder()
        {
            var table = LoadSample(new List<string>());
            EventDef def;
            Assert.IsTrue(table.TryGetEvent("EVT_FALLEN_TREE", out def));
            OutcomeDef[] o = def.Choices[0].Outcomes;
            Assert.AreEqual(60, o[0].Percent);
            Assert.AreEqual(40, o[1].Percent);
            Assert.AreEqual(100, o[1].CumulativeEnd);
        }

        [Test]
        public void Invalid_UnknownStat_Fails()
        {
            var dto = MakeDto();
            dto.events[0].choices[0].cost = new[] { new EffectDto { stat = "gold", amount = -1 } };
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "gold"));
        }

        [Test]
        public void Invalid_PercentOver100_Fails()
        {
            var dto = MakeDto();
            dto.events[0].choices[0].outcomes = new[]
            {
                new OutcomeDto { kind = "fixed", percent = 60, textKey = "a" },
                new OutcomeDto { kind = "fixed", percent = 60, textKey = "b" }
            };
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "100"));
        }

        [Test]
        public void Invalid_DuplicateId_Fails()
        {
            var dto = MakeDto();
            dto.events = new[] { dto.events[0], dto.events[0] };
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "중복"));
        }

        [Test]
        public void Invalid_HpAllRequirement_Fails()
        {
            var dto = MakeDto();
            dto.events[0].choices[0].requirements = new[] { new RequirementDto { stat = "hp_all", op = "gte", value = 1 } };
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "hp_all"));
        }

        [Test]
        public void Invalid_MissingFallback_Fails()
        {
            var dto = MakeDto();
            dto.fallback = null;
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "fallback"));
        }

        [Test]
        public void Invalid_UnknownFallback_Fails()
        {
            var dto = MakeDto();
            dto.fallback = "EVT_NOPE";
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "EVT_NOPE"));
        }

        [Test]
        public void Invalid_UnknownWeightMod_Fails()
        {
            var dto = MakeDto();
            dto.events[0].weightMods = new[] { "luck" };
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "luck"));
        }

        [Test]
        public void Sample_DaySlotsParsed()
        {
            var table = LoadSample(new List<string>());
            SlotType[] depart = table.GetDaySlots(MoveMode.Depart);
            SlotType[] settle = table.GetDaySlots(MoveMode.Settle);
            Assert.AreEqual(5, depart.Length);
            Assert.AreEqual(SlotType.Danger, depart[2]);
            Assert.AreEqual(SlotType.Night, settle[4]);
        }

        [Test]
        public void Invalid_MissingDaySlotsMode_Fails()
        {
            var dto = MakeDto();
            dto.daySlots = new[] { dto.daySlots[0] };
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "Settle"));
        }

        [Test]
        public void Invalid_UnknownDaySlot_Fails()
        {
            var dto = MakeDto();
            dto.daySlots[0].slots = new[] { "Boss" };
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "Boss"));
        }

        [Test]
        public void Invalid_NoFreeChoice_Fails()
        {
            var dto = MakeDto();
            for (int i = 0; i < dto.events[0].choices.Length; i++)
                dto.events[0].choices[i].cost = new[] { new EffectDto { stat = "fuel", amount = -1 } };
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "free choice"));
        }

        [Test]
        public void Invalid_TooManyChoices_Fails()
        {
            var dto = MakeDto();
            var many = new ChoiceDto[EventLimits.MaxChoices + 1];
            for (int i = 0; i < many.Length; i++) many[i] = MakeChoice("C" + i);
            dto.events[0].choices = many;
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "at most " + EventLimits.MaxChoices + " choices"));
        }

        [Test]
        public void Invalid_TooManyOutcomes_Fails()
        {
            var dto = MakeDto();
            dto.events[0].choices[0].outcomes = new[]
            {
                new OutcomeDto { kind = "fixed", percent = 30, textKey = "a" },
                new OutcomeDto { kind = "fixed", percent = 30, textKey = "b" },
                new OutcomeDto { kind = "rest", textKey = "c" }
            };
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "at most " + EventLimits.MaxOutcomes + " outcomes"));
        }

        [Test]
        public void Invalid_TooManyOutcomeEffects_Fails()
        {
            var dto = MakeDto();
            dto.events[0].choices[0].outcomes = new[]
            {
                new OutcomeDto
                {
                    kind = "rest", textKey = "o",
                    effects = new[]
                    {
                        new EffectDto { stat = "fuel", amount = -1 },
                        new EffectDto { stat = "food", amount = -1 },
                        new EffectDto { stat = "water", amount = -1 },
                        new EffectDto { stat = "durability", amount = -1 }
                    }
                }
            };
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "effects per outcome"));
        }

        [Test]
        public void Invalid_TooManyCostEffects_Fails()
        {
            var dto = MakeDto();
            dto.events[0].choices[0].cost = new[]
            {
                new EffectDto { stat = "fuel", amount = -1 },
                new EffectDto { stat = "food", amount = -1 },
                new EffectDto { stat = "water", amount = -1 },
                new EffectDto { stat = "durability", amount = -1 }
            };
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "cost effect"));
        }

        // 일반 n개 + 특별 m개 선택지를 만든다 / builds n normal + m special choices
        static ChoiceDto[] MakeChoices(int normal, int special)
        {
            var list = new ChoiceDto[normal + special];
            for (int i = 0; i < normal; i++) list[i] = MakeChoice("N" + i);
            for (int i = 0; i < special; i++)
            {
                ChoiceDto s = MakeChoice("S" + i);
                s.special = true;
                list[normal + i] = s;
            }
            return list;
        }

        [Test]
        public void Special_FourNormalPlusOneSpecial_Loads()
        {
            var dto = MakeDto();
            dto.events[0].choices = MakeChoices(EventLimits.MaxNormalChoices, EventLimits.MaxSpecialChoices);
            dto.events[0].choices[EventLimits.MaxNormalChoices].iconKey = "icon-special";
            var errors = new List<string>();
            var table = EventTable.Build(dto, errors);
            Assert.IsNotNull(table, string.Join("\n", errors));
            ChoiceDef sp = table.Events[0].Choices[EventLimits.MaxNormalChoices];
            Assert.IsTrue(sp.Special);
            Assert.AreEqual("icon-special", sp.IconKey);
            Assert.IsFalse(table.Events[0].Choices[0].Special);
        }

        [Test]
        public void Invalid_TwoSpecialChoices_Fails()
        {
            var dto = MakeDto();
            dto.events[0].choices = MakeChoices(2, EventLimits.MaxSpecialChoices + 1);
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "special choices"));
        }

        [Test]
        public void Invalid_FiveNormalChoices_Fails()
        {
            var dto = MakeDto();
            dto.events[0].choices = MakeChoices(EventLimits.MaxNormalChoices + 1, 0);
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "normal choices"));
        }

        // 특별 선택지는 무료여도 '항상 고를 수 있는 선택지'로 치지 않는다
        // a free special choice does not count as an always-pickable choice
        [Test]
        public void Invalid_OnlySpecialIsFree_Fails()
        {
            var dto = MakeDto();
            ChoiceDto[] choices = MakeChoices(2, 1);
            for (int i = 0; i < 2; i++)
                choices[i].cost = new[] { new EffectDto { stat = "fuel", amount = -1 } };
            dto.events[0].choices = choices;
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "free choice"));
        }

        [Test]
        public void Invalid_DuplicateCostStat_Fails()
        {
            var dto = MakeDto();
            dto.events[0].choices[0].cost = new[]
            {
                new EffectDto { stat = "fuel", amount = -1 },
                new EffectDto { stat = "fuel", amount = -2 }
            };
            var errors = new List<string>();
            Assert.IsNull(EventTable.Build(dto, errors));
            Assert.IsTrue(HasError(errors, "duplicate stat"));
        }

        [Test]
        public void Item_Stats_AcceptedInCostRequirementAndEffect()
        {
            var dto = MakeDto();
            ChoiceDto ch = dto.events[0].choices[0];
            ch.requirements = new[] { new RequirementDto { stat = "crowbar", op = "gte", value = 1 } };
            ch.cost = new[] { new EffectDto { stat = "crowbar", amount = -1 } };
            ch.outcomes = new[]
            {
        new OutcomeDto
        {
            kind = "rest", textKey = "o",
            effects = new[] { new EffectDto { stat = "steel_plate", amount = 2 } }
        }
    };
            var errors = new List<string>();
            Assert.IsNotNull(EventTable.Build(dto, errors), string.Join("\n", errors));
        }
    }
}