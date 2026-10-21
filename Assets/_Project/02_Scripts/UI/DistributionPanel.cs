using System;
using DG.Tweening;
using Roadnest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 분배 패널: 살아있는 캐릭터마다 식량/물을 줄지 정하고 확정한다. 계산은 DayFlow가 하고 여기서는 보여주기만 한다.
/// Distribution panel: choose food/water for each living character and confirm. DayFlow does the math; this only shows it.
/// </summary>
public class DistributionPanel : MonoBehaviour
{
    [Header("참조 / References")]
    [SerializeField] CanvasGroup rootGroup;
    [SerializeField] TMP_Text dayText;          // 선택: 비워도 동작 / optional
    [SerializeField] ScrollRect slotScroll;
    [SerializeField] DistributionSlotView[] slots;
    [SerializeField] Button doneButton;

    [Header("HP 색 / HP colors")]
    [SerializeField, Range(0f, 1f)] float warnRatio = 0.6f;     // 이 비율 이하면 경고 / warning at or below
    [SerializeField, Range(0f, 1f)] float dangerRatio = 0.3f;   // 이 비율 이하면 위험 / danger at or below
    [SerializeField] Color safeColor = new Color(0.56f, 0.86f, 0.34f, 1f);
    [SerializeField] Color warnColor = new Color(1f, 0.78f, 0.25f, 1f);
    [SerializeField] Color dangerColor = new Color(1f, 0.3f, 0.25f, 1f);

    [Header("연출 / Timing")]
    [SerializeField] float fadeTime = 0.2f;

    DayFlow _flow;
    Action _onConfirm;
    Action<int, ResourceType> _onToggle;
    Tween _fade;

    void Awake()
    {
        _onToggle = OnToggle;
        for (int i = 0; i < slots.Length; i++) slots[i].Init(i, _onToggle);
        doneButton.onClick.AddListener(OnDoneClicked);

        SetHidden();
        _fade = rootGroup.DOFade(1f, fadeTime).SetAutoKill(false).SetLink(gameObject).Pause();
    }

    // 슬롯 번호 = 캐릭터 번호. 죽은 캐릭터 칸은 숨긴다 / slot index = character index; dead characters are hidden
    public void Show(DayFlow flow, Action onConfirm)
    {
        Activate();
        _flow = flow;
        _onConfirm = onConfirm;

        if (dayText != null) dayText.SetText("DAY {0}", flow.Run.Day);

        CharacterState[] chars = flow.Run.Characters;
#if UNITY_EDITOR
        if (chars.Length > slots.Length)
            Debug.LogError("캐릭터 칸이 캐릭터 수보다 적음 / fewer slots than characters: " + chars.Length, this);
#endif
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < chars.Length && chars[i].IsAlive) slots[i].Bind(chars[i], HpColor(chars[i]));
            else slots[i].gameObject.SetActive(false);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(slotScroll.content);
        slotScroll.StopMovement();
        slotScroll.horizontalNormalizedPosition = 0f;

        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = true;
        rootGroup.alpha = 0f;
        _fade.Restart();
    }

    public void Hide()
    {
        _flow = null;
        _onConfirm = null;
        if (_fade != null) _fade.Pause();
        SetHidden();
    }

    Color HpColor(CharacterState c)
    {
        if (c.Hp <= c.MaxHp * dangerRatio) return dangerColor;
        if (c.Hp <= c.MaxHp * warnRatio) return warnColor;
        return safeColor;
    }

    // 줄 수 있으면 토글, 재고 부족이면 흔들림 / toggle if possible, shake when stock is short
    void OnToggle(int slot, ResourceType type)
    {
        if (_flow == null) return;
        bool now = _flow.GetRation(slot, type);
        if (_flow.SetRation(slot, type, !now)) slots[slot].SetRation(type, !now);
        else slots[slot].Shake(type);
    }

    void OnDoneClicked()
    {
        Action cb = _onConfirm;
        if (cb == null) return;
        _onConfirm = null;
        cb();
    }

    void Activate()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    void SetHidden()
    {
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;
    }
}