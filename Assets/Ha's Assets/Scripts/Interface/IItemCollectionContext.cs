using UnityEngine;

/// <summary>
/// 아이템 수집 시 필요한 컨텍스트 제공
/// Strategy들이 이를 통해 필요한 작업 수행
/// </summary>
public interface IItemCollectionContext
{
    /// <summary>
    /// 수집된 아이템 카운트 증가
    /// </summary>
    void IncrementCollectedCount();

    /// <summary>
    /// UI 업데이트
    /// </summary>
    void UpdateUI();

    /// <summary>
    /// 사운드 재생
    /// </summary>
    /// <param name="soundType">"collect", "key" 등</param>
    void PlaySound(string soundType);

    /// <summary>
    /// 아이템 페이드아웃
    /// </summary>
    void FadeOutItem(GameObject item);

    /// <summary>
    /// 현재 스테이지 인덱스
    /// </summary>
    int GetCurrentStageIndex();

    /// <summary>
    /// GameManager에 아이템 수집 알림
    /// </summary>
    void NotifyGameManager(GameObject item);

    /// <summary>
    /// SequentialRevealManager에 아이템 수집 알림
    /// </summary>
    void NotifySequentialReveal(GameObject item);

    /// <summary>
    /// 플로팅 텍스트 표시
    /// </summary>
    void ShowFloatingText(GameObject item);

    /// <summary>
    /// 아이템 캐시 가져오기
    /// </summary>
    object GetOrAddCache(GameObject item);

    /// <summary>
    /// 다음 숨겨진 배치 노출 (별 아이템용)
    /// </summary>
    void RevealNextHiddenBatch();

    /// <summary>
    /// 스테이지 완료 체크 및 NextPoint 노출
    /// </summary>
    void CheckStageCompletion();
}