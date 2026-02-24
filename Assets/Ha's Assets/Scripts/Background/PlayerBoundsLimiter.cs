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
    private float viewportPadding = 0.25f;

    private bool limitToCameraViewport = true;
    private Camera mainCamera;

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

    public void ForceClampNow()
    {
        if (autoSyncFromMapCamera) SyncBoundsFromCamera();
        if (boundsCollider == null) return;

        // 즉시 클램프 적용
        ClampPosition();

        if (limitToCameraViewport)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null) ClampToViewport();
        }
    }

    void SyncBoundsFromCamera()
    {
        if (mapCamera != null && mapCamera.CurrentBounds != null) boundsCollider = mapCamera.CurrentBounds;
    }

    void ClampToViewport()
    {
        if (mainCamera == null || !mainCamera.orthographic || playerCollider == null || boundsCollider == null) return;

        Vector3 pos = transform.position;
        Bounds playerBounds = playerCollider.bounds;

        float camHeight = mainCamera.orthographicSize;
        float camWidth = camHeight * mainCamera.aspect;
        Vector3 camPos = mainCamera.transform.position;

        // 카메라 뷰포트 내부로 들어가도록 마진 적용
        float minX = camPos.x - camWidth + playerBounds.extents.x + viewportPadding;
        float maxX = camPos.x + camWidth - playerBounds.extents.x - viewportPadding;
        float minY = camPos.y - camHeight + playerBounds.extents.y + viewportPadding;
        float maxY = camPos.y + camHeight - playerBounds.extents.y - viewportPadding;

        // Bounds와도 비교하여 더 제한적인 범위를 적용
        Bounds b = boundsCollider.bounds;
        float boundsMinX = b.min.x + playerBounds.extents.x + extraPadding;
        float boundsMaxX = b.max.x - playerBounds.extents.x - extraPadding;
        float boundsMinY = b.min.y + playerBounds.extents.y + extraPadding;
        float boundsMaxY = b.max.y - playerBounds.extents.y - extraPadding;

        minX = Mathf.Max(minX, boundsMinX);
        maxX = Mathf.Min(maxX, boundsMaxX);
        minY = Mathf.Max(minY, boundsMinY);
        maxY = Mathf.Min(maxY, boundsMaxY);

        // 방어성 처리
        if (minX <= maxX) pos.x = Mathf.Clamp(pos.x, minX, maxX);
        else pos.x = (minX + maxX) * 0.5f;

        if (minY <= maxY) pos.y = Mathf.Clamp(pos.y, minY, maxY);
        else pos.y = (minY + maxY) * 0.5f;

        transform.position = pos;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        extraPadding = Mathf.Max(0f, extraPadding);
        viewportPadding = Mathf.Max(0f, viewportPadding);
    }
#endif

    void ClampPosition()
    {
        if (boundsCollider == null || playerCollider == null) return;

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