using UnityEngine.Localization.Settings;

/// <summary>
/// 이벤트 문구 키를 현재 언어의 문구로 바꾼다. 이벤트 표시 때만 호출되므로 문자열 할당은 무시할 수준.
/// Resolves event text keys into the current language. Called only when an event is shown, so the string allocation is negligible.
/// </summary>
public static class EventText
{
    public const string Table = "Events";

    // 항목이 없으면 키를 그대로 돌려줘서 빠진 문구를 화면에서 바로 찾을 수 있다
    // Returns the key when the entry is missing so gaps are visible on screen
    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        string s = LocalizationSettings.StringDatabase.GetLocalizedString(Table, key);
        return string.IsNullOrEmpty(s) ? key : s;
    }
}