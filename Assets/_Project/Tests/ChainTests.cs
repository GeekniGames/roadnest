using System.Collections.Generic;
using NUnit.Framework;
using Roadnest.Core;

namespace Roadnest.Tests
{
    public class ChainTests
    {
        static readonly string[] Depart = { "depart" };
        static readonly string[] Settle = { "settle" };

        static OutcomeDto Out(string next = null, string type = null, int pct = 0, int weight = 0)
            => new OutcomeDto { kind = "rest", textKey = "t", next = next, nextType = type, nextPercent = pct, nextWeight = weight };

        static ChoiceDto Choice(string id, OutcomeDto o)
            => new ChoiceDto { id = id, textKey = "c", outcomes = new[] { o } };

        static EventDto Ev(string id, string[] modes, OutcomeDto link = null, bool chainOnly = false,
                           int slotMin = 0, int slotMax = 0)
            => new EventDto
            {
                id = id,
                titleKey = "t",
                textKey = "x",
                trigger = "telegraphed",
                modes = modes,
                slots = (chainOnly || modes == null) ? null : new[] { new SlotWeightDto { slot = "Minor", weight = 1 } },
                chainOnly = chainOnly,
                slotMin = slotMin,
                slotMax = slotMax,
                choices = new[] { Choice("a", link ?? Out()), Choice("b", Out()) }
            };

        static DaySlotsDto Day(string mode)
            => new DaySlotsDto { mode = mode, slots = new[] { "Minor", "Minor", "Minor", "Minor", "Minor" } };

        static EventTable Build(List<string> errors, params EventDto[] evs)
        {
            var list = new List<EventDto>(evs) { Ev("fb", null) };
            return EventTable.Build(new EventTableDto
            {
                version = 1,
                fallback = "fb",
                daySlots = new[] { Day("depart"), Day("settle") },
                events = list.ToArray()
            }, errors);
        }

        static bool HasError(List<string> errors, string part)
        {
            for (int i = 0; i < errors.Count; i++)
                if (errors[i].Contains(part)) return true;
            return false;
        }

        static EventTable Sample()
        {
            var errors = new List<string>();
            EventTable t = Build(errors,
                Ev("s", Depart, Out("f", "same_day", 100), slotMax: 3),
                Ev("f", Depart, chainOnly: true),
                Ev("g", Depart, chainOnly: true));
            Assert.IsNotNull(t, string.Join("\n", errors));
            return t;
        }

        // ---- 검증 / validation ----

        [Test]
        public void Valid_SameDayChain_Builds()
        {
            Sample();
        }

        [Test]
        public void Error_TargetNotFound()
        {
            var errors = new List<string>();
            Assert.IsNull(Build(errors, Ev("s", Depart, Out("zzz", "same_day", 50), slotMax: 3)));
            Assert.IsTrue(HasError(errors, "not found"));
        }

        [Test]
        public void Error_TargetNotChainOnly()
        {
            var errors = new List<string>();
            Assert.IsNull(Build(errors,
                Ev("s", Depart, Out("f", "next_day", 50)),
                Ev("f", Depart)));
            Assert.IsTrue(HasError(errors, "must be chainOnly"));
        }

        [Test]
        public void Error_SameDayModesDoNotCoverStarter()
        {
            var errors = new List<string>();
            Assert.IsNull(Build(errors,
                Ev("s", Depart, Out("f", "same_day", 50), slotMax: 3),
                Ev("f", Settle, chainOnly: true)));
            Assert.IsTrue(HasError(errors, "cover all modes"));
        }

        [Test]
        public void Error_SameDayCanOverflowDay()
        {
            var errors = new List<string>();
            Assert.IsNull(Build(errors,
                Ev("s", Depart, Out("f", "same_day", 50)),
                Ev("f", Depart, chainOnly: true)));
            Assert.IsTrue(HasError(errors, "can overflow"));
        }

        [Test]
        public void Error_Cycle()
        {
            var errors = new List<string>();
            Assert.IsNull(Build(errors,
                Ev("s", Depart, Out("a", "same_day", 100), slotMax: 2),
                Ev("a", Depart, Out("b", "same_day", 100), chainOnly: true),
                Ev("b", Depart, Out("a", "same_day", 100), chainOnly: true)));
            Assert.IsTrue(HasError(errors, "cycle"));
        }

        [Test]
        public void Error_MoreThanFourStages()
        {
            var errors = new List<string>();
            Assert.IsNull(Build(errors,
                Ev("s", Depart, Out("a", "next_day", 100)),
                Ev("a", Depart, Out("b", "next_day", 100), chainOnly: true),
                Ev("b", Depart, Out("c", "next_day", 100), chainOnly: true),
                Ev("c", Depart, Out("d", "next_day", 100), chainOnly: true),
                Ev("d", Depart, chainOnly: true)));
            Assert.IsTrue(HasError(errors, "chain stages"));
        }

        // ---- 선택 / selection ----

        [Test]
        public void NormalPick_NeverReturnsChainOnly()
        {
            EventTable t = Sample();
            var sel = new EventSelector(t);
            for (int day = 1; day <= 20; day++)
            {
                sel.BeginDay();
                for (int slot = 0; slot < 5; slot++)
                    Assert.IsFalse(sel.Pick(7, day, slot, MoveMode.Depart, SlotType.Minor, 0).ChainOnly);
            }
        }

        [Test]
        public void SameDay_FollowsAtNextSlotAndClears()
        {
            EventTable t = Sample();
            t.TryGetEvent("f", out EventDef f);
            var chains = new ChainState(0);
            chains.SetSameDay(f.Index, 100);

            var sel = new EventSelector(t);
            sel.BeginDay();
            Assert.AreEqual("f", sel.Pick(1, 1, 1, MoveMode.Depart, SlotType.Minor, 0, chains).Id);
            Assert.AreEqual(-1, chains.SameDayEvent);
        }

        [Test]
        public void NextDay_NotOnCreationDay_ThenAppearsAndIsRemoved()
        {
            EventTable t = Sample();
            t.TryGetEvent("g", out EventDef g);
            var chains = new ChainState(1000);
            chains.AddPending(g.Index, 1, 1);

            var sel = new EventSelector(t);
            sel.BeginDay();
            Assert.AreNotEqual("g", sel.Pick(1, 1, 0, MoveMode.Depart, SlotType.Minor, 0, chains).Id);
            Assert.AreEqual(1, chains.PendingCount);

            sel.BeginDay();
            Assert.AreEqual("g", sel.Pick(1, 2, 0, MoveMode.Depart, SlotType.Minor, 0, chains).Id);
            Assert.AreEqual(0, chains.PendingCount);
        }

        [Test]
        public void NextDay_ModeMismatch_KeepsPending()
        {
            EventTable t = Sample();
            t.TryGetEvent("g", out EventDef g);
            var chains = new ChainState(1000);
            chains.AddPending(g.Index, 1, 1);

            var sel = new EventSelector(t);
            sel.BeginDay();
            Assert.AreNotEqual("g", sel.Pick(1, 2, 0, MoveMode.Settle, SlotType.Minor, 0, chains).Id);
            Assert.AreEqual(1, chains.PendingCount);
        }

        [Test]
        public void ChainState_KeepsOnlyOnePendingPerEvent()
        {
            var chains = new ChainState(25);
            Assert.IsTrue(chains.AddPending(3, 1, 1));
            Assert.IsFalse(chains.AddPending(3, 5, 2));
            Assert.AreEqual(1, chains.PendingCount);
            Assert.AreEqual(1, chains.WeightAt(0));
        }

        // ---- 흐름 / flow ----

        [Test]
        public void DayFlow_SameDayLinkShowsFollowUpAtNextSlot()
        {
            EventTable t = Sample();
            var flow = new DayFlow(RunState.CreateDefault(new RunRules()), t, 1UL);

            Assert.IsTrue(flow.ChooseMode(MoveMode.Depart));
            Assert.AreEqual("s", flow.CurrentEvent.Id);

            flow.Choose(0);
            Assert.AreEqual("f", flow.CurrentEvent.Id);

            flow.Choose(0);
            Assert.AreNotEqual("f", flow.CurrentEvent.Id);
        }
    }
}