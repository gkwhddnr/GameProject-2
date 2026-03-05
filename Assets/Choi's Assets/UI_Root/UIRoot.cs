using UnityEngine;

public class UIRoot : MonoBehaviour
{
    public static UIRoot Instance { get; private set; }

    [Header("ItemBar Transparency")]
    public RectTransform itemBarRect;
    public CanvasGroup itemBarCanvasGroup;
    public LayerMask checkLayers;
    [Range(0f, 1f)] public float transparentAlpha = 0.4f;

    private readonly Vector3[] _corners = new Vector3[4];
    // Physics 쿼리 주기 제한: 10회/초만 실행 (60fps 기준 6프레임마다 1회)
    private const float CHECK_INTERVAL = 0.1f;
    private float _nextCheckTime = 0f;
    // GC 방지: 매프레임 new 대신 필드로 사전 할당
    private readonly Collider2D[] _overlapResults = new Collider2D[1];
    private ContactFilter2D _filter;
    private Canvas _cachedCanvas;
    private Camera _cachedCamera;

    private void Awake()
    {
        // 씬마다 UIRoot가 또 생기면 중복 제거
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // ContactFilter2D 사전 설정 (매프레임 new 방지)
        _filter = new ContactFilter2D();
        _filter.useLayerMask = true;
        _filter.useTriggers = true;
    }

    private void Update()
    {
        // 주기 제한: CHECK_INTERVAL마다만 Physics 쿼리 실행
        if (Time.unscaledTime < _nextCheckTime) return;
        _nextCheckTime = Time.unscaledTime + CHECK_INTERVAL;
        UpdateItemBarTransparency();
    }

    private void UpdateItemBarTransparency()
    {
        if (itemBarRect == null || itemBarCanvasGroup == null) return;

        itemBarRect.GetWorldCorners(_corners);

        // 캐시된 Canvas 사용 (매 호출 GetComponentInParent 방지)
        if (_cachedCanvas == null)
            _cachedCanvas = itemBarCanvasGroup.GetComponentInParent<Canvas>();

        Vector2 min, max;

        if (_cachedCanvas != null && _cachedCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            if (_cachedCamera == null) _cachedCamera = Camera.main;
            if (_cachedCamera == null) return;

            float distance = Mathf.Abs(_cachedCamera.transform.position.z);
            Vector3 world0 = _cachedCamera.ScreenToWorldPoint(new Vector3(_corners[0].x, _corners[0].y, distance));
            Vector3 world2 = _cachedCamera.ScreenToWorldPoint(new Vector3(_corners[2].x, _corners[2].y, distance));

            min = new Vector2(Mathf.Min(world0.x, world2.x), Mathf.Min(world0.y, world2.y));
            max = new Vector2(Mathf.Max(world0.x, world2.x), Mathf.Max(world0.y, world2.y));
        }
        else
        {
            min = new Vector2(Mathf.Min(_corners[0].x, _corners[2].x), Mathf.Min(_corners[0].y, _corners[2].y));
            max = new Vector2(Mathf.Max(_corners[0].x, _corners[2].x), Mathf.Max(_corners[0].y, _corners[2].y));
        }

        // 사전 할당된 filter에 LayerMask 업데이트
        _filter.SetLayerMask(checkLayers);

        // NonAlloc 버전: GC Alloc 없이 결과를 재사용 배열에 담음
        int hitCount = Physics2D.OverlapArea(min, max, _filter, _overlapResults);

        itemBarCanvasGroup.alpha = (hitCount > 0) ? transparentAlpha : 1f;
    }
}
