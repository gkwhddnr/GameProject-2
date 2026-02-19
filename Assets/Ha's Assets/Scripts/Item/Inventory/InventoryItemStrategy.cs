using UnityEngine;

/// <summary>
/// 인벤토리 아이템 수집 전략 (Kit, Shield, Bomb, Shuffle, Hint)
/// ItemCollector의 인벤토리 수집 로직 구현
/// </summary>
public class InventoryItemStrategy : IItemCollectionStrategy
{
    public bool CanCollect(GameObject item)
    {
        if (item == null) return false;

        // 레이어 이름으로 ItemType 확인
        string layerName = LayerMask.LayerToName(item.layer);
        ItemType itemType = ItemLayers.GetItemType(layerName);

        // Kit, Shield 등 인벤토리 아이템이면 true
        return itemType != ItemType.None &&
               (itemType == ItemType.Kit ||
                itemType == ItemType.Shield ||
                itemType == ItemType.Bomb ||
                itemType == ItemType.Shuffle ||
                itemType == ItemType.Hint);
    }

    public void Collect(GameObject item, IItemCollectionContext context)
    {
        // 1. ItemType 확인
        string layerName = LayerMask.LayerToName(item.layer);
        ItemType itemType = ItemLayers.GetItemType(layerName);

        Debug.Log($"[InventoryItemStrategy] {itemType} 아이템 수집: {item.name}");

        // 2. InventoryManager에 추가
        if (InventoryManager.Instance != null) InventoryManager.Instance.AddItem(itemType, 1);

        // 3. 사운드 재생
        context.PlaySound("collect");

        // 4. 플로팅 텍스트 표시
        context.ShowFloatingText(item);

        // 5. GameManager 알림
        context.NotifyGameManager(item);

        // 6. 파티클 시스템 중지 (이펙트 제거)
        ParticleSystem[] particles = item.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particles)
        {
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(true);               
                ps.gameObject.SetActive(false);
            }

        }

        // 6. SpriteRotator 처리 (있는 경우)
        SpriteRotator rotator = item.GetComponent<SpriteRotator>();
        if (rotator != null) rotator.TriggerDisappear();
        else
        {
            // SpriteRotator가 없으면 기본 페이드아웃
            context.FadeOutItem(item);
        }
    }
}