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


    private int activePreviousStageCount = 0;
    private int activeNextStageCount = 2;
    private bool useStageRangeActivation = true;

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
        if (autoActivateOnStart && stages.Count > 0) StartCoroutine(DelayedActivation());
    }

    IEnumerator DelayedActivation()
    {
        // 한 프레임 대기 (다른 모든 Start() 완료 후)
        yield return new WaitForEndOfFrame();

        int idx = Mathf.Clamp(startStageIndex, 0, stages.Count - 1);
        ActivateStage(idx, snapToBounds: true, previousDeactivateDelay: 0f);
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

    private void ApplyStageRangeActivation(int centerStageIndex)
    {
        if (!useStageRangeActivation) return;

        int minActiveIndex = Mathf.Max(0, centerStageIndex - activePreviousStageCount);
        int maxActiveIndex = Mathf.Min(stages.Count - 1, centerStageIndex + activeNextStageCount);

        for (int i = 0; i < stages.Count; i++)
        {
            if (stages[i].root == null) continue;

            bool shouldBeActive = (i >= minActiveIndex && i <= maxActiveIndex);

            if (stages[i].root.activeSelf != shouldBeActive) stages[i].root.SetActive(shouldBeActive);
        }
    }

    public void ActivateStage(int stageIndex, bool snapToBounds = true, float previousDeactivateDelay = 0f)
    {
        if (stageIndex < 0 || stageIndex >= stages.Count) return;

        if (!useStageRangeActivation)
        {
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
                    if (previousDeactivateDelay <= 0f) prev.root.SetActive(false);
                    else StartCoroutine(DeactivateAfterDelay(prev.root, previousDeactivateDelay));
                }
            }

            var st = stages[stageIndex];
            if (!st.root.activeSelf) st.root.SetActive(true);
        }
        else
        {
            if (currentStage >= 0 && currentStage < stages.Count)
            {
                var prev = stages[currentStage];
                foreach (var m in prev.movers)
                {
                    if (m == null) continue;
                    m.StopMove();
                }
            }

            ApplyStageRangeActivation(stageIndex);
        }

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
            m.SetBounds(bounds);
        }

        foreach (var m in st.movers)
        {
            if (m == null) continue;
            m.StartMove();
        }
    }

    public BoxCollider2D GetCurrentStageBounds()
    {
        if (currentStage < 0 || currentStage >= stages.Count) return null;
        return stages[currentStage].bounds;
    }

    public void SetStageRange(int previousCount, int nextCount)
    {
        activePreviousStageCount = previousCount;
        activeNextStageCount = nextCount;

        if (currentStage >= 0) ApplyStageRangeActivation(currentStage);
    }
}