using System;
using DG.Tweening;
using Roadnest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 분배 패널의 캐릭터 한 칸: 이름, HP, 식량/물 토글 버튼.
/// One character slot of the distribution panel: name, HP, food/water toggle buttons.
/// </summary>
public class DistributionSlotView : MonoBehaviour
{
    [Serializable]
    public class RationToggle
    {
        public Button button;
        public GameObject on;       // 줌 / given
        public GameObject off;      // 안 줌 / not given
    }

    [Header("참조 / References")]
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text hpValue;
    [SerializeField] RationToggle food;
    [SerializeField] RationToggle water;

    [Header("흔들림 / Shake")]
    [SerializeField] float shakePixels = 10f;
    [SerializeField] float shakeTime = 0.25f;

    int _index;
    Action<int, ResourceType> _onToggle;
    RectTransform _foodRt, _waterRt;
    Vector2 _punch;

    public void Init(int index, Action<int, ResourceType> onToggle)
    {
        _index = index;
        _onToggle = onToggle;
        _foodRt = (RectTransform)food.button.transform;
        _waterRt = (RectTransform)water.button.transform;
        _punch = new Vector2(shakePixels, 0f);
        food.button.onClick.AddListener(OnFood);
        water.button.onClick.AddListener(OnWater);
    }

    // 열릴 때마다 모두 미분배로 시작 / every opening starts with nothing given
    public void Bind(CharacterState c, Color hpColor)
    {
        gameObject.SetActive(true);
        nameText.SetText(c.Id);
        hpValue.SetText("{0}", c.Hp);
        hpValue.color = hpColor;
        SetRation(ResourceType.Food, false);
        SetRation(ResourceType.Water, false);
    }

    public void SetRation(ResourceType type, bool given)
    {
        RationToggle t = type == ResourceType.Food ? food : water;
        t.on.SetActive(given);
        t.off.SetActive(!given);
    }

    // 줄 수 없을 때 버튼이 옆으로 흔들림 / the button shakes sideways when it cannot be given
    public void Shake(ResourceType type)
    {
        RectTransform rt = type == ResourceType.Food ? _foodRt : _waterRt;
        rt.DOKill(true);
        rt.DOPunchAnchorPos(_punch, shakeTime, 20, 1f).SetLink(rt.gameObject);
    }

    void OnFood() => _onToggle?.Invoke(_index, ResourceType.Food);
    void OnWater() => _onToggle?.Invoke(_index, ResourceType.Water);
}