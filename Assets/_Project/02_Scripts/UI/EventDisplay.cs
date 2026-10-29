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

    // 앞 5개: 연료, 식량, 물, 내구도, HP. 아이템 아이콘은 itemIcons에서 가져온다
    // First 5: fuel, food, water, durability, HP. Item icons come from itemIcons
    public Sprite[] statIcons;
    public ItemIcons itemIcons;

    static readonly string[] DefaultNames =
    {
    "연료", "식량", "물", "내구도", "HP",
    "공구", "하급 수리키트", "상급 수리키트",
    "붕대", "구급약", "의료 키트",
    "빠루", "도끼", "삽", "절단기", "손전등", "밧줄",
    "나무 조각", "철판", "기계 부품", "고철",
    "천 조각", "알코올",
    "정수 알약", "조명탄", "연막탄", "무전기", "지도 조각",
    "총", "소화기", "쌍안경", "확성기"
};
    public string[] statNames = (string[])DefaultNames.Clone();
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
        // 저장된 이름이 모자라면 기본 이름으로 채운다 / fill missing names with defaults
        if (statNames == null || statNames.Length < DefaultNames.Length)
        {
            var names = new string[DefaultNames.Length];
            for (int i = 0; i < names.Length; i++)
                names[i] = statNames != null && i < statNames.Length && !string.IsNullOrEmpty(statNames[i])
                    ? statNames[i] : DefaultNames[i];
            statNames = names;
        }

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

    public Sprite Icon(int stat)
    {
        var st = (StatType)stat;
        if (itemIcons != null && StatMap.IsStored(st))
        {
            Sprite s = itemIcons.Get((int)StatMap.ToKind(st));
            if (s != null) return s;
        }
        return statIcons != null && stat < statIcons.Length ? statIcons[stat] : null;
    }

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