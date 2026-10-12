using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택 슬롯 하나의 표시와 클릭 처리. Bind 할 때만 갱신한다.
/// Display and click handling of one choice slot. Updates only on Bind.
/// </summary>
public class ChoiceSlotView : MonoBehaviour
{
    [Serializable]
    public struct ResultRow
    {
        public GameObject root;   // Result_0 / Result_1
        public Image icon;
        public TMP_Text text;
    }

    [SerializeField] Button button;
    [SerializeField] CanvasGroup group;
    [SerializeField] Image art;
    [SerializeField] TMP_Text title;
    [SerializeField] Image costIcon;
    [SerializeField] TMP_Text costText;
    [SerializeField] ResultRow[] results = new ResultRow[2];

    [Header("상태 / State")]
    [SerializeField, UnityEngine.Range(0f, 1f)] float lockedAlpha = 0.5f;
    [SerializeField] Color lockedCostColor = new Color(1f, 0.45f, 0.45f);

    [Header("눌림 연출 / Press")]
    [SerializeField] float pressPunch = -0.06f;
    [SerializeField] float pressTime = 0.15f;

    Action<int> _onClick;
    int _index;
    Color _costColor;
    Vector3 _baseScale;
    Tween _press;
    LocalizedLabel _titleLabel;
    LocalizedLabel _costLabel;
    LocalizedLabel[] _resultLabels;

    void Awake()
    {
        _costColor = costText.color;
        _baseScale = transform.localScale;

        _titleLabel = new LocalizedLabel(title);
        _costLabel = new LocalizedLabel(costText);
        _resultLabels = new LocalizedLabel[results.Length];
        for (int i = 0; i < results.Length; i++) _resultLabels[i] = new LocalizedLabel(results[i].text);

        button.onClick.AddListener(OnClick);
    }

    void OnDestroy() => Release();

    public void Init(int index, Action<int> onClick)
    {
        _index = index;
        _onClick = onClick;
    }

    public void Bind(ChoiceSlotData d, bool enabled)
    {
        KillPress();
        transform.localScale = _baseScale;

        if (d.art != null) art.sprite = d.art;
        _titleLabel.Bind(d.title, 0);
        SetLine(costIcon, _costLabel, d.cost);

        int n = d.results == null ? 0 : Mathf.Min(d.results.Length, results.Length);
        for (int i = 0; i < results.Length; i++)
        {
            bool on = i < n;
            results[i].root.SetActive(on);
            if (on) SetLine(results[i].icon, _resultLabels[i], d.results[i]);
            else _resultLabels[i].Release();
        }

        costText.color = enabled ? _costColor : lockedCostColor;
        group.alpha = enabled ? 1f : lockedAlpha;
        button.interactable = enabled;
    }

    // 패널이 닫힐 때 구독 해제 / unsubscribe when the panel closes
    public void Release()
    {
        if (_titleLabel == null) return;
        _titleLabel.Release();
        _costLabel.Release();
        for (int i = 0; i < _resultLabels.Length; i++) _resultLabels[i].Release();
    }

    public void PlayPress()
    {
        KillPress();
        _press = transform.DOPunchScale(_baseScale * pressPunch, pressTime, 1, 0f).SetLink(gameObject);
    }

    static void SetLine(Image icon, LocalizedLabel label, ChoiceLine line)
    {
        if (line == null)
        {
            label.Release();
            icon.gameObject.SetActive(false);
            return;
        }

        label.Bind(line.text, line.amount);
        bool hasIcon = line.icon != null;
        icon.gameObject.SetActive(hasIcon);
        if (hasIcon) icon.sprite = line.icon;
    }

    void KillPress()
    {
        if (_press != null && _press.IsActive()) _press.Kill();
    }

    void OnClick() => _onClick?.Invoke(_index);
}