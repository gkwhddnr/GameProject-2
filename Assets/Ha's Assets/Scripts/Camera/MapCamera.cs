using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Camera))]
public class MapCamera : MonoBehaviour
{
    [Header("카메라 움직임")]
    public float panSmooth = 0.08f;

    [Header("카메라 영역 범위 설정")]
    public BoxCollider2D boundsCollider;

    [Header("전체 배경 카메라 (둘다 체크)")]
    public bool autoFitToBounds = false;
    public bool forceFitIgnoreMaxOrtho = false;

    [Header("AutoFit 시 캐릭터 추적 옵션")]
    [Tooltip("autoFitToBounds일 때도 캐릭터를 화면 안에 유지")]
    public bool keepPlayerInView = true;
    [Tooltip("화면 가장자리로부터의 최소 여백 (월드 단위)")]
    public float playerPadding = 2f;

    [Header("Zoom")]
    public float minOrthoSize = 3f;
    public float maxOrthoSize = 20f;

    [Header("카메라 영역 설정")]
    public bool useFixedViewSize = false;
    public float viewWidth = 20f;
    public float viewHeight = 10f;

    [Header("플레이어 캐릭터 카메라")]
    public Transform playerTarget;
    public float followDeadzone = 1.5f;

    [Header("플레이어를 추적하는 카메라 영역 설정")]
    public bool autoScaleFollowView = true;
    [Range(0.01f, 1f)] public float followViewFraction = 0.25f;
    [Range(0f, 1f)] public float followZoomSmooth = 0.15f;

    public BoxCollider2D CurrentBounds { get; private set; }

    private Vector3 targetPos;
    private Vector3 velocity = Vector3.zero;
    private Camera _cam;
    private int _playerLayer;
    private float _nextSearchTime = 0f;
    private const float SEARCH_INTERVAL = 0.5f;

    private int lastScreenW = 0;
    private int lastScreenH = 0;

    private Camera Cam
    {
        get
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            return _cam;
        }
    }

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _playerLayer = LayerMask.NameToLayer("Player");
    }

    void Start()
    {
        targetPos = transform.position;

        viewWidth = Mathf.Max(0.01f, viewWidth);
        viewHeight = Mathf.Max(0.01f, viewHeight);
        minOrthoSize = Mathf.Max(0.0001f, minOrthoSize);
        maxOrthoSize = Mathf.Max(minOrthoSize, maxOrthoSize);

        RefreshCameraState();

        lastScreenW = Screen.width;
        lastScreenH = Screen.height;
    }

    void Update()
    {
        if (playerTarget == null && Time.time >= _nextSearchTime)
        {
            _nextSearchTime = Time.time + SEARCH_INTERVAL;
            TryAutoFindPlayerByLayer();
        }

        if (Screen.width != lastScreenW || Screen.height != lastScreenH)
        {
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;
            RefreshCameraState();
        }

        if (autoFitToBounds)
        {
            if (boundsCollider != null && Cam.orthographic)
            {
                FitCameraToBounds(ignoreMaxOrtho: forceFitIgnoreMaxOrtho);

                if (keepPlayerInView && playerTarget != null) AdjustCameraToKeepPlayerInView();
            }
        }
        else
        {
            if (!useFixedViewSize)
            {
                if (autoScaleFollowView && boundsCollider != null && Cam.orthographic) ApplyAutoFollowViewSizing();

                HandleFollow();
            }
            else if (Cam.orthographic)
            {
                ApplyFixedViewSize();
                if (boundsCollider != null) ClampTargetToBounds();
            }
        }

        Vector3 currentPos = transform.position;
        Vector3 nextTarget = new Vector3(targetPos.x, targetPos.y, currentPos.z);

        if (Vector3.SqrMagnitude(currentPos - nextTarget) > 0.00001f)
        {
            if (panSmooth > 0f) transform.position = Vector3.SmoothDamp(currentPos, nextTarget, ref velocity, panSmooth);
            else transform.position = nextTarget;
        }
    }

    /// <summary>
    /// autoFitToBounds 모드에서도 캐릭터가 화면 밖으로 나가지 않도록 카메라 위치를 조정합니다.
    /// </summary>
    private void AdjustCameraToKeepPlayerInView()
    {
        if (playerTarget == null || boundsCollider == null || !Cam.orthographic) return;

        // 캐릭터의 Collider2D를 가져와서 실제 영역(Bounds)을 사용
        Collider2D playerCol = playerTarget.GetComponent<Collider2D>();
        Vector3 playerMin = playerTarget.position;
        Vector3 playerMax = playerTarget.position;

        if (playerCol != null)
        {
            playerMin = playerCol.bounds.min;
            playerMax = playerCol.bounds.max;
        }

        Bounds bounds = boundsCollider.bounds;
        float vExt = Cam.orthographicSize;
        float hExt = vExt * Cam.aspect;

        // 현재 카메라 위치 기준으로 화면 범위 계산 (여백 포함)
        float screenMinX = targetPos.x - hExt + playerPadding;
        float screenMaxX = targetPos.x + hExt - playerPadding;
        float screenMinY = targetPos.y - vExt + playerPadding;
        float screenMaxY = targetPos.y + vExt - playerPadding;

        float offsetX = 0f;
        float offsetY = 0f;

        // 캐릭터의 '끝부분'이 화면 밖으로 나가는지 체크
        if (playerMin.x < screenMinX) offsetX = playerMin.x - screenMinX;
        else if (playerMax.x > screenMaxX) offsetX = playerMax.x - screenMaxX;

        if (playerMin.y < screenMinY) offsetY = playerMin.y - screenMinY;
        else if (playerMax.y > screenMaxY) offsetY = playerMax.y - screenMaxY;

        if (Mathf.Abs(offsetX) > 0.01f || Mathf.Abs(offsetY) > 0.01f)
        {
            targetPos.x += offsetX;
            targetPos.y += offsetY;

            // 카메라가 맵 밖으로 나가지 않도록 최종 제한
            ClampTargetToBounds();
        }
    }

    private void RefreshCameraState()
    {
        if (autoFitToBounds && boundsCollider != null)
        {
            FitCameraToBounds(ignoreMaxOrtho: forceFitIgnoreMaxOrtho);

            if (keepPlayerInView && playerTarget != null)
            {
                AdjustCameraToKeepPlayerInView();
                SyncTransformImmediate();
            }
        }
        else if (useFixedViewSize)
        {
            ApplyFixedViewSize(ignoreMaxOrtho: false);
            if (boundsCollider != null) ClampTargetToBounds();
            SyncTransformImmediate();
        }
        else
        {
            if (autoScaleFollowView && boundsCollider != null) ApplyAutoFollowViewSizing();
            if (playerTarget != null)
            {
                targetPos = playerTarget.position;
                if (boundsCollider != null) ClampTargetToBounds();
                SyncTransformImmediate();
            }
        }
    }

    private void SyncTransformImmediate()
    {
        transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z);
    }

    void TryAutoFindPlayerByLayer()
    {
        if (playerTarget != null || _playerLayer < 0) return;

        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            var found = RecursiveFindByLayer(root.transform, _playerLayer);
            if (found != null)
            {
                playerTarget = found;
                RefreshCameraState();
                return;
            }
        }
    }

    Transform RecursiveFindByLayer(Transform t, int layerIdx)
    {
        if (t.gameObject.layer == layerIdx) return t;
        int childCount = t.childCount;
        for (int i = 0; i < childCount; ++i)
        {
            var r = RecursiveFindByLayer(t.GetChild(i), layerIdx);
            if (r != null) return r;
        }
        return null;
    }

    void HandleFollow()
    {
        if (!Cam.orthographic)
        {
            if (playerTarget != null) targetPos = playerTarget.position;
            return;
        }

        if (boundsCollider != null)
        {
            Bounds b = boundsCollider.bounds;
            float verticalExtent = Cam.orthographicSize;
            float horizontalExtent = verticalExtent * Cam.aspect;

            if (horizontalExtent >= (b.size.x * 0.5f - 0.05f) && verticalExtent >= (b.size.y * 0.5f - 0.05f)) return;
        }

        if (playerTarget == null) return;

        float diffX = playerTarget.position.x - transform.position.x;
        float diffY = playerTarget.position.y - transform.position.y;

        if (Mathf.Abs(diffX) > followDeadzone || Mathf.Abs(diffY) > followDeadzone)
        {
            targetPos = playerTarget.position;
            if (boundsCollider != null) ClampTargetToBounds();
        }
    }

    void ApplyAutoFollowViewSizing()
    {
        if (boundsCollider == null || !Cam.orthographic) return;

        Bounds b = boundsCollider.bounds;
        float aspect = Cam.aspect;

        float desiredWidth = b.size.x * followViewFraction;
        float desiredHeight = b.size.y * followViewFraction;

        float orthoFromHeight = desiredHeight * 0.5f;
        float orthoFromWidth = (desiredWidth / aspect) * 0.5f;
        float desiredOrtho = Mathf.Clamp(Mathf.Max(orthoFromHeight, orthoFromWidth), minOrthoSize, maxOrthoSize);

        if (followZoomSmooth <= 0f) Cam.orthographicSize = desiredOrtho;
        else Cam.orthographicSize = Mathf.Lerp(Cam.orthographicSize, desiredOrtho, followZoomSmooth);

        viewHeight = Cam.orthographicSize * 2f;
        viewWidth = viewHeight * aspect;
    }

    public void FitCameraToBounds(bool ignoreMaxOrtho = true)
    {
        if (boundsCollider == null || !Cam.orthographic) return;

        Bounds b = boundsCollider.bounds;
        float aspect = Cam.aspect;

        float orthoFromHeight = b.size.y * 0.5f;
        float orthoFromWidth = (b.size.x / aspect) * 0.5f;
        float neededOrtho = Mathf.Max(orthoFromHeight, orthoFromWidth);

        float allowedOrtho = Mathf.Min(b.size.y * 0.5f, (b.size.x / aspect) * 0.5f);
        if (neededOrtho > allowedOrtho) neededOrtho = allowedOrtho;

        neededOrtho = Mathf.Max(neededOrtho, minOrthoSize);
        if (!ignoreMaxOrtho) neededOrtho = Mathf.Min(neededOrtho, maxOrthoSize);

        Cam.orthographicSize = neededOrtho;

        float horizontalExtent = neededOrtho * aspect;
        float minX = b.min.x + horizontalExtent;
        float maxX = b.max.x - horizontalExtent;
        float minY = b.min.y + neededOrtho;
        float maxY = b.max.y - neededOrtho;

        targetPos.x = (minX > maxX) ? b.center.x : Mathf.Clamp(b.center.x, minX, maxX);
        targetPos.y = (minY > maxY) ? b.center.y : Mathf.Clamp(b.center.y, minY, maxY);

        if (!keepPlayerInView) SyncTransformImmediate();

        viewHeight = neededOrtho * 2f;
        viewWidth = viewHeight * aspect;
    }

    void ApplyFixedViewSize(bool ignoreMaxOrtho = false)
    {
        if (!Cam.orthographic) return;

        float orthoFromHeight = viewHeight * 0.5f;
        float orthoFromWidth = (viewWidth / Cam.aspect) * 0.5f;
        float targetOrtho = Mathf.Max(orthoFromHeight, orthoFromWidth);

        if (boundsCollider != null)
        {
            Bounds b = boundsCollider.bounds;
            float allowedOrtho = Mathf.Min(b.size.y * 0.5f, (b.size.x / Cam.aspect) * 0.5f);
            if (targetOrtho > allowedOrtho)
            {
                targetOrtho = allowedOrtho;
                viewHeight = targetOrtho * 2f;
                viewWidth = viewHeight * Cam.aspect;
            }
        }

        Cam.orthographicSize = ignoreMaxOrtho ? Mathf.Max(0.0001f, targetOrtho) : Mathf.Clamp(targetOrtho, 0.0001f, maxOrthoSize);
    }

    void ClampTargetToBounds()
    {
        if (boundsCollider == null || !Cam.orthographic) return;

        Bounds b = boundsCollider.bounds;
        float vExt = Cam.orthographicSize;
        float hExt = vExt * Cam.aspect;

        float minX = b.min.x + hExt;
        float maxX = b.max.x - hExt;
        float minY = b.min.y + vExt;
        float maxY = b.max.y - vExt;

        targetPos.x = (minX > maxX) ? (b.min.x + b.max.x) * 0.5f : Mathf.Clamp(targetPos.x, minX, maxX);
        targetPos.y = (minY > maxY) ? (b.min.y + b.max.y) * 0.5f : Mathf.Clamp(targetPos.y, minY, maxY);
    }

    void OnDrawGizmosSelected()
    {
        if (boundsCollider != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawCube(boundsCollider.bounds.center, boundsCollider.bounds.size);
        }

        Camera c = Cam;
        if (c != null && c.orthographic)
        {
            float vExt = c.orthographicSize;
            float hExt = vExt * c.aspect;
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(new Vector3(transform.position.x, transform.position.y, 0f), new Vector3(hExt * 2f, vExt * 2f, 0.01f));

            // 캐릭터 패딩 영역 표시
            if (keepPlayerInView && playerPadding > 0f && autoFitToBounds)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                Vector3 paddedSize = new Vector3((hExt - playerPadding) * 2f, (vExt - playerPadding) * 2f, 0.01f);
                Gizmos.DrawWireCube(new Vector3(transform.position.x, transform.position.y, 0f), paddedSize);
            }
        }
    }

    public void SetBounds(BoxCollider2D newBounds, bool snapCameraToBounds = true, bool fitViewToBounds = false)
    {
        boundsCollider = newBounds;
        CurrentBounds = newBounds;
        if (boundsCollider == null) return;

        if (fitViewToBounds || autoFitToBounds) FitCameraToBounds(ignoreMaxOrtho: forceFitIgnoreMaxOrtho);

        if (snapCameraToBounds)
        {
            if (!(fitViewToBounds || autoFitToBounds)) ClampTargetToBounds();
            SyncTransformImmediate();
        }
    }
}