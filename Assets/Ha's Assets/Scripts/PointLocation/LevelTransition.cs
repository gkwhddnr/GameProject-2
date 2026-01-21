using UnityEngine;

public class LevelTransition : MonoBehaviour
{
    [Header("References")]
    public MapCamera mapCamera;

    [Tooltip("다음 배경(영역)으로 사용할 BoxCollider2D")]
    public BoxCollider2D nextBounds;
    public Transform nextPoint;
    public GameObject playerObject;

    [Header("MapCamera.SetBounds options")]
    public bool snapCameraToBounds = true;
    public bool fitViewToBounds = false;

    [Header("Background manager")]
    [Tooltip("BackgroundManager를 할당하면 전환 시 배경들도 newBounds로 갱신됩니다.")]
    public BackgroundManager backgroundManager;

    private bool resetPlayerVelocity = true;

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

        // 2) 페이드가 있는 경우 nextPoint의 fade 먼저 실행
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

        // 텔레포트
        if (nextPoint != null)
        {
            player.transform.position = nextPoint.position;
            if (resetPlayerVelocity)
            {
                var rb = player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
            }
        }

        // 카메라 전환
        if (mapCamera == null) mapCamera = FindFirstObjectByType<MapCamera>();
        if (mapCamera != null && nextBounds != null)
        {
            mapCamera.SetBounds(nextBounds, snapCameraToBounds, fitViewToBounds);
            try { mapCamera.playerTarget = player.transform; } catch { }
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
        try
        {
            var found = GameObject.FindWithTag("Player");
            if (found != null) return found;
        }
        catch { }

        return null;
    }
}
