using NUnit.Framework;
using Roadnest.Core;

namespace Roadnest.Tests
{
    public class ItemStoreTests
    {
        static ItemStore Make(int slots = 10, int cap = 5) => new ItemStore(new Inventory(slots, cap));

        [Test]
        public void Add_StopsAtInventoryLimit_AndRemoveFloorsAtZero()
        {
            var s = Make(1, 5);
            Assert.AreEqual(5, s.Add(ItemType.RepairTool, 9));
            Assert.AreEqual(5, s.Get(ItemType.RepairTool));
            Assert.AreEqual(-5, s.Add(ItemType.RepairTool, -9));
            Assert.AreEqual(0, s.Get(ItemType.RepairTool));
        }

        [Test]
        public void TryUse_ConsumesOne_FailsAtZero()
        {
            var s = Make();
            s.Add(ItemType.RepairKitLow, 1);
            Assert.IsTrue(s.TryUse(ItemType.RepairKitLow));
            Assert.IsFalse(s.TryUse(ItemType.RepairKitLow));
            Assert.AreEqual(0, s.Get(ItemType.RepairKitLow));
        }

        [Test]
        public void HasAny_ReflectsCounts()
        {
            var s = Make();
            Assert.IsFalse(s.HasAny());
            s.Add(ItemType.RepairKitHigh, 1);
            Assert.IsTrue(s.HasAny());
        }
    }
}