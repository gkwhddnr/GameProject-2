using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SequentialRevealManager : MonoBehaviour
{
    public static SequentialRevealManager Instance { get; private set; }

    [Header("스테이지 구성")]
    public StageConfig[] stageConfigs;

    [System.Serializable]
    public class BatchRevealGroup
    {
        [Tooltip("이번 단계에서 활성화할 오브젝트들")]
        public GameObject[] objectsToActivate;

        [Tooltip("체크 시: 다음 단계가 시작될 때 '이전' 단계들의 오브젝트를 모두 비활성화합니다.")]
        public bool Previous;

        [Tooltip("체크 시: '현재' 오브젝트들을 비활성화합니다.")]
        public bool Present;
    }

    [System.Serializable]
    public class StageConfig
    {
        public BoxCollider2D bound;
        public BatchRevealGroup[] batchGroups;
    }

    private readonly Dictionary<int, int> _itemToStageMap = new Dictionary<int, int>();
    private int[] _stageCollectedCounts;
    private int[] _nextBatchToActivate;
    private int[] _collectorStageIdxForConfig;
    // SRM 스테이지 인덱스 → ItemCollector.stageSettings 인덱스 (GM 인덱스와 다를 수 있는 도립적 매핑)
    private int[] _srmToCollectorStageIdx;
    private ItemCollector _collector;
    private LayerMask _defaultItemLayerMask;

    // 중복 카운트 방지용 (SRM 레벨)
    private HashSet<int> _countedItemIds = new HashSet<int>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start() { InitManager(); }

    private void InitManager()
    {
        _collector = FindAnyObjectByType<ItemCollector>();

        if (GameManager.Instance != null)
        {
            _defaultItemLayerMask = GameManager.Instance.defaultItemLayerMask;
        }

        int stageCount = stageConfigs?.Length ?? 0;
        _stageCollectedCounts = new int[stageCount];
        _nextBatchToActivate = new int[stageCount];
        _collectorStageIdxForConfig = new int[stageCount];

        for (int i = 0; i < stageCount; ++i) _collectorStageIdxForConfig[i] = -1;
        if (GameManager.Instance != null && GameManager.Instance.stageSettings != null)
        {
            for (int ci = 0; ci < stageCount; ++ci)
            {
                var srmBound = stageConfigs[ci].bound;
                if (srmBound == null) continue;

                if (_collectorStageIdxForConfig[ci] == -1 && GameManager.Instance != null)
                {
                    for (int si = 0; si < GameManager.Instance.stageSettings.Length; ++si)
                    {
                        var gmBound = GameManager.Instance.stageSettings[si].bounds;
                        if (gmBound == null || srmBound == null) continue;

                        float posDist = Vector2.Distance(gmBound.transform.position, srmBound.transform.position);
                        float sizeDist = Vector2.Distance(gmBound.bounds.size, srmBound.bounds.size);
                        if (posDist < 0.1f && sizeDist < 0.1f)
                        {
                            _collectorStageIdxForConfig[ci] = si;
                            Debug.Log($"[SRM] 근사 매핑 성공: SRM_{ci} <-> GM_{si} (posDist={posDist}, sizeDist={sizeDist})");
                            break;
                        }
                    }
                }

                if (_collectorStageIdxForConfig[ci] == -1)
                    Debug.LogWarning($"[SRM] 스테이지 {ci}의 Bound가 GameManager 설정에서 발견되지 않았습니다.");
            }
        }

        BuildItemMap();

        // ItemCollector 스테이지 인덱스 도립 매핑 (GameManager 인덱스와 독립적으로 블드)
        _srmToCollectorStageIdx = new int[stageCount];
        for (int ci = 0; ci < stageCount; ci++) _srmToCollectorStageIdx[ci] = -1;

        if (_collector != null)
        {
            for (int ci = 0; ci < stageCount; ci++)
            {
                var srmBound = stageConfigs[ci].bound;
                if (srmBound == null) continue;

                // ItemCollector의 스테이지에서 Bound를 비교하여 일치하는 인덱스 찾기
                int collectorCount = 0;
                // GetStageBounds가 있으므로 솔리하여 반복
                for (int si = 0; ; si++)
                {
                    BoxCollider2D cb = _collector.GetStageBounds(si);
                    if (cb == null) break; // 이 인덱스에 bounds가 없으면 된다

                    float pd = Vector2.Distance(cb.transform.position, srmBound.transform.position);
                    float sd = Vector2.Distance(cb.bounds.size, srmBound.bounds.size);
                    if (pd < 0.1f && sd < 0.1f)
                    {
                        _srmToCollectorStageIdx[ci] = si;
                        Debug.Log($"[SRM] ItemCollector 매핑 성공: SRM_{ci} <-> Collector_{si} (posDist={pd:F3}, sizeDist={sd:F3})");
                        break;
                    }
                    collectorCount++;
                    if (collectorCount > 50) break; // 안전망
                }

                if (_srmToCollectorStageIdx[ci] == -1)
                    Debug.LogWarning($"[SRM] SRM 스테이지 {ci}에 매치하는 ItemCollector 스테이지를 찾지 못했습니다.");
            }
        }

        // 초기 상태: 모든 batch 객체는 비활성화
        foreach (var config in stageConfigs)
        {
            if (config.batchGroups == null) continue;
            foreach (var group in config.batchGroups)
            {
                if (group.objectsToActivate == null) continue;
                foreach (var obj in group.objectsToActivate) if (obj) obj.SetActive(false);
            }
        }
    }

    private void BuildItemMap()
    {
        _itemToStageMap.Clear();
        if (stageConfigs == null) return;

        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            Collider2D[] allColliders = root.GetComponentsInChildren<Collider2D>(true);
            foreach (var col in allColliders)
            {
                if (col == null) continue;
                GameObject itemGo = col.gameObject;

                // 1단계: 이 아이템이 어느 스테이지 바운드 안에 있는지 확인
                int assignedStageIdx = -1;
                for (int i = 0; i < stageConfigs.Length; i++)
                {
                    var bound = stageConfigs[i].bound;
                    if (bound != null && bound.bounds.Contains(itemGo.transform.position))
                    {
                        assignedStageIdx = i;
                        break;
                    }
                }

                // 스테이지를 찾지 못했으면 무시
                if (assignedStageIdx == -1) continue;

                // 2단계: 해당 스테이지에서 허용하는 레이어 마스크인지 GameManager에서 확인
                int gmIdx = (assignedStageIdx < _collectorStageIdxForConfig.Length) ? _collectorStageIdxForConfig[assignedStageIdx] : -1;

                // GM 인덱스가 유효하면 해당 인덱스의 설정을, 아니면 기본 마스크 사용
                LayerMask maskToUse = (GameManager.Instance != null && gmIdx != -1)
                    ? GameManager.Instance.GetItemLayerMaskForStage(gmIdx)
                    : _defaultItemLayerMask;

                if (((1 << itemGo.layer) & maskToUse.value) != 0)
                {
                    int itemId = itemGo.GetInstanceID();
                    if (!_itemToStageMap.ContainsKey(itemId))
                    {
                        _itemToStageMap[itemId] = assignedStageIdx;
                    }
                }
            }
        }
    }

    public void NotifyCollected(GameObject item)
    {
        if (item == null) return;
        int id = item.GetInstanceID();

        if (_countedItemIds.Contains(id)) return;
        if (!_itemToStageMap.TryGetValue(id, out int stageIdx)) return;

        int gmIdx = (stageIdx < _collectorStageIdxForConfig.Length) ? _collectorStageIdxForConfig[stageIdx] : -1;

        LayerMask allowedMask = (GameManager.Instance != null)
            ? GameManager.Instance.GetItemLayerMaskForStage(gmIdx)
            : _defaultItemLayerMask;

        if (((1 << item.layer) & allowedMask.value) == 0) return;

        // 카운트 증가
        _stageCollectedCounts[stageIdx]++;
        _countedItemIds.Add(id);

        int collectorStageIdx = (_collectorStageIdxForConfig != null && stageIdx >= 0 && stageIdx < _collectorStageIdxForConfig.Length) ? _collectorStageIdxForConfig[stageIdx] : -1;

        int initial = 1;
        int subsequent = 1;
        bool stageUsesSequential = true;

        // 우선순위: ItemCollector의 Bound 매핑 인덱스를 먼저 사용, 없으면 GM 인덱스 사용, 없으면 SRM 인덧스 사용(fallback)
        int effectiveCollectorIdx = (_srmToCollectorStageIdx != null && stageIdx < _srmToCollectorStageIdx.Length)
            ? _srmToCollectorStageIdx[stageIdx] : collectorStageIdx;

        if (_collector != null && effectiveCollectorIdx >= 0)
        {
            initial = _collector.GetInitialVisibleCount(effectiveCollectorIdx);
            subsequent = _collector.GetSubsequentRevealCount(effectiveCollectorIdx);
            stageUsesSequential = _collector.GetRevealSequentially(effectiveCollectorIdx);
        }
        else if (_collector != null)
        {
            initial = _collector.GetInitialVisibleCount(stageIdx);
            subsequent = _collector.GetSubsequentRevealCount(stageIdx);
            stageUsesSequential = _collector.GetRevealSequentially(stageIdx);
        }

        initial = Mathf.Max(0, initial);
        subsequent = Mathf.Max(1, subsequent);

        // 디버그: 실제 동작 중 어떤 값실주로 활성화 조건이 판단되는지 확인
        Debug.Log($"[SRM] NotifyCollected: item={item.name}, srmStageIdx={stageIdx}, collectorStageIdx={collectorStageIdx}, initial={initial}, subsequent={subsequent}, currentCount={_stageCollectedCounts[stageIdx]}, nextBatch={_nextBatchToActivate[stageIdx]}");

        var config = stageConfigs[stageIdx];
        if (config == null || config.batchGroups == null) return;

        // 1) 현재 활성화된 배치(이미 켜진 것)의 자동 비활성화 조건 처리 (Present 플래그)
        int activeIdx = _nextBatchToActivate[stageIdx] - 1;
        if (activeIdx >= 0 && activeIdx < config.batchGroups.Length)
        {
            var activeGroup = config.batchGroups[activeIdx];
            if (activeGroup.Present)
            {
                // 배치 0: initial개 수집 후 활성화됨  → initial + subsequent 개 수집 시 비활성화
                // 배치 N: initial + subsequent*N 개 수집 후 활성화됨 → initial + subsequent*(N+1) 개 수집 시 비활성화
                int deactivateThreshold = initial + subsequent * (activeIdx + 1);
                if (_stageCollectedCounts[stageIdx] >= deactivateThreshold) DeactivateGroup(activeGroup);
            }
        }

        // 2) 새로운 배치 활성화 (연속 노출 사용 시)
        if (!stageUsesSequential)
        {
            // sequential 꺼져있으면 모든 배치 즉시 활성화 
            ActivateAllBatchesImmediate(stageIdx);
            return;
        }

        // while 루프로 여러 배치가 한 번에 활성화될 수 있도록 함
        while (true)
        {
            int nextIdx = _nextBatchToActivate[stageIdx];
            if (nextIdx >= config.batchGroups.Length) break;

            // 배치 0: initial 개 이상 수집 시 활성화
            // 배치 1: initial + subsequent 개 이상 수집 시 활성화
            // 배치 N: initial + subsequent * N 개 이상 수집 시 활성화
            int activationThreshold = initial + subsequent * nextIdx;
            if (_stageCollectedCounts[stageIdx] >= activationThreshold)
            {
                ActivateBatch(stageIdx, nextIdx);
                _nextBatchToActivate[stageIdx]++;
            }
            else break;
        }
    }

    // 모든 배치 즉시 켜기 (sequential 비활성화 스테이지용)
    private void ActivateAllBatchesImmediate(int stageIdx)
    {
        var config = stageConfigs[stageIdx];
        if (config == null || config.batchGroups == null) return;
        for (int i = 0; i < config.batchGroups.Length; ++i)
        {
            if (_nextBatchToActivate[stageIdx] <= i)
            {
                ActivateBatch(stageIdx, i);
                _nextBatchToActivate[stageIdx] = i + 1;
            }
        }
    }

    private void ActivateBatch(int stageIdx, int batchIdx)
    {
        var config = stageConfigs[stageIdx];
        if (config == null || config.batchGroups == null || batchIdx < 0 || batchIdx >= config.batchGroups.Length) return;

        var currentGroup = config.batchGroups[batchIdx];

        // Previous: 이전 단계들 끄기
        if (currentGroup.Previous)
            for (int i = 0; i < batchIdx; i++) DeactivateGroup(config.batchGroups[i]);
        

        // 현재 단계 켜기
        if (currentGroup.objectsToActivate != null)
        {
            int keyLayer = LayerMask.NameToLayer("Key");

            foreach (var obj in currentGroup.objectsToActivate)
            {
                if (obj == null) continue;

                obj.SetActive(true);

                bool looksLikeKey = (obj.layer == keyLayer) || obj.CompareTag("Key") || string.Equals(obj.name, "Key", System.StringComparison.OrdinalIgnoreCase);

                bool hasPickupScript = obj.GetComponentInChildren<ItemPickup>(true) != null
                                       || obj.GetComponent<ItemPickup>() != null;

                if (looksLikeKey || hasPickupScript)
                {
                    var cols = obj.GetComponentsInChildren<Collider2D>(true);
                    foreach (var c in cols) if (c != null) c.enabled = true;
                }
            }
        }
    }

    private void DeactivateGroup(BatchRevealGroup group)
    {
        if (group == null || group.objectsToActivate == null) return;
        foreach (var obj in group.objectsToActivate) if (obj != null) obj.SetActive(false);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
}
