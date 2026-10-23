using DG.Tweening;
using DG.Tweening.Core;
using UnityEngine;

/// <summary>
/// 전체 스크롤 속도 관리. 서서히 출발, 서서히 정지.
/// Owns global scroll speed with eased start/stop.
/// </summary>
public class ScrollController : MonoBehaviour
{
    [SerializeField] ScrollingLayer[] layers;
    [SerializeField] ScrollingGround ground;
    [SerializeField] float maxSpeed = 7.5f;   // 유닛/초 (참조 120px/s @PPU16) / units per second
    [SerializeField] float accelTime = 1f;
    [SerializeField] float decelTime = 1f;

    float _speed;
    Tween _tween;
    DOGetter<float> _get;
    DOSetter<float> _set;

    public float Speed => _speed;
    public float Speed01 => _speed / maxSpeed;   // 0~1 정규화 속도 / normalized speed

    void Awake()
    {
        _get = () => _speed;
        _set = v => _speed = v;
    }

    void OnDestroy() => KillTween();

    public void StartMoving() => TweenSpeed(maxSpeed, accelTime);
    public void StopMoving() => TweenSpeed(0f, decelTime);

    // RoadSceneController의 Update에서 호출 / called from RoadSceneController.Update
    public void Tick(float dt)
    {
        if (_speed <= 0.0001f) return;
        float d = _speed * dt;
        for (int i = 0; i < layers.Length; i++)
            layers[i].Advance(d);
        if (ground != null) ground.Advance(d);
    }

    void TweenSpeed(float target, float time)
    {
        KillTween();
        _tween = DOTween.To(_get, _set, target, time).SetEase(Ease.InOutSine).SetLink(gameObject);
    }

    void KillTween()
    {
        if (_tween != null && _tween.IsActive()) _tween.Kill();
    }
}