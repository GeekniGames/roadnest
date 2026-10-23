using UnityEngine;

/// <summary>
/// 작은 타일 한 장을 화면 크기만큼 깔고 도로와 같은 방향으로 무한 스크롤하는 땅. Update 없음.
/// Ground built from one small tile, scrolling infinitely in the road's direction. No Update; driven by ScrollController.
/// </summary>
public class ScrollingGround : MonoBehaviour
{
    [SerializeField] SpriteRenderer sprite;
    [SerializeField] ScrollingLayer roadLayer;     // 이동 방향 기준 / direction reference
    [SerializeField] Camera cam;
    [SerializeField] Vector2 groundScale = new Vector2(0.6f, 0.8f);   // 땅 스케일 X, Y 고정값 / fixed ground scale X, Y
    [SerializeField] float speedRatio = 1f;

    Vector2 _dir;
    Vector2 _offset;
    Vector2 _center;
    Vector2 _tile;         // 타일 한 장의 월드 크기(가로, 세로) / world size of one tile (x, y)
    float _z;

    void Awake()
    {
        Vector2 scale = groundScale;
        Vector2 native = sprite.sprite.bounds.size;
        _tile = new Vector2(native.x * scale.x, native.y * scale.y);

        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * cam.aspect;

        // 한 타일만큼 밀려도 덮이도록 축별 여유 / per-axis margin so it stays covered while shifting up to one tile
        sprite.drawMode = SpriteDrawMode.Tiled;
        sprite.tileMode = SpriteTileMode.Continuous;
        sprite.size = new Vector2((viewW + _tile.x * 2f) / scale.x, (viewH + _tile.y * 2f) / scale.y);

        // 모드 전환 뒤에 스케일을 적용해야 덮어씌워지지 않는다 / apply scale after the mode switch so nothing overwrites it
        ApplyScale();

        Vector3 c = cam.transform.position;
        _center = new Vector2(c.x, c.y);
        _z = transform.position.z;
        Apply();
    }

    // 모든 Awake가 끝난 뒤 방향을 읽고 스케일을 한 번 더 확정 / read the direction and re-confirm the scale after every Awake
    void Start()
    {
        _dir = roadLayer.WorldDirection;
        ApplyScale();
    }

    // ScrollController가 호출. 두 축 모두 타일 주기로 감아서 값이 커지지 않는다
    // Called by ScrollController. Both axes wrap by the tile size so values stay small
    public void Advance(float worldDistance)
    {
        _offset += _dir * (worldDistance * speedRatio);
        _offset.x -= Mathf.Floor(_offset.x / _tile.x) * _tile.x;
        _offset.y -= Mathf.Floor(_offset.y / _tile.y) * _tile.y;
        Apply();
    }

    void ApplyScale() => transform.localScale = new Vector3(groundScale.x, groundScale.y, 1f);

    void Apply() => transform.position = new Vector3(_center.x + _offset.x, _center.y + _offset.y, _z);
}