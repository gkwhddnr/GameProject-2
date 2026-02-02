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
        public bool deactivatePrevious;

        [Tooltip("체크 시: '현재' 오브젝트들을 비활성화합니다.")]
        public bool deactivateOnComplete;
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

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start(){ InitManager(); }

    private void InitManager()
    {
        _collector = FindAnyObjectByType<ItemCollector>();

        int stageCount = stageConfigs?.Length ?? 0;
        _stageCollectedCounts = new int[stageCount];
        _nextBatchToActivate = new int[stageCount];

        BuildItemMap();

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

        Collider2D[] allColliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        foreach (var col in allColliders)
        {
            if (((1 << col.gameObject.layer) & itemLayerMask.value) != 0)
            {
                GameObject itemGo = col.gameObject;
                for (int i = 0; i < stageConfigs.Length; i++)
                {
                    if (stageConfigs[i].bound != null && stageConfigs[i].bound.bounds.Contains(itemGo.transform.position))
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
        if (!_itemToStageMap.TryGetValue(id, out int stageIdx)) return;

        _stageCollectedCounts[stageIdx]++;

        int initial = _collector != null ? _collector.GetInitialVisibleCount() : 1;
        int subsequent = _collector != null ? _collector.GetSubsequentRevealCount() : 1;

        var config = stageConfigs[stageIdx];

        // 1. [핵심 추가] 현재 진행 중인 배치의 완료 여부 체크 (deactivateOnComplete 처리)
        // 현재 켜져 있는 배치가 있고, 그 배치의 목표치를 채웠다면 미리 끕니다.
        int activeIdx = _nextBatchToActivate[stageIdx] - 1;
        if (activeIdx >= 0 && activeIdx < config.batchGroups.Length)
        {
            var activeGroup = config.batchGroups[activeIdx];
            if (activeGroup.deactivateOnComplete)
            {
                // 현재 배치의 완료 기준값 계산
                int completeThreshold = initial + (subsequent * activeIdx);
                // 만약 현재 배치가 감당하는 아이템(subsequent 개수)을 다 먹었다면
                if (_stageCollectedCounts[stageIdx] >= completeThreshold + subsequent) DeactivateGroup(activeGroup);
            }
        }

        // 2. 새로운 배치 활성화 체크 루프
        while (true)
        {
            int nextIdx = _nextBatchToActivate[stageIdx];
            if (nextIdx >= config.batchGroups.Length) break;

            int activationThreshold = initial + (subsequent * nextIdx);

            if (_stageCollectedCounts[stageIdx] >= activationThreshold)
            {
                ActivateBatch(stageIdx, nextIdx);
                _nextBatchToActivate[stageIdx]++;
            }
            else break;
        }
    }

    private void ActivateBatch(int stageIdx, int batchIdx)
    {
        var config = stageConfigs[stageIdx];
        var currentGroup = config.batchGroups[batchIdx];

        // deactivatePrevious: 이전 단계들 싹 다 끄기
        if (currentGroup.deactivatePrevious)
        {
            for (int i = 0; i < batchIdx; i++) DeactivateGroup(config.batchGroups[i]);
        }

        // 현재 단계 켜기
        if (currentGroup.objectsToActivate != null)
        {
            foreach (var obj in currentGroup.objectsToActivate)
            {
                if (obj != null) obj.SetActive(true);
            }
        }
    }

    private void DeactivateGroup(BatchRevealGroup group)
    {
        if (group.objectsToActivate == null) return;
        foreach (var obj in group.objectsToActivate)
        {
            if (obj != null) obj.SetActive(false);
        }
    }

    private void OnDestroy(){ if (Instance == this) Instance = null; }
}