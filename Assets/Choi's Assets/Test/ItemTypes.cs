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
    // ★ 인벤토리 아이템
    Kit = 10,
    Shield = 11,
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

    /// <summary>
    /// 레이어 이름 -> ItemType 매핑 (디버그 로깅 추가)
    /// </summary>
    public static ItemType GetItemType(string layerName)
    {
        UnityEngine.Debug.Log($"[ItemLayers] GetItemType 호출: layerName='{layerName}'");

        ItemType result = ItemType.None;

        switch (layerName)
        {
            case Kit:
                result = ItemType.Kit;
                break;
            case Shield:
                result = ItemType.Shield;
                break;
            case Bomb:
                result = ItemType.Bomb;
                break;
            case Shuffle:
                result = ItemType.Shuffle;
                break;
            case Hint:
                result = ItemType.Hint;
                break;
            default:
                UnityEngine.Debug.LogWarning($"[ItemLayers] 알 수 없는 레이어: '{layerName}'");
                result = ItemType.None;
                break;
        }

        UnityEngine.Debug.Log($"[ItemLayers] GetItemType 결과: '{layerName}' → {result}");
        return result;
    }
}