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

    private List<SpriteRenderer>[] nextPointsSprs;
    private List<CanvasGroup>[] nextPointsCanvasGroups;
    private List<Renderer>[] nextPointsRenderers;

    // --- 최적화: 가비지 방지를 위한 캐싱 ---
    private Dictionary<int, Collider2D[]> _colliderCache = new Dictionary<int, Collider2D[]>();

    void Start()
    {
        InitializeNextPoints();
        BuildItemsList();

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
        int stageCount = stageBoundsArray.Length;
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

    void RecursiveCollectItems(Transform t)
    {
        GameObject go = t.gameObject;
        if (IsItemObject(go)) itemsList.Add(go);
        for (int i = 0; i < t.childCount; ++i) RecursiveCollectItems(t.GetChild(i));
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
        if (candidate == null || !IsItemObject(candidate)) return;
        int id = candidate.GetInstanceID();
        if (collectedInstanceIds.Contains(id)) return;

        collectedInstanceIds.Add(id);
        collected++;

        if (FloatingTextSpawner.Instance != null) FloatingTextSpawner.Instance.ShowForCollectedItem(candidate);

        UpdateUI();
        SequentialRevealManager.Instance?.NotifyCollected(candidate);

        if (GameManager.Instance != null) GameManager.Instance.OnItemCollected(candidate);
        if (disableColliderDuringItemFade) foreach (var col in GetCachedColliders(candidate)) col.enabled = false;

        if (fadeOutItems) StartCoroutine(FadeOutItemRoutine(candidate));
        else { if (destroyItemAfterFade) Destroy(candidate); else candidate.SetActive(false); }

        if (revealItemsSequentially)
        {
            int currentTotal = (stageBoundsArray != null && stageBoundsArray.Length > 0) ? currentStageTotalRevealedCount : totalRevealedCount;
            int maxCount = (stageBoundsArray != null && stageBoundsArray.Length > 0) ? currentStageItems.Count : itemsList.Count;
            if (collected >= currentTotal) RevealNextHiddenBatch(subsequentRevealCount);
        }
        if (collected >= currentStageTotalItems) RevealNextPointForStage(currentStageIndex);

        SpriteRotator rotator = candidate.GetComponent<SpriteRotator>();
        if (rotator != null){
            rotator.TriggerDisappear(); // 회전 애니메이션 시작
            StartCoroutine(HandleStageComplete(rotator));
        }

        else
        {
            if (fadeOutItems) StartCoroutine(FadeOutItemRoutine(candidate));
        }

        SoundManager.Instance?.PlayCollect();
    }

    IEnumerator HandleStageComplete(SpriteRotator rotator)
    {
        yield return StartCoroutine(rotator.WaitForDisappear());
    }

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
        StartCoroutine(FadeInNextPointRoutine(nextPoints[stageIndex], nextPointsSprs[stageIndex], nextPointsCanvasGroups[stageIndex], nextPointsRenderers[stageIndex]));
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

    public int GetInitialVisibleCount(){ return initialVisibleCount; }
    public int GetSubsequentRevealCount(){ return subsequentRevealCount; }

    void ToggleNextPointCollider(bool enabled, GameObject point) { if (point) foreach (var col in GetCachedColliders(point)) col.enabled = enabled; }
}