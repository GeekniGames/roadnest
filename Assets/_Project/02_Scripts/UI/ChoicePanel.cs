using System;
using DG.Tweening;
using UnityEngine;
using TMPro;

/// <summary>
/// 선택 패널. 2/3 선택지 영역 전환, 등장/퇴장 연출, 선택 결과 전달.
/// Choice panel : 2/3-slot area switch, show/hide tween, pick callback.
/// </summary>
public class ChoicePanel : MonoBehaviour
{
    [Header("참조 / References")]
    [SerializeField] CanvasGroup rootGroup;
    [SerializeField] TMP_Text dayTitle;
    [SerializeField] RectTransform body;
    [SerializeField] CanvasGroup bodyGroup;
    [SerializeField] GameObject area2;
    [SerializeField] GameObject area3;
    [SerializeField] ChoiceSlotView[] slots2;
    [SerializeField] ChoiceSlotView[] slots3;

    [Header("연출 / Timing")]
    [SerializeField] float showTime = 0.3f;
    [SerializeField] float hideTime = 0.2f;
    [SerializeField] float slideDistance = 120f;

    Vector2 _restPos;
    Tween _show;
    Tween _hide;
    ChoiceSlotView[] _active;
    Action<int> _onPicked;
    int _pickedIndex;
    bool _picked;



    void Awake()
    {
        _restPos = body.anchoredPosition;

        Action<int> click = OnSlotClicked;
        InitSlots(slots2, click);
        InitSlots(slots3, click);

        SetHidden();
        BuildTweens();
    }

    //선택 결과는 퇴장 연출이 끝난 뒤 전달된다 / result is delivered after the hide tween
    public void Show(int day, ChoiceSlotData[] data, int count, bool[] enabled, Action<int> onPicked)
    {
        ChoiceSlotView[] slots = count == 2 ? slots2 : count == 3 ? slots3 : null;
        if (slots == null || slots.Length < count || data.Length < count) return;
        if (!gameObject.activeSelf) gameObject.SetActive(true);   // 꺼진 채 시작해도 Awake가 먼저 실행됨 / Awake runs first even if started inactive

        dayTitle.SetText("DAY {0}", day);

        _active = slots;
        _onPicked = onPicked;
        _picked = false;

        area2.SetActive(count == 2);
        area3.SetActive(count == 3);
        for (int i = 0; i < count; i++) slots[i].Bind(data[i], enabled[i]);

        bodyGroup.alpha = 0;
        body.anchoredPosition = new Vector2(_restPos.x, _restPos.y - slideDistance);
        rootGroup.alpha = 1;
        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = false;
        _show.Restart();
    }

    void BuildTweens()
    {
        _show = DOTween.Sequence()
            .SetAutoKill(false)
            .SetLink(gameObject)
            .Append(bodyGroup.DOFade(1f, showTime).SetEase(Ease.OutSine))
            .Join(body.DOAnchorPosY(_restPos.y, showTime).SetEase(Ease.OutCubic))
            .OnComplete(OnShown)
            .Pause();

        _hide = bodyGroup.DOFade(0f, hideTime)
            .SetEase(Ease.InSine)
            .SetAutoKill(false)
            .SetLink(gameObject)
            .OnComplete(OnHidden)
            .Pause();
    }

    void OnShown() => rootGroup.interactable = true;

    void OnSlotClicked(int index)
    {
        if (_picked) return;
        _picked = true;
        _pickedIndex = index;
        rootGroup.interactable = false;
        _active[index].PlayPress();
        _hide.Restart();
    }

    void OnHidden()
    {
        SetHidden();
        for (int i = 0; i < _active.Length; i++) _active[i].Release();
        gameObject.SetActive(false);
        Action<int> cb = _onPicked;
        _onPicked = null;
        cb?.Invoke(_pickedIndex);
    }

    void SetHidden()
    {
        rootGroup.alpha = 0;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;
        bodyGroup.alpha = 0;
    }

    static void InitSlots(ChoiceSlotView[] slots, Action<int> click)
    {
        for (int i = 0; i < slots.Length; i++) slots[i].Init(i, click);
    }
}
