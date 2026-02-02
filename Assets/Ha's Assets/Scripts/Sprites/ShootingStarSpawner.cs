using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ShootingStarSpawner : MonoBehaviour
{
    [Header("Prefab & Pool (this spawner handles pooling)")]
    [Tooltip("ShootingStar 프리팹 (ShootingStar 컴포넌트 포함). 풀링 시 destroyAfterPlay는 false로 사용 권장.")]
    public GameObject shootingStarPrefab;
    [Tooltip("풀 초기 크기. 0이면 풀링 비활성(즉시 Instantiate 사용).")]
    public int poolSize = 6;
    [Tooltip("풀에서 더 가져올 때 추가 생성 허용 여부. false면 풀 고갈 시 스폰 건너뜀.")]
    public bool allowPoolGrow = false;

    [Header("Spawn timing")]
    public float spawnInterval = 1.0f;
    public float spawnIntervalRandomDelta = 0.5f;
    public bool playOnStart = true;

    [Header("Spawn area (world coords)")]
    public Vector2 startXRange = new Vector2(-10f, -8f);
    public Vector2 startYRange = new Vector2(2f, 4f);
    public Vector2 endXRange = new Vector2(4f, 8f);
    public Vector2 endYRange = new Vector2(-1f, 1f);

    [Header("Randomization ranges")]
    public Vector2 travelDurationRange = new Vector2(0.35f, 0.9f);
    public Vector2 trailTimeRange = new Vector2(0.5f, 1.0f);

    [Header("Limits")]
    [Tooltip("동시에 활성화 될 최대 개수 (0이면 poolSize가 상한)")]
    public int maxSimultaneous = 0;

    // internal
    private Queue<GameObject> pool = new Queue<GameObject>();
    private HashSet<GameObject> inUse = new HashSet<GameObject>();
    private Coroutine spawnCoroutine;

    void Awake()
    {
        if (shootingStarPrefab == null)
        {
            Debug.LogError("[ShootingStarSpawner] shootingStarPrefab not assigned.");
            enabled = false;
            return;
        }

        // 풀 초기화 (poolSize > 0 일 때)
        if (poolSize > 0)
        {
            for (int i = 0; i < poolSize; ++i)
            {
                var go = Instantiate(shootingStarPrefab, transform);
                go.SetActive(false);
                var ss = go.GetComponent<ShootingStar>();
                if (ss != null) ss.destroyAfterPlay = false;
                pool.Enqueue(go);
            }
        }
    }

    void Start()
    {
        if (playOnStart) StartSpawning();
    }

    public void StartSpawning()
    {
        if (spawnCoroutine == null) spawnCoroutine = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        if (spawnCoroutine != null) { StopCoroutine(spawnCoroutine); spawnCoroutine = null; }
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            if (CanSpawn())
                SpawnOne();

            float interval = spawnInterval;
            if (spawnIntervalRandomDelta > 0f)
                interval += Random.Range(-spawnIntervalRandomDelta, spawnIntervalRandomDelta);

            interval = Mathf.Max(0f, interval);
            yield return new WaitForSeconds(interval);
        }
    }

    private bool CanSpawn()
    {
        int cap = (maxSimultaneous > 0) ? maxSimultaneous : (poolSize > 0 ? poolSize : int.MaxValue);
        bool underLimit = inUse.Count < cap;
        bool havePoolOrInstantiate = (pool.Count > 0) || allowPoolGrow || poolSize == 0;
        return underLimit && havePoolOrInstantiate;
    }

    private void SpawnOne()
    {
        GameObject go = null;

        if (poolSize > 0)
        {
            if (pool.Count > 0)
            {
                go = pool.Dequeue();
            }
            else if (allowPoolGrow)
            {
                go = Instantiate(shootingStarPrefab, transform);
                var ssExtra = go.GetComponent<ShootingStar>();
                if (ssExtra != null) ssExtra.destroyAfterPlay = false;
            }
            else
            {
                return;
            }
        }
        else
        {
            go = Instantiate(shootingStarPrefab, transform);
            var ssExtra = go.GetComponent<ShootingStar>();
            if (ssExtra != null) ssExtra.destroyAfterPlay = false;
        }

        if (go == null) return;

        Vector3 start = new Vector3(Random.Range(startXRange.x, startXRange.y), Random.Range(startYRange.x, startYRange.y), 0f);
        Vector3 end = new Vector3(Random.Range(endXRange.x, endXRange.y), Random.Range(endYRange.x, endYRange.y), 0f);
        float travel = Random.Range(travelDurationRange.x, travelDurationRange.y);
        float trail = Random.Range(trailTimeRange.x, trailTimeRange.y);

        go.transform.position = start;
        go.SetActive(true);
        inUse.Add(go);

        var ss = go.GetComponent<ShootingStar>();
        if (ss != null)
        {
            ss.startWorldPos = start;
            ss.endWorldPos = end;
            ss.travelDuration = travel;
            ss.trailTime = trail;
            ss.playOnStart = false;
            ss.destroyAfterPlay = false;
            ss.PlayOnce(start, end, travel);
            StartCoroutine(ReturnToPoolAfter(go, travel + trail + 0.2f));
        }
        else
        {
            StartCoroutine(ReturnToPoolAfter(go, 2f));
        }
    }

    private IEnumerator ReturnToPoolAfter(GameObject go, float seconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, seconds));
        if (go == null) yield break;

        // reset visual state if needed
        var ss = go.GetComponent<ShootingStar>();
        if (ss != null)
        {
            // ensure trail cleared
            var tr = go.GetComponent<TrailRenderer>();
            if (tr != null)
            {
                tr.Clear();
                tr.emitting = false;
            }
        }

        go.SetActive(false);

        if (poolSize > 0)
        {
            inUse.Remove(go);
            pool.Enqueue(go);
        }
        else
        {
            inUse.Remove(go);
            Destroy(go);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        spawnInterval = Mathf.Max(0f, spawnInterval);
        poolSize = Mathf.Max(0, poolSize);
    }
#endif
}
