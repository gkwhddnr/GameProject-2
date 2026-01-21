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
    public bool useTagOrLayer = true;

    [Header("각 스테이지마다 보여질 DestinationPoint")]
    [Tooltip("스테이지별로 드러낼 NextPoint를 설정하세요. StageBoundsArray와 인덱스가 일치해야 합니다.")]
    public GameObject[] nextPoints; 
    public float nextPointFadeDuration = 0.8f;

    [Header("각 스테이지마다 보여질 UI 설정")]
    [Tooltip("스테이지 경계들을 배열로 설정하세요. 플레이어가 어느 경계 안에 있느냐에 따라 다른 UI 텍스트를 보여줍니다.")]
    public BoxCollider2D[] stageBoundsArray;

    [Tooltip("각 스테이지(배열 인덱스)에 보여줄 문자열. {collected}와 {total} 토큰을 쓸 수 있습니다.")]
    public string[] stageUITextMessages;

    [Header("UI")]
    public TextMeshProUGUI uiText;

    public Transform playerTransform;
    public bool showUIImmediatelyIfNoBounds = false;

    [Header("Item fade options")]
    public bool fadeOutItems = true;
    public float itemFadeDuration = 0.6f;
    public bool disableColliderDuringItemFade = true;
    public bool destroyItemAfterFade = true;

    [Header("연속으로 아이템 드러내기")]
    [Tooltip("아이템들을 장면 계층 순서로 수집하고, 처음엔 initialVisibleCount만 보입니다. 이후에는 subsequentRevealCount만큼씩 드러납니다.")]
    public bool revealItemsSequentially = true;

    [Tooltip("처음에 보이게 할 아이템 개수 (0이면 처음엔 아무것도 보이지 않음)")]
    public int initialVisibleCount = 1;
    public float itemFadeInDuration = 0.6f;

    [Tooltip("다음 아이템을 몇 개씩 보여줄지 (initial 이후에 적용). 1이면 1개씩, 2면 2개씩 등.")]
    public int subsequentRevealCount = 1;
    public bool hideByAlphaAndDisableCollider = true;

    [Tooltip("플레이어가 다른 스테이지로 들어갈 때 collected와 수거 이력(collectedInstanceIds)을 초기화할지 여부")]
    private bool resetCollectedOnStageEnter = true;

    // Internal state
    private int collected = 0;
    private HashSet<int> collectedInstanceIds = new HashSet<int>();
    private bool uiShown = false;

    private List<GameObject> itemsList = new List<GameObject>();
    private int nextHiddenIndex = 0; // 기존 전역용(스테이지 비사용 시 fallback)

    private Dictionary<SpriteRenderer, Color> origSpriteColors = new Dictionary<SpriteRenderer, Color>();
    private Dictionary<Renderer, Color> origRendererColors = new Dictionary<Renderer, Color>();

    // 사용은 남겨두지만, 스테이지 사용 시에는 스테이지 전용 카운트 사용
    private int totalRevealedCount = 0;

    // 현재 플레이어가 속한 스테이지 인덱스 (-1이면 없음)
    private int currentStageIndex = -1;

    // 각 스테이지(배열 인덱스)에 포함되는 아이템들을 담는 맵
    private List<GameObject>[] stageItemsMap = null;

    // 현재 스테이지 전용 목록 및 인덱스/카운트
    private List<GameObject> currentStageItems = new List<GameObject>();
    private int currentStageNextHiddenIndex = 0;
    private int currentStageTotalRevealedCount = 0;
    private int currentStageTotalItems = 0;

    // 내부 상태
    private List<SpriteRenderer>[] nextPointsSprs; // 각 스테이지의 NextPoint SpriteRenderer
    private List<CanvasGroup>[] nextPointsCanvasGroups; // 각 스테이지의 NextPoint CanvasGroup
    private List<Renderer>[] nextPointsRenderers; // 각 스테이지의 NextPoint Renderer

    void Start()
    {
        // NextPoint 배열 초기화
        InitializeNextPoints();

        // build master item list AND stage->items 매핑
        BuildItemsList();

        // hide items initially except initialVisibleCount if revealItemsSequentially
        if (revealItemsSequentially && itemsList.Count > 0)
        {
            if (stageBoundsArray == null || stageBoundsArray.Length == 0)
            {
                HideItemsInitially_Global();
            }
            else
            {
                HideItemsNotInAnyStage();
            }
        }

        // UI show/hide logic based on stageBoundsArray
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
            for (int i = 0; i < stageBoundsArray.Length; ++i)
            {
                var b = stageBoundsArray[i];
                if (b == null) continue;
                if (b.bounds.Contains(playerTransform.position))
                {
                    foundIndex = i;
                    break;
                }
            }

            if (foundIndex != -1)
            {
                if (!uiShown || currentStageIndex != foundIndex)
                {
                    if (currentStageIndex != foundIndex)
                    {
                        if (resetCollectedOnStageEnter)
                        {
                            ResetCollectedForNewStage(foundIndex);
                        }
                    }

                    currentStageIndex = foundIndex;
                    ShowUI();
                }
            }
            else
            {
                if (uiShown)
                {
                    HideUIInstant();
                    uiShown = false;
                    currentStageIndex = -1;
                }
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
        currentStageNextHiddenIndex = 0;
        currentStageTotalRevealedCount = 0;
        currentStageTotalItems = 0;

        if (stageItemsMap != null && newStageIndex >= 0 && newStageIndex < stageItemsMap.Length)
        {
            var list = stageItemsMap[newStageIndex];
            if (list != null)
            {
                currentStageItems = new List<GameObject>(list);
            }
        }

        currentStageTotalItems = currentStageItems != null ? currentStageItems.Count : 0;
        currentStageNextHiddenIndex = Mathf.Clamp(initialVisibleCount, 0, currentStageTotalItems);
        currentStageTotalRevealedCount = currentStageNextHiddenIndex;

        if (revealItemsSequentially && currentStageItems != null && currentStageItems.Count > 0)
        {
            HideItemsForList(currentStageItems, initialVisibleCount);
        }

        UpdateUI();
    }

    void BuildItemsList()
    {
        itemsList.Clear();
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
            RecursiveCollectItems(root.transform);

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
                int assigned = -1;
                for (int i = 0; i < stageBoundsArray.Length; ++i)
                {
                    var b = stageBoundsArray[i];
                    if (b == null) continue;
                    if (b.bounds.Contains(pos))
                    {
                        assigned = i;
                        break;
                    }
                }
                if (assigned >= 0)
                    stageItemsMap[assigned].Add(item);
            }
        }
        else
        {
            stageItemsMap = null;
        }

        Debug.Log($"[ItemCollector] Collected {itemsList.Count} items (initialVisible={initialVisibleCount}, subsequentReveal={subsequentRevealCount}). totalRevealedCount(global)={totalRevealedCount}");
    }

    void RecursiveCollectItems(Transform t)
    {
        GameObject go = t.gameObject;
        if (IsItemObject(go))
        {
            itemsList.Add(go);
        }
        for (int i = 0; i < t.childCount; ++i) RecursiveCollectItems(t.GetChild(i));
    }

    void HideItemsInitially_Global()
    {
        for (int i = 0; i < itemsList.Count; ++i)
        {
            var item = itemsList[i];
            if (i < initialVisibleCount) continue;

            var sprs = item.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var s in sprs)
            {
                if (!origSpriteColors.ContainsKey(s))
                    origSpriteColors[s] = s.color;
                var c = s.color;
                c.a = 0f;
                s.color = c;
            }

            var rends = item.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (r is SpriteRenderer) continue;
                if (r == null) continue;
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
                {
                    if (!origRendererColors.ContainsKey(r))
                        origRendererColors[r] = r.sharedMaterial.color;
                    Color mc = r.sharedMaterial.color;
                    mc.a = 0f;
                    r.sharedMaterial.color = mc;
                }
            }

            foreach (var col in item.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
        }
    }

    void HideItemsNotInAnyStage()
    {
        HashSet<GameObject> anySet = new HashSet<GameObject>();
        if (stageItemsMap != null)
        {
            foreach (var list in stageItemsMap)
            {
                if (list == null) continue;
                foreach (var it in list) if (it != null) anySet.Add(it);
            }
        }

        foreach (var item in itemsList)
        {
            if (item == null) continue;
            if (anySet.Contains(item)) continue;

            var sprs = item.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var s in sprs)
            {
                if (!origSpriteColors.ContainsKey(s))
                    origSpriteColors[s] = s.color;
                var c = s.color;
                c.a = 0f;
                s.color = c;
            }
            foreach (var r in item.GetComponentsInChildren<Renderer>(true))
            {
                if (r is SpriteRenderer) continue;
                if (r == null) continue;
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
                {
                    if (!origRendererColors.ContainsKey(r))
                        origRendererColors[r] = r.sharedMaterial.color;
                    Color mc = r.sharedMaterial.color;
                    mc.a = 0f;
                    r.sharedMaterial.color = mc;
                }
            }
            foreach (var col in item.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
        }
    }

    void HideItemsForList(List<GameObject> list, int visibleCount)
    {
        if (list == null) return;
        for (int i = 0; i < list.Count; ++i)
        {
            var item = list[i];
            if (item == null) continue;
            if (i < visibleCount) continue;

            var sprs = item.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var s in sprs)
            {
                if (!origSpriteColors.ContainsKey(s))
                    origSpriteColors[s] = s.color;
                var c = s.color;
                c.a = 0f;
                s.color = c;
            }

            var rends = item.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (r is SpriteRenderer) continue;
                if (r == null) continue;
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
                {
                    if (!origRendererColors.ContainsKey(r))
                        origRendererColors[r] = r.sharedMaterial.color;
                    Color mc = r.sharedMaterial.color;
                    mc.a = 0f;
                    r.sharedMaterial.color = mc;
                }
            }

            foreach (var col in item.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
        }
    }

    void RevealNextHiddenBatch(int count)
    {
        if (!revealItemsSequentially) return;

        if (stageBoundsArray != null && stageBoundsArray.Length > 0)
        {
            if (currentStageItems == null || currentStageItems.Count == 0) return;
            if (currentStageNextHiddenIndex >= currentStageItems.Count) return;

            int toReveal = Mathf.Max(1, count);
            int revealed = 0;
            for (int i = 0; i < toReveal && currentStageNextHiddenIndex < currentStageItems.Count; ++i)
            {
                var item = currentStageItems[currentStageNextHiddenIndex];
                currentStageNextHiddenIndex++;
                revealed++;
                StartCoroutine(FadeInItemRoutine(item, itemFadeInDuration));
            }
            currentStageTotalRevealedCount += revealed;
        }
        else
        {
            if (nextHiddenIndex >= itemsList.Count) return;
            int toReveal = Mathf.Max(1, count);
            int revealed = 0;
            for (int i = 0; i < toReveal && nextHiddenIndex < itemsList.Count; ++i)
            {
                var item = itemsList[nextHiddenIndex];
                nextHiddenIndex++;
                revealed++;
                StartCoroutine(FadeInItemRoutine(item, itemFadeInDuration));
            }
            totalRevealedCount += revealed;
        }
    }

    IEnumerator FadeInItemRoutine(GameObject item, float duration)
    {
        if (item == null) yield break;

        if (!item.activeSelf) item.SetActive(true);

        var sprs = item.GetComponentsInChildren<SpriteRenderer>(true);
        var rends = item.GetComponentsInChildren<Renderer>(true);

        foreach (var s in sprs)
        {
            if (!origSpriteColors.ContainsKey(s)) origSpriteColors[s] = s.color;
            var c = s.color;
            c.a = 0f;
            s.color = c;
        }
        foreach (var r in rends)
        {
            if (r is SpriteRenderer) continue;
            if (r == null) continue;
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
            {
                if (!origRendererColors.ContainsKey(r)) origRendererColors[r] = r.sharedMaterial.color;
                Color mc = r.sharedMaterial.color;
                mc.a = 0f;
                r.sharedMaterial.color = mc;
            }
        }

        float t = 0f;
        float dur = Mathf.Max(0.0001f, duration);

        foreach (var col in item.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;

        while (t < dur)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / dur);
            for (int i = 0; i < sprs.Length; ++i)
            {
                var s = sprs[i];
                if (s == null) continue;
                Color orig = origSpriteColors.ContainsKey(s) ? origSpriteColors[s] : s.color;
                Color cc = s.color;
                cc.a = orig.a * a;
                s.color = cc;
            }

            foreach (var r in rends)
            {
                if (r is SpriteRenderer) continue;
                if (r == null) continue;
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
                {
                    Color orig = origRendererColors.ContainsKey(r) ? origRendererColors[r] : r.sharedMaterial.color;
                    Color mc = r.sharedMaterial.color;
                    mc.a = orig.a * a;
                    r.sharedMaterial.color = mc;
                }
            }

            yield return null;
        }

        foreach (var s in sprs)
        {
            if (s == null) continue;
            Color orig = origSpriteColors.ContainsKey(s) ? origSpriteColors[s] : s.color;
            Color cc = s.color;
            cc.a = orig.a;
            s.color = cc;
        }
        foreach (var r in rends)
        {
            if (r is SpriteRenderer) continue;
            if (r == null) continue;
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
            {
                Color orig = origRendererColors.ContainsKey(r) ? origRendererColors[r] : r.sharedMaterial.color;
                Color mc = r.sharedMaterial.color;
                mc.a = orig.a;
                r.sharedMaterial.color = mc;
            }
        }

        foreach (var col in item.GetComponentsInChildren<Collider2D>(true))
            col.enabled = true;
    }

    int CountItemsInScene()
    {
        int count = 0;
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots) count += RecursiveCountItems(root.transform);
        return count;
    }

    int RecursiveCountItems(Transform t)
    {
        int count = 0;
        GameObject go = t.gameObject;
        if (IsItemObject(go)) count++;
        for (int i = 0; i < t.childCount; ++i) count += RecursiveCountItems(t.GetChild(i));
        return count;
    }

    bool IsItemObject(GameObject go)
    {
        if (go == null) return false;
        return (((1 << go.layer) & itemLayerMask.value) != 0);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other is BoxCollider2D poly && poly.isTrigger) TryCollect(other.gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryCollect(collision.collider.gameObject);
    }

    public void CollectBy(GameObject item)
    {
        TryCollect(item);
    }

    void TryCollect(GameObject candidate)
    {
        if (candidate == null) return;
        if (!IsItemObject(candidate)) return;

        int id = candidate.GetInstanceID();
        if (collectedInstanceIds.Contains(id)) return;

        collectedInstanceIds.Add(id);
        collected++;
        UpdateUI();

        if (disableColliderDuringItemFade) foreach (var col in candidate.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;

        if (fadeOutItems) StartCoroutine(FadeOutItemRoutine(candidate));
        else
        {
            if (destroyItemAfterFade) Destroy(candidate);
            else candidate.SetActive(false);
        }

        if (revealItemsSequentially)
        {
            if (stageBoundsArray != null && stageBoundsArray.Length > 0)
            {
                if (collected >= currentStageTotalRevealedCount && currentStageNextHiddenIndex < currentStageItems.Count)
                {
                    RevealNextHiddenBatch(subsequentRevealCount);
                }
            }
            else
            {
                if (collected >= totalRevealedCount && nextHiddenIndex < itemsList.Count)
                {
                    RevealNextHiddenBatch(subsequentRevealCount);
                }
            }
        }

        if (collected >= currentStageTotalItems)
        {
            RevealNextPointForStage(currentStageIndex);
        }
    }

    void RevealNextPointForStage(int stageIndex)
    {
        if (nextPoints == null || stageIndex < 0 || stageIndex >= nextPoints.Length || nextPoints[stageIndex] == null) return;

        StopCoroutine(nameof(FadeInNextPointRoutine));
        StartCoroutine(FadeInNextPointRoutine(nextPoints[stageIndex], nextPointsSprs[stageIndex], nextPointsCanvasGroups[stageIndex], nextPointsRenderers[stageIndex]));
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
            float alpha = Mathf.Clamp01(1f - (t / Mathf.Max(0.0001f, itemFadeDuration)));

            for (int i = 0; i < sprs.Length; ++i)
            {
                if (sprs[i] == null) continue;
                var color = sprs[i].color;
                color.a = origColors[i].a * alpha;
                sprs[i].color = color;
            }

            foreach (var r in rends)
            {
                if (r is SpriteRenderer || r == null) continue;
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
                {
                    var color = r.sharedMaterial.color;
                    color.a = alpha;
                    r.sharedMaterial.color = color;
                }
            }
            yield return null;
        }

        foreach (var s in sprs)
        {
            if (s == null) continue;
            var color = s.color;
            color.a = 0f;
            s.color = color;
        }

        if (target != null)
        {
            if (destroyItemAfterFade) Destroy(target);
            else target.SetActive(false);
        }
    }

    IEnumerator FadeInNextPointRoutine(GameObject point, List<SpriteRenderer> sprs, List<CanvasGroup> canvasGroups, List<Renderer> renderers)
    {
        if (point == null) yield break;
        if (!point.activeSelf) point.SetActive(true);

        SetNextPointVisualAlpha(0f, sprs, canvasGroups, renderers);

        float t = 0f;
        while (t < nextPointFadeDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / Mathf.Max(0.0001f, nextPointFadeDuration));
            SetNextPointVisualAlpha(alpha, sprs, canvasGroups, renderers);
            yield return null;
        }
        SetNextPointVisualAlpha(1f, sprs, canvasGroups, renderers);
        ToggleNextPointCollider(true, point);
    }

    void UpdateUI()
    {
        if (uiText != null && currentStageIndex >= 0 && stageUITextMessages != null && currentStageIndex < stageUITextMessages.Length)
        {
            string template = stageUITextMessages[currentStageIndex];
            uiText.text = ResolveUITextTemplate(template);
        }
        else
        {
            if (uiText != null) uiText.text = $"잃어버린 별 찾기: {collected} / {currentStageTotalItems}";
        }
    }

    string ResolveUITextTemplate(string template)
    {
        if (string.IsNullOrEmpty(template)) return $"잃어버린 별 찾기: {collected} / 1";
        template = template.Replace("{collected}", collected.ToString());
        return template;
    }

    void ShowUI()
    {
        ShowUIInstant();
        uiShown = true;
    }

    void ShowUIInstant()
    {
        if (uiText != null) uiText.gameObject.SetActive(true);
        UpdateUI();
    }

    void HideUIInstant()
    {
        if (uiText != null) uiText.gameObject.SetActive(false);
    }

    void CollectNextPointRenderers(GameObject go, List<SpriteRenderer> sprs, List<CanvasGroup> canvasGroups, List<Renderer> renderers)
    {
        sprs.Clear();
        canvasGroups.Clear();
        renderers.Clear();

        sprs.AddRange(go.GetComponentsInChildren<SpriteRenderer>(true));
        canvasGroups.AddRange(go.GetComponentsInChildren<CanvasGroup>(true));

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is SpriteRenderer) continue;
            renderers.Add(r);
        }
    }

    void SetNextPointVisualAlpha(float alpha, List<SpriteRenderer> sprs, List<CanvasGroup> canvasGroups, List<Renderer> renderers)
    {
        foreach (var s in sprs)
        {
            var color = s.color;
            color.a = Mathf.Clamp01(alpha);
            s.color = color;
        }

        foreach (var cg in canvasGroups)
        {
            cg.alpha = Mathf.Clamp01(alpha);
            cg.interactable = alpha > 0.9f;
            cg.blocksRaycasts = alpha > 0.9f;
        }

        foreach (var r in renderers)
        {
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
            {
                var color = r.sharedMaterial.color;
                color.a = Mathf.Clamp01(alpha);
                r.sharedMaterial.color = color;
            }
        }
    }

    void ToggleNextPointCollider(bool enabled, GameObject point)
    {
        if (point == null) return;
        foreach (var col in point.GetComponentsInChildren<Collider2D>(true)) col.enabled = enabled;
    }
}
