using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 차체 연출: 시동 떨림, 정차 아이들 떨림, 주행 흔들림, 바퀴 프레임.
/// Body presentation: ignition shake, idle shake, driving bob, wheel frames.
/// </summary>
public class CaravanVisual : MonoBehaviour
{
    [SerializeField] Transform body;            // CarSprite
    [SerializeField] float pixelsPerUnit = 16f;

    [Header("주행 / Driving")]
    [SerializeField] float bobPixels = 1f;
    [SerializeField] float bobPeriod = 0.2f;    // 위아래 1회 / one up-down cycle
    [SerializeField] float settleTime = 0.25f;

    [Header("시동 / Engine")]
    [SerializeField] float ignitionTime = 0.8f;
    [SerializeField] float ignitionShakePixels = 2f;
    [SerializeField] float ignitionShakePeriod = 0.05f;
    [SerializeField] float idleShakePixels = 1f;
    [SerializeField] float idleShakePeriod = 0.08f;

    [Header("바퀴 / Wheels")]
    [SerializeField] SpriteRenderer wheelRenderer;
    [SerializeField] Sprite[] wheelFrames;
    [SerializeField] float maxWheelFps = 12f;   // 최고속도에서 초당 프레임 전환 수 / frame changes per sec at max speed

    [Header("경고 / Alert")]
    [SerializeField] Transform alertMark;       // 차 앞 느낌표(자식 오브젝트) / "!" sprite, child of the car
    [SerializeField] float alertPopTime = 0.25f;
    [SerializeField] float alertHoldTime = 0.9f;

    Vector3 _alertScale;
    Sequence _alertSeq;
    Action _alertDone;
    TweenCallback _onAlertEnd;

    float _wheelTime;
    int _wheelIndex;

    Vector3 _rest;
    Tween _tween, _shakeX, _shakeY;
    TweenCallback _startIdle, _onIgnitionEnd;
    Action _ignitionDone;

    void Awake()
    {
        _rest = body.localPosition;
        _startIdle = StartIdle;
        _onIgnitionEnd = OnIgnitionEnd;
        _alertScale = alertMark.localScale;
        alertMark.gameObject.SetActive(false);
        _onAlertEnd = OnAlertEnd;
    }

    // 시동 걸림: 큰 떨림 후 아이들 떨림으로 전환, 끝나면 콜백
    // Ignition: strong shake, then idle shake; callback when finished
    public void PlayIgnition(Action onDone)
    {
        _ignitionDone = onDone;
        Shake(ignitionShakePixels, ignitionShakePeriod);
        _tween = DOVirtual.DelayedCall(ignitionTime, _onIgnitionEnd).SetLink(gameObject);
    }

    public void StartIdle() => Shake(idleShakePixels, idleShakePeriod);

    // 시동 꺼짐: 떨림 정지 / engine off: stop shaking
    public void TurnOff()
    {
        KillAll();
        _tween = body.DOLocalMove(_rest, 0.1f).SetEase(Ease.OutSine).SetLink(gameObject);
    }

    public void StartMoving()
    {
        KillAll();
        body.localPosition = _rest;
        _tween = body.DOLocalMoveY(_rest.y + bobPixels / pixelsPerUnit, bobPeriod * 0.5f)
                     .SetEase(Ease.InOutSine)
                     .SetLoops(-1, LoopType.Yoyo)
                     .SetLink(gameObject);
    }

    // 정지 후에도 시동은 켜진 상태라 아이들 떨림으로 복귀 / engine stays on, so return to idle shake
    public void StopMoving()
    {
        KillAll();
        _tween = body.DOLocalMove(_rest, settleTime)
                     .SetEase(Ease.OutSine)
                     .SetLink(gameObject)
                     .OnComplete(_startIdle);
    }

    // 느낌표가 튀어나오고 잠시 유지된 뒤 콜백 / "!" pops in, holds, then callback
    public void ShowAlert(Action onDone)
    {
        _alertDone = onDone;
        if (_alertSeq != null && _alertSeq.IsActive()) _alertSeq.Kill();

        alertMark.localScale = Vector3.zero;
        alertMark.gameObject.SetActive(true);
        _alertSeq = DOTween.Sequence().SetLink(gameObject);
        _alertSeq.Append(alertMark.DOScale(_alertScale, alertPopTime).SetEase(Ease.OutBack));
        _alertSeq.AppendInterval(alertHoldTime);
        _alertSeq.OnComplete(_onAlertEnd);
    }

    void OnAlertEnd()
    {
        alertMark.gameObject.SetActive(false);
        Action cb = _alertDone;
        _alertDone = null;
        cb?.Invoke();
    }

    // 속도에 비례해 바퀴 프레임 교체 / swap wheel frames in proportion to speed
    public void Tick(float dt, float speed01)
    {
        if (speed01 <= 0.001f) return;

        _wheelTime += dt * maxWheelFps * speed01;
        if (_wheelTime >= wheelFrames.Length) _wheelTime %= wheelFrames.Length;

        int idx = (int)_wheelTime;
        if (idx == _wheelIndex) return;

        _wheelIndex = idx;
        wheelRenderer.sprite = wheelFrames[idx];
    }

    void OnIgnitionEnd()
    {
        StartIdle();
        Action cb = _ignitionDone;
        _ignitionDone = null;
        cb?.Invoke();
    }

    // X는 주기를 다르게 해서 규칙적으로 보이지 않게 함 / X uses a different period to avoid a mechanical look
    void Shake(float pixels, float period)
    {
        KillAll();
        body.localPosition = _rest;
        float amp = pixels / pixelsPerUnit;
        _shakeY = body.DOLocalMoveY(_rest.y + amp, period * 0.5f)
                      .SetEase(Ease.Linear).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
        _shakeX = body.DOLocalMoveX(_rest.x + amp * 0.5f, period * 0.7f)
                      .SetEase(Ease.Linear).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
    }

    void KillAll()
    {
        Kill(ref _tween);
        Kill(ref _shakeX);
        Kill(ref _shakeY);
    }

    static void Kill(ref Tween t)
    {
        if (t != null && t.IsActive()) t.Kill();
        t = null;
    }
}