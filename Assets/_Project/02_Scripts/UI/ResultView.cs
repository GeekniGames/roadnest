using System;
using Roadnest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 결과 화면: 성공/실패 배너, 결과 문구, 보상 줄(최대 3). 배치는 씬에서 하고 여기서는 켜고 끄고 채우기만 한다.
/// Result view: success/fail banner, outcome text, reward rows (max 3). Layout lives in the scene; this only toggles and fills.
/// </summary>
[Serializable]
public class ResultView
{
    [Serializable]
    public class RewardRow
    {
        public GameObject root;
        public Image icon;
        public TMP_Text label;
        public TMP_Text value;
    }

    [Header("참조 / References")]
    public GameObject root;         // ResultArea
    public Image banner;            // Result (초록 칸 / green plate)
    public TMP_Text bannerText;
    public TMP_Text desc;           // 결과 문구 / outcome text
    public RewardRow[] rows;
    public Button done;

    [Header("성공 / Success")]
    public string successLabel = "SUCCESS";   // 색은 씬에 만든 원래 색 그대로 / colors stay as authored in the scene

    [Header("실패 / Fail")]
    public string failLabel = "FAIL";
    public Sprite failSprite;                  // 비우면 배너는 색만 바꿈 / leave empty to tint the banner only
    public Color failTint = new Color(1f, 0.35f, 0.3f, 1f);        // 배너 / banner
    public Color failTextColor = new Color(1f, 0.35f, 0.3f, 1f);   // FAIL 글자 / FAIL text

    [Header("보상 값 색 / Reward value colors")]
    public Color gainColor = new Color(0.45f, 0.9f, 0.35f, 1f);
    public Color lossColor = new Color(1f, 0.35f, 0.3f, 1f);

    EventDisplay _display;
    Sprite _baseSprite;
    Color _baseColor, _baseTextColor;



    public void Init(EventDisplay display, UnityAction onDone)
    {
        _display = display;
        _baseSprite = banner.sprite;
        _baseColor = banner.color;
        _baseTextColor = bannerText.color;
        done.onClick.AddListener(onDone);

#if UNITY_EDITOR
        if (rows == null || rows.Length < EventLimits.MaxOutcomeEffects)
            Debug.LogError("보상 줄이 한도보다 적음 / reward rows fewer than the limit: " + EventLimits.MaxOutcomeEffects);
#endif
        root.SetActive(false);
    }

    public void Show(ChoiceResult r)
    {
        root.SetActive(true);

        bool fail = r.OutcomeIndex > 0;
        banner.sprite = fail && failSprite != null ? failSprite : _baseSprite;
        banner.color = fail ? failTint : _baseColor;
        bannerText.color = fail ? failTextColor : _baseTextColor;
        bannerText.SetText(fail ? failLabel : successLabel);

        OutcomeDef o = r.Outcome;
        desc.SetText(EventText.Get(o.TextKey));

        Effect[] fx = o.Effects;
        for (int i = 0; i < rows.Length; i++)
        {
            bool on = i < fx.Length;
            rows[i].root.SetActive(on);
            if (on) Fill(rows[i], fx[i]);
        }
    }

    public void Hide() => root.SetActive(false);

    void Fill(RewardRow row, Effect e)
    {
        int s = (int)e.Stat;
        Sprite ic = _display.Icon(s);
        if (row.icon != null)
        {
            row.icon.sprite = ic;
            row.icon.gameObject.SetActive(ic != null);
        }
        row.label.SetText(_display.statNames[s]);
        row.value.SetText(e.Amount > 0 ? "+{0}" : "{0}", e.Amount);
        row.value.color = e.Amount > 0 ? gainColor : lossColor;
    }
}