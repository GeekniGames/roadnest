using DG.Tweening;
using Roadnest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카라반 위 인벤토리 칸 표시. 화면용 사본(_view)을 그리고, 바뀐 칸만 갱신한다.
/// Inventory cells above the caravan. Draws a display copy (_view) and updates only changed cells.
/// </summary>
public class ResourceHud : MonoBehaviour
{
    [System.Serializable]
    public class Cell
    {
        public Image icon;
        public TMP_Text count;
    }

    [Header("칸 / Cells (Item_0 ~)")]
    [SerializeField] Cell[] cells;

    [SerializeField] ItemIcons itemIcons;

    [Header("연출 / Feedback")]
    [SerializeField] float punchScale = 0.25f;
    [SerializeField] float punchTime = 0.2f;
    [SerializeField] float flashTime = 0.35f;
    [SerializeField] Color gainColor = new Color(0.6f, 1f, 0.6f);
    [SerializeField] Color lossColor = new Color(1f, 0.55f, 0.55f);
    [SerializeField] Color fullColor = new Color(1f, 0.3f, 0.25f);     // 한 칸이 가득 찼을 때 / cell at its stack cap

    Inventory _inv;
    Inventory _view;
    Color[] _baseColor;
    Vector3[] _punch;
    int[] _shownKind;       // -1 = 빈 칸 / empty
    int[] _shownCount;
    bool[] _shownFull;
    bool _ready;
    bool _held;

    void Awake() => Init();

    // Awake 순서와 무관하게 안전하게 초기화 / safe regardless of Awake order
    void Init()
    {
        if (_ready) return;
        _ready = true;
        int n = cells.Length;
        _baseColor = new Color[n];
        _punch = new Vector3[n];
        _shownKind = new int[n];
        _shownCount = new int[n];
        _shownFull = new bool[n];
        for (int i = 0; i < n; i++)
        {
            _baseColor[i] = cells[i].count.color;
            _punch[i] = cells[i].icon.transform.localScale * punchScale;
        }
    }

    public void Bind(Inventory inv)
    {
        Init();
        Unbind();
        _inv = inv;
        _view = new Inventory(inv.SlotCount, inv.StackCap);
        _view.CopyFrom(inv);
        _held = false;

        for (int i = 0; i < cells.Length; i++)
        {
            _shownKind[i] = -1;
            _shownCount[i] = 0;
            _shownFull[i] = false;
            cells[i].icon.gameObject.SetActive(false);
            cells[i].count.gameObject.SetActive(false);
        }
        Render(false);
        inv.Changed += OnChanged;
    }

    void Unbind()
    {
        if (_inv != null) _inv.Changed -= OnChanged;
        _inv = null;
    }

    void OnDestroy() => Unbind();

    // 결과 화면 동안 표시를 멈췄다가 Release에서 한꺼번에 반영
    // Freeze the display during the result view, apply everything on Release
    public void Hold() => _held = true;

    // 선택 즉시 비용만 먼저 반영 / apply only the cost right away on pick
    public void ShowCost(Effect[] cost)
    {
        if (_view == null) return;
        for (int i = 0; i < cost.Length; i++)
        {
            if (!StatMap.IsStored(cost[i].Stat) || cost[i].Amount >= 0) continue;   // 인벤토리에 없는 비용은 칸이 없음 / costs outside the inventory have no cell
            _view.Remove(StatMap.ToKind(cost[i].Stat), -cost[i].Amount);
        }
        Render(true);
    }

    public void Release()
    {
        _held = false;
        if (_inv == null) return;
        _view.CopyFrom(_inv);
        Render(true);
    }

    void OnChanged()
    {
        if (_held) return;
        _view.CopyFrom(_inv);
        Render(true);
    }

    // 표시 중인 값과 비교해 바뀐 칸만 갱신 / update only cells that differ from what is shown
    void Render(bool animate)
    {
        int used = _view.UsedSlots, cap = _view.StackCap;
        for (int i = 0; i < cells.Length; i++)
        {
            int kind = -1, cnt = 0;
            if (i < used) { kind = (int)_view.KindAt(i); cnt = _view.CountAt(i); }
            bool full = kind >= 0 && cnt >= cap;

            int oldKind = _shownKind[i], oldCnt = _shownCount[i];
            if (kind == oldKind && cnt == oldCnt && full == _shownFull[i]) continue;
            _shownKind[i] = kind;
            _shownCount[i] = cnt;
            _shownFull[i] = full;

            Cell c = cells[i];
            c.count.DOKill();
            c.icon.transform.DOKill(true);

            if (kind < 0)
            {
                c.icon.gameObject.SetActive(false);
                c.count.gameObject.SetActive(false);
                continue;
            }

            if (oldKind < 0)
            {
                c.icon.gameObject.SetActive(true);
                c.count.gameObject.SetActive(true);
            }
            if (kind != oldKind)
            {
                Sprite sp = itemIcons != null ? itemIcons.Get(kind) : null;
                c.icon.sprite = sp;
                c.icon.enabled = sp != null;
            }
            c.count.SetText("{0}", cnt);

            Color target = full ? fullColor : _baseColor[i];
            if (animate && kind == oldKind && cnt != oldCnt)
            {
                c.count.color = cnt > oldCnt ? gainColor : lossColor;
                c.count.DOColor(target, flashTime).SetLink(c.count.gameObject);
                Punch(i);
            }
            else
            {
                c.count.color = target;
                if (animate && oldKind < 0) Punch(i);       // 새로 채워진 칸 / newly filled cell
            }
        }
    }

    void Punch(int i)
    {
        Image icon = cells[i].icon;
        icon.transform.DOPunchScale(_punch[i], punchTime, 1, 0f).SetLink(icon.gameObject);
    }
}