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

    [Header("Bounds 강제 제한")]
    [Tooltip("모든 모드에서 카메라가 Bounds 밖으로 나가지 못하도록 강제 제한")]
    public bool alwaysClampToBounds = true;
    [Tooltip("LateUpdate에서 실제 transform.position을 강제로 제한")]
    public bool forceClampInLateUpdate = true;

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

    private Vector3 lastBoundsSize = Vector3.zero;
    private Vector3 lastBoundsCenter = Vector3.zero;

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
                SyncTransformImmediate();

                // SmoothDamp 에 의한 잔여 관성 제거
                velocity = Vector3.zero;

                var playerLimiter = FindFirstObjectByType<PlayerBoundsLimiter>();
                if (playerLimiter != null) playerLimiter.ForceClampNow();
            }
        }
        else
        {
            if (!useFixedViewSize)
            {
                if (autoScaleFollowView && boundsCollider != null && Cam.orthographic)
                    ApplyAutoFollowViewSizing();

                HandleFollow();
            }
            else if (Cam.orthographic)
            {
                ApplyFixedViewSize();
                if (boundsCollider != null) ClampTargetToBounds();
            }
        }

        if (alwaysClampToBounds && boundsCollider != null && Cam.orthographic)
        {
            ClampTargetToBounds();
        }

        Vector3 currentPos = transform.position;
        Vector3 nextTarget = new Vector3(targetPos.x, targetPos.y, currentPos.z);

        if (Vector3.SqrMagnitude(currentPos - nextTarget) > 0.00001f)
        {
            if (panSmooth > 0f) transform.position = Vector3.SmoothDamp(currentPos, nextTarget, ref velocity, panSmooth);
            else transform.position = nextTarget;
        }
    }

    // ★ LateUpdate에서 최종적으로 강제 제한 (수정됨!)
    void LateUpdate()
    {
        if (!forceClampInLateUpdate || boundsCollider == null || !Cam.orthographic) return;

        Vector3 pos = transform.position;
        Bounds b = boundsCollider.bounds;

        float vExt = Cam.orthographicSize;
        float hExt = vExt * Cam.aspect;

        // ★ 수정: 각 축별로 독립적으로 Clamp 처리
        float minX = b.min.x + hExt;
        float maxX = b.max.x - hExt;
        float minY = b.min.y + vExt;
        float maxY = b.max.y - vExt;

        // X축 처리
        if (minX > maxX)
        {
            // 카메라가 Bounds보다 넓으면 중앙에 고정
            pos.x = b.center.x;
        }
        else
        {
            // 정상적인 Clamp
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
        }

        // Y축 처리
        if (minY > maxY)
        {
            // 카메라가 Bounds보다 높으면 중앙에 고정
            pos.y = b.center.y;
        }
        else
        {
            // 정상적인 Clamp
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
        }

        pos.z = transform.position.z;
        transform.position = pos;

        // targetPos도 동기화
        targetPos = pos;
    }

    private void RefreshCameraState()
    {
        if (autoFitToBounds && boundsCollider != null)
        {
            FitCameraToBounds(ignoreMaxOrtho: forceFitIgnoreMaxOrtho);
            SyncTransformImmediate();
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

        targetPos.x = b.center.x;
        targetPos.y = b.center.y;

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
        }
    }

    public void SetBounds(BoxCollider2D newBounds, bool snapCameraToBounds = true, bool fitViewToBounds = false)
    {
        if (newBounds == null) return;
        

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

    public void ForceRefreshBounds()
    {
        if (boundsCollider != null)
        {
            lastBoundsSize = boundsCollider.bounds.size;
            lastBoundsCenter = boundsCollider.bounds.center;
            RefreshCameraState();
            Debug.Log("[MapCamera] Bounds 강제 재조정 완료");
        }
    }
}