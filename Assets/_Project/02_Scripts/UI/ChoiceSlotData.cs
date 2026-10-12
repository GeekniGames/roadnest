using System;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// 슬롯 한 줄(아이콘 + 번역 문구).
/// One slot line (icon + localized text).
/// </summary>
[Serializable]
public class ChoiceLine
{
    public Sprite icon;            // 비우면 아이콘 숨김 / empty hides the icon
    public LocalizedString text;
    public int amount;             // 문구의 {amount} 값 / value for {amount} in the text
}

/// <summary>
/// 선택 슬롯에 표시할 내용. 인스펙터와 코드 양쪽에서 채울 수 있다.
/// Content of a choice slot; fillable from inspector or code.
/// </summary>
[Serializable]
public class ChoiceSlotData
{
    public Sprite art;             // 비우면 씬에 놓은 이미지 유지 / empty keeps the scene image
    public LocalizedString title;
    public ChoiceLine cost;
    public ChoiceLine[] results;   // 최대 2줄 / max 2 lines
}