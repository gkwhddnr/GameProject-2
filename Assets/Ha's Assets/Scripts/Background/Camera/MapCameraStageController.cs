using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class MapCameraStageController : MonoBehaviour
{
    public enum StageCameraMode
    {
        FitBoth,        // 화면 전체 출력 (autoFit)
        AutoScaleOnly,  // 플레이어 추적 및 자동 스케일
        None            // 자동 설정 없음
    }

    [Header("참조")]
    public MapCamera mapCamera;
    public GameManager gameManager;

    [Header("스테이지별 카메라 모드")]
    public StageCameraMode[] perStageModes;

    [Header("스테이지별 AutoScaleOnly 설정 (자동 필터링)")]
    public AutoScaleSettings[] autoScaleSettings;

    [Header("Bounds 변경 감지 설정")]
    [Tooltip("스테이지 이동 시 Bounds 재조정 강제 실행")]
    public bool forceRefreshBoundsOnStageChange = true;

    private bool applyOnStart = true;
    private bool snapCameraWhenChanging = true;
    private int lastAppliedStage = -1;
    private int lastDetectedStage = -2;
    private int stageExitCounter = 0;
    private bool hasAppliedInitialStage = false;

    [Serializable]
    public class AutoScaleSettings
    {
        [HideInInspector] public string stageLabel;

        [Tooltip("AutoScaleOnly 모드에서 사용되는 followViewFraction 값")]
        [Range(0.01f, 1f)] public float followViewFraction = 0.25f;

        [Tooltip("AutoScaleOnly 모드에서 사용되는 followZoomSmooth 값")]
        [Range(0f, 1f)] public float followZoomSmooth = 0.15f;
    }

    void Awake()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>() ?? GameManager.Instance;
        if (mapCamera == null) mapCamera = FindAnyObjectByType<MapCamera>();
    }

    void OnEnable()
    {
        hasAppliedInitialStage = false;
        lastDetectedStage = -2;

        if (gameManager != null)
        {
            gameManager.OnPlayerTurnEnd -= OnPlayerTurnEnd;
            gameManager.OnPlayerTurnEnd += OnPlayerTurnEnd;
        }
        if (applyOnStart) ApplyCurrentStageSettings(forceImmediate: true);
    }

    void LateUpdate()
    {
        if (!ValidateReferences()) return;

        int currentStage = GetStageIndexForPosition(gameManager.playerTransform.position);

        if (!hasAppliedInitialStage)
        {
            hasAppliedInitialStage = true;
            lastDetectedStage = currentStage;
            ApplyCurrentStageSettings(forceImmediate: true);
            return;
        }

        if (currentStage != lastDetectedStage)
        {
            lastDetectedStage = currentStage;
            ApplyCurrentStageSettings(forceImmediate: true);
        }
    }


    void OnDisable()
    {
        if (gameManager != null) gameManager.OnPlayerTurnEnd -= OnPlayerTurnEnd;
    }

    private void OnPlayerTurnEnd()
    {
        ApplyCurrentStageSettings(forceImmediate: false);
    }

    public void ApplyCurrentStageSettingsImmediate()
    {
        ApplyCurrentStageSettings(forceImmediate: true);
    }

    private void ApplyCurrentStageSettings(bool forceImmediate = false)
    {
        if (!ValidateReferences()) return;

        Vector3 playerPos = gameManager.playerTransform.position;
        int idx = GetStageIndexForPosition(playerPos);

        if (idx < 0 || idx >= gameManager.stageSettings.Length)
        {
            // 연속으로 밖에 있으면 카운트 증가
            stageExitCounter++;

            // 아직 유예 횟수에 미달하면 아무 동작도 하지 않음 (설정 유지)
            if (stageExitCounter < Mathf.Max(1, 99))
            {
                return;
            }

            // 유예가 완료되었고 이전에 적용된 스테이지가 있었다면 None 상태로 처리(원하면 변경)
            if (lastAppliedStage != -1)
            {
                lastAppliedStage = -1;

                // 주의: ApplyModeToCamera의 None 분기는 더 이상 카메라 설정을 초기화하지 않음.
                ApplyModeToCamera(StageCameraMode.None, null, forceImmediate, -1);
                Debug.Log("[MapCameraStageController] Player remained outside; applied None mode after grace turns.");
            }
            return;
        }

        // 플레이어가 스테이지 안에 들어온 경우, exit 카운터 초기화
        stageExitCounter = 0;

        // ★ 스테이지가 변경되었는지 확인
        bool stageChanged = (lastAppliedStage != idx);

        if (lastAppliedStage == idx && !forceImmediate) return;

        lastAppliedStage = idx;
        StageCameraMode mode = (perStageModes != null && idx < perStageModes.Length)
                               ? perStageModes[idx]
                               : StageCameraMode.None;

        ApplyModeToCamera(mode, gameManager.stageSettings[idx].bounds, forceImmediate, idx);

        // ★ 스테이지 변경 시 Bounds 강제 재조정
        if (stageChanged && forceRefreshBoundsOnStageChange && mapCamera != null)
        {
            // 약간의 딜레이 후 재조정 (Bounds가 완전히 설정된 후)
            StartCoroutine(DelayedBoundsRefresh());
        }
    }

    /// <summary>
    /// Bounds 재조정을 약간 지연시켜 실행
    /// </summary>
    private System.Collections.IEnumerator DelayedBoundsRefresh()
    {
        // 1프레임 대기 (Bounds가 완전히 업데이트될 시간 확보)
        yield return null;

        if (mapCamera != null)
        {
            mapCamera.ForceRefreshBounds();
        }
    }

    private void ApplyModeToCamera(StageCameraMode mode, BoxCollider2D bounds, bool forceImmediate, int stageIndex)
    {
        if (mapCamera == null) return;

        switch (mode)
        {
            case StageCameraMode.FitBoth:
                mapCamera.autoFitToBounds = true;
                mapCamera.forceFitIgnoreMaxOrtho = true;
                mapCamera.autoScaleFollowView = false;
                break;
            case StageCameraMode.AutoScaleOnly:
                mapCamera.autoFitToBounds = false;
                mapCamera.forceFitIgnoreMaxOrtho = false;
                mapCamera.autoScaleFollowView = true;

                int settingsIdx = GetAutoScaleSettingsIndex(stageIndex);
                if (autoScaleSettings != null && settingsIdx >= 0 && settingsIdx < autoScaleSettings.Length)
                {
                    mapCamera.followViewFraction = autoScaleSettings[settingsIdx].followViewFraction;
                    mapCamera.followZoomSmooth = autoScaleSettings[settingsIdx].followZoomSmooth;
                }
                break;
            default:
                mapCamera.autoFitToBounds = false;
                mapCamera.forceFitIgnoreMaxOrtho = false;
                mapCamera.autoScaleFollowView = false;
                break;
        }

        if (mapCamera.playerTarget == null) mapCamera.playerTarget = gameManager.playerTransform;

        bool shouldSnap = forceImmediate || snapCameraWhenChanging;
        bool shouldFit = (mode == StageCameraMode.FitBoth);
        mapCamera.SetBounds(bounds, snapCameraToBounds: shouldSnap, fitViewToBounds: shouldFit);
    }

    private int GetAutoScaleSettingsIndex(int stageIdx)
    {
        if (perStageModes == null || stageIdx >= perStageModes.Length) return -1;
        if (perStageModes[stageIdx] != StageCameraMode.AutoScaleOnly) return -1;

        int count = 0;
        for (int i = 0; i < stageIdx; i++)
        {
            if (perStageModes[i] == StageCameraMode.AutoScaleOnly) count++;
        }
        return count;
    }

    private int GetStageIndexForPosition(Vector3 worldPos)
    {
        var settingsArray = gameManager.stageSettings;
        if (settingsArray == null) return -1;
        for (int i = 0; i < settingsArray.Length; i++)
        {
            if (settingsArray[i] != null && settingsArray[i].bounds != null && settingsArray[i].bounds.bounds.Contains(worldPos)) return i;
        }
        return -1;
    }

    private bool ValidateReferences()
    {
        return gameManager != null && mapCamera != null && gameManager.stageSettings != null && gameManager.playerTransform != null;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();

        if (gameManager != null && gameManager.stageSettings != null)
        {
            int stageCount = gameManager.stageSettings.Length;

            if (perStageModes == null || perStageModes.Length != stageCount)
                Array.Resize(ref perStageModes, stageCount);

            int autoScaleCount = 0;
            for (int i = 0; i < stageCount; i++)
            {
                if (perStageModes[i] == StageCameraMode.AutoScaleOnly) autoScaleCount++;
            }

            if (autoScaleSettings == null || autoScaleSettings.Length != autoScaleCount)
            {
                Array.Resize(ref autoScaleSettings, autoScaleCount);
            }

            int currentSettingsIdx = 0;
            for (int i = 0; i < stageCount; i++)
            {
                if (perStageModes[i] == StageCameraMode.AutoScaleOnly)
                {
                    if (autoScaleSettings[currentSettingsIdx] == null)
                        autoScaleSettings[currentSettingsIdx] = new AutoScaleSettings();

                    currentSettingsIdx++;
                }
            }
        }
    }
#endif
}