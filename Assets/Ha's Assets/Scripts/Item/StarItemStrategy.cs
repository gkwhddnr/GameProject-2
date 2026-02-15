using UnityEngine;

/// <summary>
/// 별 아이템 수집 전략
/// 기존 ItemCollector의 별 수집 로직을 그대로 구현
/// </summary>
public class StarItemStrategy : IItemCollectionStrategy
{
    private int itemLayerIndex = -1;

    public StarItemStrategy()
    {
        itemLayerIndex = LayerMask.NameToLayer("Item");
    }

    public bool CanCollect(GameObject item)
    {
        if (item == null) return false;
        return item.layer == itemLayerIndex;
    }

    public void Collect(GameObject item, IItemCollectionContext context)
    {
        // 1. 수집 카운트 증가
        context.IncrementCollectedCount();

        // 2. 플로팅 텍스트 표시
        context.ShowFloatingText(item);

        // 3. UI 업데이트
        context.UpdateUI();

        // 4. SequentialRevealManager 알림
        context.NotifySequentialReveal(item);

        // 5. GameManager 알림
        context.NotifyGameManager(item);

        // 6. 사운드 재생
        context.PlaySound("collect");

        // 7. SpriteRotator 처리 (기존 로직)
        SpriteRotator rotator = item.GetComponent<SpriteRotator>();
        if (rotator != null)
        {
            rotator.TriggerDisappear();
            // HandleStageComplete는 context에서 처리하도록 위임하지 않고
            // 여기서는 단순히 TriggerDisappear만 호출
        }

        // 8. 아이템 페이드아웃
        context.FadeOutItem(item);

        // 9. 다음 배치 노출 체크 (revealItemsSequentially가 true일 때)
        context.RevealNextHiddenBatch();

        // 10. 스테이지 완료 체크 및 NextPoint 노출
        context.CheckStageCompletion();
    }
}