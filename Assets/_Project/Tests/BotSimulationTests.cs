using System;
using System.Text;
using NUnit.Framework;
using Roadnest.Core;
using UnityEngine;

namespace Roadnest.Tests
{
    public class BotSimulationTests
    {
        const int Runs = 500;

        [TestCase(BotStrategy.Random)]
        [TestCase(BotStrategy.DepartOnly)]
        [TestCase(BotStrategy.SettleOnly)]
        [TestCase(BotStrategy.Weighing)]
        public void Bot_NeverDeadlocks_AndEveryRunIsAccountedFor(BotStrategy strategy)
        {
            var table = TestData.LoadSampleTable();
            BotStats s = BotSimulator.Simulate(table, new RunRules(), strategy, Runs);
            Assert.AreEqual(0, s.Deadlocks, strategy + ": 데드락 발생 / deadlock found");
            Assert.AreEqual(Runs, s.AllDead + s.VehicleDestroyed + s.TimedOut);
        }

        [Test]
        public void Bot_SameSeed_SameResult()
        {
            var table = TestData.LoadSampleTable();
            var rules = new RunRules();
            foreach (BotStrategy strategy in Enum.GetValues(typeof(BotStrategy)))
            {
                for (ulong seed = 1; seed <= 30; seed++)
                {
                    BotRunResult a = BotSimulator.PlayRun(table, rules, strategy, seed);
                    BotRunResult b = BotSimulator.PlayRun(table, rules, strategy, seed);
                    Assert.AreEqual(a.SurvivedDays, b.SurvivedDays);
                    Assert.AreEqual(a.Reason, b.Reason);
                }
            }
        }

        // 통계를 Console에 출력한다. 검사는 없다 / prints stats to the Console; nothing asserted
        [Test]
        public void Report_PrintsStatsTable()
        {
            var table = TestData.LoadSampleTable();
            var sb = new StringBuilder("[Bot] " + Runs + "판 x 전략 / runs per strategy\n");
            sb.Append("strategy | avg | median | max | allDead | vehicleDestroyed | timedOut\n");

            foreach (BotStrategy strategy in Enum.GetValues(typeof(BotStrategy)))
            {
                BotStats s = BotSimulator.Simulate(table, new RunRules(), strategy, Runs);
                sb.Append(strategy).Append(" | ")
                  .Append(s.AverageDays.ToString("F1")).Append(" | ")
                  .Append(s.MedianDays).Append(" | ")
                  .Append(s.MaxDays).Append(" | ")
                  .Append(s.AllDead).Append(" | ")
                  .Append(s.VehicleDestroyed).Append(" | ")
                  .Append(s.TimedOut).Append('\n');
            }
            Debug.Log(sb.ToString());
            Assert.Greater(sb.Length, 0);
        }
    }
}