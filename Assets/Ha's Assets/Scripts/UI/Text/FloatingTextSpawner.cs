using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class FloatingTextSpawner : MonoBehaviour
{
    public static FloatingTextSpawner Instance { get; private set; }

    [System.Serializable]
    public class ItemMessageEntry
    {
        public GameObject itemReference;
        [TextArea] public string message;

        [Header("Text Style")]
        public float fontSize = 36f;
        public FontStyles fontStyle = FontStyles.Normal;
        public Color textColor = Color.white;
    }

    [Header("References")]
    public Canvas canvas;
    public GameObject floatingTextPrefab;

    [Space]
    public float defaultFadeIn = 0.25f;
    public float defaultHold = 1.0f;
    public float defaultFadeOut = 0.5f;
    public Vector3 worldOffset = new Vector3(0, 1.5f, 0);

    [Header("Item -> Message Table")]
    public ItemMessageEntry[] itemMessageEntries;

    [Header("Boundary Settings")]
    public float edgePadding = 0.5f;

    private Camera canvasCamera;
    private RectTransform canvasRect;
    private WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();
    private Dictionary<float, WaitForSeconds> _waitCache = new Dictionary<float, WaitForSeconds>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (canvas != null)
        {
            canvasCamera = canvas.worldCamera ? canvas.worldCamera : Camera.main;
            canvasRect = canvas.GetComponent<RectTransform>();
        }
    }

    public void ShowForCollectedItem(GameObject item)
    {
        if (item == null || itemMessageEntries == null) return;

        foreach (var entry in itemMessageEntries)
        {
            if (entry == null || entry.itemReference == null) continue;

            if (item.name.Contains(entry.itemReference.name) || entry.itemReference.name.Contains(item.name))
            {
                if (!string.IsNullOrEmpty(entry.message))
                {
                    // 설정된 스타일 값을 함께 전달합니다.
                    ShowAtWorldPosition(
                        item.transform.position,
                        entry.message,
                        entry.fontSize,
                        entry.fontStyle,
                        entry.textColor
                    );
                }
                break;
            }
        }
    }

    private void ShowAtWorldPosition(Vector3 worldPos, string text, float fontSize, FontStyles fontStyle, Color color)
    {
        if (canvas == null || floatingTextPrefab == null) return;

        Vector3 finalWorldPos = worldPos + worldOffset;

        // Bounds Clamping (영역 가두기) — 월드 좌표 기준으로 먼저 클램프
        MapCamera mapCam = Object.FindFirstObjectByType<MapCamera>();
        Bounds? bounds = null;
        if (mapCam != null && mapCam.boundsCollider != null)
        {
            bounds = mapCam.boundsCollider.bounds;
            float clampedX = Mathf.Clamp(finalWorldPos.x, bounds.Value.min.x + edgePadding, bounds.Value.max.x - edgePadding);
            float clampedY = Mathf.Clamp(finalWorldPos.y, bounds.Value.min.y + edgePadding, bounds.Value.max.y - edgePadding);
            finalWorldPos = new Vector3(clampedX, clampedY, finalWorldPos.z);
        }

        // 캔버스 카메라 확보 (갱신)
        canvasCamera = canvas.worldCamera ? canvas.worldCamera : Camera.main;
        if (canvasCamera == null) canvasCamera = Camera.main;

        // 화면 위치 변환
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, finalWorldPos);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvasCamera, out Vector2 localPoint))
            return;

        GameObject go = Instantiate(floatingTextPrefab, canvas.transform);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = localPoint;

        // --- [텍스트 스타일 적용 부분] ---
        var tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = fontStyle;
            tmp.color = color;
        }

        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();

        // 결정: MapCamera의 AutoScaleFollowView(=AutoScaleOnly) 모드라면 월드 고정(추적) 동작으로 만들기
        if (mapCam != null && mapCam.autoScaleFollowView)
        {
            // Attach follow component so this UI will follow the world position until destroyed.
            var follow = go.AddComponent<FloatingTextFollow>();
            follow.Initialize(finalWorldPos, worldOffset, canvasRect, canvasCamera, edgePadding, bounds);
            // Start fade/destroy coroutine as usual
            StartCoroutine(FadeAndDestroyRoutine(go, cg));
        }
        else
        {
            // 기존 동작: 한 번 계산해서 고정된 화면 위치로 남김
            StartCoroutine(FadeAndDestroyRoutine(go, cg));
        }
    }

    private IEnumerator FadeAndDestroyRoutine(GameObject go, CanvasGroup cg)
    {
        float t = 0f;
        while (t < defaultFadeIn)
        {
            t += Time.deltaTime;
            cg.alpha = t / Mathf.Max(0.0001f, defaultFadeIn);
            yield return _waitForEndOfFrame;
        }
        cg.alpha = 1f;

        yield return GetWait(defaultHold);

        t = 0f;
        while (t < defaultFadeOut)
        {
            t += Time.deltaTime;
            cg.alpha = 1f - (t / Mathf.Max(0.0001f, defaultFadeOut));
            yield return _waitForEndOfFrame;
        }

        Destroy(go);
    }

    private WaitForSeconds GetWait(float time)
    {
        if (!_waitCache.TryGetValue(time, out var wait))
        {
            wait = new WaitForSeconds(time);
            _waitCache[time] = wait;
        }
        return wait;
    }

    // ----------------------------
    // FloatingTextFollow: spawned floating text가 월드 위치에 고정되도록 매 프레임 업데이트
    // ----------------------------
    [DisallowMultipleComponent]
    private class FloatingTextFollow : MonoBehaviour
    {
        private Vector3 worldPosition;
        private Vector3 worldOffset;
        private RectTransform canvasRect;
        private Camera canvasCamera;
        private float edgePadding;
        private Bounds? clampBounds;

        private RectTransform rt;

        public void Initialize(Vector3 worldPos, Vector3 offset, RectTransform canvasRect, Camera canvasCamera, float edgePadding, Bounds? clampBounds)
        {
            this.worldPosition = worldPos;
            this.worldOffset = offset;
            this.canvasRect = canvasRect;
            this.canvasCamera = canvasCamera;
            this.edgePadding = edgePadding;
            this.clampBounds = clampBounds;
            rt = GetComponent<RectTransform>();
            // ensure we update in LateUpdate so camera moved already
            enabled = true;
        }

        void LateUpdate()
        {
            if (rt == null) rt = GetComponent<RectTransform>();
            if (rt == null || canvasCamera == null || canvasRect == null) return;

            Vector3 finalWorld = worldPosition; // already includes initial offset/clamp

            // If clamp bounds provided, ensure finalWorld stays inside bounds (in case world pos changes externally)
            if (clampBounds.HasValue)
            {
                var b = clampBounds.Value;
                finalWorld.x = Mathf.Clamp(finalWorld.x, b.min.x + edgePadding, b.max.x - edgePadding);
                finalWorld.y = Mathf.Clamp(finalWorld.y, b.min.y + edgePadding, b.max.y - edgePadding);
            }

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, finalWorld);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvasCamera, out Vector2 localPoint))
            {
                rt.anchoredPosition = localPoint;
            }
        }
    }
}
