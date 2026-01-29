using UnityEngine;
using System;

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

    private bool applyOnStart = true;
    private bool snapCameraWhenChanging = true;
    private int lastAppliedStage = -1;

    void Awake()
    {
        // GameManager 참조 최적화
        if (gameManager == null)
        {
            gameManager = GetComponent<GameManager>();
            if (gameManager == null) gameManager = GameManager.Instance;
        }

        // MapCamera 참조 최적화 
        if (mapCamera == null) mapCamera = FindAnyObjectByType<MapCamera>();
    }

    void OnEnable()
    {
        if (gameManager != null)
        {
            // 이벤트 중복 구독 방지
            gameManager.OnPlayerTurnEnd -= OnPlayerTurnEnd;
            gameManager.OnPlayerTurnEnd += OnPlayerTurnEnd;
        }

        if (applyOnStart)
        {
            // Start 타이밍보다 OnEnable이 먼저 올 수 있으므로 한 프레임 뒤 혹은 즉시 실행
            ApplyCurrentStageSettings(forceImmediate: true);
        }
    }

    void OnDisable()
    {
        if (gameManager != null) gameManager.OnPlayerTurnEnd -= OnPlayerTurnEnd;
    }

    private void OnPlayerTurnEnd(){ ApplyCurrentStageSettings(forceImmediate: false); }

    /// 외부에서 강제로 현재 스테이지 설정을 갱신할 때 호출
    public void ApplyCurrentStageSettingsImmediate(){ ApplyCurrentStageSettings(forceImmediate: true); }

    private void ApplyCurrentStageSettings(bool forceImmediate = false)
    {
        if (!ValidateReferences()) return;

        Vector3 playerPos = gameManager.playerTransform.position;
        int idx = GetStageIndexForPosition(playerPos);

        // 플레이어가 영역 밖에 있거나 인덱스가 유효하지 않을 때
        if (idx < 0 || idx >= gameManager.stageBounds.Length)
        {
            if (lastAppliedStage != -1)
            {
                lastAppliedStage = -1;
                ApplyModeToCamera(StageCameraMode.None, null, forceImmediate);
            }
            return;
        }

        // 동일한 스테이지라면 계산 생략 (최적화)
        if (lastAppliedStage == idx && !forceImmediate) return;

        lastAppliedStage = idx;
        StageCameraMode mode = (perStageModes != null && idx < perStageModes.Length)
                               ? perStageModes[idx]
                               : StageCameraMode.None;

        BoxCollider2D bounds = gameManager.stageBounds[idx];
        ApplyModeToCamera(mode, bounds, forceImmediate);
    }

    private void ApplyModeToCamera(StageCameraMode mode, BoxCollider2D bounds, bool forceImmediate)
    {
        if (mapCamera == null) return;

        // 1. 카메라 상태 플래그 설정
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
                break;
            case StageCameraMode.None:
            default:
                mapCamera.autoFitToBounds = false;
                mapCamera.forceFitIgnoreMaxOrtho = false;
                mapCamera.autoScaleFollowView = false;
                break;
        }

        // 2. 플레이어 타겟 확인
        if (mapCamera.playerTarget == null) mapCamera.playerTarget = gameManager.playerTransform;

        // 3. Bounds 적용 및 스냅 처리
        bool shouldSnap = forceImmediate || snapCameraWhenChanging;
        bool shouldFit = (mode == StageCameraMode.FitBoth);

        mapCamera.SetBounds(bounds, snapCameraToBounds: shouldSnap, fitViewToBounds: shouldFit);

        Debug.Log($"<color=lime>[CameraStage]</color> Stage {lastAppliedStage} 적용 (Mode: {mode})");
    }

    private int GetStageIndexForPosition(Vector3 worldPos)
    {
        var boundsArray = gameManager.stageBounds;
        if (boundsArray == null) return -1;

        int count = boundsArray.Length;
        for (int i = 0; i < count; i++)
        {
            var b = boundsArray[i];
            if (b != null && b.bounds.Contains(worldPos)) return i;
        }
        return -1;
    }

    private bool ValidateReferences()
    {
        if (gameManager == null || mapCamera == null) return false;
        if (gameManager.stageBounds == null || gameManager.playerTransform == null) return false;
        return true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // 인스펙터 편의 기능: 배열 크기 자동 맞춤
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (gameManager != null && gameManager.stageBounds != null)
            if (perStageModes == null || perStageModes.Length != gameManager.stageBounds.Length) Array.Resize(ref perStageModes, gameManager.stageBounds.Length);
    }
#endif
}