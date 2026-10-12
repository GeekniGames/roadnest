using UnityEngine;
using System;
using TMPro;
using DG.Tweening;

/// <summary>
/// 화면 중앙 DAY 텍스트 연출. 시퀀스는 한 번만 만들어 재사용한다.
/// Center-screen DAY banner. The sequenve is built once and reused.
/// </summary>

public class DayBanner : MonoBehaviour
{
    [SerializeField] TMP_Text label;
    [SerializeField] CanvasGroup group;
    [SerializeField] float fadeIn = 0.3f;
    [SerializeField] float hold = 1f;
    [SerializeField] float fadeOut = 0.4f;
    [SerializeField, Range(0.5f, 1f)] float startScale = 0.8f;

    Sequence _seq;
    Transform _tf;
    Action _onDone;

    public bool IsPlaying => _seq != null && _seq.IsActive() && _seq.IsPlaying();



    void Awake()
    {
        _tf = label.transform;
        group.alpha = 0;
        group.interactable = false;
        group.blocksRaycasts = false;
        _tf.localScale = Vector3.one * startScale;
    }

    //숫자만 바꿔서 매일 재사용 / reusable for every new day
    public void Show(int day, Action onDone = null)
    {
        _onDone = onDone;
        label.SetText("DAY {0}", day);
        if (_seq == null) Build();
        else _seq.Restart();
    }

    void Build()
    {
        _seq = DOTween.Sequence()
            .SetAutoKill(false)
            .SetLink(gameObject)
            .Append(group.DOFade(1f, fadeIn).SetEase(Ease.OutSine))
            .Join(_tf.DOScale(1f, fadeIn).SetEase(Ease.OutBack))
            .AppendInterval(hold)
            .Append(group.DOFade(0f, fadeOut).SetEase(Ease.InSine))
            .OnComplete(OnDone);
    }

    void OnDone() => _onDone?.Invoke();
}
