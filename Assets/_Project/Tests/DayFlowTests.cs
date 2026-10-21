using System;
using NUnit.Framework;
using Roadnest.Core;

namespace Roadnest.Tests
{
    public class DayFlowTests
    {
        static ChoiceDto Safe(string id) => new ChoiceDto
        {
            id = id,
            textKey = id,
            outcomes = new[] { new OutcomeDto { kind = "rest", textKey = "o" } }
        };

        static ChoiceDto Deadly(string id) => new ChoiceDto
        {
            id = id,
            textKey = id,
            outcomes = new[]
            {
                new OutcomeDto
                {
                    kind = "rest", textKey = "o",
                    effects = new[] { new EffectDto { stat = "durability", amount = -500 } }
                }
            }
        };

        // 폴백 이벤트만 있는 테이블: 모든 슬롯이 폴백으로 채워진다 / fallback-only table: every slot becomes the fallback
        static DayFlow NewFlow(ChoiceDto[] choices = null, RunRules rules = null, int characters = 2)
        {
            rules = rules ?? new RunRules();
            var dto = new EventTableDto
            {
                version = 1,
                fallback = "EVT_FB",
                daySlots = TestData.DaySlots(),
                events = new[]
                {
                    new EventDto
                    {
                        id = "EVT_FB", titleKey = "t", textKey = "x", trigger = "telegraphed",
                        choices = choices ?? new[] { Safe("A"), Safe("B") }
                    }
                }
            };
            var errors = new System.Collections.Generic.List<string>();
            var table = EventTable.Build(dto, errors);
            Assert.IsNotNull(table, string.Join("\n", errors));
            var chars = new CharacterState[characters];
            for (int i = 0; i < characters; i++) chars[i] = new CharacterState("c" + i, null, rules.MaxCharacterHp);
            var run = new RunState(rules, new ResourceStore(rules.ResourceCap), chars);
            return new DayFlow(run, table, 1);
        }

        static void ToDistribution(DayFlow f, MoveMode mode)
        {
            Assert.IsTrue(f.ChooseMode(mode));
            while (f.Phase == DayPhase.Event) Assert.IsTrue(f.Choose(0).Success);
            Assert.AreEqual(DayPhase.Distribution, f.Phase);
        }

        static void GiveAll(DayFlow f)
        {
            for (int i = 0; i < f.Run.Characters.Length; i++)
            {
                f.SetRation(i, ResourceType.Food, true);
                f.SetRation(i, ResourceType.Water, true);
            }
        }

        static void PlayDay(DayFlow f, MoveMode mode)
        {
            ToDistribution(f, mode);
            GiveAll(f);
            Assert.IsTrue(f.ConfirmDistribution());
        }

        [Test]
        public void Depart_PaysFuelAndDurability_EntersEventPhase()
        {
            var f = NewFlow();
            Assert.IsTrue(f.ChooseMode(MoveMode.Depart));
            Assert.AreEqual(4, f.Run.Resources.Get(ResourceType.Fuel));
            Assert.AreEqual(95, f.Run.Durability);
            Assert.AreEqual(0, f.Run.ConsecutiveSettles);
            Assert.AreEqual(DayPhase.Event, f.Phase);
            Assert.AreEqual(0, f.SlotIndex);
            Assert.IsNotNull(f.CurrentEvent);
        }

        [Test]
        public void Depart_WithoutFuel_IsRejected_SettleStillWorks()
        {
            var f = NewFlow();
            f.Run.Apply(new Effect(StatType.Fuel, -5));
            Assert.IsFalse(f.CanDepart);
            Assert.IsFalse(f.ChooseMode(MoveMode.Depart));
            Assert.AreEqual(DayPhase.ChooseMode, f.Phase);
            Assert.IsTrue(f.ChooseMode(MoveMode.Settle));
        }

        [Test]
        public void Settle_NoFuelCost_RestoresDurability_AddsStreak()
        {
            var f = NewFlow();
            f.Run.Apply(new Effect(StatType.Durability, -10));
            Assert.IsTrue(f.ChooseMode(MoveMode.Settle));
            Assert.AreEqual(5, f.Run.Resources.Get(ResourceType.Fuel));
            Assert.AreEqual(93, f.Run.Durability);
            Assert.AreEqual(1, f.Run.ConsecutiveSettles);
        }

        [Test]
        public void FullDay_Depart_ConsumesRationsRepairsAndAdvancesDay()
        {
            var f = NewFlow();
            f.Run.Apply(new Effect(StatType.Durability, -30));
            PlayDay(f, MoveMode.Depart);
            Assert.AreEqual(2, f.Run.Day);
            Assert.AreEqual(DayPhase.ChooseMode, f.Phase);
            Assert.AreEqual(3, f.Run.Resources.Get(ResourceType.Food));
            Assert.AreEqual(3, f.Run.Resources.Get(ResourceType.Water));
            Assert.AreEqual(75, f.Run.Durability); // 70 - 5 + 10
        }

        [Test]
        public void FullDay_Settle_UsesBiggerRepair()
        {
            var f = NewFlow();
            f.Run.Apply(new Effect(StatType.Durability, -50));
            PlayDay(f, MoveMode.Settle);
            Assert.AreEqual(2, f.Run.Day);
            Assert.AreEqual(68, f.Run.Durability); // 50 + 3 + 15
            Assert.AreEqual(5, f.Run.Resources.Get(ResourceType.Fuel));
        }

        [Test]
        public void Distribution_MissingRations_CostsHp()
        {
            var f = NewFlow();
            ToDistribution(f, MoveMode.Depart);
            f.SetRation(0, ResourceType.Food, true); // 0번: 식량만 / char 0: food only
            Assert.IsTrue(f.ConfirmDistribution());  // 1번: 아무것도 못 받음 / char 1: nothing
            Assert.AreEqual(85, f.Run.Characters[0].Hp);
            Assert.AreEqual(65, f.Run.Characters[1].Hp);
            Assert.AreEqual(4, f.Run.Resources.Get(ResourceType.Food));
        }

        [Test]
        public void Distribution_CannotGiveMoreThanStock()
        {
            var f = NewFlow();
            f.Run.Apply(new Effect(StatType.Food, -4));
            ToDistribution(f, MoveMode.Depart);
            Assert.IsTrue(f.SetRation(0, ResourceType.Food, true));
            Assert.IsFalse(f.SetRation(1, ResourceType.Food, true));
            Assert.IsTrue(f.SetRation(0, ResourceType.Food, false));
            Assert.IsTrue(f.SetRation(1, ResourceType.Food, true));
        }

        [Test]
        public void Distribution_Unassigned_CountsAsMissed_ResourcesRemain()
        {
            var f = NewFlow();
            ToDistribution(f, MoveMode.Depart);
            Assert.IsTrue(f.ConfirmDistribution());
            Assert.AreEqual(5, f.Run.Resources.Get(ResourceType.Food));
            Assert.AreEqual(65, f.Run.Characters[0].Hp);
            Assert.AreEqual(65, f.Run.Characters[1].Hp);
        }

        [Test]
        public void Death_DuringEvent_EndsRunImmediately()
        {
            var f = NewFlow(new[] { Deadly("K"), Safe("S") });
            Assert.IsTrue(f.ChooseMode(MoveMode.Depart));
            Assert.IsTrue(f.Choose(0).Success);
            Assert.AreEqual(DayPhase.Over, f.Phase);
            Assert.AreEqual(EndReason.VehicleDestroyed, f.Run.EndReason);
            Assert.AreEqual(1, f.Run.Day);
            Assert.IsNull(f.CurrentEvent);
        }

        [Test]
        public void Death_AtSettlement_KeepsDay_SkipsRepair()
        {
            var f = NewFlow();
            f.Run.Apply(new Effect(StatType.HpAll, -90));
            ToDistribution(f, MoveMode.Depart);
            Assert.IsTrue(f.ConfirmDistribution());
            Assert.AreEqual(DayPhase.Over, f.Phase);
            Assert.AreEqual(EndReason.AllDead, f.Run.EndReason);
            Assert.AreEqual(1, f.Run.Day);
            Assert.AreEqual(95, f.Run.Durability);
        }

        [Test]
        public void Choose_InvalidIndex_IsRejected()
        {
            var f = NewFlow();
            f.ChooseMode(MoveMode.Depart);
            Assert.IsFalse(f.Choose(5).Success);
            Assert.IsFalse(f.Choose(-1).Success);
            Assert.AreEqual(0, f.SlotIndex);
            Assert.AreEqual(DayPhase.Event, f.Phase);
        }

        [Test]
        public void SettleStreak_CountsConsecutiveSettles_DepartResets()
        {
            var f = NewFlow(null, new RunRules { StartFood = 20, StartWater = 20 });
            PlayDay(f, MoveMode.Settle);
            Assert.AreEqual(1, f.Run.ConsecutiveSettles);
            PlayDay(f, MoveMode.Settle);
            Assert.AreEqual(2, f.Run.ConsecutiveSettles);
            PlayDay(f, MoveMode.Depart);
            Assert.AreEqual(0, f.Run.ConsecutiveSettles);
        }

        [Test]
        public void Constructor_SlotCountMismatch_Throws()
        {
            Assert.Throws<ArgumentException>(() => NewFlow(null, new RunRules { EventsPerDay = 4 }));
        }

        [Test]
        public void HpLoss_Preview_FollowsRations_AndMatchesConfirm()
        {
            var f = NewFlow();
            ToDistribution(f, MoveMode.Depart);
            Assert.AreEqual(35, f.GetHpLoss(0));    // 아무것도 안 줌 / nothing given
            f.SetRation(0, ResourceType.Food, true);
            Assert.AreEqual(15, f.GetHpLoss(0));
            f.SetRation(0, ResourceType.Water, true);
            Assert.AreEqual(0, f.GetHpLoss(0));

            int expected = f.GetHpLoss(1);          // 1번은 그대로 -35 / char 1 stays at -35
            int before = f.Run.Characters[1].Hp;
            Assert.IsTrue(f.ConfirmDistribution());
            Assert.AreEqual(before - expected, f.Run.Characters[1].Hp);
        }

        [Test]
        public void HpLoss_OutsideDistribution_IsZero()
        {
            var f = NewFlow();
            Assert.AreEqual(0, f.GetHpLoss(0));
        }

        [Test]
        public void HpLoss_DeadCharacter_IsZero()
        {
            var f = NewFlow();
            ToDistribution(f, MoveMode.Depart);
            f.Run.Characters[0].Hp = 0;
            Assert.AreEqual(0, f.GetHpLoss(0));
        }
    }
}