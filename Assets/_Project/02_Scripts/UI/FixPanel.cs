using System;
using DG.Tweening;
using Roadnest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 수리 패널: 공구 / 하급 키트 / 상급 키트 중 하나를 골라 수리한다. 보유/내구도 규칙은 DayFlow가 정한다.
/// Repair panel: pick tool / low kit / high kit to repair. DayFlow owns the item and durability rules.
/// </summary>
public class FixPanel : MonoBehaviour
{
    [Serializable]
    class Slot
    {
        public Button button;       // Button_Fix
        public TMP_Text value;      // Text (TMP)_FixSlotValue
    }

    const int BackIndex = ItemStore.TypeCount;

    [Header("참조 / References")]
    [SerializeField] CanvasGroup rootGroup;
    [SerializeField] Slot[] slots;          // 순서 = ItemType (공구, 하급, 상급) / order = ItemType
    [SerializeField] Button backButton;

    [Header("연출 / Timing")]
    [SerializeField] float fadeTime = 0.2f;
    [SerializeField] float pressScale = 0.08f;
    [SerializeField] float pressTime = 0.18f;

    Action<ItemType> _onUse;
    Action _onBack;
    int _pending;
    bool _busy;
    Tween _fade;
    Tween[] _press;         // 슬롯 순서 + 뒤로가기 / slots in order, then back
    TweenCallback _onPressDone;

    void Awake()
    {
#if UNITY_EDITOR
        if (slots.Length != ItemStore.TypeCount)
            Debug.LogError("슬롯 수가 아이템 종류와 다름 / slot count differs from item types: " + slots.Length, this);
#endif
        _onPressDone = OnPressDone;
        _press = new Tween[ItemStore.TypeCount + 1];

        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;
            slots[i].button.onClick.AddListener(() => Pick(index));
            _press[i] = MakePress(slots[i].button);
        }
        backButton.onClick.AddListener(() => Pick(BackIndex));
        _press[BackIndex] = MakePress(backButton);

        SetHidden();
        _fade = rootGroup.DOFade(1f, fadeTime).SetAutoKill(false).SetLink(gameObject).Pause();
    }

    Tween MakePress(Button b)
    {
        return b.transform.DOPunchScale(Vector3.one * -pressScale, pressTime, 1, 0.5f)
            .SetAutoKill(false).SetLink(gameObject).OnComplete(_onPressDone).Pause();
    }

    public void Show(DayFlow flow, Action<ItemType> onUse, Action onBack)
    {
        Activate();
        _onUse = onUse;
        _onBack = onBack;
        _busy = false;

        RunRules rules = flow.Run.Rules;
        for (int i = 0; i < slots.Length; i++)
        {
            ItemType t = (ItemType)i;
            slots[i].button.interactable = flow.CanUseRepair(t);
            slots[i].value.SetText("+{0}", rules.GetRepairAmount(t));
        }

        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = true;
        rootGroup.alpha = 0f;
        _fade.Restart();
    }

    public void Hide()
    {
        _onUse = null;
        _onBack = null;
        _busy = false;
        if (_fade != null) _fade.Pause();
        SetHidden();
    }

    // 눌림 연출을 먼저 재생하고, 끝나면 한 번만 전달 / play the press first, then deliver once
    void Pick(int index)
    {
        if (_busy || _onUse == null) return;
        _busy = true;
        _pending = index;
        _press[index].Restart();
    }

    void OnPressDone()
    {
        Action<ItemType> use = _onUse;
        Action back = _onBack;
        _busy = false;
        if (use == null) return;
        _onUse = null;
        _onBack = null;

        if (_pending == BackIndex) back?.Invoke();
        else use((ItemType)_pending);
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