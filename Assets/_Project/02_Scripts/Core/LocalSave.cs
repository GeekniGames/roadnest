using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace Roadnest.Core
{
    /// <summary>
    /// 저장 내용. 나중에 해금, 선택 캐릭터 등을 여기에 추가한다.
    /// Saved contents. Unlocks, selected character, etc. get added here later.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public int gold;
    }

    /// <summary>
    /// 기기 안 저장 파일(JSON). 임시 파일에 쓴 뒤 바꿔치기해서 쓰는 도중 꺼져도 복구한다.
    /// On-device save file (JSON). Writes a temp file then swaps it in, so a crash mid-write can be recovered.
    /// </summary>
    public sealed class LocalSave
    {
        readonly string _path;
        readonly string _tmp;

        public SaveData Data { get; private set; } = new SaveData();

        public LocalSave(string path)
        {
            _path = path;
            _tmp = path + ".tmp";
        }

        // 파일이 없거나 깨졌으면 기본값 / defaults when the file is missing or corrupt
        public void Load()
        {
            Data = new SaveData();
            string src = File.Exists(_path) ? _path : (File.Exists(_tmp) ? _tmp : null);
            if (src == null) return;

            try
            {
                SaveData d = JsonUtility.FromJson<SaveData>(File.ReadAllText(src));
                if (d == null) return;
                if (d.gold < 0) d.gold = 0;
                else if (d.gold > GoldWallet.MaxGold) d.gold = GoldWallet.MaxGold;
                Data = d;
            }
            catch (Exception ex) { LogWarn(ex); }
        }

        public bool Save()
        {
            try
            {
                File.WriteAllText(_tmp, JsonUtility.ToJson(Data));
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(_tmp, _path);
                return true;
            }
            catch (Exception ex)
            {
                LogWarn(ex);
                return false;
            }
        }

        // 저장 내용을 처음 상태로 되돌리고 바로 저장 / back to a fresh save and write it right away
        public void Reset()
        {
            Data = new SaveData();
            Save();
        }

        [Conditional("UNITY_EDITOR")]
        static void LogWarn(Exception ex)
            => UnityEngine.Debug.LogWarning("저장 파일 오류 / save file error: " + ex.Message);
    }
}