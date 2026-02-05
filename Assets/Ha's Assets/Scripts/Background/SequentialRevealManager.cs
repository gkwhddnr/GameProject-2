using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SequentialRevealManager : MonoBehaviour
{
    public static SequentialRevealManager Instance { get; private set; }

    [Header("설정")]
    public LayerMask itemLayerMask;

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
    private ItemCollector _collector;
    private int[] _collectorStageIdxForConfig;

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

        int stageCount = stageConfigs?.Length ?? 0;
        _stageCollectedCounts = new int[stageCount];
        _nextBatchToActivate = new int[stageCount];
        _collectorStageIdxForConfig = new int[stageCount];

        for (int i = 0; i < stageCount; ++i) _collectorStageIdxForConfig[i] = -1;
        if (_collector != null && _collector.stageSettings != null)
        {
            for (int ci = 0; ci < stageCount; ++ci)
            {
                var cfgBound = stageConfigs[ci].bound;
                if (cfgBound == null) continue;
                for (int si = 0; si < _collector.stageSettings.Length; ++si)
                {
                    var colBound = _collector.stageSettings[si].stageBounds;
                    if (colBound != null && colBound == cfgBound)
                    {
                        _collectorStageIdxForConfig[ci] = si;
                        break;
                    }
                }
            }
        }

        BuildItemMap();

        // 초기 상태: 모든 batch 객체는 비활성화
        foreach (var config in stageConfigs)
        {
            if (config.batchGroups == null) continue;
            foreach (var group in config.batchGroups)
            {
                if (group.objectsToActivate == null) continue;
                foreach (var obj in group.objectsToActivate)
                {
                    if (obj) obj.SetActive(false);
                }
            }
        }
    }

    private void BuildItemMap()
    {
        _itemToStageMap.Clear();
        if (stageConfigs == null) return;

        // 씬 내 모든 Collider2D 검사해서 itemLayerMask에 속하는 게임오브젝트를 스테이지 인덱스로 맵핑
        Collider2D[] allColliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        foreach (var col in allColliders)
        {
            if (((1 << col.gameObject.layer) & itemLayerMask.value) != 0)
            {
                GameObject itemGo = col.gameObject;
                for (int i = 0; i < stageConfigs.Length; i++)
                {
                    var bound = stageConfigs[i].bound;
                    if (bound != null && bound.bounds.Contains(itemGo.transform.position))
                    {
                        _itemToStageMap[itemGo.GetInstanceID()] = i;
                        break;
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

        // 카운트 증가
        _stageCollectedCounts[stageIdx]++;
        _countedItemIds.Add(id);

        int collectorStageIdx = (_collectorStageIdxForConfig != null && stageIdx >= 0 && stageIdx < _collectorStageIdxForConfig.Length) ? _collectorStageIdxForConfig[stageIdx] : -1;

        int initial = 1;
        int subsequent = 1;
        bool stageUsesSequential = true;

        // 우선순위: ItemCollector의 스테이지값(매핑이 되어 있다면) -> 아니면 ItemCollector의 전역값 접근 (GetInitialVisibleCount(-1) 같은 오버로드가 없으므로 fallback) -> 최종 하드코딩 safe 값
        if (_collector != null && collectorStageIdx >= 0)
        {
            initial = _collector.GetInitialVisibleCount(collectorStageIdx);
            subsequent = _collector.GetSubsequentRevealCount(collectorStageIdx);
            stageUsesSequential = _collector.GetRevealSequentially(collectorStageIdx);
        }
        else if (_collector != null)
        {
            initial = _collector.GetInitialVisibleCount(stageIdx);
            subsequent = _collector.GetSubsequentRevealCount(stageIdx);
            stageUsesSequential = _collector.GetRevealSequentially(stageIdx);
        }

        initial = Mathf.Max(0, initial);
        subsequent = Mathf.Max(1, subsequent);

        var config = stageConfigs[stageIdx];
        if (config == null || config.batchGroups == null) return;

        // 1) 현재 활성화된 배치(이미 켜진 것)의 자동 비활성화 조건 처리 (Present 플래그)
        int activeIdx = _nextBatchToActivate[stageIdx] - 1;
        if (activeIdx >= 0 && activeIdx < config.batchGroups.Length)
        {
            var activeGroup = config.batchGroups[activeIdx];
            if (activeGroup.Present)
            {
                int completeThreshold = (activeIdx == 0) ? initial : (initial + subsequent * activeIdx);
                if (_stageCollectedCounts[stageIdx] >= completeThreshold + subsequent) DeactivateGroup(activeGroup);
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

            int activationThreshold = (nextIdx == 0) ? initial : (initial + subsequent * nextIdx);

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
        {
            for (int i = 0; i < batchIdx; i++) DeactivateGroup(config.batchGroups[i]);
        }

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
        foreach (var obj in group.objectsToActivate)
        {
            if (obj != null) obj.SetActive(false);
        }
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
}
