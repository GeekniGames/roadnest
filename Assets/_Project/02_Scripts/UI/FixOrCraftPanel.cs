using System;
using DG.Tweening;
using Roadnest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하루 끝 정비 패널: 수리 / 제작 / 넘어가기 중 하나를 고른다. 규칙은 DayFlow가 정하고 여기서는 보여주기만 한다.
/// End-of-day maintenance panel: pick repair / craft / pass. DayFlow owns the rules; this only shows them.
/// </summary>
public class FixOrCraftPanel : MonoBehaviour
{
    [Header("참조 / References")]
    [SerializeField] CanvasGroup rootGroup;
    [SerializeField] RectTransform body;        // FixOrCraftPanelBody
    [SerializeField] TMP_Text dayText;          // 선택: 비워도 동작 / optional
    [SerializeField] Button fixButton;          // Mask_FixImage
    [SerializeField] Button craftButton;        // Mask_CraftImage
    [SerializeField] Button passButton;         // Mask_PassImage
    [SerializeField] CanvasGroup fixGroup;      // Mask_FixImage의 CanvasGroup / on Mask_FixImage
    [SerializeField] CanvasGroup craftGroup;    // Mask_CraftImage의 CanvasGroup / on Mask_CraftImage
    [SerializeField, Range(0f, 1f)] float lockedAlpha = 0.45f;

    [Header("연출 / Timing")]
    [SerializeField] float fadeTime = 0.2f;
    [SerializeField] float popTime = 0.3f;
    [SerializeField, Range(0.5f, 1f)] float popFrom = 0.9f;
    [SerializeField] float pressScale = 0.08f;  // 눌렀을 때 줄어드는 정도 / how much a button shrinks when pressed
    [SerializeField] float pressTime = 0.18f;   // 눌림 연출 길이, 끝나면 선택 전달 / press length; the pick fires after it

    Action<MaintenanceChoice> _onPick;
    MaintenanceChoice _pending;
    bool _busy;
    Tween _fade;
    Tween _pop;
    Tween[] _press;     // 버튼 순서 = MaintenanceChoice 값 (Repair, Craft, Pass) / button order = MaintenanceChoice value
    TweenCallback _onPressDone;

    void Awake()
    {
        fixButton.onClick.AddListener(OnFixClicked);
        craftButton.onClick.AddListener(OnCraftClicked);
        passButton.onClick.AddListener(OnPassClicked);

        SetHidden();
        _fade = rootGroup.DOFade(1f, fadeTime).SetAutoKill(false).SetLink(gameObject).Pause();
        _pop = body.DOScale(1f, popTime).SetEase(Ease.OutBack).SetAutoKill(false).SetLink(gameObject).Pause();

        _onPressDone = OnPressDone;
        _press = new Tween[3];
        _press[(int)MaintenanceChoice.Repair] = MakePress(fixButton);
        _press[(int)MaintenanceChoice.Craft] = MakePress(craftButton);
        _press[(int)MaintenanceChoice.Pass] = MakePress(passButton);
    }

    Tween MakePress(Button b)
    {
        return b.transform.DOPunchScale(Vector3.one * -pressScale, pressTime, 1, 0.5f)
            .SetAutoKill(false).SetLink(gameObject).OnComplete(_onPressDone).Pause();
    }

    public void Show(DayFlow flow, Action<MaintenanceChoice> onPick)
    {
        Activate();
        _onPick = onPick;
        _busy = false;
        Refresh(flow);

        body.gameObject.SetActive(true);
        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = true;
        rootGroup.alpha = 0f;
        body.localScale = Vector3.one * popFrom;
        _fade.Restart();
        _pop.Restart();
    }

    // FixPanel에서 돌아올 때: 루트 페이드 없이 본문만 다시 띄운다
    // Returning from FixPanel: reshow only the body, no root fade
    public void ShowBody(DayFlow flow, Action<MaintenanceChoice> onPick)
    {
        _onPick = onPick;
        _busy = false;
        Refresh(flow);

        body.gameObject.SetActive(true);
        body.localScale = Vector3.one * popFrom;
        _pop.Restart();
    }

    // FixPanel이 떠 있는 동안 본문을 숨긴다 / hide the body while FixPanel is up
    public void HideBody() => body.gameObject.SetActive(false);

    void Refresh(DayFlow flow)
    {
        if (dayText != null) dayText.SetText("DAY {0}", flow.Run.Day);

        bool canRepair = flow.CanRepair;
        fixButton.interactable = canRepair;
        fixGroup.alpha = canRepair ? 1f : lockedAlpha;

        bool canCraft = flow.CanCraft;
        craftButton.interactable = canCraft;
        craftGroup.alpha = canCraft ? 1f : lockedAlpha;
    }

    public void Hide()
    {
        _onPick = null;
        _busy = false;
        if (_fade != null) _fade.Pause();
        if (_pop != null) _pop.Pause();
        SetHidden();
    }

    void OnFixClicked() => Pick(MaintenanceChoice.Repair);
    void OnCraftClicked() => Pick(MaintenanceChoice.Craft);
    void OnPassClicked() => Pick(MaintenanceChoice.Pass);

    // 눌림 연출을 먼저 재생하고, 끝나면 한 번만 전달 / play the press first, then deliver once
    void Pick(MaintenanceChoice choice)
    {
        if (_onPick == null || _busy) return;
        _busy = true;
        _pending = choice;
        _press[(int)choice].Restart();
    }

    void OnPressDone()
    {
        Action<MaintenanceChoice> cb = _onPick;
        _busy = false;
        if (cb == null) return;
        _onPick = null;
        cb(_pending);
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