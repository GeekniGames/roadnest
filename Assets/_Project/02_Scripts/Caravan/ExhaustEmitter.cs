using UnityEngine;

/// <summary>
/// 배기가스 풀링 방출. 퍼프별 트윈 대신 단일 Tick으로 갱신해 GC 없음.
/// Pooled exhaust puffs. Updated from a single Tick instead of per-puff tweens, zero GC.
/// </summary>
public class ExhaustEmitter : MonoBehaviour
{
    [Header("참조 / References")]
    [SerializeField] Transform exhaustPoint;
    [SerializeField] Sprite[] puffSprites;
    [SerializeField] string sortingLayerName = "Default";
    [SerializeField] int sortingOrder = 0;
    [SerializeField] int poolSize = 12;

    [Header("방출 / Emit")]
    [SerializeField] Vector2 intervalRange = new Vector2(0.6f, 2f);
    [SerializeField] float burstScale = 1.4f;

    [Header("연기 / Puff")]
    [SerializeField] Vector2 lifeRange = new Vector2(0.7f, 1.1f);
    [SerializeField] Vector2 driftDir = new Vector2(-1f, 0.5557f);  // 도로가 밀려나는 방향 / direction the road slides
    [SerializeField] float idleDrift = 0.4f;     // 정차 시 이동 거리 (unit) / travel when parked
    [SerializeField] float runDrift = 2.2f;      // 최고속도 시 이동 거리 / travel at full speed
    [SerializeField] float rise = 0.35f;
    [SerializeField] float scaleFrom = 0.6f;
    [SerializeField] float scaleTo = 1.4f;
    [SerializeField, UnityEngine.Range(0f, 1f)] float startAlpha = 0.8f;
    [SerializeField] Color tint = Color.white;

    struct Puff
    {
        public Transform tf;
        public SpriteRenderer sr;
        public Vector3 start;
        public Vector3 move;
        public float age, life, scaleMul;
        public bool active;
    }

    Puff[] _puffs;
    Vector2 _dir;
    int _activeCount;

    bool _emitting;
    float _timer;
    bool _burstPending;
    float _burstTimer;

    void Awake()
    {
        _dir = driftDir.normalized;
        _puffs = new Puff[poolSize];
        for (int i = 0; i < _puffs.Length; i++)
        {
            var go = new GameObject("Puff");
            var tf = go.transform;
            tf.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (!string.IsNullOrEmpty(sortingLayerName)) sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;
            sr.enabled = false;
            _puffs[i].tf = tf;
            _puffs[i].sr = sr;
        }
    }

    public void StartEmitting()
    {
        _emitting = true;
        _timer = Random.Range(0f, intervalRange.x);
    }

    public void StopEmitting() => _emitting = false;

    // 시동 때 한 번 뿜기 / single puff for ignition
    public void Burst(float delay)
    {
        _burstPending = true;
        _burstTimer = delay;
    }

    public void Tick(float dt, float speed01)
    {
        if (_burstPending)
        {
            _burstTimer -= dt;
            if (_burstTimer <= 0f)
            {
                _burstPending = false;
                Spawn(burstScale, speed01);
            }
        }

        if (_emitting)
        {
            _timer -= dt;
            if (_timer <= 0f)
            {
                Spawn(1f, speed01);
                _timer = Random.Range(intervalRange.x, intervalRange.y);
            }
        }

        if (_activeCount == 0) return;

        for (int i = 0; i < _puffs.Length; i++)
        {
            ref Puff p = ref _puffs[i];
            if (!p.active) continue;

            p.age += dt;
            float t = p.age / p.life;
            if (t >= 1f)
            {
                p.active = false;
                p.sr.enabled = false;
                _activeCount--;
                continue;
            }

            float e = 1f - (1f - t) * (1f - t);
            p.tf.position = p.start + p.move * e;
            float s = Mathf.Lerp(scaleFrom, scaleTo, e) * p.scaleMul;
            p.tf.localScale = new Vector3(s, s, 1f);
            Color c = tint;
            c.a = startAlpha * (1f - t);
            p.sr.color = c;
        }
    }

    void Spawn(float scaleMul, float speed01)
    {
        if (puffSprites == null || puffSprites.Length == 0) return;

        int slot = -1;
        for (int i = 0; i < _puffs.Length; i++)
        {
            if (!_puffs[i].active) { slot = i; break; }
        }
        if (slot < 0) return;   // 풀 소진 시 생략 / skip when pool is exhausted

        ref Puff p = ref _puffs[slot];
        float dist = Mathf.Lerp(idleDrift, runDrift, speed01);
        p.active = true;
        p.age = 0f;
        p.life = Random.Range(lifeRange.x, lifeRange.y);
        p.scaleMul = scaleMul;
        p.start = exhaustPoint.position;
        p.move = new Vector3(_dir.x * dist, _dir.y * dist + rise, 0f);
        p.tf.position = p.start;
        p.sr.sprite = puffSprites[Random.Range(0, puffSprites.Length)];
        p.sr.enabled = true;
        _activeCount++;
    }
}