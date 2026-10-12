using UnityEngine;

/// <summary>
/// 복제본을 이어 붙여 무한 반복하는 스크롤 레이어.
/// Looping scroll layer built from repeated copies of one sprite.
/// Update 없음, ScrollController가 Advance를 호출한다. / No Update; driven by ScrollController.
/// </summary>
public class ScrollingLayer : MonoBehaviour
{
    [SerializeField] SpriteRenderer source;            // 원본 스프라이트 / template sprite
    [SerializeField, Min(2)] int copyCount = 4;
    [SerializeField] float speedRatio = 1f;            // 시차 비율 / parallax ratio
    [SerializeField] float slope = 0.506f;             // 이미지 기울기(세로/가로) / image slope (dy/dx)

    Transform[] _copies;
    Vector3 _origin;
    Vector3 _period;       // 로컬 기준 한 주기 / one period, local space
    float _periodWorld;    // 월드 길이 / world length of a period
    float _phase;          // 0~1
    int _half;

    void Awake()
    {
        float w = source.sprite.bounds.size.x;
        _period = new Vector3(w, -w * slope, 0f);
        _periodWorld = _period.magnitude * transform.lossyScale.x;
        _origin = source.transform.localPosition;
        _half = copyCount / 2;

        _copies = new Transform[copyCount];
        _copies[0] = source.transform;
        for (int i = 1; i < copyCount; i++)
            _copies[i] = Instantiate(source, transform).transform;

        Apply();
    }

    // 월드 거리만큼 도로 반대 방향(왼쪽 위)으로 이동 / moves toward upper-left by world distance
    public void Advance(float worldDistance)
    {
        _phase += worldDistance * speedRatio / _periodWorld;
        _phase -= Mathf.Floor(_phase);
        Apply();
    }

    void Apply()
    {
        for (int i = 0; i < _copies.Length; i++)
            _copies[i].localPosition = _origin + _period * (i - _half - _phase);
    }
}