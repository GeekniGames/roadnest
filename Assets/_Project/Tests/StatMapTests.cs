using System;
using System.Collections.Generic;
using NUnit.Framework;
using Roadnest.Core;

namespace Roadnest.Tests
{
    public class StatMapTests
    {
        [Test]
        public void Counts_MatchEnums()
        {
            Assert.AreEqual(StatMap.Count, Enum.GetValues(typeof(StatType)).Length);
            Assert.AreEqual(Inventory.KindCount, Enum.GetValues(typeof(ItemKind)).Length);
        }

        [Test]
        public void JsonNames_AreUnique_AndRoundTrip()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < StatMap.Count; i++)
            {
                var s = (StatType)i;
                string name = StatMap.NameOf(s);
                Assert.IsTrue(seen.Add(name), "중복 이름: " + name);
                StatType back;
                Assert.IsTrue(StatMap.TryParse(name, out back));
                Assert.AreEqual(s, back);
            }
        }

        [Test]
        public void StoredStats_MapToKindWithSameName_CoveringEveryKindOnce()
        {
            var covered = new bool[Inventory.KindCount];
            for (int i = 0; i < StatMap.Count; i++)
            {
                var s = (StatType)i;
                if (!StatMap.IsStored(s)) continue;
                ItemKind k = StatMap.ToKind(s);
                Assert.AreEqual(s.ToString(), k.ToString());
                Assert.IsFalse(covered[(int)k]);
                covered[(int)k] = true;
            }
            for (int k = 0; k < covered.Length; k++) Assert.IsTrue(covered[k], "누락: " + (ItemKind)k);
        }

        [Test]
        public void Unknown_Name_Fails()
        {
            StatType s;
            Assert.IsFalse(StatMap.TryParse("gold", out s));
            Assert.IsFalse(StatMap.TryParse(null, out s));
        }
    }
}