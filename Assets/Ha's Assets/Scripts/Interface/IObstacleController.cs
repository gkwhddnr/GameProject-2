using UnityEngine;

/// <summary>
/// 장애물(Lock) 관리 인터페이스
/// KeyItemStrategy가 이를 통해 장애물 제거 요청
/// </summary>
public interface IObstacleController
{
    /// <summary>
    /// 키 수집 시 가장 가까운 장애물 제거
    /// </summary>
    void HandleKeyCollected(GameObject key, int keyStageIndex);

    /// <summary>
    /// 장애물 페이드아웃 및 제거
    /// </summary>
    void FadeOutObstacle(GameObject obstacle);
}