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
    }

    [Serializable]
    public class ItemSlotSettings
    {
        public GameObject slotPrefab;
        public int extraTurns;
        public bool consumeOnCollect = true;
    }

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
    private bool isGameOver = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (stageSettings != null)
        {
            stageRemainingCounts = new int[stageSettings.Length];
            for (int i = 0; i < stageSettings.Length; i++)
            {
                stageRemainingCounts[i] = stageSettings[i].assignedCount;
            }
        }
        else { stageRemainingCounts = new int[0]; }

        UpdateCurrentStage();
        UpdateUI();
    }

    public void NotifyTurnProcessed()
    {
        if (isGameOver) return;
        MoveCount++;
        UpdateCurrentStage();

        if (IsValidStage(currentStageIndex))
        {
            // --- 추가된 조건: 할당된 카운트가 0보다 클 때만 차감 및 게임오버 체크 ---
            if (stageSettings[currentStageIndex].assignedCount > 0)
            {
                stageRemainingCounts[currentStageIndex] = Mathf.Max(0, stageRemainingCounts[currentStageIndex] - 1);

                if (stageRemainingCounts[currentStageIndex] <= 0)
                    HandleGameOver($"Stage {currentStageIndex} Empty");
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

            countText.text = $"Stage {currentStageIndex} : {displayCount}";
        }
        else
        {
            countText.text = $"Count: {MoveCount}";
        }
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
}