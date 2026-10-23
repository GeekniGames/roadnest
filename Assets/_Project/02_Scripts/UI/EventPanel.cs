using System;
using System.Text;
using DG.Tweening;
using Roadnest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이벤트 패널: 선택 화면(스크롤 슬롯 목록)과 임시 결과 화면을 한 패널에서 전환한다.
/// Event panel: switches between the pick view (scrolling slot list) and a temporary result view.
/// </summary>
public class EventPanel : MonoBehaviour
{
    [Header("참조 / References")]
    [SerializeField] CanvasGroup rootGroup;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text dayText;         // 선택: 비워도 동작 / optional, safe to leave empty
    [SerializeField] TMP_Text bodyText;        // EventDec
    [SerializeField] ScrollRect slotScroll;
    [SerializeField] EventSlotView[] slots;    // 일반 슬롯 / normal slots
    [SerializeField] EventSlotView specialSlot;
    [SerializeField] ResultView result;
    [SerializeField] TMP_Text statusText;      // 선택: 내구도/HP 임시 표시, 3-2에서 HUD로 대체 / optional temporary readout, replaced by the HUD in 3-2

    [Header("표시 / Display")]
    [SerializeField] EventDisplay display;

    [Header("연출 / Timing")]
    [SerializeField] float fadeTime = 0.2f;

    readonly StringBuilder _slotBuilder = new StringBuilder(256);
    int[] _slotChoice;      // 슬롯 번호 -> 선택지 번호, 마지막 칸이 특별 슬롯 / slot -> choice index, last entry is the special slot
    Action<int> _onPick;
    Action _onContinue;
    Tween _fade;

    void Awake()
    {
        display.Build();
        _slotChoice = new int[slots.Length + 1];
        Action<int> click = OnSlotClicked;
        for (int i = 0; i < slots.Length; i++) slots[i].Init(i, display, click);
        specialSlot.Init(slots.Length, display, click);
        result.Init(display, OnContinueClicked);

#if UNITY_EDITOR
        if (slots.Length < EventLimits.MaxNormalChoices)
            Debug.LogError("일반 슬롯이 한도보다 적음 / normal slots fewer than the limit: " + EventLimits.MaxNormalChoices, this);
#endif

        SetHidden();
        _fade = rootGroup.DOFade(1f, fadeTime).SetAutoKill(false).SetLink(gameObject).Pause();
    }

    // 현재 이벤트를 보여주고 선택하면 선택지 번호(EventDef.Choices 기준)를 돌려준다
    // Shows the current event; the callback gets the choice index into EventDef.Choices
    public void ShowEvent(DayFlow flow, Action<int> onPick)
    {
        Activate();
        EventDef def = flow.CurrentEvent;
        _onPick = onPick;
        _onContinue = null;

        titleText.SetText(EventText.Get(def.TitleKey));
        if (dayText != null) dayText.SetText("DAY {0}", flow.Run.Day);
        bodyText.SetText(EventText.Get(def.TextKey));
        BindStatus(flow.Run);

        result.Hide();
        slotScroll.gameObject.SetActive(true);
        ChoiceDef[] choices = def.Choices;
        int slot = 0;
        bool hasSpecial = false;
        for (int i = 0; i < choices.Length; i++)
        {
            ChoiceStatus st = flow.GetChoiceStatus(i);
            if (st == ChoiceStatus.Hidden) continue;

            bool available = st == ChoiceStatus.Available;
            if (choices[i].Special)
            {
                _slotChoice[slots.Length] = i;
                specialSlot.Bind(choices[i], available);
                hasSpecial = true;
            }
            else if (slot < slots.Length)
            {
                _slotChoice[slot] = i;
                slots[slot].Bind(choices[i], available);
                slot++;
            }
        }
        for (; slot < slots.Length; slot++) slots[slot].gameObject.SetActive(false);
        if (!hasSpecial) specialSlot.gameObject.SetActive(false);

        // 슬롯 개수가 바뀌므로 레이아웃을 갱신한 뒤 맨 위로 / rebuild layout, then scroll to the top
        LayoutRebuilder.ForceRebuildLayoutImmediate(slotScroll.content);
        slotScroll.StopMovement();
        slotScroll.verticalNormalizedPosition = 1f;

        Present();
    }

    // 결과 화면: 슬롯 목록 대신 ResultArea를 보여준다. 확인 버튼을 누르면 다음으로
    // Result view: ResultArea replaces the slot list; the Done button continues
    public void ShowResult(EventDef ev, ChoiceResult r, RunState run, Action onContinue)
    {
        Activate();
        _onPick = null;
        _onContinue = onContinue;

        titleText.SetText(EventText.Get(ev.TitleKey));

        slotScroll.gameObject.SetActive(false);
        result.Show(r);
        Present();
    }

    public void Hide()
    {
        _onPick = null;
        _onContinue = null;
        if (_fade != null) _fade.Pause();
        SetHidden();
    }

    void BindStatus(RunState run)
    {
        if (statusText == null) return;
        _slotBuilder.Clear();
        _slotBuilder.Append(display.statNames[(int)StatType.Durability]).Append(' ').Append(run.Durability);
        for (int i = 0; i < run.Characters.Length; i++)
            _slotBuilder.Append("   ").Append(run.Characters[i].Id).Append(' ')
               .Append(display.statNames[(int)StatType.HpAll]).Append(' ').Append(run.Characters[i].Hp);
        statusText.SetText(_slotBuilder.ToString());
    }

    void OnSlotClicked(int slot)
    {
        Action<int> cb = _onPick;
        if (cb == null) return;
        _onPick = null;
        cb(_slotChoice[slot]);
    }

    void OnContinueClicked()
    {
        Action cb = _onContinue;
        if (cb == null) return;
        _onContinue = null;
        cb();
    }

    // 이미 보이는 중이면 내용만 바꾸고, 숨겨져 있으면 페이드로 등장 / swap content if visible, fade in if hidden
    void Present()
    {
        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = true;
        if (rootGroup.alpha < 0.99f)
        {
            rootGroup.alpha = 0f;
            _fade.Restart();
        }
    }

    // 꺼진 채 시작해도 보이도록 스스로 켠다 / self-activates so it works even when it starts disabled
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