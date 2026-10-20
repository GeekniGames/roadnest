using System;
using System.Collections.Generic;
using Roadnest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 효과·선택지 표시용 공용 설정: 아이콘, 이름, 문구. 아이콘이 없으면 이름을 글자로 붙인다.
/// Shared display settings for effects and choices: icons, names, labels. Without an icon the name is prefixed.
/// </summary>
[Serializable]
public class EventDisplay
{
    [Serializable]
    public struct ChoiceIcon
    {
        public string key;
        public Sprite sprite;
    }

    // 순서: 연료, 식량, 물, 내구도, HP (StatType 순서) / order matches StatType
    public Sprite[] statIcons;
    public string[] statNames = { "연료", "식량", "물", "내구도", "HP" };
    public string noCostLabel = "비용 없음 / No cost";
    public string noChangeLabel = "변화 없음 / No change";
    [Range(0f, 1f)] public float lockedAlpha = 0.4f;

    // 선택지 이미지: JSON의 iconKey로 찾는다. 없으면 기본 이미지, 기본도 없으면 숨김
    // Choice images looked up by the JSON iconKey; falls back to the default, hidden if that is empty too
    public ChoiceIcon[] choiceIcons;
    public Sprite defaultChoiceIcon;

    string[] _posFmt;
    string[] _negFmt;
    Dictionary<string, Sprite> _choiceIconMap;

    // 서식과 조회표를 미리 만들어 두어 표시할 때 할당이 없다 / prebuild formats and the lookup so showing allocates nothing
    public void Build()
    {
        int n = statNames.Length;
        _posFmt = new string[n];
        _negFmt = new string[n];
        for (int i = 0; i < n; i++)
        {
            string prefix = Icon(i) != null ? "" : statNames[i] + " ";
            _posFmt[i] = prefix + "+{0}";
            _negFmt[i] = prefix + "{0}";
        }

        int m = choiceIcons == null ? 0 : choiceIcons.Length;
        _choiceIconMap = new Dictionary<string, Sprite>(m);
        for (int i = 0; i < m; i++)
        {
            string key = choiceIcons[i].key;
            if (string.IsNullOrEmpty(key)) continue;
            if (_choiceIconMap.ContainsKey(key))
            {
#if UNITY_EDITOR
                Debug.LogError("선택지 이미지 키 중복 / duplicate choice image key: " + key);
#endif
                continue;
            }
            _choiceIconMap.Add(key, choiceIcons[i].sprite);
        }
    }

    public Sprite Icon(int stat) => statIcons != null && stat < statIcons.Length ? statIcons[stat] : null;

    public void SetEffect(Image icon, TMP_Text value, Effect e)
    {
        int i = (int)e.Stat;
        Sprite s = Icon(i);
        if (icon != null)
        {
            icon.sprite = s;
            icon.gameObject.SetActive(s != null);
        }
        value.SetText(e.Amount > 0 ? _posFmt[i] : _negFmt[i], e.Amount);
    }

    public void SetChoiceIcon(Image image, string key)
    {
        Sprite s = defaultChoiceIcon;
        if (!string.IsNullOrEmpty(key))
        {
            if (_choiceIconMap.TryGetValue(key, out Sprite found)) s = found;
#if UNITY_EDITOR
            else Debug.LogWarning("선택지 이미지 키 없음 / choice image key not found: " + key);
#endif
        }
        image.sprite = s;
        image.gameObject.SetActive(s != null);
    }
}