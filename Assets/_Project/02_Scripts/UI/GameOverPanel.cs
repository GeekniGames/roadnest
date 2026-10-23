using System;
using DG.Tweening;
using Roadnest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

/// <summary>
/// 게임오버 패널: 종료 사유에 맞는 이미지와 문구를 보여주고 재시작 버튼을 누르면 콜백을 호출한다.
/// Game over panel: shows the image and text for the end reason
/// </summary>
public class GameOverPanel : MonoBehaviour
{
    [Serializable]
    class Entry
    {
        public EndReason reason;
        public Sprite image;
        public LocalizedString title;
        public LocalizedString desc;
    }

    [Header("참조 / Refernces")]
    [SerializeField] CanvasGroup rootGroup;
    [SerializeField] RectTransform window;
    [SerializeField] Image reasonImage;
    [SerializeField] TMP_Text dayText;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descText;
    [SerializeField] TMP_Text buttonText;
    [SerializeField] Button restartButton;

    [Header("사유별 내용 / Per-reson content")]
    [SerializeField] Entry[] entries;
    [SerializeField] LocalizedString buttonLabel;

    [Header("연출 / Timing")]
    [SerializeField] float fadeTime = 0.3f;
    [SerializeField] float popTime = 0.4f;
    [SerializeField, Range(0.5f, 1f)] float popFrom = 0.85f;

    LocalizedLabel _title, _desc, _button;
    Action _onRestart;
    Tween _fade, _pop;
    Vector3 _popStart;


    void Awake()
    {
        _title = new LocalizedLabel(titleText);
        _desc = new LocalizedLabel(descText);
        if (buttonText != null) _button = new LocalizedLabel(buttonText);
        _popStart = new Vector3(popFrom, popFrom, 1f);
        restartButton.onClick.AddListener(OnRestartClicked);

        SetHidden();
        _fade = rootGroup.DOFade(1f, fadeTime).SetAutoKill(false).SetLink(gameObject).Pause();
        _pop = window.DOScale(1f, popTime).SetEase(Ease.OutBack).SetAutoKill(false).SetLink(gameObject).Pause();
    }

    void OnDestroy()
    {
        _title?.Release();
        _desc?.Release();
        _button?.Release();
    }

    public void Show(EndReason reason, int day, Action onRestart)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        _onRestart = onRestart;

        Entry e = Find(reason);
        if (e != null)
        {
            reasonImage.sprite = e.image;
            _title.Bind(e.title, 0);
            _desc.Bind(e.desc, 0);
        }
#if UNITY_EDITOR
        else Debug.LogError("종료 사유 항목 없음 / no entry for end reason: " + reason, this);
#endif
        _button.Bind(buttonLabel, 0);
        dayText.SetText("DAY {0}", day);

        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = true;
        window.localScale = _popStart;
        _fade.Restart();
        _pop.Restart();
    }

    Entry Find(EndReason reason)
    {
        for (int i = 0; i < entries.Length; i++)
            if (entries[i].reason == reason) return entries[i];
        return null;
    }

    void OnRestartClicked()
    {
        Action cb = _onRestart;
        if (cb == null) return;
        _onRestart = null;
        cb();
    }

    void SetHidden()
    {
        rootGroup.alpha = 0;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;
    }
}
