using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class NavigationPointer : MonoBehaviour
{
    [Header("UI references")]
    public GameObject arrowVisual;
    public TextMeshProUGUI distanceText;

    [Header("Smoothing")]
    public float positionSmoothTime = 0.06f;
    public float rotationSmoothTime = 0.08f;

    [Header("Layout")]
    public float edgePadding = 50f;
    public float onScreenOffsetPixels = 60f;

    [Header("Optimization")]
    [Tooltip("목적지를 다시 검색하는 간격 (초). 너무 자주하면 성능 저하.")]
    public float targetRefreshInterval = 0.5f;

    private RectTransform selfRect;
    private RectTransform arrowRect; // 캐싱
    private Transform playerTransform;
    private RectTransform canvasRect;
    private Camera uiCamera;
    private Camera mainCamera;
    private MapCameraStageController stageController;

    private Transform explicitTarget;
    private Transform targetTransform;

    private Vector2 velocityPos = Vector2.zero;
    private float velocityAngle = 0f;
    private float currentAngle = 0f;

    private int lastStageIdx = -2;
    private float nextRefreshTime = 0f;
    private bool initialized = false;

    public void Initialize(Transform player, Canvas canvasRoot, MapCameraStageController stageControl,
                           Transform explicitTarget = null, float padding = 50f, float offset = 60f)
    {
        playerTransform = player;
        stageController = stageControl;
        this.explicitTarget = explicitTarget;
        edgePadding = padding;
        onScreenOffsetPixels = offset;

        selfRect = GetComponent<RectTransform>();
        if (arrowVisual != null) arrowRect = arrowVisual.GetComponent<RectTransform>();

        if (canvasRoot != null)
        {
            canvasRect = canvasRoot.GetComponent<RectTransform>();
            uiCamera = (canvasRoot.worldCamera != null) ? canvasRoot.worldCamera : Camera.main;
        }

        mainCamera = Camera.main;
        targetTransform = explicitTarget;

        initialized = (selfRect != null && canvasRect != null && playerTransform != null && mainCamera != null);
    }

    void LateUpdate()
    {
        if (!initialized || playerTransform == null || stageController == null)
        {
            SetUIActive(false);
            return;
        }

        int currentIdx = GetCurrentStageIndexXY();
        if (currentIdx < 0)
        {
            SetUIActive(false);
            return;
        }

        // 1. 카메라 모드 체크 (AutoScaleOnly 인지 확인)
        if (!IsAutoScaleOnly(currentIdx))
        {
            SetUIActive(false);
            return;
        }

        // 2. 최적화된 타겟 검색 (스테이지 변경 시 또는 일정 주기마다)
        if (currentIdx != lastStageIdx || Time.time >= nextRefreshTime)
        {
            RefreshTarget(currentIdx);
            lastStageIdx = currentIdx;
            nextRefreshTime = Time.time + targetRefreshInterval;
        }

        if (targetTransform == null || !targetTransform.gameObject.activeInHierarchy)
        {
            SetUIActive(false);
            return;
        }

        // 3. 시야 체크 및 시각화 업데이트
        Vector3 vp = mainCamera.WorldToViewportPoint(targetTransform.position);
        bool isOffScreen = (vp.z < 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f);

        if (isOffScreen)
        {
            SetUIActive(true);
            UpdateVisualsSmooth();
        }
        else
        {
            SetUIActive(false);
        }
    }

    private bool IsAutoScaleOnly(int idx)
    {
        if (stageController.perStageModes == null || idx >= stageController.perStageModes.Length) return false;
        return stageController.perStageModes[idx] == MapCameraStageController.StageCameraMode.AutoScaleOnly;
    }

    private int GetCurrentStageIndexXY()
    {
        var settings = GameManager.Instance?.stageSettings; // 수정됨
        if (settings == null) return -1;

        Vector2 p = playerTransform.position;
        for (int i = 0; i < settings.Length; ++i)
        {
            if (settings[i] == null || settings[i].bounds == null) continue;
            Bounds bb = settings[i].bounds.bounds;
            if (p.x >= bb.min.x && p.x <= bb.max.x && p.y >= bb.min.y && p.y <= bb.max.y) return i;
        }
        return -1;
    }

    private void RefreshTarget(int stageIdx)
    {
        if (explicitTarget != null)
        {
            targetTransform = explicitTarget;
            return;
        }

        var settings = GameManager.Instance.stageSettings; // 수정됨
        if (stageIdx < 0 || stageIdx >= settings.Length || settings[stageIdx].bounds == null) return;

        Bounds b = settings[stageIdx].bounds.bounds;

        var allPoints = FindObjectsByType<DestinationPoint>(FindObjectsSortMode.None);
        targetTransform = null;

        foreach (var dp in allPoints)
        {
            if (dp == null || !dp.gameObject.activeInHierarchy) continue;
            Vector2 pos = dp.transform.position;
            if (pos.x >= b.min.x && pos.x <= b.max.x && pos.y >= b.min.y && pos.y <= b.max.y)
            {
                targetTransform = dp.transform;
                break;
            }
        }
    }

    void UpdateVisualsSmooth()
    {
        // 2D 거리 계산
        float dist = Vector2.Distance(playerTransform.position, targetTransform.position);
        if (distanceText != null) distanceText.text = Mathf.RoundToInt(dist).ToString();

        Vector3 screen3 = mainCamera.WorldToScreenPoint(targetTransform.position);
        if (screen3.z < 0f) screen3 *= -1f;
        Vector2 screenPx = new Vector2(screen3.x, screen3.y);

        Vector2 clamped = new Vector2(
            Mathf.Clamp(screenPx.x, edgePadding, Screen.width - edgePadding),
            Mathf.Clamp(screenPx.y, edgePadding, Screen.height - edgePadding)
        );

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, clamped, uiCamera, out Vector2 localPoint))
        {
            selfRect.anchoredPosition = Vector2.SmoothDamp(selfRect.anchoredPosition, localPoint, ref velocityPos, positionSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        Vector2 toTarget = screenPx - clamped;
        if (toTarget.sqrMagnitude < 0.01f) toTarget = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) - clamped;
        

        float desiredAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
        currentAngle = Mathf.SmoothDampAngle(currentAngle, desiredAngle, ref velocityAngle, rotationSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        if (arrowRect != null) arrowRect.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
    }

    void SetUIActive(bool on)
    {
        if (arrowVisual != null && arrowVisual.activeSelf != on) arrowVisual.SetActive(on);
        if (distanceText != null && distanceText.gameObject.activeSelf != on) distanceText.gameObject.SetActive(on);
    }
}