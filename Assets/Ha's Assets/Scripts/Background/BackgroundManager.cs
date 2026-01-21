using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BackgroundManager : MonoBehaviour
{
    [Header("Stage settings")]
    public int startStageIndex = 0;
    public bool deactivatePreviousStage = true;
    public bool stopBeforeApply = true;
    public bool autoActivateOnStart = true;

    class Stage
    {
        public GameObject root;
        public BoxCollider2D bounds;
        public BackgroundMover[] movers;
    }

    List<Stage> stages = new List<Stage>();
    int currentStage = -1;

    void Awake() => BuildStagesFromChildren();

    void Start()
    {
        if (autoActivateOnStart && stages.Count > 0)
        {
            int idx = Mathf.Clamp(startStageIndex, 0, stages.Count - 1);
            ActivateStage(idx, snapToBounds: true, previousDeactivateDelay: 0f);
        }
    }

    void BuildStagesFromChildren()
    {
        stages.Clear();
        for (int i = 0; i < transform.childCount; ++i)
        {
            var child = transform.GetChild(i).gameObject;
            if (child == null) continue;
            var s = new Stage();
            s.root = child;
            s.bounds = child.GetComponentInChildren<BoxCollider2D>(true);
            s.movers = child.GetComponentsInChildren<BackgroundMover>(true);
            stages.Add(s);
        }
    }

    public int CurrentStageIndex => currentStage;
    public int StageCount => stages.Count;

    /// <summary>
    /// 이전 스테이지를 previousDeactivateDelay 초 후에 비활성화(옵션)하고 새 스테이지 활성화
    /// </summary>
    public void ActivateStage(int stageIndex, bool snapToBounds = true, float previousDeactivateDelay = 0f)
    {
        if (stageIndex < 0 || stageIndex >= stages.Count) return;

        // 이전 스테이지 처리 (지연 비활성화 가능)
        if (currentStage >= 0 && currentStage < stages.Count)
        {
            var prev = stages[currentStage];
            foreach (var m in prev.movers)
            {
                if (m == null) continue;
                m.StopMove();
            }

            if (deactivatePreviousStage)
            {
                if (previousDeactivateDelay <= 0f)
                {
                    prev.root.SetActive(false);
                }
                else
                {
                    StartCoroutine(DeactivateAfterDelay(prev.root, previousDeactivateDelay));
                }
            }
        }

        // 새 스테이지 활성화
        var st = stages[stageIndex];
        if (!st.root.activeSelf) st.root.SetActive(true);

        // bounds 적용 및 movers 시작
        ApplyBoundsToStage(stageIndex, snapToBounds);

        currentStage = stageIndex;
    }

    IEnumerator DeactivateAfterDelay(GameObject go, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (go != null) go.SetActive(false);
    }

    public bool AdvanceToNextStage(float previousDeactivateDelay = 0f)
    {
        int next = currentStage + 1;
        if (next >= stages.Count) return false;
        ActivateStage(next, snapToBounds: true, previousDeactivateDelay: previousDeactivateDelay);
        return true;
    }

    void ApplyBoundsToStage(int stageIndex, bool snapToBounds)
    {
        if (stageIndex < 0 || stageIndex >= stages.Count) return;
        var st = stages[stageIndex];
        var bounds = st.bounds;
        if (stopBeforeApply)
        {
            foreach (var m in st.movers)
            {
                if (m == null) continue;
                m.StopMove();
            }
        }

        foreach (var m in st.movers)
        {
            if (m == null) continue;
            m.SetBounds(bounds); // Bounds null 허용 — BackgroundMover가 처리해야 함
        }

        // 한 번에 시작
        foreach (var m in st.movers)
        {
            if (m == null) continue;
            m.StartMove();
        }
    }

    // public helper
    public BoxCollider2D GetCurrentStageBounds()
    {
        if (currentStage < 0 || currentStage >= stages.Count) return null;
        return stages[currentStage].bounds;
    }
}
