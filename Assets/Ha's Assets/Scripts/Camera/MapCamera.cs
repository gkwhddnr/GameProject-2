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
    private const float SEARCH_INTERVAL = 0.5f; // 플레이어 탐색 간격 (초)

    private int lastScreenW = 0;
    private int lastScreenH = 0;

    private Camera Cam {
        get {
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

        // 안전값 설정
        viewWidth = Mathf.Max(0.01f, viewWidth);
        viewHeight = Mathf.Max(0.01f, viewHeight);
        minOrthoSize = Mathf.Max(0.0001f, minOrthoSize);
        maxOrthoSize = Mathf.Max(minOrthoSize, maxOrthoSize);

        // 초기 실행
        RefreshCameraState();

        lastScreenW = Screen.width;
        lastScreenH = Screen.height;
    }

    void Update()
    {
        // [최적화] 플레이어가 없을 때만 정해진 주기로 탐색 (매 프레임 X)
        if (playerTarget == null && Time.time >= _nextSearchTime)
        {
            _nextSearchTime = Time.time + SEARCH_INTERVAL;
            TryAutoFindPlayerByLayer();
        }

        // 해상도 변경 체크
        if (Screen.width != lastScreenW || Screen.height != lastScreenH)
        {
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;
            RefreshCameraState();
        }

        // 동작 분기
        if (autoFitToBounds)
        {
            if (boundsCollider != null && Cam.orthographic) FitCameraToBounds(ignoreMaxOrtho: forceFitIgnoreMaxOrtho);
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

        // [최적화] 위치 이동 (목표지점과의 거리가 아주 작으면 연산 스킵)
        Vector3 currentPos = transform.position;
        Vector3 nextTarget = new Vector3(targetPos.x, targetPos.y, currentPos.z);
        
        if (Vector3.SqrMagnitude(currentPos - nextTarget) > 0.00001f)
        {
            if (panSmooth > 0f) transform.position = Vector3.SmoothDamp(currentPos, nextTarget, ref velocity, panSmooth);
            else transform.position = nextTarget;
        }
    }

    private void RefreshCameraState()
    {
        if (autoFitToBounds && boundsCollider != null) FitCameraToBounds(ignoreMaxOrtho: forceFitIgnoreMaxOrtho);
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

    private void SyncTransformImmediate(){ transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z); }



    void TryAutoFindPlayerByLayer()
    {
        if (playerTarget != null || _playerLayer < 0) return;

        // [최적화] 씬의 루트 오브젝트들만 순회하여 가비지 발생 최소화
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

            // 이미 화면이 영역 전체를 다 보여주고 있다면 이동 불필요
            if (horizontalExtent >= (b.size.x * 0.5f - 0.05f) && verticalExtent >= (b.size.y * 0.5f - 0.05f)) return;
        }

        if (playerTarget == null) return;

        // Deadzone 체크
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

        // 영역을 벗어나지 않는 최대 사이즈
        float allowedOrtho = Mathf.Min(b.size.y * 0.5f, (b.size.x / aspect) * 0.5f);
        if (neededOrtho > allowedOrtho) neededOrtho = allowedOrtho;

        neededOrtho = Mathf.Max(neededOrtho, minOrthoSize);
        if (!ignoreMaxOrtho) neededOrtho = Mathf.Min(neededOrtho, maxOrthoSize);

        Cam.orthographicSize = neededOrtho;

        // 중심점 클램프 연산
        float horizontalExtent = neededOrtho * aspect;
        float minX = b.min.x + horizontalExtent;
        float maxX = b.max.x - horizontalExtent;
        float minY = b.min.y + neededOrtho;
        float maxY = b.max.y - neededOrtho;

        targetPos.x = (minX > maxX) ? b.center.x : Mathf.Clamp(b.center.x, minX, maxX);
        targetPos.y = (minY > maxY) ? b.center.y : Mathf.Clamp(b.center.y, minY, maxY);

        SyncTransformImmediate();

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
        // 기즈모 코드는 에디터 기능이므로 메모리 최적화와는 무관하여 유지합니다.
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