using Roadnest.Core;
using UnityEngine;

/// <summary>
/// 아이템 아이콘 30개를 한곳에 모은 에셋. 결과 화면과 인벤토리 HUD가 함께 쓴다.
/// One asset holding the 30 item icons, shared by the result view and the inventory HUD.
/// </summary>
[CreateAssetMenu(menuName = "ROADNEST/Item Icons", fileName = "ItemIcons")]
public class ItemIcons : ScriptableObject
{
    // ItemKind 순서 (ITEMS.md 1~30번) / ItemKind order (ITEMS.md #1-30)
    public Sprite[] icons = new Sprite[Inventory.KindCount];

    public Sprite Get(int kind) => (uint)kind < (uint)icons.Length ? icons[kind] : null;
}