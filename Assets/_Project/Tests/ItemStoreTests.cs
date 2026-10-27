using NUnit.Framework;
using Roadnest.Core;

namespace Roadnest.Tests
{
    public class ItemStoreTests
    {
        [Test]
        public void Add_ClampsToCapAndFloor()
        {
            var s = new ItemStore(5);
            s.Add(ItemType.RepairTool, 9);
            Assert.AreEqual(5, s.Get(ItemType.RepairTool));
            s.Add(ItemType.RepairTool, -9);
            Assert.AreEqual(0, s.Get(ItemType.RepairTool));
        }

        [Test]
        public void TryUse_ConsumesOne_FailsAtZero()
        {
            var s = new ItemStore(5);
            s.Add(ItemType.RepairKitLow, 1);
            Assert.IsTrue(s.TryUse(ItemType.RepairKitLow));
            Assert.IsFalse(s.TryUse(ItemType.RepairKitLow));
            Assert.AreEqual(0, s.Get(ItemType.RepairKitLow));
        }

        [Test]
        public void HasAny_ReflectsCounts()
        {
            var s = new ItemStore(5);
            Assert.IsFalse(s.HasAny());
            s.Add(ItemType.RepairKitHigh, 1);
            Assert.IsTrue(s.HasAny());
        }
    }
}