using System;
using Roadnest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택지 슬롯 한 칸. 소모/성공/실패 영역이 각각 최대 3줄(아이콘 + 값)을 보여준다.
/// One choice slot. The cost / success / failure areas each show up to 3 rows (icon + value).
/// </summary>
public class EventSlotView : MonoBehaviour
{
    [Serializable]
    public class EffectRow
    {
        public GameObject root;
        public Image icon;
        public TMP_Text value;
    }

    [Serializable]
    public class EffectArea
    {
        public GameObject root;     // 실패 영역만 숨기므로 Fail만 필수 / only the fail area is hidden, so only Fail needs it
        public EffectRow[] rows;
    }

    [Header("참조 / References")]
    [SerializeField] Button button;
    [SerializeField] CanvasGroup group;
    [SerializeField] TMP_Text title;
    [SerializeField] Image slotIcon;
    [SerializeField] TMP_Text rate;         // 성공 확률 / success chance
    [SerializeField] EffectArea cost;
    [SerializeField] EffectArea success;
    [SerializeField] EffectArea fail;

    EventDisplay _display;
    Action<int> _onClick;
    int _index;

    public void Init(int index, EventDisplay display, Action<int> onClick)
    {
        _index = index;
        _display = display;
        _onClick = onClick;
        button.onClick.AddListener(OnClick);

#if UNITY_EDITOR
        if (Short(cost, EventLimits.MaxCostEffects) || Short(success, EventLimits.MaxOutcomeEffects) || Short(fail, EventLimits.MaxOutcomeEffects))
            Debug.LogError("효과 줄이 한도보다 적음 / effect rows fewer than the limit", this);
        if (fail.root == null)
            Debug.LogError("Fail Root가 비어 있음 / Fail root is empty", this);
#endif
    }

    public void Bind(ChoiceDef c, bool enabled)
    {
        gameObject.SetActive(true);

        title.SetText(c.TextKey);
        _display.SetChoiceIcon(slotIcon, c.IconKey);
        BindArea(cost, c.Cost, _display.noCostLabel);

        OutcomeDef[] o = c.Outcomes;
        rate.SetText("{0}%", o[0].Percent);
        BindArea(success, o[0].Effects, _display.noChangeLabel);

        bool hasFail = o.Length > 1;
        fail.root.SetActive(hasFail);
        if (hasFail) BindArea(fail, o[1].Effects, _display.noChangeLabel);

        group.alpha = enabled ? 1f : _display.lockedAlpha;
        button.interactable = enabled;
    }

    // 효과가 없으면 첫 줄에 안내 문구만 보여준다 / with no effects, the first row shows only the label
    void BindArea(EffectArea a, Effect[] fx, string emptyLabel)
    {
        EffectRow[] rows = a.rows;
        if (fx.Length == 0)
        {
            EffectRow first = rows[0];
            first.root.SetActive(true);
            first.icon.gameObject.SetActive(false);
            first.value.SetText(emptyLabel);
            for (int i = 1; i < rows.Length; i++) rows[i].root.SetActive(false);
            return;
        }

        for (int i = 0; i < rows.Length; i++)
        {
            bool on = i < fx.Length;
            rows[i].root.SetActive(on);
            if (on) _display.SetEffect(rows[i].icon, rows[i].value, fx[i]);
        }
    }

    void OnClick() => _onClick?.Invoke(_index);

#if UNITY_EDITOR
    static bool Short(EffectArea a, int need) => a.rows == null || a.rows.Length < need;
#endif
}