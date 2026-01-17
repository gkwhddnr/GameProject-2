using UnityEngine;

/// <summary>
/// 플레이어가 지정된 Bounds(BoxCollider2D) 영역 안에서만
/// 이동할 수 있도록 위치를 Clamp 하는 스크립트
///
/// - GridMovementSystem / InputAction 수정 불필요
/// - Teleport / 카메라 전환 후에도 정상 동작
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlayerBoundsLimiter : MonoBehaviour
{
    [Header("Bounds Reference")]
    [Tooltip("플레이어 이동을 제한할 영역 (배경의 BoxCollider2D)")]
    public BoxCollider2D boundsCollider;

    [Tooltip("MapCamera가 사용하는 Bounds를 자동으로 따라갈지 여부")]
    public bool autoSyncFromMapCamera = true;

    [Tooltip("MapCamera 참조 (autoSyncFromMapCamera 사용 시 필요)")]
    public MapCamera mapCamera;

    Collider2D playerCollider;

    void Awake()
    {
        playerCollider = GetComponent<Collider2D>();

        if (autoSyncFromMapCamera && mapCamera == null)
            mapCamera = FindFirstObjectByType<MapCamera>();
    }

    void LateUpdate()
    {
        if (autoSyncFromMapCamera)
        {
            SyncBoundsFromCamera();
        }

        if (boundsCollider == null) return;

        ClampPosition();
    }

    void SyncBoundsFromCamera()
    {
        if (mapCamera != null && mapCamera.CurrentBounds != null)
        {
            boundsCollider = mapCamera.CurrentBounds;
        }
    }

    void ClampPosition()
    {
        Bounds bounds = boundsCollider.bounds;
        Bounds playerBounds = playerCollider.bounds;

        Vector3 pos = transform.position;

        float minX = bounds.min.x + playerBounds.extents.x;
        float maxX = bounds.max.x - playerBounds.extents.x;
        float minY = bounds.min.y + playerBounds.extents.y;
        float maxY = bounds.max.y - playerBounds.extents.y;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        transform.position = pos;
    }
}
