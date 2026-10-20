using System.Collections.Generic;
using NUnit.Framework;
using Roadnest.Core;

namespace Roadnest.Tests
{
    public class EventSelectorTests
    {
        static ChoiceDto Choice(string id)
        {
            return new ChoiceDto
            {
                id = id,
                textKey = id,
                outcomes = new[] { new OutcomeDto { kind = "rest", textKey = "o" } }
            };
        }

        static EventDto Ev(string id, int weight, string[] mods = null, int dayMin = 0)
        {
            return new EventDto
            {
                id = id,
                titleKey = "t",
                textKey = "x",
                trigger = "telegraphed",
                modes = new[] { "depart" },
                slots = new[] { new SlotWeightDto { slot = "Minor", weight = weight } },
                weightMods = mods,
                dayMin = dayMin,
                choices = new[] { Choice(id + "_A"), Choice(id + "_B") }
            };
        }

        static EventTable Table(params EventDto[] events)
        {
            var all = new List<EventDto>(events);
            all.Add(new EventDto
            {
                id = "EVT_FB",
                titleKey = "t",
                textKey = "x",
                trigger = "telegraphed",
                choices = new[] { Choice("FB_A"), Choice("FB_B") }
            });
            var dto = new EventTableDto { version = 1, fallback = "EVT_FB", daySlots = TestData.DaySlots(), events = all.ToArray() };
            var errors = new List<string>();
            var table = EventTable.Build(dto, errors);
            Assert.IsNotNull(table, string.Join("\n", errors));
            return table;
        }

        static int CountPicks(EventSelector sel, string id, int streak)
        {
            int n = 0;
            for (int i = 1; i <= 2000; i++)
            {
                sel.BeginDay();
                if (sel.Pick((ulong)i, 1, 0, MoveMode.Depart, SlotType.Minor, streak).Id == id) n++;
            }
            return n;
        }

        [Test]
        public void EffectiveWeight_NoMods_IsBaseTimesScale()
        {
            Assert.AreEqual(100000L, EventSelector.EffectiveWeight(10, WeightMods.None, 30, 9));
        }

        [Test]
        public void EffectiveWeight_SettleStreak_CapsAtFourSteps()
        {
            Assert.AreEqual(100000L, EventSelector.EffectiveWeight(10, WeightMods.SettleStreak, 1, 1));
            Assert.AreEqual(160000L, EventSelector.EffectiveWeight(10, WeightMods.SettleStreak, 1, 3));
            Assert.AreEqual(220000L, EventSelector.EffectiveWeight(10, WeightMods.SettleStreak, 1, 5));
            Assert.AreEqual(220000L, EventSelector.EffectiveWeight(10, WeightMods.SettleStreak, 1, 9));
        }

        [Test]
        public void EffectiveWeight_Day_AddsFivePercentPerDay()
        {
            Assert.AreEqual(100000L, EventSelector.EffectiveWeight(10, WeightMods.Day, 1, 0));
            Assert.AreEqual(150000L, EventSelector.EffectiveWeight(10, WeightMods.Day, 11, 0));
        }

        [Test]
        public void Pick_SameInputs_SameEvent()
        {
            var table = Table(Ev("A", 10), Ev("B", 10), Ev("C", 10), Ev("D", 10), Ev("E", 10));
            var s1 = new EventSelector(table);
            var s2 = new EventSelector(table);
            for (int i = 1; i <= 100; i++)
            {
                s1.BeginDay();
                s2.BeginDay();
                var a = s1.Pick((ulong)i, 3, 1, MoveMode.Depart, SlotType.Minor, 0);
                var b = s2.Pick((ulong)i, 3, 1, MoveMode.Depart, SlotType.Minor, 0);
                Assert.AreEqual(a.Id, b.Id);
            }
        }

        [Test]
        public void Pick_ExcludesEventsSeenToday()
        {
            var sel = new EventSelector(Table(Ev("A", 10), Ev("B", 10)));
            sel.BeginDay();
            var first = sel.Pick(1, 1, 0, MoveMode.Depart, SlotType.Minor, 0);
            var second = sel.Pick(1, 1, 1, MoveMode.Depart, SlotType.Minor, 0);
            Assert.AreNotEqual(first.Id, second.Id);
        }

        [Test]
        public void Pick_WhenAllSeen_RepeatsInsteadOfFallback()
        {
            var sel = new EventSelector(Table(Ev("A", 10), Ev("B", 10)));
            sel.BeginDay();
            sel.Pick(1, 1, 0, MoveMode.Depart, SlotType.Minor, 0);
            sel.Pick(1, 1, 1, MoveMode.Depart, SlotType.Minor, 0);
            var third = sel.Pick(1, 1, 2, MoveMode.Depart, SlotType.Minor, 0);
            Assert.AreNotEqual("EVT_FB", third.Id);
        }

        [Test]
        public void Pick_NoCandidates_ReturnsFallback()
        {
            var sel = new EventSelector(Table(Ev("A", 10)));
            sel.BeginDay();
            var picked = sel.Pick(1, 1, 0, MoveMode.Settle, SlotType.Night, 0);
            Assert.AreEqual("EVT_FB", picked.Id);
        }

        [Test]
        public void Pick_RespectsDayRange()
        {
            var sel = new EventSelector(Table(Ev("OLD", 10, null, 5), Ev("A", 10)));
            for (int i = 1; i <= 200; i++)
            {
                sel.BeginDay();
                var picked = sel.Pick((ulong)i, 1, 0, MoveMode.Depart, SlotType.Minor, 0);
                Assert.AreNotEqual("OLD", picked.Id);
            }
        }

        [Test]
        public void Pick_DistributionFollowsWeights()
        {
            var sel = new EventSelector(Table(Ev("A", 90), Ev("B", 10)));
            int a = CountPicks(sel, "A", 0);
            Assert.IsTrue(a > 1700 && a < 1900, "A count = " + a);
        }

        [Test]
        public void Pick_SettleStreakBoostsModdedEvent()
        {
            var sel = new EventSelector(Table(Ev("RAID", 10, new[] { "settle_streak" }), Ev("CALM", 10)));
            int low = CountPicks(sel, "RAID", 1);
            int high = CountPicks(sel, "RAID", 5);
            Assert.IsTrue(high > low + 200, "low = " + low + ", high = " + high);
        }
    }
}