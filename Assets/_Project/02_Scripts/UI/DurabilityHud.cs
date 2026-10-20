using DG.Tweening;
using Roadnest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 내구도 바: 값이 바뀌면 바와 숫자가 부드럽게 따라가고, 일정 비율 이하면 색이 바뀌며 깜빡인다.
/// Durability bar: the bar and number follow changes smoothly; below a ratio it changes color and blinks.
/// </summary>
public class DurabilityHud : MonoBehaviour
{
    [SerializeField] Image bar;             // Image Type = Filled, Horizontal
    [SerializeField] TMP_Text label;        // 선택: 비워도 동작 / optional, safe to leave empty

    [Header("연출 / Feedback")]
    [SerializeField] float moveTime = 0.4f;
    [SerializeField] float flashTime = 0.35f;
    [SerializeField] Color gainColor = new Color(0.6f, 1f, 0.6f);
    [SerializeField] Color lossColor = new Color(1f, 0.55f, 0.55f);

    [Header("경고 / Warning")]
    [SerializeField, Range(0f, 1f)] float warnRatio = 0.3f;
    [SerializeField] Sprite warnSprite;     // 비우면 색만 바꿈 / leave empty to tint only
    [SerializeField] Color warnColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField, Range(0f, 1f)] float blinkAlpha = 0.4f;
    [SerializeField] float blinkTime = 0.35f;

    int _max = 1;
    int _shown;             // 목표 값 / target value
    float _fill;            // 연출 중인 값 / animated value
    bool _low;
    bool _ready;
    Sprite _barSprite;
    Color _barColor, _labelColor;
    Tween _move, _blink;
    TweenCallback<float> _onFill;

    void Awake() => Init();

    // Awake 순서와 무관하게 안전하게 초기화 / safe regardless of Awake order
    void Init()
    {
        if (_ready) return;
        _ready = true;
        _barSprite = bar.sprite;
        _barColor = bar.color;
        if (label != null) _labelColor = label.color;
        _onFill = OnFill;
        _blink = bar.DOFade(blinkAlpha, blinkTime)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetAutoKill(false)
                    .SetLink(gameObject)
                    .Pause();
    }

    public void Bind(int current, int max)
    {
        Init();
        _max = max < 1 ? 1 : max;
        _shown = Mathf.Clamp(current, 0, _max);
        _fill = _shown;
        if (_move != null && _move.IsActive()) _move.Kill();
        OnFill(_fill);

        _low = false;
        _blink.Pause();
        bar.sprite = _barSprite;
        bar.color = _barColor;
        SetLow(IsLow(_shown));
    }

    // 값을 목표로 부드럽게 이동 / animate toward the target value
    public void Show(int value)
    {
        value = Mathf.Clamp(value, 0, _max);
        if (value == _shown) return;

        bool gain = value > _shown;
        _shown = value;

        if (_move != null && _move.IsActive()) _move.Kill();
        _move = DOVirtual.Float(_fill, value, moveTime, _onFill).SetEase(Ease.OutQuad).SetLink(gameObject);
        SetLow(IsLow(value));

        if (label == null) return;
        label.DOKill();
        label.color = gain ? gainColor : lossColor;
        label.DOColor(_labelColor, flashTime).SetLink(label.gameObject);
    }

    // 선택 즉시 내구도 비용만 반영 / apply only the durability cost right away on pick
    public void ShowCost(Effect[] cost)
    {
        for (int i = 0; i < cost.Length; i++)
            if (cost[i].Stat == StatType.Durability) Show(_shown + cost[i].Amount);
    }

    bool IsLow(int v) => v <= _max * warnRatio;

    void SetLow(bool low)
    {
        if (low == _low) return;
        _low = low;
        if (low)
        {
            if (warnSprite != null) bar.sprite = warnSprite;
            else bar.color = warnColor;
            _blink.Restart();
        }
        else
        {
            _blink.Pause();
            bar.sprite = _barSprite;
            bar.color = _barColor;
        }
    }

    void OnFill(float v)
    {
        _fill = v;
        bar.fillAmount = v / _max;
        if (label != null) label.SetText("{0}/{1}", Mathf.RoundToInt(v), _max);
    }
}