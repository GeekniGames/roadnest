using NUnit.Framework;
using Roadnest.Core;

namespace Roadnest.Tests
{
    public class InventoryTests
    {
        static Inventory Inv(int slots = 10, int cap = 10) => new Inventory(slots, cap);

        [Test]
        public void Add_FillsPartialCellFirst_ThenNewCellAtEnd()
        {
            var inv = Inv();
            inv.Add(ItemKind.Fuel, 4);
            inv.Add(ItemKind.Food, 3);
            inv.Add(ItemKind.Fuel, 8);
            Assert.AreEqual(3, inv.UsedSlots);
            Assert.AreEqual(10, inv.CountAt(0));
            Assert.AreEqual(ItemKind.Food, inv.KindAt(1));
            Assert.AreEqual(ItemKind.Fuel, inv.KindAt(2));
            Assert.AreEqual(2, inv.CountAt(2));
        }

        [Test]
        public void Add_ReturnsOnlyWhatFits()
        {
            var inv = Inv(2, 10);
            Assert.AreEqual(20, inv.Add(ItemKind.Fuel, 25));
            Assert.AreEqual(0, inv.Add(ItemKind.Food, 1));
            Assert.AreEqual(20, inv.Get(ItemKind.Fuel));
        }

        [Test]
        public void Remove_TakesFromLastCellOfKind()
        {
            var inv = Inv();
            inv.Add(ItemKind.Fuel, 10);
            inv.Add(ItemKind.Food, 5);
            inv.Add(ItemKind.Fuel, 4);
            Assert.AreEqual(6, inv.Remove(ItemKind.Fuel, 6));
            Assert.AreEqual(2, inv.UsedSlots);
            Assert.AreEqual(8, inv.CountAt(0));
            Assert.AreEqual(ItemKind.Food, inv.KindAt(1));
            Assert.AreEqual(8, inv.Get(ItemKind.Fuel));
        }

        [Test]
        public void Remove_EmptiedCell_ShiftsFollowingCellsForward()
        {
            var inv = Inv();
            inv.Add(ItemKind.Fuel, 3);
            inv.Add(ItemKind.Food, 4);
            inv.Add(ItemKind.Water, 5);
            inv.Remove(ItemKind.Fuel, 3);
            Assert.AreEqual(2, inv.UsedSlots);
            Assert.AreEqual(ItemKind.Food, inv.KindAt(0));
            Assert.AreEqual(4, inv.CountAt(0));
            Assert.AreEqual(ItemKind.Water, inv.KindAt(1));
        }

        [Test]
        public void Remove_IsClampedToStock()
        {
            var inv = Inv();
            inv.Add(ItemKind.Food, 3);
            Assert.AreEqual(3, inv.Remove(ItemKind.Food, 10));
            Assert.AreEqual(0, inv.UsedSlots);
        }

        [Test]
        public void ReacquiredKind_GoesToLastCell()
        {
            var inv = Inv();
            inv.Add(ItemKind.Fuel, 3);
            inv.Add(ItemKind.Food, 4);
            inv.Remove(ItemKind.Fuel, 3);
            inv.Add(ItemKind.Fuel, 2);
            Assert.AreEqual(ItemKind.Food, inv.KindAt(0));
            Assert.AreEqual(ItemKind.Fuel, inv.KindAt(1));
        }

        [Test]
        public void SetStackCap_MergesSameKindIntoEarlierCell()
        {
            var inv = Inv();
            inv.Add(ItemKind.Fuel, 10);
            inv.Add(ItemKind.Food, 5);
            inv.Add(ItemKind.Water, 5);
            inv.Add(ItemKind.Fuel, 4);
            Assert.AreEqual(4, inv.UsedSlots);

            inv.SetStackCap(15);
            Assert.AreEqual(3, inv.UsedSlots);
            Assert.AreEqual(ItemKind.Fuel, inv.KindAt(0));
            Assert.AreEqual(14, inv.CountAt(0));
            Assert.AreEqual(ItemKind.Food, inv.KindAt(1));
            Assert.AreEqual(ItemKind.Water, inv.KindAt(2));
            Assert.AreEqual(14, inv.Get(ItemKind.Fuel));
        }

        [Test]
        public void SetStackCap_LowerOrEqual_IsIgnored()
        {
            var inv = Inv();
            inv.Add(ItemKind.Fuel, 10);
            inv.SetStackCap(5);
            Assert.AreEqual(10, inv.StackCap);
            Assert.AreEqual(10, inv.CountAt(0));
        }

        [Test]
        public void SetSlotCount_AddsEmptyCells()
        {
            var inv = Inv(2, 10);
            inv.Add(ItemKind.Fuel, 30);
            inv.SetSlotCount(3);
            Assert.AreEqual(10, inv.Add(ItemKind.Fuel, 30));
        }

        [Test]
        public void Changed_FiresOncePerOperation()
        {
            var inv = Inv();
            int n = 0;
            inv.Changed += () => n++;
            inv.Add(ItemKind.Fuel, 25);
            Assert.AreEqual(1, n);
            inv.Add(ItemKind.Fuel, 0);
            Assert.AreEqual(1, n);
        }

        [Test]
        public void CopyFrom_ClonesStateWithoutRaisingEvent()
        {
            var a = Inv();
            a.Add(ItemKind.Fuel, 12);
            var b = Inv(5, 3);
            int n = 0;
            b.Changed += () => n++;
            b.CopyFrom(a);
            Assert.AreEqual(0, n);
            Assert.AreEqual(12, b.Get(ItemKind.Fuel));
            Assert.AreEqual(2, b.UsedSlots);
            Assert.AreEqual(10, b.StackCap);
        }
    }
}