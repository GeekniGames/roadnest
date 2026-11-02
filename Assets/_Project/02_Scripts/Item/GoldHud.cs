using System;
using DG.Tweening;
using Roadnest.Core;
using TMPro;
using UnityEngine;

/// <summary>
/// 골드 HUD: 숫자가 천 단위 쉼표로 부드럽게 따라가고 코인이 튄다. 이벤트 결과 화면 동안 표시를 멈출 수 있다.
/// Gold HUD: the number follows smoothly with thousands separators and the coin pops. The display can be held during the result view.
/// </summary>
public class GoldHud : MonoBehaviour
{
    [SerializeField] RectTransform icon;    // GoldFrame/Icon
    [SerializeField] TMP_Text value;        // Text (TMP)_GoldValue

    [Header("연출 / Feedback")]
    [SerializeField] float moveTime = 0.4f;
    [SerializeField] float flashTime = 0.35f;
    [SerializeField] float punchScale = 0.3f;
    [SerializeField] float punchTime = 0.3f;
    [SerializeField] Color gainColor = new Color(0.6f, 1f, 0.6f);
    [SerializeField] Color lossColor = new Color(1f, 0.55f, 0.55f);

    static readonly char[] Buf = new char[12];

    GoldWallet _wallet;
    Color _restColor;
    float _fill;
    int _target;
    bool _held;
    bool _ready;
    Tween _move;
    TweenCallback<float> _onFill;
    Action _onChanged;

    void Awake() => Init();

    void Init()
    {
        if (_ready) return;
        _ready = true;
        _restColor = value.color;
        _onFill = OnFill;
        _onChanged = OnChanged;
    }

    // 런 시작 시 현재 값으로 즉시 맞춘다 / snap to the current value at run start
    public void Bind(GoldWallet wallet)
    {
        Init();
        Unbind();
        _wallet = wallet;
        _held = false;
        _target = wallet.Amount;
        _fill = _target;
        Render(_target);
        wallet.Changed += _onChanged;
    }

    void Unbind()
    {
        if (_wallet != null) _wallet.Changed -= _onChanged;
        _wallet = null;
    }

    void OnDestroy() => Unbind();

    // 결과 화면 동안 표시를 멈췄다가 Release에서 반영 / freeze during the result view, apply on Release
    public void Hold() => _held = true;

    // 선택 즉시 골드 비용만 먼저 반영 / apply only the gold cost right away on pick
    public void ShowCost(Effect[] cost)
    {
        for (int i = 0; i < cost.Length; i++)
        {
            if (cost[i].Stat != StatType.Gold || cost[i].Amount >= 0) continue;
            AnimateTo(Mathf.Max(0, _target + cost[i].Amount));
        }
    }

    public void Release()
    {
        _held = false;
        if (_wallet != null) AnimateTo(_wallet.Amount);
    }

    void OnChanged()
    {
        if (_held) return;
        AnimateTo(_wallet.Amount);
    }

    void AnimateTo(int v)
    {
        if (v == _target) return;
        bool gain = v > _target;
        _target = v;

        if (_move != null && _move.IsActive()) _move.Kill();
        _move = DOVirtual.Float(_fill, v, moveTime, _onFill).SetEase(Ease.OutQuad).SetLink(gameObject);

        value.DOKill();
        value.color = gain ? gainColor : lossColor;
        value.DOColor(_restColor, flashTime).SetLink(gameObject);

        icon.DOKill(true);
        icon.DOPunchScale(Vector3.one * punchScale, punchTime, 6, 0.5f).SetLink(gameObject);
    }

    void OnFill(float v)
    {
        _fill = v;
        Render(Mathf.RoundToInt(v));
    }

    // 문자열을 만들지 않고 글자 배열에 직접 쓴다 / write straight into a char buffer, no string allocation
    void Render(int v)
    {
        int pos = Buf.Length;
        int digits = 0;
        if (v <= 0) Buf[--pos] = '0';
        while (v > 0)
        {
            if (digits > 0 && digits % 3 == 0) Buf[--pos] = ',';
            Buf[--pos] = (char)('0' + v % 10);
            v /= 10;
            digits++;
        }
        value.SetText(Buf, pos, Buf.Length - pos);
    }
}