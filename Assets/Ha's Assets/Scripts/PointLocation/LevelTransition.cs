using UnityEngine;

public class LevelTransition : MonoBehaviour
{
    [Header("References")]
    public MapCamera mapCamera;

    [Tooltip("다음 배경(영역)으로 사용할 BoxCollider2D")]
    public BoxCollider2D nextBounds;
    public Transform nextPoint;
    public GameObject playerObject;
    public string playerTag = "Player";

    [Header("MapCamera.SetBounds options")]
    public bool snapCameraToBounds = true;
    public bool fitViewToBounds = false;

    [Header("Teleport options")]
    [Tooltip("텔레포트 시 플레이어 Rigidbody2D의 속도를 0으로 초기화")]
    public bool resetPlayerVelocity = true;

    [Header("Background manager")]
    [Tooltip("BackgroundManager를 할당하면 전환 시 배경들도 newBounds로 갱신됩니다.")]
    public BackgroundManager backgroundManager;

    /// <summary>
    /// DestinationPoint.onReached 에 연결
    /// </summary>
    public void TeleportAndSwitch()
    {
        GameObject player = ResolvePlayer();
        if (player == null) return;

        // 1) 플레이어 멈춤 등 (기존 로직)
        var moveSystem = player.GetComponent<GridMovementSystem>();
        if (moveSystem != null) moveSystem.enabled = false;

        // 2) 페이드가 있는 경우 nextPoint의 fade 먼저 실행 (Optional)
        float fadeDuration = 0f;
        if (nextPoint != null)
        {
            var fade = nextPoint.GetComponentInChildren<PointSpriteFadeController>();
            if (fade != null)
            {
                fade.FadeOut();
                fadeDuration = fade.fadeOutDuration;
            }
        }

        // 3) 텔레포트
        if (nextPoint != null)
        {
            player.transform.position = nextPoint.position;
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        if (moveSystem != null) moveSystem.enabled = true;

        // 4) 카메라 바꾸기
        if (mapCamera == null) mapCamera = FindFirstObjectByType<MapCamera>();
        if (mapCamera != null && nextBounds != null)
        {
            mapCamera.SetBounds(nextBounds, snapCameraToBounds, fitViewToBounds);
            mapCamera.playerTarget = player.transform;
        }

        // 5) 배경 매니저: 이전 스테이지 비활성화를 fadeDuration 만큼 지연
        if (backgroundManager == null) backgroundManager = FindFirstObjectByType<BackgroundManager>();
        if (backgroundManager != null)
        {
            backgroundManager.AdvanceToNextStage(previousDeactivateDelay: fadeDuration);
        }
    }

    // playerObject 또는 tag로 플레이어 찾기
    GameObject ResolvePlayer()
    {
        if (playerObject != null) return playerObject;

        if (!string.IsNullOrEmpty(playerTag))
        {
            try
            {
                var found = GameObject.FindWithTag(playerTag);
                if (found != null) return found;
            }
            catch { /* 태그 미존재 등 예외 무시 */ }
        }

        return null;
    }
}
