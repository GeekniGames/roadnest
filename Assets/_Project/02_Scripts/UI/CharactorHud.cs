using System;
using DG.Tweening;
using Roadnest.Core;
using TMPro;
using UnityEngine;

/// <summary>
/// 캐릭터별 HP HUD: 숫자가 부드럽게 따라가고 하트가 튀며, HP 비율에 따라 숫자 색이 바뀐다. 죽은 캐릭터 칸은 숨긴다.
/// Per-character HP HUD: the number follows smoothly and the heart pops; the number color follows the HP ratio. Dead slots are hidden.
/// </summary>
public class CharactorHud : MonoBehaviour
{
    [Serializable]
    class Slot
    {
        public GameObject root;         // CharactorSlot_N
        public RectTransform hpIcon;    // HpIcon
        public TMP_Text hpValue;        // Text (TMP)_HpValue
    }

    [SerializeField] Slot[] slots = new Slot[4];     // 인덱스 = 캐릭터 인덱스 / index = character index

    [Header("연출 / Feedback")]
    [SerializeField] float moveTime = 0.4f;
    [SerializeField] float flashTime = 0.35f;
    [SerializeField] float punchScale = 0.3f;
    [SerializeField] float punchTime = 0.3f;
    [SerializeField] Color gainColor = new Color(0.6f, 1f, 0.6f);
    [SerializeField] Color lossColor = new Color(1f, 0.55f, 0.55f);

    [Header("HP 색 / HP colors (분배 패널과 같은 값 사용 / use the same values as the distribution panel)")]
    [SerializeField] Color safeColor = Color.white;
    [SerializeField] Color warnColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] Color dangerColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField, Range(0f, 1f)] float warnRatio = 0.6f;
    [SerializeField, Range(0f, 1f)] float dangerRatio = 0.3f;

    int[] _shown, _max;
    float[] _fill;
    Tween[] _move;
    TweenCallback<float>[] _onFill;
    TweenCallback[] _onHide;
    bool _ready;

    void Awake() => Init();

    void Init()
    {
        if (_ready) return;
        _ready = true;
        int n = slots.Length;
        _shown = new int[n];
        _max = new int[n];
        _fill = new float[n];
        _move = new Tween[n];
        _onFill = new TweenCallback<float>[n];
        _onHide = new TweenCallback[n];
        for (int i = 0; i < n; i++)
        {
            int idx = i;
            _onFill[i] = v => OnFill(idx, v);
            _onHide[i] = () => slots[idx].root.SetActive(false);
        }
    }

    // 런 시작 시 현재 HP로 즉시 맞춘다 / snap to current HP at run start
    public void Bind(CharacterState[] chars)
    {
        Init();
        for (int i = 0; i < slots.Length; i++)
        {
            if (_move[i] != null && _move[i].IsActive()) _move[i].Kill();
            bool alive = i < chars.Length && chars[i].IsAlive;
            slots[i].root.SetActive(alive);
            if (!alive) { _shown[i] = 0; continue; }

            _max[i] = chars[i].MaxHp < 1 ? 1 : chars[i].MaxHp;
            _shown[i] = chars[i].Hp;
            _fill[i] = chars[i].Hp;
            OnFill(i, _fill[i]);
            slots[i].hpValue.color = RestColor(i, _shown[i]);
        }
    }

    // 현재 HP를 목표로 연출 / animate toward current HP
    public void Show(CharacterState[] chars)
    {
        int n = chars.Length < slots.Length ? chars.Length : slots.Length;
        for (int i = 0; i < n; i++) Apply(i, chars[i].Hp);
    }

    // 선택 즉시 HP 비용만 반영 / apply only the HP cost right away on pick
    public void ShowCost(Effect[] cost)
    {
        for (int c = 0; c < cost.Length; c++)
        {
            if (cost[c].Stat != StatType.HpAll) continue;
            for (int i = 0; i < slots.Length; i++)
                if (_shown[i] > 0) Apply(i, _shown[i] + cost[c].Amount);
        }
    }

    void Apply(int i, int value)
    {
        value = Mathf.Clamp(value, 0, _max[i]);
        if (value == _shown[i]) return;

        bool gain = value > _shown[i];
        _shown[i] = value;
        Slot s = slots[i];

        if (_move[i] != null && _move[i].IsActive()) _move[i].Kill();
        _move[i] = DOVirtual.Float(_fill[i], value, moveTime, _onFill[i])
                            .SetEase(Ease.OutQuad)
                            .SetLink(s.root);
        if (value == 0) _move[i].OnComplete(_onHide[i]);

        s.hpValue.DOKill();
        s.hpValue.color = gain ? gainColor : lossColor;
        s.hpValue.DOColor(RestColor(i, value), flashTime).SetLink(s.root);

        s.hpIcon.DOKill(true);
        s.hpIcon.DOPunchScale(Vector3.one * punchScale, punchTime, 6, 0.5f).SetLink(s.root);
    }

    Color RestColor(int i, int hp)
    {
        float r = (float)hp / _max[i];
        if (r <= dangerRatio) return dangerColor;
        return r <= warnRatio ? warnColor : safeColor;
    }

    void OnFill(int i, float v)
    {
        _fill[i] = v;
        slots[i].hpValue.SetText("{0}", Mathf.RoundToInt(v));
    }
}