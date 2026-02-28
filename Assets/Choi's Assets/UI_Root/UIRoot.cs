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

    private void Awake()
    {
        // 씬마다 UIRoot가 또 생기면 중복 제거
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // 씬 바뀌어도 유지
    }

    private void Update()
    {
        UpdateItemBarTransparency();
    }

    private void UpdateItemBarTransparency()
    {
        if (itemBarRect == null || itemBarCanvasGroup == null) return;

        itemBarRect.GetWorldCorners(_corners);
        
        Vector2 min, max;
        Canvas canvas = itemBarCanvasGroup.GetComponentInParent<Canvas>();

        // Canvas 렌더 모드에 따라 월드 좌표를 적절히 변환합니다.
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            // 스크린 좌표를 메인 카메라 기준의 월드 좌표로 변환
            float distance = Mathf.Abs(cam.transform.position.z);
            Vector3 world0 = cam.ScreenToWorldPoint(new Vector3(_corners[0].x, _corners[0].y, distance));
            Vector3 world2 = cam.ScreenToWorldPoint(new Vector3(_corners[2].x, _corners[2].y, distance));
            
            min = new Vector2(Mathf.Min(world0.x, world2.x), Mathf.Min(world0.y, world2.y));
            max = new Vector2(Mathf.Max(world0.x, world2.x), Mathf.Max(world0.y, world2.y));
        }
        else
        {
            // ScreenSpaceCamera의 경우 이미 월드 좌표이므로 바로 사용
            min = new Vector2(Mathf.Min(_corners[0].x, _corners[2].x), Mathf.Min(_corners[0].y, _corners[2].y));
            max = new Vector2(Mathf.Max(_corners[0].x, _corners[2].x), Mathf.Max(_corners[0].y, _corners[2].y));
        }

        // 아이템이나 플레이어 충돌체가 Trigger 상태일 수 있으므로 이를 포함하여 검사합니다.
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(checkLayers);
        filter.useLayerMask = true;
        filter.useTriggers = true; 

        Collider2D[] results = new Collider2D[1];
        int hitCount = Physics2D.OverlapArea(min, max, filter, results);

        if (hitCount > 0)
        {
            itemBarCanvasGroup.alpha = transparentAlpha;
        }
        else
        {
            itemBarCanvasGroup.alpha = 1f;
        }
    }
}
