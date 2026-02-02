using UnityEngine;
using TMPro;
using System;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public event Action OnPlayerTurnEnd;

    [Header("UI & Stage Settings")]
    public TextMeshProUGUI countText;
    public Transform playerTransform;
    public BoxCollider2D[] stageBounds;
    public int[] stageAssignedCounts;

    [Header("Item Slot Settings")]
    public GameObject[] itemSlots;
    public int[] itemSlotExtraTurns;

    [Header("Key Slot Settings")]
    public GameObject[] keySlots;

    private bool[] keySlotConsumeOnCollect;
    private bool[] itemSlotConsumeOnCollect;
    private int MoveCount = 0;
    private int[] stageRemainingCounts;
    private int currentStageIndex = -1;
    private bool isGameOver = false;

    void Awake()
    {
        if (Instance == null) Instance = this; else Destroy(gameObject);

        // 할당된 카운트 복사 (LINQ보다 빠른 Clone 사용)
        stageRemainingCounts = stageAssignedCounts?.Clone() as int[] ?? new int[0];
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
            stageRemainingCounts[currentStageIndex] = Mathf.Max(0, stageRemainingCounts[currentStageIndex] - 1);
            if (stageRemainingCounts[currentStageIndex] <= 0) HandleGameOver($"Stage {currentStageIndex} Empty");
        }
        UpdateUI();
        OnPlayerTurnEnd?.Invoke();
    }

    public void OnItemCollected(GameObject item)
    {
        if (isGameOver || item == null || itemSlots == null) return;
        UpdateCurrentStage();

        for (int i = 0; i < itemSlots.Length; i++)
        {
            var slot = itemSlots[i];
            if (slot == null) continue;

            // 1.참조, 2.태그, 3.이름(Clone포함) 매칭 로직 그대로 유지
            bool matched = (item == slot) ||
                           (!string.IsNullOrEmpty(slot.tag) && slot.tag != "Untagged" && item.CompareTag(slot.tag)) ||
                           (item.name.Contains(slot.name) || slot.name.Contains(item.name));

            if (matched)
            {
                int add = (itemSlotExtraTurns != null && i < itemSlotExtraTurns.Length) ? itemSlotExtraTurns[i] : 0;

                if (IsValidStage(currentStageIndex)) stageRemainingCounts[currentStageIndex] += add;
                else MoveCount += add;

                if (itemSlotConsumeOnCollect != null && i < itemSlotConsumeOnCollect.Length && itemSlotConsumeOnCollect[i])
                    itemSlots[i] = null;

                UpdateUI();
                return;
            }
        }

        FloatingTextSpawner.Instance?.ShowForCollectedItem(item);
    }

    private void UpdateCurrentStage()
    {
        if (!playerTransform || stageBounds == null) return;
        Vector3 pos = playerTransform.position;
        currentStageIndex = -1;
        for (int i = 0; i < stageBounds.Length; i++)
        {
            if (stageBounds[i] && stageBounds[i].bounds.Contains(pos)) { currentStageIndex = i; break; }
        }
    }

    private bool IsValidStage(int index) => index >= 0 && index < stageRemainingCounts.Length;

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

    public void UpdateUI()
    {
        if (!countText) return;
        if (isGameOver) { countText.text = "Game Over"; return; }

        countText.text = IsValidStage(currentStageIndex)
            ? $"Stage {currentStageIndex} : {stageRemainingCounts[currentStageIndex]}"
            : $"Count: {MoveCount}";
    }

    public void RefreshPlayerStage()
    {
        if (isGameOver) return;

        UpdateCurrentStage();
        UpdateUI();
        MapCameraStageController stageController = FindAnyObjectByType<MapCameraStageController>();
        stageController?.ApplyCurrentStageSettingsImmediate();

        // 필요하다면 스테이지가 바뀌었을 때 추가 로직을 여기에 넣을 수 있습니다.
        Debug.Log($"Stage Refreshed: Current Index is {currentStageIndex}");
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

            if (matched)
            {
                slotIndex = i;
                return true;
            }
        }

        return false;
    }

    public void ConsumeKeySlot(int slotIndex)
    {
        if (keySlots == null || slotIndex < 0 || slotIndex >= keySlots.Length) return;
        if (keySlotConsumeOnCollect != null && slotIndex < keySlotConsumeOnCollect.Length && keySlotConsumeOnCollect[slotIndex]) keySlots[slotIndex] = null;
    }
}
