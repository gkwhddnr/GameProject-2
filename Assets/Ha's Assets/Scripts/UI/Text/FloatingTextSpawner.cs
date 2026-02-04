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
    public Vector3 worldOffset = new Vector3(0, 0.5f, 0);

    [Header("Item -> Message Table")]
    public ItemMessageEntry[] itemMessageEntries;

    private bool autoPopulateFromGameManager = true;
    private float edgePadding = 0.5f;
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

        // 시도적으로 즉시 채우기(하지만 GameManager 인스턴스가 아직 없을 수 있음)
        if (autoPopulateFromGameManager) TryPopulateFromGameManager();
    }

    void Start()
    {
        // Awake 시 GameManager가 없어서 실패했을 경우 재시도
        if (autoPopulateFromGameManager) TryPopulateFromGameManager();
    }

    /// <summary>
    /// 시도적으로 GameManager에서 slotPrefabs를 읽어 itemMessageEntries에 추가.
    /// 중복(참조 또는 이름 포함)은 건너뜀.
    /// </summary>
    private void TryPopulateFromGameManager()
    {
        // 이미 수동으로 동일한 레퍼런스가 있으면 아무 작업 안함
        if (!autoPopulateFromGameManager) return;

        // 우선 찾기: GameManager.Instance 우선, 없으면 씬에서 검색
        GameManager gm = GameManager.Instance;
        if (gm == null) gm = FindAnyObjectByType<GameManager>();
        if (gm == null || gm.itemSlotSettings == null || gm.itemSlotSettings.Length == 0) return;

        // 기존 entries를 리스트로 복사 (null 필터링)
        var existing = new List<ItemMessageEntry>();
        if (itemMessageEntries != null)
        {
            foreach (var e in itemMessageEntries)
                if (e != null && e.itemReference != null) existing.Add(e);
        }

        var existingRefs = new HashSet<GameObject>();
        foreach (var e in existing) existingRefs.Add(e.itemReference);

        var added = new List<ItemMessageEntry>(existing);

        // itemSlotSettings에서 slotPrefab을 읽어 중복이 없으면 추가
        foreach (var slot in gm.itemSlotSettings)
        {
            if (slot == null) continue;
            var prefab = slot.slotPrefab;
            if (prefab == null) continue;
            if (existingRefs.Contains(prefab)) continue;

            // 새 엔트리 생성 (기본 메시지/스타일 사용)
            var entry = new ItemMessageEntry
            {
                itemReference = prefab,
                fontSize = 36f,
                fontStyle = FontStyles.Normal,
                textColor = Color.white
            };

            added.Add(entry);
            existingRefs.Add(prefab);
        }

        // 길이가 바뀌었으면 덮어쓰기
        itemMessageEntries = added.ToArray();
    }

    public void ShowForCollectedItem(GameObject item)
    {
        if (item == null || itemMessageEntries == null) return;

        foreach (var entry in itemMessageEntries)
        {
            if (entry == null || entry.itemReference == null) continue;

            // 이름 기반 매칭: 기존 로직 유지 (포함 또는 포함당함)
            if (item.name.Contains(entry.itemReference.name) || entry.itemReference.name.Contains(item.name))
            {
                if (!string.IsNullOrEmpty(entry.message))
                {
                    ShowAtWorldPosition(
                        item.transform.position,
                        entry.message,
                        entry.fontSize,
                        entry.fontStyle,
                        entry.textColor
                    );
                }
                return; // 첫 매칭 항목만 사용
            }
        }

        // (Fallback) 만약 매칭되는 항목이 없고 GameManager 슬롯과 매칭되면 기본 메시지로 띄움
        GameManager gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        if (gm != null && gm.itemSlotSettings != null)
        {
            foreach (var slot in gm.itemSlotSettings)
            {
                if (slot == null || slot.slotPrefab == null) continue;
                if (item.name.Contains(slot.slotPrefab.name) || slot.slotPrefab.name.Contains(item.name)) return;
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
            enabled = true;
        }

        void LateUpdate()
        {
            if (rt == null) rt = GetComponent<RectTransform>();
            if (rt == null || canvasCamera == null || canvasRect == null) return;

            Vector3 finalWorld = worldPosition; // already includes initial offset/clamp

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
