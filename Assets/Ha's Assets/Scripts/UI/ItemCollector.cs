using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class ItemCollector : MonoBehaviour
{
    // --- 스테이지별 설정을 하나로 묶는 데이터 클래스 ---
    [Serializable]
    public class StageItemSettings
    {
        public string stageName; // 인스펙터 식별용 이름
        public BoxCollider2D stageBounds;
        [TextArea(1, 3)]
        public string uiTextMessage = "잃어버린 별 찾기: {collected} / {total}";
        public GameObject nextPoint;
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
    private int initialVisibleCount = 1;
    private int subsequentRevealCount = 2;

    [Tooltip("아이템 페이드 옵션")]
    private float itemFadeDuration = 0.6f;
    private bool fadeOutItems = true;
    private bool disableColliderDuringItemFade = true;
    private bool destroyItemAfterFade = true;

    [Tooltip("플레이어가 다른 스테이지로 들어갈 때 collected와 수거 이력 초기화 여부")]
    private bool resetCollectedOnStageEnter = true;
    private float obstacleFadeDuration = 1.5f;

    // --- 내부 상태 유지 ---
    private HashSet<int> collectedInstanceIds = new HashSet<int>();
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

    private Dictionary<int, Collider2D[]> _colliderCache = new Dictionary<int, Collider2D[]>();
    private Dictionary<SpriteRenderer, Color> origSpriteColors = new Dictionary<SpriteRenderer, Color>();
    private Dictionary<Renderer, Color> origRendererColors = new Dictionary<Renderer, Color>();

    private int keyLayerIndex = -1;
    private int lockLayerIndex = -1;
    private int itemLayerIndex = -1;
    private int currentStageIndex = -1;

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

        nextHiddenIndex = Mathf.Clamp(initialVisibleCount, 0, itemLayerItemsList.Count);
        totalRevealedCount = nextHiddenIndex;

        BuildObstacleMap();

        var stageController = FindAnyObjectByType<MapCameraStageController>();
        if (stageController == null) Debug.LogError("MapCameraStageController를 씬에서 찾을 수 없습니다!");

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
        _colliderCache.Clear();
        RemoveActiveNavigationPointer();
        itemsList.Clear();
        itemLayerItemsList.Clear();
        stageItemsMap = null;
        stageObstacleMap = null;
        origSpriteColors.Clear();
        origRendererColors.Clear();
    }

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
                nextHiddenIndex = Mathf.Clamp(initialVisibleCount, 0, itemsList.Count);
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
            if (bounds == null) continue;

            if (bounds.bounds.Contains(playerPosition))
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
        currentStageNextHiddenIndex = Mathf.Clamp(initialVisibleCount, 0, currentStageTotalItems);
        currentStageTotalRevealedCount = currentStageNextHiddenIndex;

        if (revealItemsSequentially && currentStageItems.Count > 0) HideItemsForList(currentStageItems, initialVisibleCount);

        UpdateUI();
        RemoveActiveNavigationPointer();
    }

    void BuildItemsList()
    {
        itemsList.Clear();
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots) RecursiveCollectItems(root.transform);

        if (subsequentRevealCount < 1) subsequentRevealCount = 1;

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

    Collider2D[] GetCachedColliders(GameObject go)
    {
        int id = go.GetInstanceID();
        if (!_colliderCache.TryGetValue(id, out var cols))
        {
            cols = go.GetComponentsInChildren<Collider2D>(true);
            _colliderCache[id] = cols;
        }
        return cols;
    }

    void HideItemsInitially_Global() => HideItemsForList(itemLayerItemsList, initialVisibleCount);

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
        var sprs = item.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var s in sprs)
        {
            if (!origSpriteColors.ContainsKey(s)) origSpriteColors[s] = s.color;
            Color c = s.color; c.a = 0f; s.color = c;
        }
        var rends = item.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends)
        {
            if (r is SpriteRenderer || r == null) continue;
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
            {
                if (!origRendererColors.ContainsKey(r)) origRendererColors[r] = r.sharedMaterial.color;
                Color mc = r.sharedMaterial.color; mc.a = 0f; r.sharedMaterial.color = mc;
            }
        }
        foreach (var col in GetCachedColliders(item)) col.enabled = false;
    }

    void RevealNextHiddenBatch(int count)
    {
        if (!revealItemsSequentially) return;
        bool isStage = stageSettings != null && stageSettings.Length > 0;
        var list = isStage ? currentStageItems : itemLayerItemsList;
        int idx = isStage ? currentStageNextHiddenIndex : nextHiddenIndex;
        int toReveal = Mathf.Max(1, count);
        int revealed = 0;

        for (int i = 0; i < toReveal && idx < list.Count; ++i)
        {
            StartCoroutine(FadeInItemRoutine(list[idx], itemFadeInDuration));
            idx++; revealed++;
        }

        if (isStage) { currentStageNextHiddenIndex = idx; currentStageTotalRevealedCount += revealed; }
        else { nextHiddenIndex = idx; totalRevealedCount += revealed; }
    }

    IEnumerator FadeInItemRoutine(GameObject item, float duration)
    {
        if (item == null) yield break;
        item.SetActive(true);
        var sprs = item.GetComponentsInChildren<SpriteRenderer>(true);
        var rends = item.GetComponentsInChildren<Renderer>(true);
        var cols = GetCachedColliders(item);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            UpdateItemAlpha(item, sprs, rends, Mathf.Clamp01(t / duration));
            yield return null;
        }
        UpdateItemAlpha(item, sprs, rends, 1f);
        foreach (var col in cols) col.enabled = true;
    }

    void UpdateItemAlpha(GameObject item, SpriteRenderer[] sprs, Renderer[] rends, float alpha)
    {
        foreach (var s in sprs)
        {
            if (s == null) continue;
            Color orig = origSpriteColors.ContainsKey(s) ? origSpriteColors[s] : s.color;
            Color c = s.color; c.a = orig.a * alpha; s.color = c;
        }
        foreach (var r in rends)
        {
            if (r == null || r is SpriteRenderer) continue;
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
            {
                Color orig = origRendererColors.ContainsKey(r) ? origRendererColors[r] : r.sharedMaterial.color;
                Color mc = r.sharedMaterial.color; mc.a = orig.a * alpha; r.sharedMaterial.color = mc;
            }
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

    // --- 수집 메인 로직 ---
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

        if (disableColliderDuringItemFade) foreach (var col in GetCachedColliders(candidate)) col.enabled = false;

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
                if (collected >= currentTotal) RevealNextHiddenBatch(subsequentRevealCount);
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
            foreach (var obst in obstaclesInStage)
            {
                if (obst == null || !obst.activeInHierarchy) continue;
                float distSqr = (obst.transform.position - keyPos).sqrMagnitude;
                if (distSqr < closestDistSqr) { closestDistSqr = distSqr; targetObstacle = obst; }
            }
        }

        if (targetObstacle == null)
        {
            foreach (var go in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
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
        foreach (var c in GetCachedColliders(obstacle)) if (c) c.enabled = false;
        var sprs = obstacle.GetComponentsInChildren<SpriteRenderer>(true);
        var rends = obstacle.GetComponentsInChildren<Renderer>(true);
        float elapsed = 0f;
        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
        int colorPropID = Shader.PropertyToID("_Color");

        while (elapsed < obstacleFadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (elapsed / obstacleFadeDuration));
            foreach (var s in sprs) { if (s) { Color c = s.color; c.a = alpha; s.color = c; } }
            foreach (var r in rends)
            {
                if (r == null || r is SpriteRenderer) continue;
                r.GetPropertyBlock(propBlock);
                Color currentC = r.sharedMaterial.HasProperty(colorPropID) ? r.sharedMaterial.color : Color.white;
                currentC.a = alpha; propBlock.SetColor(colorPropID, currentC); r.SetPropertyBlock(propBlock);
            }
            yield return null;
        }
        if (obstacle != null) { if (destroyItemAfterFade) Destroy(obstacle); else obstacle.SetActive(false); }
    }

    IEnumerator HandleStageComplete(SpriteRotator rotator) { yield return StartCoroutine(rotator.WaitForDisappear()); }

    IEnumerator FadeOutItemRoutine(GameObject target)
    {
        if (target == null) yield break;
        var sprs = target.GetComponentsInChildren<SpriteRenderer>(true);
        var origColors = new List<Color>();
        foreach (var s in sprs) origColors.Add(s.color);
        float t = 0f;
        while (t < itemFadeDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (t / itemFadeDuration));
            for (int i = 0; i < sprs.Length; ++i) { if (sprs[i]) { Color c = sprs[i].color; c.a = origColors[i].a * alpha; sprs[i].color = c; } }
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

    void UpdateUI()
    {
        if (uiText == null) return;

        if (currentStageIndex >= 0 && currentStageIndex < stageSettings.Length)
        {
            // 1. 현재 스테이지의 메시지 템플릿을 가져옴
            string template = stageSettings[currentStageIndex].uiTextMessage;

            // 2. 템플릿 안의 내용을 치환하여 최종 텍스트 결정
            uiText.text = ResolveUITextTemplate(template);
        }
        else
        {
            // 스테이지 외부일 때의 기본 표시
            uiText.text = $"잃어버린 별 찾기: {collected} / {currentStageTotalItems}";
        }
    }

    private string GetDynamicStageName(int stageIdx)
    {
        // 인덱스 범위 확인
        if (stageIdx < 0 || stageIdx >= stageSettings.Length) return "알 수 없는 구역";

        var currentSettings = stageSettings[stageIdx];
        BoxCollider2D currentBounds = currentSettings.stageBounds;

        // 1순위: StageBoundsUIUpdater에서 동일한 Bounds를 사용하는지 확인
        if (uiUpdater != null && uiUpdater.stageEntries != null)
        {
            foreach (var entry in uiUpdater.stageEntries)
            {
                if (entry.bounds == null) continue;

                // 리스트 내부를 순회하며 참조(Reference)가 같은 콜라이더인지 비교
                foreach (var b in entry.bounds)
                {
                    if (b != null && b == currentBounds)
                    {
                        // 일치하는 것을 찾았다면 UIUpdater의 message를 반환
                        // (만약 message가 비어있다면 stageName으로 대체)
                        return !string.IsNullOrEmpty(entry.message) ? entry.message : currentSettings.stageName;
                    }
                }
            }
        }

        // 2순위: 일치하는 Bounds가 없거나 UIUpdater가 없다면 
        // ItemCollector 인스펙터에서 직접 수정한 stageName을 반환
        return currentSettings.stageName;
    }

    string ResolveUITextTemplate(string template)
    {
        if (string.IsNullOrEmpty(template))
            template = "{stageName}: {collected} / {total}";

        // 여기서 GetDynamicStageName을 호출하여 우선순위에 따른 이름을 가져옵니다.
        string dynamicName = GetDynamicStageName(currentStageIndex);

        return template
            .Replace("{stageName}", dynamicName)
            .Replace("{collected}", collected.ToString())
            .Replace("{total}", currentStageTotalItems.ToString());
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
        foreach (var r in renderers) { if (r && r.sharedMaterial.HasProperty("_Color")) { Color c = r.sharedMaterial.color; c.a = alpha; r.sharedMaterial.color = c; } }
    }
    void ToggleNextPointCollider(bool enabled, GameObject point) { if (point) foreach (var col in GetCachedColliders(point)) col.enabled = enabled; }

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
    public int GetInitialVisibleCount() { return initialVisibleCount; }
    public int GetSubsequentRevealCount() { return subsequentRevealCount; }
    public void CollectBy(GameObject item) => TryCollect(item);
}