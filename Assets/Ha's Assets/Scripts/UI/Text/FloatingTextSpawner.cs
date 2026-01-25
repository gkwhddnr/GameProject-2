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

        // Bounds Clamping (영역 가두기)
        MapCamera mapCam = Object.FindFirstObjectByType<MapCamera>();
        if (mapCam != null && mapCam.boundsCollider != null)
        {
            Bounds b = mapCam.boundsCollider.bounds;
            float clampedX = Mathf.Clamp(finalWorldPos.x, b.min.x + edgePadding, b.max.x - edgePadding);
            float clampedY = Mathf.Clamp(finalWorldPos.y, b.min.y + edgePadding, b.max.y - edgePadding);
            finalWorldPos = new Vector3(clampedX, clampedY, finalWorldPos.z);
        }

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

        StartCoroutine(FadeAndDestroyRoutine(go, cg));
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
}