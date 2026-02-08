using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlayerBoundsLimiter : MonoBehaviour
{
    [Header("Bounds Reference")]
    public BoxCollider2D boundsCollider;
    public MapCamera mapCamera;
    private bool autoSyncFromMapCamera = true;

    [Header("Visibility Settings")]
    [Tooltip("캐릭터가 화면 끝에서 잘리지 않도록 추가적으로 주는 여백")]
    private float extraPadding = 0.25f;

    Collider2D playerCollider;

    void Awake()
    {
        playerCollider = GetComponent<Collider2D>();
        if (autoSyncFromMapCamera && mapCamera == null) mapCamera = FindFirstObjectByType<MapCamera>();
    }

    void LateUpdate()
    {
        if (autoSyncFromMapCamera) SyncBoundsFromCamera();
        if (boundsCollider == null) return;

        ClampPosition();
    }

    void SyncBoundsFromCamera()
    {
        if (mapCamera != null && mapCamera.CurrentBounds != null) boundsCollider = mapCamera.CurrentBounds;
    }

    void ClampPosition()
    {
        Bounds bounds = boundsCollider.bounds;
        // 캐릭터 콜라이더의 크기 반영
        Vector3 playerExtents = playerCollider.bounds.extents;

        Vector3 pos = transform.position;

        // 핵심: 맵 경계에서 캐릭터의 절반 크기 + 추가 여백만큼 안쪽으로 제한
        float minX = bounds.min.x + playerExtents.x + extraPadding;
        float maxX = bounds.max.x - playerExtents.x - extraPadding;
        float minY = bounds.min.y + playerExtents.y + extraPadding;
        float maxY = bounds.max.y - playerExtents.y - extraPadding;

        // 맵이 캐릭터보다 작을 경우를 대비한 방어 코드
        if (minX <= maxX) pos.x = Mathf.Clamp(pos.x, minX, maxX);
        else pos.x = bounds.center.x;

        if (minY <= maxY) pos.y = Mathf.Clamp(pos.y, minY, maxY);
        else pos.y = bounds.center.y;

        transform.position = pos;
    }
}