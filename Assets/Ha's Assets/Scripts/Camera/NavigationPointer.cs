using UnityEngine;
using TMPro;

public class NavigationPointer : MonoBehaviour
{
    [Header("UI references")]
    public GameObject arrowVisual;
    public TextMeshProUGUI distanceText;

    private Transform playerTransform;
    private RectTransform canvasRect;
    private Camera uiCamera;
    private Camera mainCamera;
    private MapCameraStageController stageController;
    private Transform targetTransform;

    private float edgePadding = 50f;
    private bool initialized = false;

    public void Initialize(Transform player, Canvas canvasRoot, MapCameraStageController stageControl)
    {
        playerTransform = player;
        stageController = stageControl;
        canvasRect = canvasRoot.GetComponent<RectTransform>();
        uiCamera = (canvasRoot.worldCamera != null) ? canvasRoot.worldCamera : Camera.main;
        mainCamera = Camera.main;

        initialized = true;
    }

    void LateUpdate()
    {
        if (!initialized || playerTransform == null || GameManager.Instance == null) return;

        // 1. 현재 스테이지 인덱스 (Z축 무시 판정)
        int currentIdx = GetCurrentStageIndexXY();

        // 2. 목적지 검색 (Z축 무시하고 X, Y 평면상에 있는지 체크)
        FindTargetXY(currentIdx);

        // 3. 조건 검사
        bool isAutoScale = IsAutoScaleOnly(currentIdx);
        bool hasTarget = targetTransform != null && targetTransform.gameObject.activeInHierarchy;

        if (currentIdx != -1 && isAutoScale && hasTarget)
        {
            // 4. 시야 체크 (X, Y 좌표 기준 화면 밖인지)
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(targetTransform.position);

            // 화면 밖 조건: X나 Y가 0~1 범위를 벗어남 (Z값은 무시하기 위해 판정 제외)
            bool isOffScreen = (viewportPos.x < 0f || viewportPos.x > 1f || viewportPos.y < 0f || viewportPos.y > 1f);

            if (isOffScreen)
            {
                SetUIActive(true);
                UpdateVisuals();
            }
            else
            {
                SetUIActive(false);
            }
        }
        else
        {
            SetUIActive(false);
        }
    }

    // [수정] Z축 무시하고 X, Y로만 스테이지 바운드 판정
    int GetCurrentStageIndexXY()
    {
        var bounds = GameManager.Instance.stageBounds;
        if (bounds == null) return -1;

        Vector2 playerPos2D = new Vector2(playerTransform.position.x, playerTransform.position.y);

        for (int i = 0; i < bounds.Length; i++)
        {
            if (bounds[i] == null) continue;

            Bounds b = bounds[i].bounds;
            // X, Y 범위만 체크
            if (playerPos2D.x >= b.min.x && playerPos2D.x <= b.max.x &&
                playerPos2D.y >= b.min.y && playerPos2D.y <= b.max.y)
            {
                return i;
            }
        }
        return -1;
    }

    // [수정] Z축 무시하고 X, Y로만 목적지 검색
    void FindTargetXY(int stageIdx)
    {
        if (stageIdx == -1) { targetTransform = null; return; }

        Bounds b = GameManager.Instance.stageBounds[stageIdx].bounds;
        var targets = FindObjectsByType<DestinationPoint>(FindObjectsSortMode.None);

        targetTransform = null;
        foreach (var t in targets)
        {
            if (!t.gameObject.activeInHierarchy) continue;

            Vector2 targetPos2D = new Vector2(t.transform.position.x, t.transform.position.y);

            // 목적지의 X, Y 좌표가 현재 스테이지 바운드 박스 안에 있는지 확인
            if (targetPos2D.x >= b.min.x && targetPos2D.x <= b.max.x &&
                targetPos2D.y >= b.min.y && targetPos2D.y <= b.max.y)
            {
                targetTransform = t.transform;
                break;
            }
        }
    }

    bool IsAutoScaleOnly(int stageIdx)
    {
        if (stageController == null || stageIdx < 0 || stageIdx >= stageController.perStageModes.Length) return false;
        return stageController.perStageModes[stageIdx] == MapCameraStageController.StageCameraMode.AutoScaleOnly;
    }

    void UpdateVisuals()
    {
        if (!targetTransform) return;

        // 거리 계산도 Vector2로 (Z차이 무시)
        float dist = Vector2.Distance(
            new Vector2(playerTransform.position.x, playerTransform.position.y),
            new Vector2(targetTransform.position.x, targetTransform.position.y)
        );

        if (distanceText) distanceText.text = Mathf.RoundToInt(dist).ToString();

        // UI 위치는 카메라 투영을 따름
        Vector3 screenPos = mainCamera.WorldToScreenPoint(targetTransform.position);
        if (screenPos.z < 0) screenPos *= -1;

        Vector2 clampedPos = new Vector2(
            Mathf.Clamp(screenPos.x, edgePadding, Screen.width - edgePadding),
            Mathf.Clamp(screenPos.y, edgePadding, Screen.height - edgePadding)
        );

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, clampedPos, uiCamera, out Vector2 localPoint))
        {
            ((RectTransform)transform).anchoredPosition = localPoint;
        }

        if (arrowVisual)
        {
            Vector2 dir = (Vector2)screenPos - clampedPos;
            if (dir.sqrMagnitude > 0.1f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                arrowVisual.transform.localRotation = Quaternion.Euler(0, 0, angle - 90f);
            }
        }
    }

    void SetUIActive(bool active)
    {
        if (arrowVisual && arrowVisual.activeSelf != active) arrowVisual.SetActive(active);
        if (distanceText && distanceText.gameObject.activeSelf != active) distanceText.gameObject.SetActive(active);
    }
}