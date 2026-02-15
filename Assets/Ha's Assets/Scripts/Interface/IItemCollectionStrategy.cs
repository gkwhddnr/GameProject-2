using UnityEngine;

/// <summary>
/// 아이템 수집 전략 인터페이스
/// 각 아이템 타입마다 구현 (별, 키, 인벤토리)
/// </summary>
public interface IItemCollectionStrategy
{
    /// <summary>
    /// 이 Strategy가 해당 아이템을 처리할 수 있는지 확인
    /// </summary>
    bool CanCollect(GameObject item);

    /// <summary>
    /// 아이템 수집 처리
    /// </summary>
    /// <param name="item">수집할 아이템</param>
    /// <param name="context">수집 컨텍스트</param>
    void Collect(GameObject item, IItemCollectionContext context);
}