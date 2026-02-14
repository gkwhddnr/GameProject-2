using System;

/// <summary>
/// 아이템 타입 정의 (확장 가능)
/// </summary>
[Serializable]
public enum ItemType
{
    None = 0,
    Bomb = 1,
    Shuffle = 2,
    Hint = 3,
    // ★ 새로운 타입 추가
    Kit = 10,
    Shield = 11,
    // 앞으로 추가될 아이템들...
    // Potion = 20,
    // Key = 21,
}

/// <summary>
/// 아이템 레이어 이름 상수
/// </summary>
public static class ItemLayers
{
    public const string Kit = "Kit";
    public const string Shield = "Shield";
    public const string Bomb = "Bomb";
    public const string Shuffle = "Shuffle";
    public const string Hint = "Hint";

    // 레이어 이름 -> ItemType 매핑
    public static ItemType GetItemType(string layerName)
    {
        switch (layerName)
        {
            case Kit: return ItemType.Kit;
            case Shield: return ItemType.Shield;
            case Bomb: return ItemType.Bomb;
            case Shuffle: return ItemType.Shuffle;
            case Hint: return ItemType.Hint;
            default: return ItemType.None;
        }
    }
}