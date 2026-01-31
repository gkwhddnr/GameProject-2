using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class ItemCollector : MonoBehaviour
{
    [Header("수집 아이템")]
    public LayerMask itemLayerMask = 0;

    [Header("각 스테이지마다 보여질 DestinationPoint")]
    public GameObject[] nextPoints;
    private float nextPointFadeDuration = 0.8f;

    [Header("각 스테이지마다 보여질 UI 설정")]
    public BoxCollider2D[] stageBoundsArray;
    public string[] stageUITextMessages;

    [Header("UI")]
    public TextMeshProUGUI uiText;
    public Transform playerTransform;
    public GameObject navigationPointerPrefab;
    public Canvas uiCanvas;
    private bool showUIImmediatelyIfNoBounds = false;

    [Header("Item fade options")]
    public float itemFadeDuration = 0.6f;
    private bool fadeOutItems = true;
    private bool disableColliderDuringItemFade = true;
    private bool destroyItemAfterFade = true;

    [Header("연속으로 아이템 드러내기")]
    public bool revealItemsSequentially = true;
    private float itemFadeInDuration = 0.6f;
    private int initialVisibleCount = 1;
    private int subsequentRevealCount = 2;

    [Tooltip("플레이어가 다른 스테이지로 들어갈 때 collected와 수거 이력 초기화 여부")]
    private bool resetCollectedOnStageEnter = true;
    private float obstacleFadeDuration = 1.5f;

    // --- 내부 상태 유지 ---
    private int collected = 0;
    private HashSet<int> collectedInstanceIds = new HashSet<int>();
    private bool uiShown = false;
    private List<GameObject> itemsList = new List<GameObject>();
    private int nextHiddenIndex = 0;
    private Dictionary<SpriteRenderer, Color> origSpriteColors = new Dictionary<SpriteRenderer, Color>();
    private Dictionary<Renderer, Color> origRendererColors = new Dictionary<Renderer, Color>();
    private int totalRevealedCount = 0;
    private int currentStageIndex = -1;
    private List<GameObject>[] stageItemsMap = null;
    private List<GameObject> currentStageItems = new List<GameObject>();
    private int currentStageNextHiddenIndex = 0;
    private int currentStageTotalRevealedCount = 0;
    private int currentStageTotalItems = 0;
    private GameObject activeNavGO = null;

    private List<SpriteRenderer>[] nextPointsSprs;
    private List<CanvasGroup>[] nextPointsCanvasGroups;
    private List<Renderer>[] nextPointsRenderers;
    private List<GameObject>[] stageObstacleMap = null;

    private Dictionary<int, Collider2D[]> _colliderCache = new Dictionary<int, Collider2D[]>();

    // 레이어 인덱스 캐시
    private int keyLayerIndex = -1;
    private int lockLayerIndex = -1;

    void Start()
    {
        keyLayerIndex = LayerMask.NameToLayer("Key");
        lockLayerIndex = LayerMask.NameToLayer("Lock");

        InitializeNextPoints();
        BuildItemsList();

        // 장애물(레이어 == "Lock") 맵 구축
        BuildObstacleMap();

        var stageController = FindAnyObjectByType<MapCameraStageController>();
        if (stageController == null) Debug.LogError("MapCameraStageController를 씬에서 찾을 수 없습니다!");

        // 2. 내비게이션 생성 및 초기화
        if (navigationPointerPrefab != null && uiCanvas != null)
        {
            if (activeNavGO == null)
            {
                activeNavGO = Instantiate(navigationPointerPrefab, uiCanvas.transform);
                var nav = activeNavGO.GetComponent<NavigationPointer>();
                if (nav != null)
                {
                    nav.Initialize(playerTransform, uiCanvas, stageController);
                }
            }
        }

        if (revealItemsSequentially && itemsList.Count > 0)
        {
            if (stageBoundsArray == null || stageBoundsArray.Length == 0) HideItemsInitially_Global();
            else HideItemsNotInAnyStage();
        }

        if (stageBoundsArray != null && stageBoundsArray.Length > 0)
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
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _colliderCache.Clear(); // 메모리 정리
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HideUIInstant();
        currentStageIndex = -1;

        // 씬이 바뀌었으니 장애물 맵 재구성 필요
        BuildObstacleMap();
    }

    void Update()
    {
        if (stageBoundsArray != null && stageBoundsArray.Length > 0 && playerTransform != null)
        {
            int foundIndex = -1;
            Vector3 pos = playerTransform.position; // 매번 호출 최적화
            for (int i = 0; i < stageBoundsArray.Length; ++i)
            {
                var b = stageBoundsArray[i];
                if (b == null) continue;
                if (b.bounds.Contains(pos))
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
    }

    void InitializeNextPoints()
    {
        if (nextPoints == null || nextPoints.Length == 0) return;
        int stageCount = stageBoundsArray != null ? stageBoundsArray.Length : 0;
        nextPointsSprs = new List<SpriteRenderer>[stageCount];
        nextPointsCanvasGroups = new List<CanvasGroup>[stageCount];
        nextPointsRenderers = new List<Renderer>[stageCount];

        for (int i = 0; i < stageCount; i++)
        {
            if (nextPoints.Length > i && nextPoints[i] != null)
            {
                nextPointsSprs[i] = new List<SpriteRenderer>();
                nextPointsCanvasGroups[i] = new List<CanvasGroup>();
                nextPointsRenderers[i] = new List<Renderer>();
                CollectNextPointRenderers(nextPoints[i], nextPointsSprs[i], nextPointsCanvasGroups[i], nextPointsRenderers[i]);
                SetNextPointVisualAlpha(0f, nextPointsSprs[i], nextPointsCanvasGroups[i], nextPointsRenderers[i]);
                ToggleNextPointCollider(false, nextPoints[i]);
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
        nextHiddenIndex = Mathf.Clamp(initialVisibleCount, 0, itemsList.Count);
        totalRevealedCount = nextHiddenIndex;


        if (stageBoundsArray != null && stageBoundsArray.Length > 0)
        {
            stageItemsMap = new List<GameObject>[stageBoundsArray.Length];
            for (int i = 0; i < stageItemsMap.Length; ++i) stageItemsMap[i] = new List<GameObject>();

            foreach (var item in itemsList)
            {
                if (item == null) continue;
                Vector3 pos = item.transform.position;
                for (int i = 0; i < stageBoundsArray.Length; ++i)
                {
                    if (stageBoundsArray[i] != null && stageBoundsArray[i].bounds.Contains(pos))
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
        if (stageBoundsArray == null || stageBoundsArray.Length == 0) return;

        int stageCount = stageBoundsArray.Length;
        stageObstacleMap = new List<GameObject>[stageCount];
        for (int i = 0; i < stageCount; ++i) stageObstacleMap[i] = new List<GameObject>();

        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        HashSet<GameObject> processedParents = new HashSet<GameObject>(); // 중복 방지

        foreach (var root in roots)
        {
            // 씬 내의 모든 'Lock' 레이어 자식을 찾음
            Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
            foreach (var child in allChildren)
            {
                if (child.gameObject.layer == lockLayerIndex)
                {
                    GameObject obstacleParent = FindTargetObstacle(child.gameObject);

                    if (obstacleParent != null && !processedParents.Contains(obstacleParent))
                    {
                        Vector3 pos = obstacleParent.transform.position;
                        for (int i = 0; i < stageBoundsArray.Length; ++i)
                        {
                            if (stageBoundsArray[i] != null && stageBoundsArray[i].bounds.Contains(pos))
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

    GameObject FindTargetObstacle(GameObject child)
    {
        if (child.transform.parent == null) return child;
        return child.transform.parent.gameObject;
    }

    // 재귀적으로 트리를 돌면서 레이어가 lockLayerIndex인 GameObject들을 수집
    void RecursiveCollectLockedObjects(Transform t, List<GameObject> collector)
    {
        if (t == null) return;
        if (t.gameObject.layer == lockLayerIndex)
        {
            collector.Add(t.gameObject);
            // 주의: 같은 트리의 하위 노드들도 layer==lock이면 별도로 추가됩니다(의도적).
        }

        for (int i = 0; i < t.childCount; ++i)
            RecursiveCollectLockedObjects(t.GetChild(i), collector);
    }

    void RecursiveCollectItems(Transform t)
    {
        GameObject go = t.gameObject;
        if (IsItemObject(go)) itemsList.Add(go);
        for (int i = 0; i < t.childCount; ++i) RecursiveCollectItems(t.GetChild(i));
    }

    // --- 간단한 collider 캐시 ---
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

    void HideItemsInitially_Global() => HideItemsForList(itemsList, initialVisibleCount);

    void HideItemsNotInAnyStage()
    {
        HashSet<GameObject> anySet = new HashSet<GameObject>();
        if (stageItemsMap != null)
        {
            foreach (var list in stageItemsMap)
                if (list != null) foreach (var it in list) if (it != null) anySet.Add(it);
        }

        foreach (var item in itemsList)
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

    // --- 중복 코드 최적화 ---
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
        bool isStage = stageBoundsArray != null && stageBoundsArray.Length > 0;
        var list = isStage ? currentStageItems : itemsList;
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

        // 아이템 자체의 페이드 인 연출
        var sprs = item.GetComponentsInChildren<SpriteRenderer>(true);
        var rends = item.GetComponentsInChildren<Renderer>(true);
        var cols = GetCachedColliders(item);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / duration);
            UpdateItemAlpha(item, sprs, rends, a);
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
        // 1. 아이템 수집 체크 (기존 로직)
        if (other is BoxCollider2D poly && poly.isTrigger) TryCollect(other.gameObject);

        SpriteRotator rotator = other.GetComponent<SpriteRotator>();
        if (rotator != null && other.gameObject.CompareTag("NextPoint")) // 태그나 레이어로 구분
        {
            rotator.TriggerDisappear(); // 빠르게 회전하며 사라짐 실행
            SoundManager.Instance?.PlayDestination(); // 도착 사운드 재생
        }
    }
    void OnCollisionEnter2D(Collision2D collision) { TryCollect(collision.collider.gameObject); }
    public void CollectBy(GameObject item) => TryCollect(item);

    void TryCollect(GameObject candidate)
    {
        if (candidate == null) return;

        // 1. 키 판별 로직 (GameManager, Layer, Name 모두 체크)
        bool isKeyByGameManager = false;
        int matchedKeySlotIndex = -1;
        if (GameManager.Instance != null)
        {
            isKeyByGameManager = GameManager.Instance.IsKeySlotMatch(candidate, out matchedKeySlotIndex);
        }

        bool isItemByMask = IsItemObject(candidate);
        bool isKeyByLayer = (candidate.layer == keyLayerIndex);
        bool isKeyByName = string.Equals(candidate.name, "Key", StringComparison.OrdinalIgnoreCase);

        // 이 오브젝트가 '키'인지 최종 확인
        bool isAnyKey = isKeyByGameManager || isKeyByLayer || isKeyByName;

        // 만약 아이템도 아니고 키도 아니면 무시
        if (!isItemByMask && !isAnyKey) return;

        // 2. 중복 수집 방지
        int id = candidate.GetInstanceID();
        if (collectedInstanceIds.Contains(id)) return;

        collectedInstanceIds.Add(id);
        collected++;

        // 3. UI 및 공통 이펙트 처리
        if (FloatingTextSpawner.Instance != null) FloatingTextSpawner.Instance.ShowForCollectedItem(candidate);
        UpdateUI();
        SequentialRevealManager.Instance?.NotifyCollected(candidate);
        if (GameManager.Instance != null) GameManager.Instance.OnItemCollected(candidate);

        // 수집 즉시 콜라이더 비활성화 (다중 충돌 방지)
        if (disableColliderDuringItemFade) foreach (var col in GetCachedColliders(candidate)) col.enabled = false;

        // 4. [핵심] 키(Key) vs 일반 아이템 사운드 및 로직 분기
        if (isAnyKey)
        {
            SoundManager.Instance?.PlayKey();

            // GameManager 슬롯 소비
            if (isKeyByGameManager) GameManager.Instance.ConsumeKeySlot(matchedKeySlotIndex);

            // 스테이지 계산 및 락 해제 처리
            int keyStageIndex = GetStageIndexForPosition(candidate.transform.position);
            if (keyStageIndex < 0) keyStageIndex = currentStageIndex;
            HandleKeyCollected(candidate, keyStageIndex);
        }
        else SoundManager.Instance?.PlayCollect();
        

        // 5. 시각적 제거 연출 (SpriteRotator 또는 FadeOut)
        SpriteRotator rotator = candidate.GetComponent<SpriteRotator>();
        if (rotator != null)
        {
            rotator.TriggerDisappear(); // 회전하며 사라짐
            StartCoroutine(HandleStageComplete(rotator));
        }

        if (fadeOutItems) StartCoroutine(FadeOutItemRoutine(candidate));
        else
        {
            if (destroyItemAfterFade) Destroy(candidate);
            else candidate.SetActive(false);
        }

        // 6. 다음 아이템 노출 및 목적지 활성화 로직
        if (revealItemsSequentially)
        {
            int currentTotal = (stageBoundsArray != null && stageBoundsArray.Length > 0) ? currentStageTotalRevealedCount : totalRevealedCount;
            if (collected >= currentTotal) RevealNextHiddenBatch(subsequentRevealCount);
        }

        // 스테이지 모든 아이템 수집 시 목적지 오픈
        if (collected >= currentStageTotalItems) RevealNextPointForStage(currentStageIndex);
    }

    // KEY 수집 시 처리
    void HandleKeyCollected(GameObject key, int keyStageIndex)
    {
        // keyStageIndex는 키의 위치 기준으로 계산된 스테이지 인덱스.
        if (keyStageIndex < 0 || stageBoundsArray == null || stageObstacleMap == null) return;
        if (keyStageIndex >= stageObstacleMap.Length) return;

        var obstaclesInStage = stageObstacleMap[keyStageIndex];
        if (obstaclesInStage == null || obstaclesInStage.Count == 0) return;

        GameObject targetObstacle = null;
        float closestDistSqr = float.MaxValue;
        Vector3 keyPos = key.transform.position;

        // 리스트를 역순으로 순회 (이미 제거된 null/비활성 항목 정리)
        for (int i = obstaclesInStage.Count - 1; i >= 0; i--)
        {
            GameObject obst = obstaclesInStage[i];
            if (obst == null || !obst.activeInHierarchy)
            {
                obstaclesInStage.RemoveAt(i);
                continue;
            }

            float distSqr = (obst.transform.position - keyPos).sqrMagnitude;
            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                targetObstacle = obst;
            }
        }

        if (targetObstacle != null)
        {
            StartCoroutine(FadeOutObstacleRoutine(targetObstacle));

            obstaclesInStage.Remove(targetObstacle);
        }
    }

    IEnumerator FadeOutObstacleRoutine(GameObject obstacle)
    {
        if (obstacle == null) yield break;

        // 1. 물리 충돌 즉시 제거
        var cols = GetCachedColliders(obstacle);
        foreach (var c in cols) if (c) c.enabled = false;

        // 2. 하위의 모든 렌더러 수집 (부모+자식 모두 포함)
        var sprs = obstacle.GetComponentsInChildren<SpriteRenderer>(true);
        var rends = obstacle.GetComponentsInChildren<Renderer>(true);

        float elapsed = 0f;
        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
        int colorPropID = Shader.PropertyToID("_Color");

        while (elapsed < obstacleFadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (elapsed / obstacleFadeDuration));

            // SpriteRenderer 처리
            foreach (var s in sprs)
            {
                if (s == null) continue;
                Color c = s.color;
                c.a = alpha;
                s.color = c;
            }

            // 일반 MeshRenderer 처리 (부모의 머티리얼 포함)
            foreach (var r in rends)
            {
                if (r == null || r is SpriteRenderer) continue;
                r.GetPropertyBlock(propBlock);
                // 기존 색상을 유지하면서 알파만 조절
                Color currentC = r.sharedMaterial.HasProperty(colorPropID) ? r.sharedMaterial.color : Color.white;
                currentC.a = alpha;
                propBlock.SetColor(colorPropID, currentC);
                r.SetPropertyBlock(propBlock);
            }
            yield return null;
        }

        if (obstacle != null)
        {
            if (destroyItemAfterFade) Destroy(obstacle);
            else obstacle.SetActive(false);
        }
    }

    IEnumerator HandleStageComplete(SpriteRotator rotator){ yield return StartCoroutine(rotator.WaitForDisappear()); }

    IEnumerator FadeOutItemRoutine(GameObject target)
    {
        if (target == null) yield break;
        var sprs = target.GetComponentsInChildren<SpriteRenderer>(true);
        var rends = target.GetComponentsInChildren<Renderer>(true);
        var origColors = new List<Color>();
        foreach (var s in sprs) origColors.Add(s.color);

        float t = 0f;
        while (t < itemFadeDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (t / itemFadeDuration));
            for (int i = 0; i < sprs.Length; ++i)
            {
                if (sprs[i] == null) continue;
                Color c = sprs[i].color; c.a = origColors[i].a * alpha; sprs[i].color = c;
            }
            yield return null;
        }
        if (target != null) { if (destroyItemAfterFade) Destroy(target); else target.SetActive(false); }
    }

    void RevealNextPointForStage(int stageIndex)
    {
        if (nextPoints == null || stageIndex < 0 || stageIndex >= nextPoints.Length || nextPoints[stageIndex] == null) return;

        // 목적지 활성화(페이드 인 등)
        StartCoroutine(FadeInNextPointRoutine(nextPoints[stageIndex], nextPointsSprs[stageIndex], nextPointsCanvasGroups[stageIndex], nextPointsRenderers[stageIndex]));

        // 내비게이션 화살표가 없으면 생성
        if (navigationPointerPrefab != null)
        {
            if (uiCanvas == null)
                uiCanvas = (FloatingTextSpawner.Instance != null) ? FloatingTextSpawner.Instance.canvas : FindFirstObjectByType<Canvas>();

            if (activeNavGO == null) // 하나만 있으면 됨
            {
                activeNavGO = Instantiate(navigationPointerPrefab, uiCanvas.transform);
                var nav = activeNavGO.GetComponent<NavigationPointer>();
                var stageController = FindAnyObjectByType<MapCameraStageController>();

                // 타겟 안 넘겨줘도 됨! GameManager 바운드로 스스로 찾음.
                nav.Initialize(playerTransform, uiCanvas, stageController);
            }
        }
    }


    void RemoveActiveNavigationPointer()
    {
        if (activeNavGO != null)
        {
            Destroy(activeNavGO);
            activeNavGO = null;
        }
    }


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
        if (uiText != null)
        {
            if (currentStageIndex >= 0 && stageUITextMessages != null && currentStageIndex < stageUITextMessages.Length)
                uiText.text = ResolveUITextTemplate(stageUITextMessages[currentStageIndex]);
            else
                uiText.text = $"잃어버린 별 찾기: {collected} / {currentStageTotalItems}";
        }
    }

    string ResolveUITextTemplate(string template)
    {
        if (string.IsNullOrEmpty(template)) return $"잃어버린 별 찾기: {collected} / 1";
        return template.Replace("{collected}", collected.ToString()).Replace("{total}", currentStageTotalItems.ToString());
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

    public int GetInitialVisibleCount() { return initialVisibleCount; }
    public int GetSubsequentRevealCount() { return subsequentRevealCount; }

    // Helper: 주어진 월드 위치가 어느 stageBoundsArray 인덱스에 들어가는지 반환
    int GetStageIndexForPosition(Vector3 worldPos)
    {
        if (stageBoundsArray == null) return -1;
        for (int i = 0; i < stageBoundsArray.Length; ++i)
        {
            var b = stageBoundsArray[i];
            if (b == null) continue;
            if (b.bounds.Contains(worldPos)) return i;
        }
        return -1;
    }
}
