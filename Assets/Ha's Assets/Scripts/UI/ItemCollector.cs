using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class ItemCollector : MonoBehaviour
{
    // --- 최적화를 위한 내부 데이터 구조 ---
    private class ObjectDataCache
    {
        public SpriteRenderer[] sprs;
        public Renderer[] rends;
        public CanvasGroup[] cgs;
        public Collider2D[] colliders;
        public Color[] origSprColors;
        public Color[] origRendColors;
        public ParticleSystem[] particles;
    }

    [Serializable]
    public class StageItemSettings
    {
        public string stageName;
        public BoxCollider2D stageBounds;
        [TextArea(1, 3)]
        public string uiTextMessage = "잃어버린 별 찾기: {collected} / {total}";
        public GameObject nextPoint;

        [Header("스테이지별 연속 노출 설정")]
        public bool revealSequentially = true;
        public int initialVisibleCount = 1;
        public int subsequentRevealCount = 2;
    }

    [Header("스테이지별 통합 설정")]
    public StageItemSettings[] stageSettings;

    [Header("수집 아이템 설정")]
    public LayerMask itemLayerMask = 0;
    private float nextPointFadeDuration = 0.8f;

    [Header("UI 참조")]
    public TextMeshProUGUI uiText;
    public Transform playerTransform;
    public GameObject navigationPointerPrefab;
    public Canvas uiCanvas;
    private bool showUIImmediatelyIfNoBounds = false;

    [Tooltip("연속으로 아이템 드러내기")]
    private bool revealItemsSequentially = true;
    private float itemFadeInDuration = 0.6f;

    [Tooltip("아이템 단계 노출: 스테이지에 설정이 없을 때의 초기값")]
    private int defaultInitialVisibleCount = 1;

    [Tooltip("아이템 단계 노출: 스테이지에 설정이 없을 때의 후속 노출 수")]
    private int defaultSubsequentRevealCount = 1;

    [Tooltip("아이템 페이드 옵션")]
    private float itemFadeDuration = 0.6f;
    private bool fadeOutItems = true;
    private bool disableColliderDuringItemFade = true;
    private bool destroyItemAfterFade = true;

    [Tooltip("플레이어가 다른 스테이지로 들어갈 때 collected와 수거 이력 초기화 여부")]
    private bool resetCollectedOnStageEnter = true;
    private float obstacleFadeDuration = 1.5f;

    // --- 내부 상태 및 최적화 필드 ---
    private HashSet<int> collectedInstanceIds = new HashSet<int>();
    private Dictionary<int, ObjectDataCache> _dataCache = new Dictionary<int, ObjectDataCache>();
    private StringBuilder _sb = new StringBuilder(256);
    private MaterialPropertyBlock _mpb;
    private static readonly int _BaseColorID = Shader.PropertyToID("_Color");

    private bool uiShown = false;
    private int collected = 0;
    private int nextHiddenIndex = 0;
    private int totalRevealedCount = 0;
    private int currentStageNextHiddenIndex = 0;
    private int currentStageTotalRevealedCount = 0;
    private int currentStageTotalItems = 0;
    private GameObject activeNavGO = null;
    private StageBoundsUIUpdater uiUpdater;

    private List<SpriteRenderer>[] nextPointsSprs;
    private List<CanvasGroup>[] nextPointsCanvasGroups;
    private List<Renderer>[] nextPointsRenderers;
    private List<GameObject>[] stageObstacleMap = null;
    private List<GameObject>[] stageItemsMap = null;
    private List<GameObject> itemLayerItemsList = new List<GameObject>();
    private List<GameObject> currentStageItems = new List<GameObject>();
    private List<GameObject> itemsList = new List<GameObject>();

    private int keyLayerIndex = -1;
    private int lockLayerIndex = -1;
    private int itemLayerIndex = -1;
    private int currentStageIndex = -1;

    // --- 초기화 로직 ---

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        uiUpdater = FindAnyObjectByType<StageBoundsUIUpdater>();

        keyLayerIndex = LayerMask.NameToLayer("Key");
        lockLayerIndex = LayerMask.NameToLayer("Lock");
        itemLayerIndex = LayerMask.NameToLayer("Item");

        InitializeNextPoints();
        BuildItemsList();

        itemLayerItemsList.Clear();
        foreach (var it in itemsList)
        {
            if (it != null && it.layer == itemLayerIndex) itemLayerItemsList.Add(it);
        }

        nextHiddenIndex = Mathf.Clamp(defaultInitialVisibleCount, 0, itemLayerItemsList.Count);
        totalRevealedCount = nextHiddenIndex;

        BuildObstacleMap();

        var stageController = FindAnyObjectByType<MapCameraStageController>();
        InitializeNavigationPointer(stageController);

        if (revealItemsSequentially && itemsList.Count > 0)
        {
            if (stageSettings == null || stageSettings.Length == 0) HideItemsInitially_Global();
            else HideItemsNotInAnyStage();
        }

        InitializeUI();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy() { Cleanup(); }
    void OnDisable() { Cleanup(); }

    void Cleanup()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _dataCache.Clear();
        RemoveActiveNavigationPointer();
        itemsList.Clear();
        itemLayerItemsList.Clear();
        stageItemsMap = null;
        stageObstacleMap = null;
    }

    // --- 원본 함수 로직 유지 ---

    void InitializeNavigationPointer(MapCameraStageController stageController)
    {
        if (navigationPointerPrefab != null && uiCanvas != null && activeNavGO == null)
        {
            activeNavGO = Instantiate(navigationPointerPrefab, uiCanvas.transform);
            var nav = activeNavGO.GetComponent<NavigationPointer>();
            if (nav != null) nav.Initialize(playerTransform, uiCanvas, stageController);
        }
    }

    void InitializeUI()
    {
        if (stageSettings != null && stageSettings.Length > 0)
        {
            HideUIInstant();
            uiShown = false;
            currentStageIndex = -1;
        }
        else
        {
            if (showUIImmediatelyIfNoBounds)
            {
                ShowUIInstant();
                uiShown = true;
                currentStageIndex = -1;
                nextHiddenIndex = Mathf.Clamp(defaultInitialVisibleCount, 0, itemsList.Count);
                totalRevealedCount = nextHiddenIndex;
            }
            else
            {
                HideUIInstant();
                uiShown = false;
                currentStageIndex = -1;
            }
        }
        UpdateUI();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HideUIInstant();
        currentStageIndex = -1;
        BuildObstacleMap();
    }

    void Update()
    {
        if (stageSettings == null || stageSettings.Length == 0 || playerTransform == null) return;

        Vector3 playerPosition = playerTransform.position;
        int foundIndex = -1;

        for (int i = 0; i < stageSettings.Length; ++i)
        {
            var bounds = stageSettings[i].stageBounds;
            if (bounds != null && bounds.bounds.Contains(playerPosition))
            {
                foundIndex = i;
                break;
            }
        }

        if (foundIndex != -1)
        {
            if (!uiShown || currentStageIndex != foundIndex)
            {
                if (currentStageIndex != foundIndex && resetCollectedOnStageEnter) ResetCollectedForNewStage(foundIndex);
                currentStageIndex = foundIndex;
                ShowUI();
            }
        }
        else if (uiShown)
        {
            HideUIInstant();
            uiShown = false;
            currentStageIndex = -1;
        }
    }

    void InitializeNextPoints()
    {
        if (stageSettings == null || stageSettings.Length == 0) return;
        int stageCount = stageSettings.Length;
        nextPointsSprs = new List<SpriteRenderer>[stageCount];
        nextPointsCanvasGroups = new List<CanvasGroup>[stageCount];
        nextPointsRenderers = new List<Renderer>[stageCount];

        for (int i = 0; i < stageCount; i++)
        {
            GameObject nPoint = stageSettings[i].nextPoint;
            if (nPoint != null)
            {
                nextPointsSprs[i] = new List<SpriteRenderer>();
                nextPointsCanvasGroups[i] = new List<CanvasGroup>();
                nextPointsRenderers[i] = new List<Renderer>();
                CollectNextPointRenderers(nPoint, nextPointsSprs[i], nextPointsCanvasGroups[i], nextPointsRenderers[i]);
                SetNextPointVisualAlpha(0f, nextPointsSprs[i], nextPointsCanvasGroups[i], nextPointsRenderers[i]);
                ToggleNextPointCollider(false, nPoint);
            }
        }
    }

    void ResetCollectedForNewStage(int newStageIndex)
    {
        collected = 0;
        collectedInstanceIds.Clear();
        currentStageItems.Clear();

        if (stageItemsMap != null && newStageIndex >= 0 && newStageIndex < stageItemsMap.Length)
        {
            var list = stageItemsMap[newStageIndex];
            if (list != null) currentStageItems.AddRange(list);
        }

        currentStageTotalItems = currentStageItems.Count;

        int iniVisible = defaultInitialVisibleCount;
        bool stageUsesSequential = revealItemsSequentially;
        if (stageSettings != null && newStageIndex >= 0 && newStageIndex < stageSettings.Length)
        {
            iniVisible = Mathf.Clamp(stageSettings[newStageIndex].initialVisibleCount, 0, currentStageTotalItems);
            stageUsesSequential = stageSettings[newStageIndex].revealSequentially;
        }

        currentStageNextHiddenIndex = Mathf.Clamp(iniVisible, 0, currentStageTotalItems);
        currentStageTotalRevealedCount = currentStageNextHiddenIndex;

        if (stageUsesSequential && currentStageItems.Count > 0)
            HideItemsForList(currentStageItems, iniVisible);

        UpdateUI();
        RemoveActiveNavigationPointer();
    }

    void BuildItemsList()
    {
        itemsList.Clear();
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots) RecursiveCollectItems(root.transform);

        if (defaultSubsequentRevealCount < 1) defaultSubsequentRevealCount = 1;

        if (stageSettings != null && stageSettings.Length > 0)
        {
            stageItemsMap = new List<GameObject>[stageSettings.Length];
            for (int i = 0; i < stageItemsMap.Length; ++i) stageItemsMap[i] = new List<GameObject>();

            foreach (var item in itemsList)
            {
                if (item == null || item.layer != itemLayerIndex) continue;

                Vector3 pos = item.transform.position;
                for (int i = 0; i < stageSettings.Length; ++i)
                {
                    if (stageSettings[i].stageBounds != null && stageSettings[i].stageBounds.bounds.Contains(pos))
                    {
                        stageItemsMap[i].Add(item);
                        break;
                    }
                }
            }
        }
    }

    void BuildObstacleMap()
    {
        if (stageSettings == null || stageSettings.Length == 0) return;

        int stageCount = stageSettings.Length;
        stageObstacleMap = new List<GameObject>[stageCount];
        for (int i = 0; i < stageCount; ++i) stageObstacleMap[i] = new List<GameObject>();

        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        HashSet<GameObject> processedParents = new HashSet<GameObject>();

        foreach (var root in roots)
        {
            Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
            foreach (var child in allChildren)
            {
                if (child.gameObject.layer == lockLayerIndex)
                {
                    GameObject obstacleParent = FindTargetObstacle(child.gameObject);
                    if (obstacleParent != null && !processedParents.Contains(obstacleParent))
                    {
                        Vector3 pos = obstacleParent.transform.position;
                        for (int i = 0; i < stageSettings.Length; ++i)
                        {
                            if (stageSettings[i].stageBounds != null && stageSettings[i].stageBounds.bounds.Contains(pos))
                            {
                                stageObstacleMap[i].Add(obstacleParent);
                                processedParents.Add(obstacleParent);
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    GameObject FindTargetObstacle(GameObject child) => (child.transform.parent == null) ? child : child.transform.parent.gameObject;

    void RecursiveCollectItems(Transform t)
    {
        if (IsItemObject(t.gameObject)) itemsList.Add(t.gameObject);
        for (int i = 0; i < t.childCount; ++i) RecursiveCollectItems(t.GetChild(i));
    }

    // --- 캐싱 시스템 ---

    private ObjectDataCache GetOrAddCache(GameObject go)
    {
        int id = go.GetInstanceID();
        if (_dataCache.TryGetValue(id, out var cache)) return cache;

        var sprs = go.GetComponentsInChildren<SpriteRenderer>(true);
        var rends = go.GetComponentsInChildren<Renderer>(true);
        var rendList = new List<Renderer>();
        var rendColors = new List<Color>();

        foreach (var r in rends)
        {
            if (r is SpriteRenderer) continue;
            rendList.Add(r);
            rendColors.Add(r.sharedMaterial.HasProperty(_BaseColorID) ? r.sharedMaterial.color : Color.white);
        }

        var sprColors = new Color[sprs.Length];
        for (int i = 0; i < sprs.Length; i++) sprColors[i] = sprs[i].color;

        cache = new ObjectDataCache
        {
            sprs = sprs,
            rends = rendList.ToArray(),
            cgs = go.GetComponentsInChildren<CanvasGroup>(true),
            colliders = go.GetComponentsInChildren<Collider2D>(true),
            origSprColors = sprColors,
            origRendColors = rendColors.ToArray(),
            particles = go.GetComponentsInChildren<ParticleSystem>(true)
        };
        _dataCache[id] = cache;
        return cache;
    }

    void HideItemsInitially_Global() => HideItemsForList(itemLayerItemsList, defaultInitialVisibleCount);

    void HideItemsNotInAnyStage()
    {
        HashSet<GameObject> anySet = new HashSet<GameObject>();
        if (stageItemsMap != null)
        {
            foreach (var list in stageItemsMap)
                if (list != null) foreach (var it in list) if (it != null) anySet.Add(it);
        }

        foreach (var item in itemLayerItemsList)
        {
            if (item == null || anySet.Contains(item)) continue;
            ApplyInitialHide(item);
        }
    }

    void HideItemsForList(List<GameObject> list, int visibleCount)
    {
        if (list == null) return;
        for (int i = 0; i < list.Count; ++i)
        {
            if (i < visibleCount) continue;
            ApplyInitialHide(list[i]);
        }
    }

    void ApplyInitialHide(GameObject item)
    {
        if (item == null) return;
        var cache = GetOrAddCache(item);
        UpdateItemAlphaInternal(cache, 0f);
        foreach (var col in cache.colliders) col.enabled = false;

        foreach (var ps in cache.particles)
        {
            if (ps != null)
            {
                ps.Stop();
                ps.Clear();
                ps.gameObject.SetActive(false);
            }
        }
    }

    void RevealNextHiddenBatch(int count)
    {
        bool isStageContext = stageSettings != null && stageSettings.Length > 0 && currentStageIndex >= 0;
        int revealCount = count;
        if (revealCount <= 0)
        {
            if (isStageContext) revealCount = Mathf.Max(1, stageSettings[currentStageIndex].subsequentRevealCount);
            else revealCount = Mathf.Max(1, defaultSubsequentRevealCount);
        }

        var list = isStageContext ? currentStageItems : itemLayerItemsList;
        int idx = isStageContext ? currentStageNextHiddenIndex : nextHiddenIndex;
        int revealed = 0;

        for (int i = 0; i < revealCount && idx < list.Count; ++i)
        {
            StartCoroutine(FadeInItemRoutine(list[idx], itemFadeInDuration));
            idx++; revealed++;
        }

        if (isStageContext) { currentStageNextHiddenIndex = idx; currentStageTotalRevealedCount += revealed; }
        else { nextHiddenIndex = idx; totalRevealedCount += revealed; }
    }

    IEnumerator FadeInItemRoutine(GameObject item, float duration)
    {
        if (item == null) yield break;
        item.SetActive(true);
        var cache = GetOrAddCache(item);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            UpdateItemAlphaInternal(cache, Mathf.Clamp01(t / duration));
            yield return null;
        }
        UpdateItemAlphaInternal(cache, 1f);
        foreach (var col in cache.colliders) col.enabled = true;
        foreach (var ps in cache.particles)
        {
            if (ps != null)
            {
                ps.gameObject.SetActive(true);
                ps.Play();
            }
        }
    }

    void UpdateItemAlphaInternal(ObjectDataCache cache, float alpha)
    {
        for (int i = 0; i < cache.sprs.Length; i++)
        {
            if (!cache.sprs[i]) continue;
            Color c = cache.origSprColors[i];
            c.a *= alpha;
            cache.sprs[i].color = c;
        }
        for (int i = 0; i < cache.rends.Length; i++)
        {
            if (!cache.rends[i]) continue;
            cache.rends[i].GetPropertyBlock(_mpb);
            Color c = cache.origRendColors[i];
            c.a *= alpha;
            _mpb.SetColor(_BaseColorID, c);
            cache.rends[i].SetPropertyBlock(_mpb);
        }
    }

    bool IsItemObject(GameObject go) => go != null && ((1 << go.layer) & itemLayerMask.value) != 0;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other is BoxCollider2D poly && poly.isTrigger) TryCollect(other.gameObject);
        SpriteRotator rotator = other.GetComponent<SpriteRotator>();
        if (rotator != null && other.gameObject.CompareTag("NextPoint"))
        {
            rotator.TriggerDisappear();
            SoundManager.Instance?.PlayDestination();
        }
    }

    void OnCollisionEnter2D(Collision2D collision) { TryCollect(collision.collider.gameObject); }

    void TryCollect(GameObject candidate)
    {
        if (candidate == null) return;
        bool isKeyByGameManager = false;
        int matchedKeySlotIndex = -1;
        if (GameManager.Instance != null) isKeyByGameManager = GameManager.Instance.IsKeySlotMatch(candidate, out matchedKeySlotIndex);

        bool isItemByMask = IsItemObject(candidate);
        bool isKeyByLayer = (candidate.layer == keyLayerIndex);
        bool isKeyByName = string.Equals(candidate.name, "Key", StringComparison.OrdinalIgnoreCase);
        bool isAnyKey = isKeyByGameManager || isKeyByLayer || isKeyByName;

        if (!isItemByMask && !isAnyKey) return;

        int id = candidate.GetInstanceID();
        if (collectedInstanceIds.Contains(id)) return;
        collectedInstanceIds.Add(id);

        bool isCountedAsItem = (candidate.layer == itemLayerIndex);
        if (isCountedAsItem) collected++;

        if (FloatingTextSpawner.Instance != null) FloatingTextSpawner.Instance.ShowForCollectedItem(candidate);
        UpdateUI();
        SequentialRevealManager.Instance?.NotifyCollected(candidate);
        if (GameManager.Instance != null) GameManager.Instance.OnItemCollected(candidate);

        var cache = GetOrAddCache(candidate);
        if (disableColliderDuringItemFade) foreach (var col in cache.colliders) col.enabled = false;

        if (isAnyKey)
        {
            SoundManager.Instance?.PlayKey();
            if (isKeyByGameManager) GameManager.Instance.ConsumeKeySlot(matchedKeySlotIndex);
            int keyStageIndex = GetStageIndexForPosition(candidate.transform.position);
            HandleKeyCollected(candidate, keyStageIndex >= 0 ? keyStageIndex : currentStageIndex);
        }
        else SoundManager.Instance?.PlayCollect();

        SpriteRotator rotator = candidate.GetComponent<SpriteRotator>();
        if (rotator != null) { rotator.TriggerDisappear(); StartCoroutine(HandleStageComplete(rotator)); }

        if (fadeOutItems) StartCoroutine(FadeOutItemRoutine(candidate));
        else { if (destroyItemAfterFade) Destroy(candidate); else candidate.SetActive(false); }

        if (isCountedAsItem)
        {
            if (revealItemsSequentially)
            {
                int currentTotal = (stageSettings != null && stageSettings.Length > 0) ? currentStageTotalRevealedCount : totalRevealedCount;
                if (collected >= currentTotal) RevealNextHiddenBatch(0);
            }
            if (collected >= currentStageTotalItems) RevealNextPointForStage(currentStageIndex);
        }
    }

    void HandleKeyCollected(GameObject key, int keyStageIndex)
    {
        Vector3 keyPos = key.transform.position;
        GameObject targetObstacle = null;
        float closestDistSqr = float.MaxValue;

        if (keyStageIndex >= 0 && stageObstacleMap != null && keyStageIndex < stageObstacleMap.Length)
        {
            var obstaclesInStage = stageObstacleMap[keyStageIndex];
            for (int i = obstaclesInStage.Count - 1; i >= 0; i--)
            {
                var obst = obstaclesInStage[i];
                if (obst == null || !obst.activeInHierarchy) continue;
                float distSqr = (obst.transform.position - keyPos).sqrMagnitude;
                if (distSqr < closestDistSqr) { closestDistSqr = distSqr; targetObstacle = obst; }
            }
        }

        if (targetObstacle == null)
        {
            GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in allObjects)
            {
                if (go.activeInHierarchy && (go.layer == lockLayerIndex || HasLockChild(go)))
                {
                    float distSqr = (go.transform.position - keyPos).sqrMagnitude;
                    if (distSqr < closestDistSqr) { closestDistSqr = distSqr; targetObstacle = go; }
                }
            }
        }

        if (targetObstacle != null)
        {
            StartCoroutine(FadeOutObstacleRoutine(targetObstacle));
            if (keyStageIndex >= 0 && stageObstacleMap[keyStageIndex].Contains(targetObstacle)) stageObstacleMap[keyStageIndex].Remove(targetObstacle);
        }
    }

    IEnumerator FadeOutObstacleRoutine(GameObject obstacle)
    {
        if (obstacle == null) yield break;
        var cache = GetOrAddCache(obstacle);
        foreach (var c in cache.colliders) if (c) c.enabled = false;

        float elapsed = 0f;
        while (elapsed < obstacleFadeDuration)
        {
            elapsed += Time.deltaTime;
            UpdateItemAlphaInternal(cache, Mathf.Clamp01(1f - (elapsed / obstacleFadeDuration)));
            yield return null;
        }
        if (obstacle != null) { if (destroyItemAfterFade) Destroy(obstacle); else obstacle.SetActive(false); }
    }

    IEnumerator HandleStageComplete(SpriteRotator rotator) { yield return StartCoroutine(rotator.WaitForDisappear()); }

    IEnumerator FadeOutItemRoutine(GameObject target)
    {
        if (target == null) yield break;
        var cache = GetOrAddCache(target);
        float t = 0f;
        while (t < itemFadeDuration)
        {
            t += Time.deltaTime;
            UpdateItemAlphaInternal(cache, Mathf.Clamp01(1f - (t / itemFadeDuration)));
            yield return null;
        }
        if (target != null) { if (destroyItemAfterFade) Destroy(target); else target.SetActive(false); }
    }

    void RevealNextPointForStage(int stageIndex)
    {
        if (stageSettings == null || stageIndex < 0 || stageIndex >= stageSettings.Length || stageSettings[stageIndex].nextPoint == null) return;

        GameObject nPoint = stageSettings[stageIndex].nextPoint;
        StartCoroutine(FadeInNextPointRoutine(nPoint, nextPointsSprs[stageIndex], nextPointsCanvasGroups[stageIndex], nextPointsRenderers[stageIndex]));

        if (navigationPointerPrefab != null && activeNavGO == null)
        {
            if (uiCanvas == null) uiCanvas = (FloatingTextSpawner.Instance != null) ? FloatingTextSpawner.Instance.canvas : FindFirstObjectByType<Canvas>();
            activeNavGO = Instantiate(navigationPointerPrefab, uiCanvas.transform);
            var nav = activeNavGO.GetComponent<NavigationPointer>();
            var sc = FindAnyObjectByType<MapCameraStageController>();
            nav.Initialize(playerTransform, uiCanvas, sc, nPoint.transform, 50f, 60f);
        }
    }

    void RemoveActiveNavigationPointer() { if (activeNavGO) { Destroy(activeNavGO); activeNavGO = null; } }

    IEnumerator FadeInNextPointRoutine(GameObject point, List<SpriteRenderer> sprs, List<CanvasGroup> canvasGroups, List<Renderer> renderers)
    {
        if (point == null) yield break;
        point.SetActive(true);
        float t = 0f;
        while (t < nextPointFadeDuration)
        {
            t += Time.deltaTime;
            SetNextPointVisualAlpha(t / nextPointFadeDuration, sprs, canvasGroups, renderers);
            yield return null;
        }
        SetNextPointVisualAlpha(1f, sprs, canvasGroups, renderers);
        ToggleNextPointCollider(true, point);
    }

    // --- UI 및 문자열 최적화 ---

    void UpdateUI()
    {
        if (uiText == null) return;

        if (currentStageIndex >= 0 && currentStageIndex < stageSettings.Length)
        {
            string template = stageSettings[currentStageIndex].uiTextMessage;
            uiText.text = ResolveUITextTemplate(template);
        }
        else
        {
            _sb.Clear();
            _sb.Append("잃어버린 별 찾기: ").Append(collected).Append(" / ").Append(currentStageTotalItems);
            uiText.text = _sb.ToString();
        }
    }

    private string GetDynamicStageName(int stageIdx)
    {
        if (stageIdx < 0 || stageIdx >= stageSettings.Length) return "알 수 없는 구역";

        var currentSettings = stageSettings[stageIdx];
        BoxCollider2D currentBounds = currentSettings.stageBounds;

        if (uiUpdater != null && uiUpdater.stageEntries != null)
        {
            foreach (var entry in uiUpdater.stageEntries)
            {
                if (entry.bounds == null) continue;
                foreach (var b in entry.bounds)
                {
                    if (b != null && b == currentBounds)
                    {
                        return !string.IsNullOrEmpty(entry.message) ? entry.message : currentSettings.stageName;
                    }
                }
            }
        }
        return currentSettings.stageName;
    }

    string ResolveUITextTemplate(string template)
    {
        if (string.IsNullOrEmpty(template)) template = "{stageName}: {collected} / {total}";
        string dynamicName = GetDynamicStageName(currentStageIndex);

        _sb.Clear();
        _sb.Append(template);
        _sb.Replace("{stageName}", dynamicName);
        _sb.Replace("{collected}", collected.ToString());
        _sb.Replace("{total}", currentStageTotalItems.ToString());

        return _sb.ToString();
    }

    void ShowUIInstant() { if (uiText) uiText.gameObject.SetActive(true); UpdateUI(); }
    void HideUIInstant() { if (uiText) uiText.gameObject.SetActive(false); }
    void ShowUI() { ShowUIInstant(); uiShown = true; }

    void CollectNextPointRenderers(GameObject go, List<SpriteRenderer> sprs, List<CanvasGroup> canvasGroups, List<Renderer> renderers)
    {
        sprs.AddRange(go.GetComponentsInChildren<SpriteRenderer>(true));
        canvasGroups.AddRange(go.GetComponentsInChildren<CanvasGroup>(true));
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) if (!(r is SpriteRenderer)) renderers.Add(r);
    }

    void SetNextPointVisualAlpha(float alpha, List<SpriteRenderer> sprs, List<CanvasGroup> canvasGroups, List<Renderer> renderers)
    {
        foreach (var s in sprs) { if (s) { Color c = s.color; c.a = alpha; s.color = c; } }
        foreach (var cg in canvasGroups) { if (cg) { cg.alpha = alpha; cg.interactable = cg.blocksRaycasts = alpha > 0.9f; } }
        foreach (var r in renderers)
        {
            if (r)
            {
                r.GetPropertyBlock(_mpb);
                Color c = r.sharedMaterial.HasProperty(_BaseColorID) ? r.sharedMaterial.color : Color.white;
                c.a = alpha;
                _mpb.SetColor(_BaseColorID, c);
                r.SetPropertyBlock(_mpb);
            }
        }
    }

    void ToggleNextPointCollider(bool enabled, GameObject point)
    {
        if (point)
        {
            var cache = GetOrAddCache(point);
            foreach (var col in cache.colliders) col.enabled = enabled;
        }
    }

    int GetStageIndexForPosition(Vector3 worldPos)
    {
        if (stageSettings == null) return -1;
        for (int i = 0; i < stageSettings.Length; ++i)
        {
            if (stageSettings[i].stageBounds != null && stageSettings[i].stageBounds.bounds.Contains(worldPos)) return i;
        }
        return -1;
    }

    bool HasLockChild(GameObject parent)
    {
        foreach (Transform child in parent.transform) if (child.gameObject.layer == lockLayerIndex) return true;
        return false;
    }

    public int GetInitialVisibleCount(int stageIndex)
    {
        if (stageSettings != null && stageIndex >= 0 && stageIndex < stageSettings.Length)
            return Mathf.Max(0, stageSettings[stageIndex].initialVisibleCount);
        return Mathf.Max(0, defaultInitialVisibleCount);
    }

    public int GetSubsequentRevealCount(int stageIndex)
    {
        if (stageSettings != null && stageIndex >= 0 && stageIndex < stageSettings.Length)
            return Mathf.Max(1, stageSettings[stageIndex].subsequentRevealCount);
        return Mathf.Max(1, defaultSubsequentRevealCount);
    }

    public bool GetRevealSequentially(int stageIndex)
    {
        if (stageSettings != null && stageIndex >= 0 && stageIndex < stageSettings.Length)
            return stageSettings[stageIndex].revealSequentially;
        return revealItemsSequentially;
    }

    public void CollectBy(GameObject item) => TryCollect(item);
}