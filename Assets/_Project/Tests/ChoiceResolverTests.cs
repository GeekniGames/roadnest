using System.Collections.Generic;
using NUnit.Framework;
using Roadnest.Core;

namespace Roadnest.Tests
{
    public class ChoiceResolverTests
    {
        static RunState NewRun()
        {
            var rules = new RunRules();
            return RunState.CreateDefault(rules, new ResourceStore(rules.ResourceCap));
        }

        static ChoiceDto Dummy(string id) => new ChoiceDto
        {
            id = id,
            textKey = id,
            outcomes = new[] { new OutcomeDto { kind = "rest", textKey = "o" } }
        };

        // 테스트할 선택지 하나를 이벤트에 끼워 넣어 EventTable로 빌드 / embed one choice in an event and build via EventTable
        static ChoiceDef Build(ChoiceDto choice)
        {
            var dto = new EventTableDto
            {
                version = 1,
                fallback = "FB",
                daySlots = TestData.DaySlots(),
                events = new[]
                {
                    new EventDto
                    {
                        id = "EVT", titleKey = "t", textKey = "x", trigger = "telegraphed",
                        modes = new[] { "depart" },
                        slots = new[] { new SlotWeightDto { slot = "Minor", weight = 1 } },
                        choices = new[] { choice, Dummy("D1"), Dummy("D2") }
                    },
                    new EventDto
                    {
                        id = "FB", titleKey = "t", textKey = "x", trigger = "telegraphed",
                        choices = new[] { Dummy("F1"), Dummy("F2") }
                    }
                }
            };
            var errors = new List<string>();
            var table = EventTable.Build(dto, errors);
            Assert.IsNotNull(table, string.Join("\n", errors));
            return table.Events[0].Choices[0];
        }

        static ChoiceDto Choice(OutcomeDto[] outcomes, EffectDto[] cost = null,
                                RequirementDto[] reqs = null, bool hidden = false)
        {
            return new ChoiceDto { id = "C", textKey = "c", hidden = hidden, cost = cost, requirements = reqs, outcomes = outcomes };
        }

        static OutcomeDto Fixed(int percent, params EffectDto[] fx)
            => new OutcomeDto { kind = "fixed", percent = percent, textKey = "o", effects = fx };

        static OutcomeDto Rest(params EffectDto[] fx)
            => new OutcomeDto { kind = "rest", textKey = "o", effects = fx };

        static EffectDto Fx(string stat, int amount) => new EffectDto { stat = stat, amount = amount };

        [Test]
        public void Status_UnmetRequirement_IsDisabled()
        {
            var c = Build(Choice(new[] { Rest() }, null,
                new[] { new RequirementDto { stat = "fuel", op = "gte", value = 50 } }));
            Assert.AreEqual(ChoiceStatus.Disabled, ChoiceResolver.GetStatus(NewRun(), c));
        }

        [Test]
        public void Status_Hidden_UnmetIsHidden_MetIsAvailable()
        {
            var c = Build(Choice(new[] { Rest() }, null,
                new[] { new RequirementDto { stat = "fuel", op = "gte", value = 6 } }, true));
            var run = NewRun();
            Assert.AreEqual(ChoiceStatus.Hidden, ChoiceResolver.GetStatus(run, c));
            run.Apply(new Effect(StatType.Fuel, 1));
            Assert.AreEqual(ChoiceStatus.Available, ChoiceResolver.GetStatus(run, c));
        }

        [Test]
        public void Status_CannotPayCost_IsDisabled()
        {
            var c = Build(Choice(new[] { Rest() }, new[] { Fx("fuel", -6) }));
            Assert.AreEqual(ChoiceStatus.Disabled, ChoiceResolver.GetStatus(NewRun(), c));
        }

        [Test]
        public void Resolve_PaysCostAndAppliesEffects()
        {
            var c = Build(Choice(new[] { Rest(Fx("durability", -15)) }, new[] { Fx("fuel", -2) }));
            var run = NewRun();
            var r = ChoiceResolver.Resolve(run, c, 1, 1, 0);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(3, run.Resources.Get(ResourceType.Fuel));
            Assert.AreEqual(85, run.Durability);
        }

        [Test]
        public void Resolve_Unavailable_ChangesNothing()
        {
            var c = Build(Choice(new[] { Rest(Fx("durability", -15)) }, new[] { Fx("fuel", -6) }));
            var run = NewRun();
            var r = ChoiceResolver.Resolve(run, c, 1, 1, 0);
            Assert.IsFalse(r.Success);
            Assert.AreEqual(5, run.Resources.Get(ResourceType.Fuel));
            Assert.AreEqual(100, run.Durability);
        }

        [Test]
        public void Resolve_SameSeed_SameOutcome()
        {
            var c = Build(Choice(new[] { Fixed(50), Rest() }));
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var a = ChoiceResolver.Resolve(NewRun(), c, seed, 2, 1);
                var b = ChoiceResolver.Resolve(NewRun(), c, seed, 2, 1);
                Assert.AreEqual(a.OutcomeIndex, b.OutcomeIndex);
            }
        }

        [Test]
        public void Resolve_DistributionFollowsPercent()
        {
            var c = Build(Choice(new[] { Fixed(60), Rest() }));
            int first = 0;
            for (ulong seed = 1; seed <= 2000; seed++)
                if (ChoiceResolver.Resolve(NewRun(), c, seed, 1, 0).OutcomeIndex == 0) first++;
            Assert.IsTrue(first > 1100 && first < 1300, "first = " + first);
        }

        [Test]
        public void Resolve_ResourceNeverGoesBelowZero()
        {
            var c = Build(Choice(new[] { Rest(Fx("food", -50)) }));
            var run = NewRun();
            ChoiceResolver.Resolve(run, c, 1, 1, 0);
            Assert.AreEqual(0, run.Resources.Get(ResourceType.Food));
            Assert.IsFalse(run.IsOver);
        }

        [Test]
        public void Resolve_AllEffectsApplied_FirstEndReasonWins()
        {
            var c = Build(Choice(new[] { Rest(Fx("durability", -500), Fx("hp_all", -500)) }));
            var run = NewRun();
            ChoiceResolver.Resolve(run, c, 1, 1, 0);
            Assert.AreEqual(EndReason.VehicleDestroyed, run.EndReason);
            Assert.AreEqual(0, run.Durability);
            Assert.AreEqual(0, run.AliveCount);
        }

        [Test]
        public void Resolve_AfterRunOver_DoesNothing()
        {
            var kill = Build(Choice(new[] { Rest(Fx("durability", -500)) }));
            var next = Build(Choice(new[] { Rest(Fx("fuel", 1)) }));
            var run = NewRun();
            ChoiceResolver.Resolve(run, kill, 1, 1, 0);
            var r = ChoiceResolver.Resolve(run, next, 1, 1, 1);
            Assert.IsFalse(r.Success);
            Assert.AreEqual(5, run.Resources.Get(ResourceType.Fuel));
        }
    }
}