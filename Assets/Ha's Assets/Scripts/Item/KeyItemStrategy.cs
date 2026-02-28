using UnityEngine;

/// <summary>
/// 키 아이템 수집 전략
/// 기존 ItemCollector의 키 수집 로직을 그대로 구현
/// </summary>
public class KeyItemStrategy : IItemCollectionStrategy
{
    private int keyLayerIndex = -1;
    private IObstacleController obstacleController;

    public KeyItemStrategy(IObstacleController obstacleController)
    {
        keyLayerIndex = LayerMask.NameToLayer("Key");
        this.obstacleController = obstacleController;
    }

    public bool CanCollect(GameObject item)
    {
        if (item == null) return false;

        // KeyActivator 컴포넌트가 있는 경우
        KeyActivator keyActivator = item.GetComponent<KeyActivator>();
        if (keyActivator != null) return true;

        // GameManager의 키 슬롯 매칭
        bool isKeyByGameManager = false;
        int matchedKeySlotIndex = -1;
        if (GameManager.Instance != null)
        {
            isKeyByGameManager = GameManager.Instance.IsKeySlotMatch(item, out matchedKeySlotIndex);
        }

        // 키 레이어인 경우
        bool isKeyByLayer = (item.layer == keyLayerIndex);

        // 이름이 "Key"인 경우
        bool isKeyByName = string.Equals(item.name, "Key", System.StringComparison.OrdinalIgnoreCase);

        return isKeyByGameManager || isKeyByLayer || isKeyByName;
    }

    public void Collect(GameObject item, IItemCollectionContext context)
    {
        // KeyActivator가 있는 경우 처리
        KeyActivator keyActivator = item.GetComponent<KeyActivator>();
        if (keyActivator != null)
        {
            context.PlaySound("key");
            keyActivator.Activate(context.GetCurrentStageIndex());
            context.NotifySequentialReveal(item);
            context.FadeOutItem(item);
            return;
        }

        // 일반 키 처리
        context.PlaySound("key");

        // GameManager 키 슬롯 처리
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.IsKeySlotMatch(item, out int matchedKeySlotIndex))
            {
                GameManager.Instance.ConsumeKeySlot(matchedKeySlotIndex);
            }
        }

        context.ShowFloatingText(item);

        // 가장 가까운 장애물 찾아서 제거
        Vector3 keyPos = item.transform.position;
        int keyStageIndex = context.GetCurrentStageIndex();

        if (obstacleController != null)
        {
            obstacleController.HandleKeyCollected(item, keyStageIndex);
        }

        context.NotifySequentialReveal(item);

        var rotator = item.GetComponent<SpriteRotator>();
        if (rotator != null)
        {
            rotator.TriggerDisappear();
        }

        context.FadeOutItem(item);
    }
}