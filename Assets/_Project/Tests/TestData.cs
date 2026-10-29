using System.Collections.Generic;
using NUnit.Framework;
using Roadnest.Core;
using UnityEditor;
using UnityEngine;

namespace Roadnest.Tests
{
    internal static class TestData
    {
        public const string EventsJsonPath = "Assets/_Project/Tests/events_test.json";

        public static DaySlotsDto[] DaySlots() => new[]
        {
            new DaySlotsDto { mode = "depart", slots = new[] { "Minor", "ResourceExplore", "Danger", "Minor", "Major" } },
            new DaySlotsDto { mode = "settle", slots = new[] { "Minor", "MaintenanceRecovery", "NearbyExplore", "Danger", "Night" } }
        };

        // 실제 events.json을 읽어 테이블을 만든다 / builds the table from the real events.json
        public static EventTable LoadSampleTable()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(EventsJsonPath);
            Assert.IsNotNull(asset, "events.json을 찾을 수 없음: " + EventsJsonPath);
            var errors = new List<string>();
            var table = EventTable.FromJson(asset.text, errors);
            Assert.IsNotNull(table, string.Join("\n", errors));
            return table;
        }
    }
}