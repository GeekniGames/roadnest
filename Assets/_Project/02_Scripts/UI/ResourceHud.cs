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

    void Awake() => Init();

    // Awake 순서와 무관하게 안전하게 초기화 / safe regardless of Awake order
    void Init()
    {
        if (_ready) return;
        _ready = true;
        _baseColor = new Color[ResourceStore.Count];
        _punch = new Vector3[ResourceStore.Count];
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
        for (int i = 0; i < ResourceStore.Count; i++)
            labels[i].SetText("{0}", store.Get((ResourceType)i));
        store.Changed += OnChanged;
    }

    void Unbind()
    {
        if (_store != null) _store.Changed -= OnChanged;
        _store = null;
    }

    void OnDestroy() => Unbind();

    void OnChanged(ResourceType t, int oldValue, int newValue)
    {
        int i = (int)t;
        labels[i].SetText("{0}", newValue);

        icons[i].DOKill(true);
        labels[i].DOKill();
        labels[i].color = newValue > oldValue ? gainColor : lossColor;
        labels[i].DOColor(_baseColor[i], flashTime).SetLink(labels[i].gameObject);
        icons[i].DOPunchScale(_punch[i], punchTime, 1, 0f).SetLink(icons[i].gameObject);
    }
}