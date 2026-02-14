using UnityEngine;
using TMPro;
using System;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public event Action OnPlayerTurnEnd;

    [Serializable]
    public class StageSettings
    {
        public BoxCollider2D bounds;
        public int assignedCount; // 0이면 무한 스테이지

        [Header("캐릭터 이동속도 및 픽셀 설정")]
        public float moveSpeed = 0.15f;
        public float gridSize = 1f;
    }

    [Serializable]
    public class ItemSlotSettings
    {
        public GameObject slotPrefab;
        public int extraTurns;
        public bool consumeOnCollect = true;
    }

    [Header("설정")]
    [Tooltip("에디터에서 시작 지점(빈 오브젝트 등)을 끌어다 놓으세요.")]
    public Transform startPoint;

    private GridMovementSystem _movementSystem;
    private Collider2D _collider;

    [Header("UI & Stage Settings")]
    public TextMeshProUGUI countText;
    public Transform playerTransform;
    public StageSettings[] stageSettings;

    [Header("Item Slot Settings")]
    public ItemSlotSettings[] itemSlotSettings;

    [Tooltip("Key Slot Settings")]
    private GameObject[] keySlots;
    private bool[] keySlotConsumeOnCollect;

    private int MoveCount = 0;
    private int[] stageRemainingCounts;
    private int currentStageIndex = -1;
    private int _prevStageIndex = -1;
    private bool isGameOver = false;
    private bool isRespawning = false;

    public bool IsRespawning => isRespawning;

    // --- Player GridMovementSystem 기본값 캐시 ---
    private float _playerDefaultMoveSpeed = 0f;
    private float _playerDefaultGridSize = 0f;
    private bool _playerDefaultsCached = false;

    void Awake()
    {
        _movementSystem = GetComponent<GridMovementSystem>();
        _collider = GetComponent<Collider2D>();

        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (stageSettings != null)
        {
            stageRemainingCounts = new int[stageSettings.Length];
            for (int i = 0; i < stageSettings.Length; i++) stageRemainingCounts[i] = stageSettings[i].assignedCount;
        }
        else stageRemainingCounts = new int[0];

        UpdateCurrentStage();
        ApplyMovementSettingsToPlayer(); // 초기 적용 (Awake 시)
        UpdateUI();
    }

    public void NotifyTurnProcessed()
    {
        if (isGameOver) return;
        MoveCount++;
        UpdateCurrentStage();

        if (IsValidStage(currentStageIndex))
        {
            // 할당된 카운트가 0보다 클 때만 차감 및 게임오버 체크
            if (stageSettings[currentStageIndex].assignedCount > 0)
            {
                stageRemainingCounts[currentStageIndex] = Mathf.Max(0, stageRemainingCounts[currentStageIndex] - 1);

                if (stageRemainingCounts[currentStageIndex] <= 0) HandleGameOver($"Stage {currentStageIndex} Empty");
            }
            // assignedCount가 0이면 '무한'이므로 아무 작업도 하지 않음
        }

        UpdateUI();
        OnPlayerTurnEnd?.Invoke();
    }

    public void OnItemCollected(GameObject item)
    {
        if (isGameOver || item == null || itemSlotSettings == null) return;
        UpdateCurrentStage();

        for (int i = 0; i < itemSlotSettings.Length; i++)
        {
            var setting = itemSlotSettings[i];
            if (setting == null || setting.slotPrefab == null) continue;

            bool matched = (item == setting.slotPrefab) ||
                           (!string.IsNullOrEmpty(setting.slotPrefab.tag) && setting.slotPrefab.tag != "Untagged" && item.CompareTag(setting.slotPrefab.tag)) ||
                           (item.name.Contains(setting.slotPrefab.name) || setting.slotPrefab.name.Contains(item.name));

            if (matched)
            {
                int add = setting.extraTurns;

                if (IsValidStage(currentStageIndex))
                {
                    // 무한 스테이지라도 일단 내부 값은 증가시키되 로직상 영향은 없음
                    stageRemainingCounts[currentStageIndex] += add;
                }
                else MoveCount += add;

                if (setting.consumeOnCollect) setting.slotPrefab = null;

                UpdateUI();
                return;
            }
        }
        FloatingTextSpawner.Instance?.ShowForCollectedItem(item);
    }

    private void UpdateCurrentStage()
    {
        if (!playerTransform || stageSettings == null) return;
        Vector3 pos = playerTransform.position;
        currentStageIndex = -1;
        for (int i = 0; i < stageSettings.Length; i++)
        {
            if (stageSettings[i].bounds != null && stageSettings[i].bounds.bounds.Contains(pos))
            {
                currentStageIndex = i;
                break;
            }
        }

        // 스테이지 변경 감지: 변경 시 플레이어 이동 설정 적용
        if (currentStageIndex != _prevStageIndex)
        {
            _prevStageIndex = currentStageIndex;
            ApplyMovementSettingsToPlayer();
        }
    }

    private bool IsValidStage(int index) => index >= 0 && index < stageRemainingCounts.Length;

    public void UpdateUI()
    {
        if (!countText) return;
        if (isGameOver) { countText.text = "Game Over"; return; }

        if (IsValidStage(currentStageIndex))
        {
            // --- UI 조건: 0(무한)일 때는 ∞ 표시, 아니면 숫자 표시 ---
            string displayCount = (stageSettings[currentStageIndex].assignedCount == 0)
                                  ? "∞"
                                  : stageRemainingCounts[currentStageIndex].ToString();

            countText.text = $"Stage {currentStageIndex + 1} : {displayCount}";
        }
        else
        {
            countText.text = $"Count: {MoveCount}";
        }
    }
    public void DieAndRespawn()
    {
        // 이미 리스폰 중이면 무시 (이중 사망 방지)
        if (isRespawning) return;

        // 리스폰 프로세스 시작
        StartCoroutine(RespawnProcess());
    }

    // ★ 2. 실제 로직: 하나로 합쳐서 순서대로 실행
    private IEnumerator RespawnProcess()
    {

        isRespawning = true; // 무적 모드 ON
        if (startPoint != null)
        {
            playerTransform.position = startPoint.position;
        }
        else
        {
            Debug.Log("스타트포인트 없음");
        }

        if (_movementSystem != null) _movementSystem.ResetMovement();
        if (_collider != null) _collider.enabled = false;

        Physics2D.SyncTransforms();

        yield return null;
        yield return new WaitForSeconds(0.2f);
        // --- [4] 기능 복구 ---
        if (_collider != null) _collider.enabled = true;

        isRespawning = false; // 무적 모드 OFF
    }

    private void HandleGameOver(string reason)
    {
        if (isGameOver) return;
        isGameOver = true;
        UpdateUI();
        StartCoroutine(QuitRoutine());
    }

    private IEnumerator QuitRoutine()
    {
        yield return new WaitForSeconds(0.5f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void RefreshPlayerStage()
    {
        if (isGameOver) return;
        UpdateCurrentStage();
        UpdateUI();
        ApplyMovementSettingsToPlayer(); // 수동 갱신에서도 적용
        FindAnyObjectByType<MapCameraStageController>()?.ApplyCurrentStageSettingsImmediate();
    }

    public bool IsKeySlotMatch(GameObject item, out int slotIndex)
    {
        slotIndex = -1;
        if (item == null || keySlots == null) return false;
        for (int i = 0; i < keySlots.Length; ++i)
        {
            var slot = keySlots[i];
            if (slot == null) continue;
            bool matched = (item == slot) ||
                           (!string.IsNullOrEmpty(slot.tag) && slot.tag != "Untagged" && item.CompareTag(slot.tag)) ||
                           (item.name.Contains(slot.name) || slot.name.Contains(item.name));
            if (matched) { slotIndex = i; return true; }
        }
        return false;
    }

    public void ConsumeKeySlot(int slotIndex)
    {
        if (keySlots == null || slotIndex < 0 || slotIndex >= keySlots.Length) return;
        if (keySlotConsumeOnCollect != null && slotIndex < keySlotConsumeOnCollect.Length && keySlotConsumeOnCollect[slotIndex]) keySlots[slotIndex] = null;
    }

    // Movement settings helpers
    public void ApplyMovementSettingsToPlayer()
    {
        if (playerTransform == null) return;

        // GridMovementSystem은 보통 플레이어 오브젝트에 붙어있음
        var gridComp = playerTransform.GetComponent<GridMovementSystem>();
        if (gridComp == null) gridComp = playerTransform.GetComponentInChildren<GridMovementSystem>();

        if (gridComp == null) return;

        // 최초 발견 시 기본값 캐시
        if (!_playerDefaultsCached)
        {
            _playerDefaultMoveSpeed = gridComp.moveSpeed;
            _playerDefaultGridSize = gridComp.gridSize;
            _playerDefaultsCached = true;
        }

        // 유효한 스테이지이면 해당 스테이지의 override 적용 (0 이하이면 무시)
        if (IsValidStage(currentStageIndex))
        {
            var s = stageSettings[currentStageIndex];
            if (s != null)
            {
                if (s.moveSpeed > 0f) gridComp.moveSpeed = s.moveSpeed; else gridComp.moveSpeed = _playerDefaultMoveSpeed;
                if (s.gridSize > 0f) gridComp.gridSize = s.gridSize; else gridComp.gridSize = _playerDefaultGridSize;
                return;
            }
        }

        // 스테이지가 없거나 설정이 없으면 기본값 복원
        gridComp.moveSpeed = _playerDefaultMoveSpeed;
        gridComp.gridSize = _playerDefaultGridSize;
    }
}
