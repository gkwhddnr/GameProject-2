using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 효과 생성 팩토리 (Factory Pattern)
/// 새 아이템 추가 시 여기만 수정하면 됨
/// </summary>
public static class ItemEffectFactory
{
    private static Dictionary<ItemType, IItemEffect> effectCache = new Dictionary<ItemType, IItemEffect>();

    /// <summary>
    /// 아이템 타입에 맞는 효과 객체 반환
    /// </summary>
    public static IItemEffect GetEffect(ItemType type)
    {
        // 캐시에 있으면 재사용
        if (effectCache.TryGetValue(type, out var cachedEffect)) return cachedEffect;

        // 새로 생성
        IItemEffect effect = CreateEffect(type);

        if (effect != null) effectCache[type] = effect;
        return effect;
    }

    /// <summary>
    /// 실제 효과 객체 생성
    /// ★ 새 아이템 추가 시 여기에 case 추가
    /// </summary>
    private static IItemEffect CreateEffect(ItemType type)
    {
        switch (type)
        {
            case ItemType.Kit:
                return new KitItemEffect();

            case ItemType.Shield:
                return new ShieldItemEffect();

            case ItemType.Bomb:
                return new BombItemEffect();

            case ItemType.Shuffle:
                return new ShuffleItemEffect();

            case ItemType.Hint:
                return new HintItemEffect();

            // ★ 새 아이템 추가 예시:
            // case ItemType.Potion:
            //     return new PotionItemEffect();

            default:
                Debug.LogWarning($"[ItemEffectFactory] 알 수 없는 아이템 타입: {type}");
                return null;
        }
    }

    /// <summary>
    /// 캐시 초기화 (씬 전환 시 필요하면 호출)
    /// </summary>
    public static void ClearCache(){ effectCache.Clear(); }
}