using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class ItemCollector : MonoBehaviour
{
    [Header("Item detection")]
    public string itemTag = "item";
    public LayerMask itemLayerMask = 0;
    public bool useTagOrLayer = true;

    [Header("Counts")]
    public int totalItems = 0;
    public bool countInactiveObjects = false;

    [Header("NextPoint to reveal")]
    public GameObject nextPoint;
    public float nextPointFadeDuration = 0.8f;

    [Header("UI")]
    public TextMeshProUGUI uiText;

    [Header("Behavior")]
    public bool destroyItemOnCollect = false;
    public bool hideNextPointOnStart = true;

    [Header("UI Show on Stage Enter")]
    public BoxCollider2D stageBounds;
    public Transform playerTransform;
    public string playerTag = "Player";
    public bool showUIImmediatelyIfNoBounds = false;

    [Header("Item fade options")]
    public bool fadeOutItems = true;
    public float itemFadeDuration = 0.6f;
    public bool disableColliderDuringItemFade = true;
    public bool destroyItemAfterFade = true;

    // Internal state
    private int collected = 0;
    private HashSet<int> collectedInstanceIds = new HashSet<int>();
    private List<SpriteRenderer> nextSprs = new List<SpriteRenderer>();
    private List<CanvasGroup> nextCanvasGroups = new List<CanvasGroup>();
    private List<Renderer> nextRenderers = new List<Renderer>();
    private bool uiShown = false;

    void Start()
    {
        if (totalItems <= 0)
            totalItems = CountItemsInScene();

        if (nextPoint != null)
        {
            CollectNextPointRenderers(nextPoint);
            if (hideNextPointOnStart)
            {
                SetNextPointVisualAlpha(0f);
                ToggleNextPointCollider(false);
                foreach (var cg in nextCanvasGroups)
                {
                    cg.alpha = 0f;
                    cg.interactable = false;
                    cg.blocksRaycasts = false;
                }
            }
        }

        if (playerTransform == null && !string.IsNullOrEmpty(playerTag))
        {
            var found = GameObject.FindWithTag(playerTag);
            if (found != null) playerTransform = found.transform;
        }

        if (stageBounds != null)
        {
            HideUIInstant();
            uiShown = false;
        }
        else
        {
            if (showUIImmediatelyIfNoBounds)
            {
                ShowUIInstant();
                uiShown = true;
            }
            else
            {
                HideUIInstant();
                uiShown = false;
            }
        }

        UpdateUI();

        // Register to sceneLoaded event to hide UI on scene change
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        // Unregister from sceneLoaded event to avoid memory leaks
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Hide UI when a new scene is loaded
        HideUIInstant();
    }

    void Update()
    {
        if (stageBounds != null && playerTransform != null)
        {
            if (stageBounds.bounds.Contains(playerTransform.position))
            {
                if (!uiShown) ShowUI();
            }
            else
            {
                if (uiShown)
                {
                    HideUIInstant();
                    uiShown = false;
                }
            }
        }
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
        if (IsItemObject(t.gameObject)) count++;
        for (int i = 0; i < t.childCount; ++i) count += RecursiveCountItems(t.GetChild(i));
        return count;
    }

    bool IsItemObject(GameObject go)
    {
        if (go == null) return false;
        if (useTagOrLayer && !string.IsNullOrEmpty(itemTag) && go.CompareTag(itemTag)) return true;
        if (((1 << go.layer) & itemLayerMask.value) != 0) return true;
        return false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other is BoxCollider2D polygonCollider && polygonCollider.isTrigger) TryCollect(other.gameObject);
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
        if (candidate == null || !IsItemObject(candidate) || collectedInstanceIds.Contains(candidate.GetInstanceID())) return;

        collectedInstanceIds.Add(candidate.GetInstanceID());
        collected++;
        UpdateUI();

        if (disableColliderDuringItemFade)
        {
            foreach (var col in candidate.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
        }

        if (fadeOutItems)
        {
            StartCoroutine(FadeOutItemRoutine(candidate));
        }
        else
        {
            if (destroyItemOnCollect || destroyItemAfterFade) Destroy(candidate);
            else candidate.SetActive(false);
        }

        if (collected >= totalItems) RevealNextPoint();
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

    void UpdateUI()
    {
        if (uiText != null) uiText.text = $"잃어버린 별 찾기: {collected} / {totalItems}";
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

    void CollectNextPointRenderers(GameObject go)
    {
        nextSprs.Clear();
        nextCanvasGroups.Clear();
        nextRenderers.Clear();

        nextSprs.AddRange(go.GetComponentsInChildren<SpriteRenderer>(true));
        nextCanvasGroups.AddRange(go.GetComponentsInChildren<CanvasGroup>(true));

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is SpriteRenderer) continue;
            nextRenderers.Add(r);
        }
    }

    void SetNextPointVisualAlpha(float alpha)
    {
        foreach (var s in nextSprs)
        {
            var color = s.color;
            color.a = Mathf.Clamp01(alpha);
            s.color = color;
        }

        foreach (var cg in nextCanvasGroups)
        {
            cg.alpha = Mathf.Clamp01(alpha);
            cg.interactable = alpha > 0.9f;
            cg.blocksRaycasts = alpha > 0.9f;
        }

        foreach (var r in nextRenderers)
        {
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
            {
                var color = r.sharedMaterial.color;
                color.a = Mathf.Clamp01(alpha);
                r.sharedMaterial.color = color;
            }
        }
    }

    void ToggleNextPointCollider(bool enabled)
    {
        if (nextPoint == null) return;
        foreach (var col in nextPoint.GetComponentsInChildren<Collider2D>(true)) col.enabled = enabled;
    }

    IEnumerator FadeInNextPointRoutine()
    {
        if (nextPoint == null) yield break;
        if (!nextPoint.activeSelf) nextPoint.SetActive(true);
        if (nextSprs.Count == 0 && nextCanvasGroups.Count == 0 && nextRenderers.Count == 0)
            CollectNextPointRenderers(nextPoint);

        SetNextPointVisualAlpha(0f);
        float t = 0f;
        while (t < nextPointFadeDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / Mathf.Max(0.0001f, nextPointFadeDuration));
            SetNextPointVisualAlpha(alpha);
            yield return null;
        }
        SetNextPointVisualAlpha(1f);
        ToggleNextPointCollider(true);
    }

    void RevealNextPoint()
    {
        if (nextPoint == null) return;
        StopCoroutine(nameof(FadeInNextPointRoutine));
        StartCoroutine(FadeInNextPointRoutine());
    }
}
