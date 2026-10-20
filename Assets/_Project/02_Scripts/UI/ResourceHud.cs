using DG.Tweening;
using Roadnest.Core;
using System.Runtime.Versioning;
using TMPro;
using UnityEngine;

/// <summary>
/// 카라반 위 자원 아이콘/숫자 표시. 값이 바뀔 때만 갱신한다.
/// Shows resource icons and counts above the caravan. Updates only on change.
/// </summary>
public class ResourceHud : MonoBehaviour
{
    // 순서: 연료, 식량, 물 (ResourceType 순서와 동일) / order matches ResourceType
    [SerializeField] Transform[] icons = new Transform[ResourceStore.Count];
    [SerializeField] TMP_Text[] labels = new TMP_Text[ResourceStore.Count];

    [Header("연출 / Feedback")]
    [SerializeField] float punchScale = 0.25f;
    [SerializeField] float punchTime = 0.2f;
    [SerializeField] float flashTime = 0.35f;
    [SerializeField] Color gainColor = new Color(0.6f, 1f, 0.6f);
    [SerializeField] Color lossColor = new Color(1f, 0.55f, 0.55f);

    ResourceStore _store;
    Color[] _baseColor;
    Vector3[] _punch;
    bool _ready;
    int[] _shown;       // 화면에 보이는 값 / values currently displayed
    bool _held;


    void Awake() => Init();

    // Awake 순서와 무관하게 안전하게 초기화 / safe regardless of Awake order
    void Init()
    {
        if (_ready) return;
        _ready = true;
        _baseColor = new Color[ResourceStore.Count];
        _punch = new Vector3[ResourceStore.Count];
        _shown = new int[ResourceStore.Count];
        for (int i = 0; i < ResourceStore.Count; i++)
        {
            _baseColor[i] = labels[i].color;
            _punch[i] = icons[i].localScale * punchScale;
        }
    }

    public void Bind(ResourceStore store)
    {
        Init();
        Unbind();
        _store = store;
        _held = false;
        for (int i = 0; i < ResourceStore.Count; i++)
        {
            _shown[i] = store.Get((ResourceType)i);
            labels[i].SetText("{0}", _shown[i]);
        }
        store.Changed += OnChanged;
    }

    void Unbind()
    {
        if (_store != null) _store.Changed -= OnChanged;
        _store = null;
    }

    void OnDestroy() => Unbind();

    // 결과 화면 동안 표시를 멈췄다가 Release에서 한꺼번에 반영
    // Freeze the display during the result view, apply everything on Release
    public void Hold() => _held = true;

    // 선택 즉시 비용만 먼저 반영 / apply only the cost right away on pick
    public void ShowCost(Effect[] cost)
    {
        for (int i = 0; i < cost.Length; i++)
        {
            int s = (int)cost[i].Stat;
            if (s >= ResourceStore.Count) continue;   // 자원이 아닌 비용은 HUD에 칸이 없음 / non-resource costs have no HUD slot
            int v = _shown[s] + cost[i].Amount;
            Show(s, v < 0 ? 0 : v);
        }
    }

    public void Release()
    {
        _held = false;
        if (_store == null) return;
        for (int i = 0; i < ResourceStore.Count; i++)
        {
            int v = _store.Get((ResourceType)i);
            if (v != _shown[i]) Show(i, v);
        }
    }

    void OnChanged(ResourceType t, int oldValue, int newValue)
    {
        if (_held) return;
        Show((int)t, newValue);
    }

    void Show(int i, int newValue)
    {
        int old = _shown[i];
        _shown[i] = newValue;
        labels[i].SetText("{0}", newValue);

        icons[i].DOKill(true);
        labels[i].DOKill();
        labels[i].color = newValue > old ? gainColor : lossColor;
        labels[i].DOColor(_baseColor[i], flashTime).SetLink(labels[i].gameObject);
        icons[i].DOPunchScale(_punch[i], punchTime, 1, 0f).SetLink(icons[i].gameObject);
    }
}