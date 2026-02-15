using UnityEngine;

/// <summary>
/// 스테이지 설정 데이터를 제공하는 인터페이스
/// GameManager, SequentialRevealManager가 이를 통해 데이터 접근
/// </summary>
public interface IStageDataProvider
{
    /// <summary>
    /// 지정된 스테이지의 초기 노출 아이템 개수
    /// </summary>
    int GetInitialVisibleCount(int stageIndex);

    /// <summary>
    /// 지정된 스테이지의 후속 노출 아이템 개수
    /// </summary>
    int GetSubsequentRevealCount(int stageIndex);

    /// <summary>
    /// 지정된 스테이지가 순차 노출을 사용하는지 여부
    /// </summary>
    bool GetRevealSequentially(int stageIndex);

    /// <summary>
    /// 총 스테이지 개수
    /// </summary>
    int GetStageCount();

    /// <summary>
    /// 지정된 스테이지의 경계 영역
    /// </summary>
    BoxCollider2D GetStageBounds(int stageIndex);

    /// <summary>
    /// 현재 활성화된 스테이지 인덱스
    /// </summary>
    int GetCurrentStageIndex();
}