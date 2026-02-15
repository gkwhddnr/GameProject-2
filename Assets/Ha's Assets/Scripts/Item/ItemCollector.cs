using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Strategy 패턴 적용 + 인터페이스 구현
/// </summary>
[DisallowMultipleComponent]
public class ItemCollector : MonoBehaviour,
    IStageDataProvider, IItemCollectionContext, IObstacleController
{
    #region Nested Classes

    private class ObjectDataCache
    {
        public SpriteRenderer[] sprs;
        public Renderer[] rends;
        public CanvasGroup[] cgs;
        public Collider2D[] colliders;
        public Color[] origSprColors;
        public Color[] origRendColors;
        public ParticleSystem[] particles;

        public void Clear()
        {
            sprs = null;
            rends = null;
            cgs = null;
            colliders = null;
            origSprColors = null;
            origRendColors = null;
            particles = null;
        }
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

    #endregion

    #region Inspector Fields

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

    #endregion

    #region Private Fields

    // ★ Strategy 리스트 (추가)
    private List<IItemCollectionStrategy> collectionStrategies;

    // 최적화된 내부 상태 필드
    private HashSet<int> collectedInstanceIds = new HashSet<int>(32);
    private Dictionary<int, ObjectDataCache> _dataCache = new Dictionary<int, ObjectDataCache>(64);
    private StringBuilder _sb = new StringBuilder(256);
    private MaterialPropertyBlock _mpb;
    private static readonly int _BaseColorID = Shader.PropertyToID("_Color");

    // 자주 사용되는 컴포넌트 배열 재사용
    private static readonly List<SpriteRenderer> _tempSprList = new List<SpriteRenderer>(16);
    private static readonly List<Renderer> _tempRendList = new List<Renderer>(16);
    private static readonly List<Color> _tempColorList = new List<Color>(16);

    private bool uiShown = false;
    private int collected = 0;
    private int nextHiddenIndex = 0;
    private int totalRevealedCount = 0;
    private int currentStageNextHiddenIndex = 0;
    private int currentStageTotalRevealedCount = 0;
    private int currentStageTotalItems = 0;
    private GameObject activeNavGO = null;
    private Sprite cachedUnlockSprite = null;
    private StageBoundsUIUpdater uiUpdater;

    // 배열 대신 리스트로 통일
    private List<SpriteRenderer>[] nextPointsSprs;
    private List<CanvasGroup>[] nextPointsCanvasGroups;
    private List<Renderer>[] nextPointsRenderers;
    private List<ParticleSystem>[] nextPointsParticles;
    private List<GameObject>[] stageObstacleMap = null;
    private List<GameObject>[] stageItemsMap = null;

    // 리스트 용량 최적화
    private List<GameObject> itemLayerItemsList = new List<GameObject>(32);
    private List<GameObject> currentStageItems = new List<GameObject>(32);
    private List<GameObject> itemsList = new List<GameObject>(64);

    private int keyLayerIndex = -1;
    private int lockLayerIndex = -1;
    private int itemLayerIndex = -1;
    private int currentStageIndex = -1;

    // 코루틴 캐싱
    private WaitForEndOfFrame _waitForEndOfFrame;
    private Dictionary<float, WaitForSeconds> _waitForSecondsCache = new Dictionary<float, WaitForSeconds>(8);

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _waitForEndOfFrame = new WaitForEndOfFrame();
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
        itemLayerItemsList.Capacity = Mathf.Max(itemLayerItemsList.Capacity, itemsList.Count / 2);

        foreach (var it in itemsList)
            if (it != null && it.layer == itemLayerIndex) itemLayerItemsList.Add(it);

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
        CacheUnlockSprite();

        InitializeStrategies();
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
                if (currentStageIndex != foundIndex && resetCollectedOnStageEnter)
                    ResetCollectedForNewStage(foundIndex);
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

    void OnCollisionEnter2D(Collision2D collision){ TryCollect(collision.collider.gameObject); }

    void OnDestroy() { Cleanup(); }
    void OnDisable() { Cleanup(); }

    #endregion

    #region Strategy Initialization

    /// <summary>
    /// Strategy 패턴 초기화
    /// </summary>
    private void InitializeStrategies()
    {
        collectionStrategies = new List<IItemCollectionStrategy>
        {
            new StarItemStrategy(),
            new KeyItemStrategy(this), // this = IObstacleController
            new InventoryItemStrategy()
        };
    }

    #endregion

    #region Collection Logic (★ 수정됨 - Strategy 패턴 적용)

    /// <summary>
    /// Strategy 패턴 적용
    /// 기존 TryCollect 로직을 Strategy로 위임
    /// </summary>
    void TryCollect(GameObject candidate)
    {
        if (candidate == null) return;

        // 중복 수집 방지
        int id = candidate.GetInstanceID();
        if (collectedInstanceIds.Contains(id)) return;
        collectedInstanceIds.Add(id);

        // ★ Strategy 패턴 적용: 적절한 Strategy 찾아서 처리
        foreach (var strategy in collectionStrategies)
        {
            if (strategy.CanCollect(candidate))
            {
                strategy.Collect(candidate, this); // this = IItemCollectionContext
                return;
            }
        }
    }

    #endregion

    #region Initialization Methods (기존 유지)

    void CacheUnlockSprite()
    {
        SpriteRenderer[] allSprites = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (var sr in allSprites)
        {
            if (sr.sprite != null && sr.sprite.name.Contains("unlock"))
            {
                cachedUnlockSprite = sr.sprite;
                return;
            }
        }

        cachedUnlockSprite = Resources.Load<Sprite>("Sprites/unlock-256");
        if (cachedUnlockSprite != null) return;
    }

    void Cleanup()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        foreach (var cache in _dataCache.Values) cache.Clear();
        _dataCache.Clear();

        RemoveActiveNavigationPointer();

        itemsList.Clear();
        itemLayerItemsList.Clear();
        currentStageItems.Clear();

        stageItemsMap = null;
        stageObstacleMap = null;

        _waitForSecondsCache.Clear();
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

    void InitializeNextPoints()
    {
        if (stageSettings == null || stageSettings.Length == 0) return;
        int stageCount = stageSettings.Length;
        nextPointsSprs = new List<SpriteRenderer>[stageCount];
        nextPointsCanvasGroups = new List<CanvasGroup>[stageCount];
        nextPointsRenderers = new List<Renderer>[stageCount];
        nextPointsParticles = new List<ParticleSystem>[stageCount];

        for (int i = 0; i < stageCount; i++)
        {
            GameObject nPoint = stageSettings[i].nextPoint;
            if (nPoint != null)
            {
                nextPointsSprs[i] = new List<SpriteRenderer>(4);
                nextPointsCanvasGroups[i] = new List<CanvasGroup>(4);
                nextPointsRenderers[i] = new List<Renderer>(4);
                nextPointsParticles[i] = new List<ParticleSystem>(4);

                CollectNextPointRenderers(nPoint, nextPointsSprs[i], nextPointsCanvasGroups[i], nextPointsRenderers[i], nextPointsParticles[i]);
                SetNextPointVisualAlpha(0f, nextPointsSprs[i], nextPointsCanvasGroups[i], nextPointsRenderers[i]);
                ToggleNextPointCollider(false, nPoint);

                for (int j = 0; j < nextPointsParticles[i].Count; j++)
                {
                    var ps = nextPointsParticles[i][j];
                    if (ps != null)
                    {
                        ps.Stop();
                        ps.Clear();
                        ps.gameObject.SetActive(false);
                    }
                }
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
            if (list != null)
            {
                currentStageItems.Capacity = Mathf.Max(currentStageItems.Capacity, list.Count);
                currentStageItems.AddRange(list);
            }
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

        if (stageUsesSequential && currentStageItems.Count > 0) HideItemsForList(currentStageItems, iniVisible);

        UpdateUI();
        RemoveActiveNavigationPointer();
    }

    void BuildItemsList()
    {
        itemsList.Clear();
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots) RecursiveCollectItems(root.transform, includeInactive: true);

        if (defaultSubsequentRevealCount < 1) defaultSubsequentRevealCount = 1;

        if (stageSettings != null && stageSettings.Length > 0)
        {
            stageItemsMap = new List<GameObject>[stageSettings.Length];
            for (int i = 0; i < stageItemsMap.Length; ++i)
                stageItemsMap[i] = new List<GameObject>(16);

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
        for (int i = 0; i < stageCount; ++i) stageObstacleMap[i] = new List<GameObject>(8);

        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        HashSet<GameObject> processedParents = new HashSet<GameObject>(32);

        foreach (var root in roots)
        {
            Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
            for (int c = 0; c < allChildren.Length; c++)
            {
                var child = allChildren[c];
                if (child.gameObject.layer == lockLayerIndex)
                {
                    GameObject obstacleParent = FindTargetObstacle(child.gameObject);
                    if (obstacleParent != null && !processedParents.Contains(obstacleParent))
                    {
                        Vector3 pos = obstacleParent.transform.position;
                        for (int i = 0; i < stageSettings.Length; ++i)
                        {
                            if (stageSettings[i].stageBounds != null &&
                                stageSettings[i].stageBounds.bounds.Contains(pos))
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

    GameObject FindTargetObstacle(GameObject child){ return (child.transform.parent == null) ? child : child.transform.parent.gameObject; }

    void RecursiveCollectItems(Transform t, bool includeInactive)
    {
        if (includeInactive || t.gameObject.activeInHierarchy)
        {
            if (IsItemObject(t.gameObject)) itemsList.Add(t.gameObject);
        }
        int childCount = t.childCount;
        for (int i = 0; i < childCount; ++i) RecursiveCollectItems(t.GetChild(i), includeInactive);
    }

    #endregion

    #region Caching System (기존 유지)

    private ObjectDataCache GetOrAddCache(GameObject go)
    {
        int id = go.GetInstanceID();
        if (_dataCache.TryGetValue(id, out var cache)) return cache;

        _tempSprList.Clear();
        _tempRendList.Clear();
        _tempColorList.Clear();

        var sprs = go.GetComponentsInChildren<SpriteRenderer>(true);
        var rends = go.GetComponentsInChildren<Renderer>(true);

        foreach (var r in rends)
        {
            if (r is SpriteRenderer) continue;
            _tempRendList.Add(r);
            _tempColorList.Add(r.sharedMaterial.HasProperty(_BaseColorID) ?
                r.sharedMaterial.color : Color.white);
        }

        var sprColors = new Color[sprs.Length];
        for (int i = 0; i < sprs.Length; i++) sprColors[i] = sprs[i].color;

        cache = new ObjectDataCache
        {
            sprs = sprs,
            rends = _tempRendList.ToArray(),
            cgs = go.GetComponentsInChildren<CanvasGroup>(true),
            colliders = go.GetComponentsInChildren<Collider2D>(true),
            origSprColors = sprColors,
            origRendColors = _tempColorList.ToArray(),
            particles = go.GetComponentsInChildren<ParticleSystem>(true)
        };
        _dataCache[id] = cache;
        return cache;
    }

    #endregion

    #region Visibility Management (기존 유지)

    void HideItemsInitially_Global(){ HideItemsForList(itemLayerItemsList, defaultInitialVisibleCount); }

    void HideItemsNotInAnyStage()
    {
        HashSet<GameObject> anySet = new HashSet<GameObject>(itemsList.Count);
        if (stageItemsMap != null)
        {
            foreach (var list in stageItemsMap)
                if (list != null)
                    for (int i = 0; i < list.Count; i++)
                        if (list[i] != null) anySet.Add(list[i]);
        }

        for (int i = 0; i < itemLayerItemsList.Count; i++)
        {
            var item = itemLayerItemsList[i];
            if (item == null || anySet.Contains(item)) continue;
            ApplyInitialHide(item);
        }
    }

    void HideItemsForList(List<GameObject> list, int visibleCount)
    {
        if (list == null) return;
        for (int i = visibleCount; i < list.Count; ++i) ApplyInitialHide(list[i]);
    }

    void ApplyInitialHide(GameObject item)
    {
        if (item == null) return;
        var cache = GetOrAddCache(item);
        UpdateItemAlphaInternal(cache, 0f);

        for (int i = 0; i < cache.colliders.Length; i++)
            if (cache.colliders[i]) cache.colliders[i].enabled = false;

        for (int i = 0; i < cache.particles.Length; i++)
        {
            var ps = cache.particles[i];
            if (ps != null)
            {
                ps.Stop();
                ps.Clear();
                ps.gameObject.SetActive(false);
            }
        }

        item.SetActive(false);
    }

    public void RevealNextHiddenBatch(int count)
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

        if (isStageContext)
        {
            currentStageNextHiddenIndex = idx;
            currentStageTotalRevealedCount += revealed;
        }
        else
        {
            nextHiddenIndex = idx;
            totalRevealedCount += revealed;
        }
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

        for (int i = 0; i < cache.colliders.Length; i++)
            if (cache.colliders[i]) cache.colliders[i].enabled = true;
        for (int i = 0; i < cache.particles.Length; i++)
        {
            var ps = cache.particles[i];
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

    #endregion

    #region Obstacle Management (기존 유지 - IObstacleController 구현)

    public void HandleKeyCollected(GameObject key, int keyStageIndex)
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
                if (distSqr < closestDistSqr)
                {
                    closestDistSqr = distSqr;
                    targetObstacle = obst;
                }
            }
        }

        if (targetObstacle == null)
        {
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            for (int i = 0; i < allObjects.Length; ++i)
            {
                var go = allObjects[i];
                if (go == null || !go.activeInHierarchy) continue;

                GameObject candidateParent = null;

                if (HasLockChild(go)) candidateParent = go;
                else if (go.layer == lockLayerIndex) candidateParent = FindTargetObstacle(go);
                else continue;

                if (candidateParent == null || !candidateParent.activeInHierarchy) continue;

                float distSqr = (candidateParent.transform.position - keyPos).sqrMagnitude;
                if (distSqr < closestDistSqr)
                {
                    closestDistSqr = distSqr;
                    targetObstacle = candidateParent;
                }
            }
        }

        if (targetObstacle != null)
        {
            if (keyStageIndex >= 0 && stageObstacleMap != null && keyStageIndex < stageObstacleMap.Length)
                if (stageObstacleMap[keyStageIndex].Contains(targetObstacle)) stageObstacleMap[keyStageIndex].Remove(targetObstacle);

                else if (stageObstacleMap != null)
                {
                    for (int si = 0; si < stageObstacleMap.Length; ++si)
                    {
                        var list = stageObstacleMap[si];
                        if (list != null && list.Contains(targetObstacle)) list.Remove(targetObstacle);
                    }
                }

            ChangeLockSpriteToUnlock(targetObstacle);
            StartCoroutine(FadeOutObstacleRoutine(targetObstacle));
        }
    }

    void ChangeLockSpriteToUnlock(GameObject obstacle)
    {
        if (obstacle == null) return;
        Transform[] allChildren = obstacle.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in allChildren)
        {
            if (child.gameObject.layer == lockLayerIndex)
            {
                SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    string currentSpriteName = sr.sprite.name;
                    string unlockSpriteName = GetUnlockSpriteName(currentSpriteName);
                    Sprite unlockSprite = LoadUnlockSprite(unlockSpriteName);

                    if (unlockSprite != null) sr.sprite = unlockSprite;
                }
                break;
            }
        }
    }

    string GetUnlockSpriteName(string currentName)
    {
        if (currentName.StartsWith("lock", StringComparison.OrdinalIgnoreCase)) return "unlock" + currentName.Substring(4);
        if (currentName.Contains("Lock")) return currentName.Replace("Lock", "Unlock");
        if (currentName.Contains("lock")) return currentName.Replace("lock", "unlock");

        return "unlock-" + currentName;
    }

    Sprite LoadUnlockSprite(string spriteName)
    {
        if (cachedUnlockSprite != null) return cachedUnlockSprite;

        SpriteRenderer[] allSprites = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (var sr in allSprites)
        {
            if (sr.sprite != null && sr.sprite.name == spriteName)
            {
                cachedUnlockSprite = sr.sprite;
                return sr.sprite;
            }
        }

        Sprite sprite = Resources.Load<Sprite>("Sprites/" + spriteName);
        if (sprite != null)
        {
            cachedUnlockSprite = sprite;
            return sprite;
        }

        sprite = Resources.Load<Sprite>(spriteName);
        if (sprite != null)
        {
            cachedUnlockSprite = sprite;
            return sprite;
        }

        return null;
    }

    IEnumerator FadeOutObstacleRoutine(GameObject obstacle)
    {
        if (obstacle == null) yield break;
        var cache = GetOrAddCache(obstacle);

        for (int i = 0; i < cache.colliders.Length; i++)
            if (cache.colliders[i]) cache.colliders[i].enabled = false;

        float elapsed = 0f;
        while (elapsed < obstacleFadeDuration)
        {
            elapsed += Time.deltaTime;
            UpdateItemAlphaInternal(cache, Mathf.Clamp01(1f - (elapsed / obstacleFadeDuration)));
            yield return null;
        }
        if (obstacle != null)
        {
            if (destroyItemAfterFade) Destroy(obstacle);
            else obstacle.SetActive(false);
        }
    }

    #endregion

    #region Fade Effects (기존 유지)

    IEnumerator HandleStageComplete(SpriteRotator rotator){ yield return StartCoroutine(rotator.WaitForDisappear()); }

    public IEnumerator FadeOutItemRoutine(GameObject target)
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
        if (target != null)
        {
            if (destroyItemAfterFade) Destroy(target);
            else target.SetActive(false);
        }
    }

    #endregion

    #region NextPoint Management (기존 유지)

    public void RevealNextPointForStage(int stageIndex)
    {
        if (stageSettings == null || stageIndex < 0 || stageIndex >= stageSettings.Length || stageSettings[stageIndex].nextPoint == null) return;

        GameObject nPoint = stageSettings[stageIndex].nextPoint;
        StartCoroutine(FadeInNextPointRoutine(nPoint, nextPointsSprs[stageIndex],
            nextPointsCanvasGroups[stageIndex], nextPointsRenderers[stageIndex], nextPointsParticles[stageIndex]));

        if (navigationPointerPrefab != null && activeNavGO == null)
        {
            if (uiCanvas == null)
                uiCanvas = (FloatingTextSpawner.Instance != null) ?
                    FloatingTextSpawner.Instance.canvas : FindFirstObjectByType<Canvas>();
            activeNavGO = Instantiate(navigationPointerPrefab, uiCanvas.transform);
            var nav = activeNavGO.GetComponent<NavigationPointer>();
            var sc = FindAnyObjectByType<MapCameraStageController>();
            nav.Initialize(playerTransform, uiCanvas, sc, nPoint.transform, 50f, 60f);
        }
    }

    void RemoveActiveNavigationPointer()
    {
        if (activeNavGO)
        {
            Destroy(activeNavGO);
            activeNavGO = null;
        }
    }

    IEnumerator FadeInNextPointRoutine(GameObject point, List<SpriteRenderer> sprs,
        List<CanvasGroup> canvasGroups, List<Renderer> renderers, List<ParticleSystem> particles)
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

        for (int i = 0; i < particles.Count; i++)
        {
            var ps = particles[i];
            if (ps != null)
            {
                ps.gameObject.SetActive(true);
                ps.Play();
            }
        }
    }

    void CollectNextPointRenderers(GameObject go, List<SpriteRenderer> sprs, List<CanvasGroup> canvasGroups,
        List<Renderer> renderers, List<ParticleSystem> particles)
    {
        sprs.AddRange(go.GetComponentsInChildren<SpriteRenderer>(true));
        canvasGroups.AddRange(go.GetComponentsInChildren<CanvasGroup>(true));

        var allRenderers = go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < allRenderers.Length; i++)
            if (!(allRenderers[i] is SpriteRenderer)) renderers.Add(allRenderers[i]);

        particles.AddRange(go.GetComponentsInChildren<ParticleSystem>(true));
    }

    void SetNextPointVisualAlpha(float alpha, List<SpriteRenderer> sprs,
        List<CanvasGroup> canvasGroups, List<Renderer> renderers)
    {
        for (int i = 0; i < sprs.Count; i++)
            if (sprs[i])
            {
                Color c = sprs[i].color;
                c.a = alpha;
                sprs[i].color = c;
            }

        for (int i = 0; i < canvasGroups.Count; i++)
        {
            if (canvasGroups[i])
            {
                canvasGroups[i].alpha = alpha;
                canvasGroups[i].interactable = canvasGroups[i].blocksRaycasts = alpha > 0.9f;
            }
        }

        for (int i = 0; i < renderers.Count; i++)
        {
            var r = renderers[i];
            if (r)
            {
                r.GetPropertyBlock(_mpb);
                Color c = r.sharedMaterial.HasProperty(_BaseColorID) ?
                    r.sharedMaterial.color : Color.white;
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
            for (int i = 0; i < cache.colliders.Length; i++)
                if (cache.colliders[i]) cache.colliders[i].enabled = enabled;
        }
    }

    #endregion

    #region UI Management (기존 유지)

    public void UpdateUI()
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
            for (int e = 0; e < uiUpdater.stageEntries.Length; e++)
            {
                var entry = uiUpdater.stageEntries[e];
                if (entry.bounds == null) continue;
                for (int b = 0; b < entry.bounds.Length; b++)
                    if (entry.bounds[b] != null && entry.bounds[b] == currentBounds)
                        return !string.IsNullOrEmpty(entry.message) ?
                            entry.message : currentSettings.stageName;
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

    void ShowUIInstant()
    {
        if (uiText) uiText.gameObject.SetActive(true);
        UpdateUI();
    }

    void HideUIInstant(){ if (uiText) uiText.gameObject.SetActive(false); }

    void ShowUI()
    {
        ShowUIInstant();
        uiShown = true;
    }

    #endregion

    #region Utility Methods 

    bool IsItemObject(GameObject go){ return go != null && ((1 << go.layer) & itemLayerMask.value) != 0; }

    int GetStageIndexForPosition(Vector3 worldPos)
    {
        if (stageSettings == null) return -1;
        for (int i = 0; i < stageSettings.Length; ++i)
            if (stageSettings[i].stageBounds != null &&
                stageSettings[i].stageBounds.bounds.Contains(worldPos)) return i;

        return -1;
    }

    bool HasLockChild(GameObject parent)
    {
        int childCount = parent.transform.childCount;
        for (int i = 0; i < childCount; i++)
            if (parent.transform.GetChild(i).gameObject.layer == lockLayerIndex) return true;

        return false;
    }

    #endregion

    #region Public API 

    public int GetInitialVisibleCount(int stageIndex)
    {
        if (stageSettings != null && stageIndex >= 0 && stageIndex < stageSettings.Length) return Mathf.Max(0, stageSettings[stageIndex].initialVisibleCount);
        return Mathf.Max(0, defaultInitialVisibleCount);
    }

    public int GetSubsequentRevealCount(int stageIndex)
    {
        if (stageSettings != null && stageIndex >= 0 && stageIndex < stageSettings.Length) return Mathf.Max(1, stageSettings[stageIndex].subsequentRevealCount);
        return Mathf.Max(1, defaultSubsequentRevealCount);
    }

    public bool GetRevealSequentially(int stageIndex)
    {
        if (stageSettings != null && stageIndex >= 0 && stageIndex < stageSettings.Length) return stageSettings[stageIndex].revealSequentially;
        return revealItemsSequentially;
    }

    public void CollectBy(GameObject item) => TryCollect(item);

    public void FadeOutTarget(GameObject obstacle)
    {
        if (obstacle == null) return;
        StartCoroutine(FadeOutObstacleRoutine(obstacle));
    }

    #endregion

    #region Interface Implementations

    // ================================================================
    // IStageDataProvider 구현
    // ================================================================

    public int GetStageCount(){ return stageSettings != null ? stageSettings.Length : 0; }

    public BoxCollider2D GetStageBounds(int stageIndex)
    {
        if (stageSettings != null && stageIndex >= 0 && stageIndex < stageSettings.Length) return stageSettings[stageIndex].stageBounds;
        return null;
    }

    public int GetCurrentStageIndex(){ return currentStageIndex; }

    // ================================================================
    // IItemCollectionContext 구현
    // ================================================================

    public void IncrementCollectedCount(){ collected++; }

    public void PlaySound(string soundType)
    {
        if (soundType == "key") SoundManager.Instance?.PlayKey();
        else if (soundType == "collect") SoundManager.Instance?.PlayCollect();
        else if (soundType == "destination") SoundManager.Instance?.PlayDestination();
    }

    public void FadeOutItem(GameObject item)
    {
        if (item != null)
        {
            var cache = GetOrAddCache(item);

            if (disableColliderDuringItemFade)
            {
                for (int i = 0; i < cache.colliders.Length; i++)
                    if (cache.colliders[i]) cache.colliders[i].enabled = false;
            }

            if (fadeOutItems)
                StartCoroutine(FadeOutItemRoutine(item));
            else
            {
                if (destroyItemAfterFade) Destroy(item);
                else item.SetActive(false);
            }
        }
    }

    public void NotifyGameManager(GameObject item){ GameManager.Instance?.OnItemCollected(item); }

    public void NotifySequentialReveal(GameObject item){ SequentialRevealManager.Instance?.NotifyCollected(item); }

    public void ShowFloatingText(GameObject item) { FloatingTextSpawner.Instance?.ShowForCollectedItem(item); }

    object IItemCollectionContext.GetOrAddCache(GameObject item){ return GetOrAddCache(item); }

    void IItemCollectionContext.RevealNextHiddenBatch()
    {
        if (revealItemsSequentially)
        {
            int currentTotal = (stageSettings != null && stageSettings.Length > 0)
                ? currentStageTotalRevealedCount : totalRevealedCount;
            if (collected >= currentTotal)
                RevealNextHiddenBatch(0);
        }
    }

    public void CheckStageCompletion(){ if (collected >= currentStageTotalItems) RevealNextPointForStage(currentStageIndex); }

    // ================================================================
    // IObstacleController 구현
    // ================================================================

    void IObstacleController.FadeOutObstacle(GameObject obstacle){ if (obstacle != null) StartCoroutine(FadeOutObstacleRoutine(obstacle)); }

    #endregion
}