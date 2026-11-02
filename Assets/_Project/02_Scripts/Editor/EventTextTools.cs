using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Roadnest.Core;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

/// <summary>
/// Text 폴더의 문구 CSV를 Events 문자열 표에 반영하고, Json 폴더가 쓰는 키가 모두 있는지 검사한다. 에디터 전용.
/// Pushes the text CSVs in the Text folder into the Events string table and checks every key used by the Json folder exists. Editor only.
/// </summary>
public static class EventTextTools
{
    const string JsonFolder = "Assets/_Project/04_Data/Events/Json";
    const string TextFolder = "Assets/_Project/04_Data/Events/Text";

    [MenuItem("Roadnest/Events/Rebuild Texts")]
    static void RebuildTexts()
    {
        var entries = new Dictionary<string, Dictionary<string, string>>();
        var errors = new List<string>();
        if (!LoadTexts(entries, errors)) { Report(errors); return; }

        StringTableCollection col = LocalizationEditorSettings.GetStringTableCollection(EventText.Table);
        if (col == null)
        {
            Debug.LogError("문자열 표를 찾을 수 없음 / string table not found: " + EventText.Table);
            return;
        }

        var tables = new Dictionary<string, StringTable>();
        for (int i = 0; i < col.StringTables.Count; i++)
            tables[col.StringTables[i].LocaleIdentifier.Code] = col.StringTables[i];

        // CSV가 쓰는 언어가 표에 있어야 한다 / every CSV language must exist in the table
        var codes = new HashSet<string>();
        foreach (var kv in entries)
            foreach (var l in kv.Value) codes.Add(l.Key);
        foreach (string code in codes)
            if (!tables.ContainsKey(code)) errors.Add("표에 없는 언어 / locale not in table: " + code);
        if (errors.Count > 0) { Report(errors); return; }

        SharedTableData shared = col.SharedData;

        // CSV에 없는 키는 지운다 / remove keys that no CSV has
        var stale = new List<long>();
        for (int i = 0; i < shared.Entries.Count; i++)
            if (!entries.ContainsKey(shared.Entries[i].Key)) stale.Add(shared.Entries[i].Id);
        foreach (long id in stale)
        {
            foreach (var t in tables) t.Value.Remove(id);
            shared.RemoveKey(id);
        }

        int added = 0, updated = 0;
        foreach (var kv in entries)
        {
            foreach (var t in tables)
            {
                string text;
                if (!kv.Value.TryGetValue(t.Key, out text) || text.Length == 0)
                {
                    Debug.LogWarning("문구 비어 있음 / empty text: " + kv.Key + " [" + t.Key + "]");
                    continue;
                }

                StringTableEntry entry = t.Value.GetEntry(kv.Key);
                if (entry == null) { t.Value.AddEntry(kv.Key, text); added++; }
                else if (entry.Value != text) { entry.Value = text; updated++; }
            }
        }

        foreach (var t in tables) EditorUtility.SetDirty(t.Value);
        EditorUtility.SetDirty(shared);
        AssetDatabase.SaveAssets();

        Debug.Log("[EventText] 반영 완료 / done: 키 " + entries.Count + "개, 추가 " + added + ", 수정 " + updated + ", 삭제 " + stale.Count);
        CheckCoverage(entries);
    }

    [MenuItem("Roadnest/Events/Check Texts")]
    static void CheckTexts()
    {
        var entries = new Dictionary<string, Dictionary<string, string>>();
        var errors = new List<string>();
        if (!LoadTexts(entries, errors)) { Report(errors); return; }
        CheckCoverage(entries);
    }

    // CSV 전부를 읽는다. 첫 칸은 Key, 나머지는 헤더의 (언어코드)로 구분. Id 같은 열은 무시
    // Reads every CSV. First column is Key; others are told apart by the (locale code) in the header. Columns like Id are ignored
    static bool LoadTexts(Dictionary<string, Dictionary<string, string>> entries, List<string> errors)
    {
        string[] files = Directory.GetFiles(TextFolder, "*.csv");
        Array.Sort(files, StringComparer.Ordinal);
        if (files.Length == 0)
        {
            errors.Add("CSV가 없음 / no CSV in " + TextFolder);
            return false;
        }

        for (int f = 0; f < files.Length; f++)
        {
            string name = Path.GetFileName(files[f]);
            List<string[]> rows = ParseCsv(File.ReadAllText(files[f], Encoding.UTF8));
            if (rows.Count == 0) { errors.Add(name + ": 비어 있음 / empty"); continue; }

            string[] head = rows[0];
            if (head.Length < 2 || head[0].Trim() != "Key")
            {
                errors.Add(name + ": 첫 칸은 Key여야 함 / first column must be Key");
                continue;
            }

            var codes = new string[head.Length];
            for (int c = 1; c < head.Length; c++) codes[c] = LocaleCode(head[c]);

            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                if (row.Length == 1 && row[0].Length == 0) continue;    // 빈 줄 / blank line

                string key = row[0].Trim();
                if (key.Length == 0) { errors.Add(name + " " + (r + 1) + "행: 키가 비어 있음 / empty key"); continue; }
                if (entries.ContainsKey(key)) { errors.Add(name + " " + (r + 1) + "행: 키 중복 / duplicate key: " + key); continue; }

                var map = new Dictionary<string, string>();
                for (int c = 1; c < head.Length && c < row.Length; c++)
                    if (codes[c] != null) map[codes[c]] = row[c];
                entries.Add(key, map);
            }
        }
        return errors.Count == 0;
    }

    // JSON이 쓰는 키가 CSV에 있는지, CSV에만 있는 키는 없는지 본다
    // Checks every key the JSON uses exists in the CSVs, and lists CSV keys nothing uses
    static void CheckCoverage(Dictionary<string, Dictionary<string, string>> entries)
    {
        string[] files = Directory.GetFiles(JsonFolder, "*.json");
        Array.Sort(files, StringComparer.Ordinal);
        var jsons = new string[files.Length];
        var names = new string[files.Length];
        for (int i = 0; i < files.Length; i++)
        {
            jsons[i] = File.ReadAllText(files[i], Encoding.UTF8);
            names[i] = Path.GetFileName(files[i]);
        }

        var errors = new List<string>();
        EventTable table = EventTable.FromJsons(jsons, names, errors);
        if (table == null) { Report(errors); return; }

        var used = new HashSet<string>();
        for (int i = 0; i < table.Events.Length; i++)
        {
            EventDef e = table.Events[i];
            used.Add(e.TitleKey);
            used.Add(e.TextKey);
            for (int c = 0; c < e.Choices.Length; c++)
            {
                used.Add(e.Choices[c].TextKey);
                OutcomeDef[] os = e.Choices[c].Outcomes;
                for (int k = 0; k < os.Length; k++) used.Add(os[k].TextKey);
            }
        }

        int missing = 0;
        foreach (string key in used)
        {
            Dictionary<string, string> map;
            if (!entries.TryGetValue(key, out map))
            {
                Debug.LogError("문구 없음 / missing text: " + key);
                missing++;
                continue;
            }
            foreach (var l in map)
                if (l.Value.Length == 0) Debug.LogWarning("문구 비어 있음 / empty text: " + key + " [" + l.Key + "]");
        }

        int unused = 0;
        foreach (var kv in entries)
        {
            if (used.Contains(kv.Key) || kv.Key.StartsWith("common-", StringComparison.Ordinal)) continue;
            Debug.LogWarning("쓰이지 않는 키 / unused key: " + kv.Key);
            unused++;
        }

        Debug.Log("[EventText] 검사 완료 / checked: 사용 키 " + used.Count + "개, 없음 " + missing + ", 미사용 " + unused);
    }

    // 헤더 "English(en)"에서 en을 꺼낸다. 괄호가 없으면 null
    // Pulls "en" out of the header "English(en)"; null without parentheses
    static string LocaleCode(string header)
    {
        int open = header.LastIndexOf('(');
        int close = header.LastIndexOf(')');
        if (open < 0 || close < open) return null;
        return header.Substring(open + 1, close - open - 1).Trim();
    }

    static void Report(List<string> errors)
    {
        for (int i = 0; i < errors.Count; i++) Debug.LogError(errors[i]);
    }

    // 따옴표 안의 쉼표, 줄바꿈, ""를 처리하는 CSV 파서 / CSV parser handling commas, line breaks and "" inside quotes
    static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var sb = new StringBuilder();
        bool quoted = false;
        int i = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;

        for (; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                    else quoted = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(sb.ToString()); sb.Length = 0; }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(sb.ToString()); sb.Length = 0;
                rows.Add(row.ToArray()); row.Clear();
            }
            else sb.Append(c);
        }
        if (sb.Length > 0 || row.Count > 0) { row.Add(sb.ToString()); rows.Add(row.ToArray()); }
        return rows;
    }
}