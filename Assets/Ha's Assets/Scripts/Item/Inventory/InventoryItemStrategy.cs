using UnityEngine;

/// <summary>
/// 인벤토리 아이템 수집 전략 (Kit, Shield, Bomb, Shuffle, Hint)
/// 수집 시 아이템 오브젝트의 Sprite를 자동으로 InventoryManager에 등록
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

        // 2. ★ 아이템의 Sprite를 InventoryManager에 등록
        SpriteRenderer spriteRenderer = item.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.RegisterItemSprite(itemType, spriteRenderer.sprite);
                Debug.Log($"[InventoryItemStrategy] {itemType} Sprite 등록: {spriteRenderer.sprite.name}");
            }
        }
        else
        {
            Debug.LogWarning($"[InventoryItemStrategy] {item.name}에 SpriteRenderer 또는 Sprite가 없습니다!");
        }

        // 3. InventoryManager에 아이템 추가
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(itemType, 1);
        }

        // 4. 사운드 재생
        context.PlaySound("collect");

        // 5. 플로팅 텍스트 표시
        context.ShowFloatingText(item);

        // 6. GameManager 알림
        context.NotifyGameManager(item);

        // 7. 파티클 시스템 중지 (이펙트 제거)
        ParticleSystem[] particles = item.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particles)
        {
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(true);
                ps.gameObject.SetActive(false);
            }
        }

        // 8. SpriteRotator 처리 (있는 경우)
        SpriteRotator rotator = item.GetComponent<SpriteRotator>();
        if (rotator != null)
        {
            rotator.TriggerDisappear();
        }
        else
        {
            // SpriteRotator가 없으면 기본 페이드아웃
            context.FadeOutItem(item);
        }
    }
}