using NUnit.Framework;
using Roadnest.Core;

namespace Roadnest.Tests
{
    public class RunStateTests
    {
        static RunState Make()
        {
            var rules = new RunRules();
            return RunState.CreateDefault(rules, new ResourceStore(rules.ResourceCap));
        }

        static Effect Fx(StatType stat, int amount) => new Effect(stat, amount);

        [Test]
        public void Resource_AddsAndClamps()
        {
            var s = Make();
            s.Apply(Fx(StatType.Fuel, 2));
            Assert.AreEqual(7, s.Resources.Get(ResourceType.Fuel));
            s.Apply(Fx(StatType.Fuel, 500));
            Assert.AreEqual(99, s.Resources.Get(ResourceType.Fuel));
            s.Apply(Fx(StatType.Fuel, -500));
            Assert.AreEqual(0, s.Resources.Get(ResourceType.Fuel));
        }

        [Test]
        public void Durability_ZeroDestroysVehicle()
        {
            var s = Make();
            s.Apply(Fx(StatType.Durability, -30));
            Assert.AreEqual(70, s.Durability);
            Assert.IsFalse(s.IsOver);
            s.Apply(Fx(StatType.Durability, -100));
            Assert.AreEqual(0, s.Durability);
            Assert.AreEqual(EndReason.VehicleDestroyed, s.EndReason);
        }

        [Test]
        public void HpAll_KillingEveryone_EndsRun()
        {
            var s = Make();
            s.Apply(Fx(StatType.HpAll, -40));
            Assert.AreEqual(2, s.AliveCount);
            s.Apply(Fx(StatType.HpAll, -100));
            Assert.AreEqual(0, s.AliveCount);
            Assert.AreEqual(EndReason.AllDead, s.EndReason);
        }

        [Test]
        public void DeadCharacter_IsNotHealed()
        {
            var rules = new RunRules();
            var chars = new[] { new CharacterState("a", null, 100, 5), new CharacterState("b", null, 100) };
            var s = new RunState(rules, new ResourceStore(rules.ResourceCap), chars);

            s.Apply(Fx(StatType.HpAll, -10));
            Assert.AreEqual(0, chars[0].Hp);
            Assert.AreEqual(90, chars[1].Hp);
            Assert.AreEqual(1, s.AliveCount);
            Assert.IsFalse(s.IsOver);

            s.Apply(Fx(StatType.HpAll, 20));
            Assert.AreEqual(0, chars[0].Hp);
            Assert.AreEqual(100, chars[1].Hp);
        }

        [Test]
        public void FirstEndReason_Wins()
        {
            var s = Make();
            s.Apply(new[] { Fx(StatType.Durability, -100), Fx(StatType.HpAll, -100) });
            Assert.AreEqual(EndReason.VehicleDestroyed, s.EndReason);
            Assert.AreEqual(0, s.AliveCount);
        }

        [Test]
        public void CanPay_ChecksResourcesOnly()
        {
            var s = Make();
            Assert.IsTrue(s.CanPay(new[] { Fx(StatType.Fuel, -5) }));
            Assert.IsFalse(s.CanPay(new[] { Fx(StatType.Fuel, -6) }));
            Assert.IsTrue(s.CanPay(new[] { Fx(StatType.Durability, -500) }));
        }

        [Test]
        public void Requirements_CompareCurrentValue()
        {
            var s = Make();
            Assert.IsTrue(s.MeetsRequirements(new[] { new Requirement(StatType.Fuel, CompareOp.Gte, 5) }));
            Assert.IsFalse(s.MeetsRequirements(new[] { new Requirement(StatType.Fuel, CompareOp.Gte, 6) }));
        }

        [Test]
        public void HpAll_RespectsPerCharacterMax()
        {
            var rules = new RunRules();
            var chars = new[] { new CharacterState("a", null, 50), new CharacterState("b", null, 150) };
            var s = new RunState(rules, new ResourceStore(rules.ResourceCap), chars);

            s.Apply(Fx(StatType.HpAll, -10));
            Assert.AreEqual(40, chars[0].Hp);
            Assert.AreEqual(140, chars[1].Hp);

            s.Apply(Fx(StatType.HpAll, 100));
            Assert.AreEqual(50, chars[0].Hp);
            Assert.AreEqual(150, chars[1].Hp);
        }
    }
}