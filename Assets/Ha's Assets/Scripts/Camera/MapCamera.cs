using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 2D 맵용 카메라 컨트롤러
/// - SetBound() : 외부에서 bound 설정
/// - boundsCollider: 카메라 이동 제한(카메라가 영역 밖으로 나가지 않도록)
/// - FitCameraToBounds() : boundsCollider 전체가 화면에 들어오도록 orthographicSize를 정확히 맞춤
/// - useFixedViewSize == false 일 때 카메라가 Player 레이어 오브젝트를 따라다님 (deadzone 기반)
/// - autoScaleFollowView: follow 모드일 때 bounds 크기의 일부로 카메라 뷰를 자동 설정
/// - Zoom : 카메라 확대/축소
/// </summary>
[RequireComponent(typeof(Camera))]
public class MapCamera : MonoBehaviour
{
    [Header("카메라 움직임")]
    public float panSmooth = 0.08f;

    [Header("카메라 영역 범위 설정")]
    public BoxCollider2D boundsCollider; // 카메라 이동 클램프용

    [Header("전체 배경에 맞게 자동 카메라 영역 설정")]
    [Tooltip("true면 boundsCollider 영역 전체가 보이도록 카메라를 자동으로 맞춥니다. (이 경우 카메라는 정지)")]
    public bool autoFitToBounds = false;

    [Header("Zoom")]
    public bool allowZoom = false;        // 마우스 휠 줌 허용 (useFixedViewSize가 켜져있으면 무시됨)
    public float zoomSpeed = 5f;
    public float minOrthoSize = 3f;
    public float maxOrthoSize = 20f;

    [Header("카메라 영역 설정")]
    public bool useFixedViewSize = false;
    public float viewWidth = 20f;
    public float viewHeight = 10f;

    [Header("Fit behavior")]
    [Tooltip("Bounds에 맞춰 카메라를 맞출 때 maxOrthoSize를 무시하고 강제로 맞출지 여부")]
    public bool forceFitIgnoreMaxOrtho = false;

    [Header("플레이어 캐릭터 카메라")]
    public Transform playerTarget;

    [Tooltip("플레이어가 이 값(월드 단위) 이상 카메라 중심에서 벗어나면 카메라가 따라감")]
    public float followDeadzone = 1.5f;
    public string playerLayerName = "Player";

    [Header("플레이어를 추적하는 카메라 영역 설정")]
    [Tooltip("플레이어 추적 모드일 때 boundsCollider의 일부 크기로 카메라 뷰를 자동 계산할지 여부")]
    public bool autoScaleFollowView = true;
    [Tooltip("bounds 대비 카메라가 차지할 비율(예: 0.25 = 1/4, 0.125 = 1/8). 0.01 ~ 1.0")]
    [Range(0.01f, 1f)]
    public float followViewFraction = 0.25f;
    [Tooltip("카메라 크기 변화의 부드러움(0 = 즉시, 0.1~0.3 추천)")]
    [Range(0f, 1f)]
    public float followZoomSmooth = 0.15f;

    public BoxCollider2D CurrentBounds { get; private set; }

    // 내부 상태
    Vector3 targetPos;
    Vector3 velocity = Vector3.zero;
    Camera cam;

    // 화면 리사이즈 감지용
    int lastScreenW = 0;
    int lastScreenH = 0;

    void Start()
    {
        cam = GetComponent<Camera>();
        targetPos = transform.position;

        // 안전값
        viewWidth = Mathf.Max(0.01f, viewWidth);
        viewHeight = Mathf.Max(0.01f, viewHeight);
        minOrthoSize = Mathf.Max(0.0001f, minOrthoSize);
        maxOrthoSize = Mathf.Max(minOrthoSize, maxOrthoSize);

        // 플레이어 자동 검색 시도(인스펙터에 없으면)
        TryAutoFindPlayerByLayer();

        // 시작 시 동작:
        if (autoFitToBounds && boundsCollider != null)
        {
            FitCameraToBounds(ignoreMaxOrtho: forceFitIgnoreMaxOrtho);
            ClampTargetToBounds();
            transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z);
        }
        else
        {
            if (useFixedViewSize)
            {
                ApplyFixedViewSize(ignoreMaxOrtho: false);
            }
           
            if (!useFixedViewSize && playerTarget != null)
            {
                targetPos.x = playerTarget.position.x;
                targetPos.y = playerTarget.position.y;
                if (boundsCollider != null) ClampTargetToBounds();
                transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z);
            }
            else if (boundsCollider != null)
            {
                ClampTargetToBounds();
                transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z);
            }
        }

        lastScreenW = Screen.width;
        lastScreenH = Screen.height;
    }

    void Update()
    {
        // 플레이어 자동 검색(런타임에 플레이어가 생성될 수 있으므로)
        if (playerTarget == null)
            TryAutoFindPlayerByLayer();

        // 화면 크기(해상도) 변경 시 재적용
        if (Screen.width != lastScreenW || Screen.height != lastScreenH)
        {
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;

            if (autoFitToBounds && boundsCollider != null)
            {
                FitCameraToBounds(ignoreMaxOrtho: forceFitIgnoreMaxOrtho);
                ClampTargetToBounds();
            }
            else if (useFixedViewSize)
            {
                ApplyFixedViewSize();
                if (boundsCollider != null) ClampTargetToBounds();
            }
        }

        // 줌
        if (!autoFitToBounds && !useFixedViewSize && allowZoom && cam != null && cam.orthographic)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - scroll * zoomSpeed, minOrthoSize, maxOrthoSize);
            }
        }

        // 동작 분기
        if (autoFitToBounds)
        {
            if (boundsCollider != null && cam != null && cam.orthographic)
            {
                ClampTargetToBounds();
            }
        }
        else
        {
            if (!useFixedViewSize)
            {
                // 플레이어 따라다니기 + follow 뷰 자동 스케일 적용
                if (autoScaleFollowView && boundsCollider != null && cam != null && cam.orthographic)
                {
                    ApplyAutoFollowViewSizing();
                }

                HandleFollow();
            }
            else
            {
                if (cam != null && cam.orthographic)
                {
                    ApplyFixedViewSize();
                    if (boundsCollider != null) ClampTargetToBounds();
                }
            }
        }

        // Smooth 이동
        if (panSmooth > 0f)
            transform.position = Vector3.SmoothDamp(transform.position, new Vector3(targetPos.x, targetPos.y, transform.position.z), ref velocity, panSmooth);
        else
            transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z);
    }

    // player 자동 탐색
    void TryAutoFindPlayerByLayer()
    {
        if (playerTarget != null) return;
        if (string.IsNullOrEmpty(playerLayerName)) return;

        int layerIdx = LayerMask.NameToLayer(playerLayerName);
        if (layerIdx < 0) return;

        GameObject candidate = null;
        try
        {
            candidate = Object.FindFirstObjectByType<GameObject>();
        }
        catch
        {
            candidate = null;
        }

        if (candidate != null && candidate.layer == layerIdx)
        {
            playerTarget = candidate.transform;
            return;
        }

        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            var found = RecursiveFindByLayer(root.transform, layerIdx);
            if (found != null)
            {
                playerTarget = found;
                return;
            }
        }
    }

    // 루트->자식 재귀 탐색
    Transform RecursiveFindByLayer(Transform t, int layerIdx)
    {
        if (t.gameObject.layer == layerIdx) return t;
        for (int i = 0; i < t.childCount; ++i)
        {
            var child = t.GetChild(i);
            var r = RecursiveFindByLayer(child, layerIdx);
            if (r != null) return r;
        }
        return null;
    }

    // 팔로우 처리 (deadzone 체크 후 이동, bounds 내로 클램프)
    void HandleFollow()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null || !cam.orthographic)
        {
            if (playerTarget != null)
            {
                targetPos.x = playerTarget.position.x;
                targetPos.y = playerTarget.position.y;
            }
            return;
        }

        // bounds 전체를 커버하면 팔로우 중지
        if (boundsCollider != null)
        {
            Bounds b = boundsCollider.bounds;
            float boundsWidth = Mathf.Max(0.0001f, b.size.x);
            float boundsHeight = Mathf.Max(0.0001f, b.size.y);

            float verticalExtent = cam.orthographicSize;
            float horizontalExtent = cam.orthographicSize * cam.aspect;

            float eps = 0.05f;
            bool coversHor = horizontalExtent >= (boundsWidth * 0.5f - eps);
            bool coversVer = verticalExtent >= (boundsHeight * 0.5f - eps);

            if (coversHor && coversVer) return;
        }

        if (playerTarget == null) return;

        Vector3 camCenter = new Vector3(transform.position.x, transform.position.y, 0f);
        Vector3 toPlayer = (Vector3)playerTarget.position - camCenter;

        if (Mathf.Abs(toPlayer.x) > followDeadzone || Mathf.Abs(toPlayer.y) > followDeadzone)
        {
            targetPos.x = playerTarget.position.x;
            targetPos.y = playerTarget.position.y;

            if (boundsCollider != null)
                ClampTargetToBounds();
        }
    }

    /// <summary>
    /// follow 모드에서 bounds의 일부 크기를 사용해 카메라 orthographicSize를 계산/적용.
    /// followViewFraction 비율로 boundsWidth/boundsHeight를 줄여 사용.
    /// followZoomSmooth로 부드럽게 보간.
    /// </summary>
    void ApplyAutoFollowViewSizing()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null || !cam.orthographic) return;
        if (boundsCollider == null) return;

        Bounds b = boundsCollider.bounds;
        float boundsWidth = Mathf.Max(0.0001f, b.size.x);
        float boundsHeight = Mathf.Max(0.0001f, b.size.y);

        // 원하는 view (월드 단위)
        float desiredWidth = Mathf.Max(0.01f, boundsWidth * followViewFraction);
        float desiredHeight = Mathf.Max(0.01f, boundsHeight * followViewFraction);

        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);

        float orthoFromHeight = desiredHeight * 0.5f;
        float orthoFromWidth = (desiredWidth / aspect) * 0.5f;
        float desiredOrtho = Mathf.Max(orthoFromHeight, orthoFromWidth);

        // clamp with min/max
        desiredOrtho = Mathf.Max(desiredOrtho, minOrthoSize);
        desiredOrtho = Mathf.Min(desiredOrtho, maxOrthoSize);

        if (followZoomSmooth <= 0f)
        {
            cam.orthographicSize = desiredOrtho;
        }
        else
        {
            // 부드럽게 보간 (프레임 단위 단순 Lerp)
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, desiredOrtho, followZoomSmooth);
        }

        // viewWidth/viewHeight 반영(정보용)
        float appliedHeight = cam.orthographicSize * 2f;
        float appliedWidth = appliedHeight * aspect;
        viewWidth = appliedWidth;
        viewHeight = appliedHeight;
    }

    /// <summary>
    /// boundsCollider(또는 viewWidth/viewHeight) 기반으로 orthographicSize를 계산하여 카메라에 적용.
    /// ignoreMaxOrtho = true이면 maxOrthoSize 제한을 무시하고 정확히 맞춤.
    /// </summary>
    public void FitCameraToBounds(bool ignoreMaxOrtho = true)
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null || !cam.orthographic) return;
        if (boundsCollider == null) return;

        Bounds b = boundsCollider.bounds;
        float boundsWidth = Mathf.Max(0.0001f, b.size.x);
        float boundsHeight = Mathf.Max(0.0001f, b.size.y);

        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);

        // needed ortho to fit both width and height
        float orthoFromHeight = boundsHeight * 0.5f;
        float orthoFromWidth = (boundsWidth / aspect) * 0.5f;
        float neededOrtho = Mathf.Max(orthoFromHeight, orthoFromWidth);

        // Respect minOrthoSize
        neededOrtho = Mathf.Max(neededOrtho, minOrthoSize);

        // Apply maxOrthoSize unless we are ignoring it
        if (!ignoreMaxOrtho)
            neededOrtho = Mathf.Min(neededOrtho, maxOrthoSize);

        // 강제 적용
        cam.orthographicSize = neededOrtho;

        // 적용된 화면 크기를 viewWidth/viewHeight에 반영
        float appliedHeight = neededOrtho * 2f;
        float appliedWidth = appliedHeight * aspect;
        viewWidth = appliedWidth;
        viewHeight = appliedHeight;

        Debug.Log($"FitCameraToBounds applied: ortho={neededOrtho:F3}, viewW={viewWidth:F3}, viewH={viewHeight:F3}, aspect={aspect:F3}");
    }

    /// <summary>
    /// viewWidth x viewHeight 를 기준으로 orthographicSize 계산 후 적용.
    /// ignoreMaxOrtho = true 이면 maxOrthoSize 제한을 무시하고 필요한 ortho를 강제 설정.
    /// </summary>
    void ApplyFixedViewSize(bool ignoreMaxOrtho = false)
    {
        if (cam == null || !cam.orthographic) return;

        float orthoFromHeight = viewHeight * 0.5f;
        float orthoFromWidth = (viewWidth / cam.aspect) * 0.5f;
        float targetOrtho = Mathf.Max(orthoFromHeight, orthoFromWidth);

        // bounds가 있으면 bounds에 맞춰 축소(필요하면)
        if (boundsCollider != null)
        {
            Bounds b = boundsCollider.bounds;
            float boundsWidth = Mathf.Max(0.0001f, b.size.x);
            float boundsHeight = Mathf.Max(0.0001f, b.size.y);

            float allowedOrthoFromHeight = boundsHeight * 0.5f;
            float allowedOrthoFromWidth = (boundsWidth / cam.aspect) * 0.5f;
            float allowedOrtho = Mathf.Min(allowedOrthoFromHeight, allowedOrthoFromWidth);
            allowedOrtho = Mathf.Max(0.0001f, allowedOrtho);

            if (targetOrtho > allowedOrtho)
            {
                targetOrtho = allowedOrtho;

                float appliedHeight = targetOrtho * 2f;
                float appliedWidth = appliedHeight * cam.aspect;

                viewWidth = appliedWidth;
                viewHeight = appliedHeight;
            }
        }

        if (!ignoreMaxOrtho) targetOrtho = Mathf.Clamp(targetOrtho, 0.0001f, maxOrthoSize);
        else targetOrtho = Mathf.Max(0.0001f, targetOrtho);
        
        cam.orthographicSize = targetOrtho;
    }

    // targetPos가 bounds 내부가 되도록 클램프(가운데 고정)
    void ClampTargetToBounds()
    {
        if (boundsCollider == null || cam == null || !cam.orthographic) return;

        Bounds b = boundsCollider.bounds;
        float verticalExtent = cam.orthographicSize;
        float horizontalExtent = cam.orthographicSize * cam.aspect;

        float minX = b.min.x + horizontalExtent;
        float maxX = b.max.x - horizontalExtent;
        float minY = b.min.y + verticalExtent;
        float maxY = b.max.y - verticalExtent;

        if (minX > maxX)
        {
            float centerX = (b.min.x + b.max.x) / 2f;
            minX = maxX = centerX;
        }
        if (minY > maxY)
        {
            float centerY = (b.min.y + b.max.y) / 2f;
            minY = maxY = centerY;
        }

        targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
        targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);
    }

    // 씬에서 영역 및 카메라 위치 시각화
    void OnDrawGizmosSelected()
    {
        if (boundsCollider != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawCube(boundsCollider.bounds.center, boundsCollider.bounds.size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(boundsCollider.bounds.center, boundsCollider.bounds.size);
        }

        Camera c = (cam != null) ? cam : GetComponent<Camera>();
        if (c != null && c.orthographic)
        {
            float vExt = c.orthographicSize;
            float hExt = vExt * c.aspect;
            Vector3 center = transform.position;
            center.z = 0f;
            Gizmos.color = new Color(0f, 0f, 1f, 0.15f);
            Gizmos.DrawCube(new Vector3(center.x, center.y, 0f), new Vector3(hExt * 2f, vExt * 2f, 0.01f));
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0f), new Vector3(hExt * 2f, vExt * 2f, 0.01f));
        }

        if (useFixedViewSize)
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.15f);
            Vector3 size = new Vector3(viewWidth, viewHeight, 0.01f);
            Gizmos.DrawCube(new Vector3(transform.position.x, transform.position.y, 0f), size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(new Vector3(transform.position.x, transform.position.y, 0f), size);
        }
    }

    // 외부에서 bounds를 설정하고 즉시 fit 시킬 수 있게 함
    public void SetBounds(BoxCollider2D newBounds, bool snapCameraToBounds = true, bool fitViewToBounds = false)
    {
        CurrentBounds = newBounds;
        boundsCollider = newBounds;
        if (cam == null) cam = GetComponent<Camera>();
        if (boundsCollider == null) return;

        if (fitViewToBounds || autoFitToBounds)
        {
            FitCameraToBounds(ignoreMaxOrtho: forceFitIgnoreMaxOrtho);
        }

        if (snapCameraToBounds)
        {
            ClampTargetToBounds();
            transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z);
        }
    }
}
