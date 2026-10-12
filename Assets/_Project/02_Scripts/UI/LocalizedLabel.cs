using TMPro;
using UnityEngine.Localization;
using UnityEngine.Scripting;

/// <summary>
/// TMP 텍스트 하나를 번역 키에 연결한다. 연결된 동안에만 구독한다.
/// Links one TMP text to a localization key; subscribed only while bound.
/// </summary>
public sealed class LocalizedLabel
{
    // 문구의 {amount} 자리를 채운다. 리플렉션으로 읽혀서 빌드 스트리핑에서 보존한다.
    // Fills {amount} in the text. Read via reflection, so keep it from build stripping.
    [Preserve]
    sealed class Args
    {
        [Preserve] public int amount { get; set; }
    }

    readonly TMP_Text _text;
    readonly LocalizedString _string = new LocalizedString();
    readonly Args _args = new Args();
    readonly object[] _argList;
    bool _bound;

    public LocalizedLabel(TMP_Text text)
    {
        _text = text;
        _argList = new object[] { _args };
    }

    public void Bind(LocalizedString source, int amount)
    {
        Release();
        if (source == null || source.IsEmpty)
        {
            _text.SetText(string.Empty);
            return;
        }

        _string.SetReference(source.TableReference, source.TableEntryReference);
        _args.amount = amount;
        _string.Arguments = _argList;
        _string.StringChanged += OnChanged;   // 언어가 바뀌면 다시 호출됨 / re-fires on language change
        _bound = true;
    }

    public void Release()
    {
        if (!_bound) return;
        _string.StringChanged -= OnChanged;
        _bound = false;
    }

    void OnChanged(string value) => _text.SetText(value);
}