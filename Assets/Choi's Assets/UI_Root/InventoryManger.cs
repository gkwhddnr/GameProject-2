using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    [Serializable]
    public class ItemDef
    {
        public ItemType type;
        public Sprite icon;
    }

    public static InventoryManager Instance { get; private set; }

    [Header("Slot UI (Top -> Bottom)")]
    public InventorySlotUI[] slots = new InventorySlotUI[6];

    [Header("Item Definitions")]
    public ItemDef[] itemDefs; // 아이템 타입별 아이콘 등록

    private GameObject playerObject;
    private Dictionary<ItemType, Sprite> iconMap = new();

    public float waitForGameManagerTimeout = 5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildIconMap();

        // UI 초기화(슬롯 존재여부 체크 포함)
        RefreshAllUI();
    }

    private void Start()
    {
        // inspector에 수동으로 넣어놨으면 그대로 사용
        if (playerObject != null) return;

        // 우선 바로 GameManager가 있으면 가능한 값으로 셋팅
        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            playerObject = GameManager.Instance.playerTransform.gameObject;
            Debug.Log("[InventoryManager] playerObject를 GameManager.playerTransform에서 설정했습니다.");
            return;
        }

        // GameManager가 아직 없거나 playerTransform이 비어있다면 대기 코루틴 시작
        StartCoroutine(WaitForGameManagerAndAssign(waitForGameManagerTimeout));
    }

    private IEnumerator WaitForGameManagerAndAssign(float timeoutSeconds)
    {
        float elapsed = 0f;
        while ((GameManager.Instance == null || GameManager.Instance.playerTransform == null) && elapsed < timeoutSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            playerObject = GameManager.Instance.playerTransform.gameObject;
            Debug.Log("[InventoryManager] 대기 후 GameManager.playerTransform에서 playerObject를 할당했습니다.");
        }
        else
        {
            Debug.LogWarning("[InventoryManager] GameManager 또는 playerTransform을 찾지 못했습니다. playerObject가 null 상태입니다. (Inspector에 수동 할당 권장)");
        }
    }

    private void BuildIconMap()
    {
        iconMap.Clear();
        if (itemDefs == null) return;

        foreach (var def in itemDefs)
        {
            if (!iconMap.ContainsKey(def.type)) iconMap.Add(def.type, def.icon);
        }
    }

    /// <summary>
    /// 랜덤 아이템 지급 (테스트용 버튼)
    /// </summary>
    public void GiveRandomItem()
    {
        var values = (ItemType[])Enum.GetValues(typeof(ItemType));
        var validTypes = new List<ItemType>();
        foreach (var v in values) if (v != ItemType.None) validTypes.Add(v);

        if (validTypes.Count == 0) return;

        var randomType = validTypes[UnityEngine.Random.Range(0, validTypes.Count)];
        AddItem(randomType, 1);
    }

    /// <summary>
    /// 아이템 추가 (스택 + 순서 유지)
    /// </summary>
    public void AddItem(ItemType type, int amount)
    {
        Debug.Log($"[InventoryManager] {type} 아이템 {amount}개 추가");

        // 1) 이미 가지고 있으면 수량만 증가
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            if (slots[i].itemType.HasValue && slots[i].itemType.Value == type)
            {
                slots[i].count += amount;
                slots[i].RefreshCountText();
                return;
            }
        }

        // 2) 없으면 첫 빈 슬롯에 추가
        int emptyIndex = FindFirstEmptySlot();
        if (emptyIndex == -1)
        {
            Debug.LogWarning("[InventoryManager] 인벤토리 가득 찼습니다!");
            return;
        }

        var icon = GetIcon(type);
        slots[emptyIndex].Set(type, icon, amount);
    }

    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            if (!slots[i].itemType.HasValue) return i;
        }
        return -1;
    }

    private Sprite GetIcon(ItemType type)
    {
        if (iconMap.TryGetValue(type, out var icon)) return icon;
        return null;
    }

    /// <summary>
    /// 슬롯 클릭 시 호출: 아이템 사용
    /// </summary>
    public void TryUseSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        var slot = slots[slotIndex];

        if (slot == null) return;

        // 아이템 없으면 사용 불가
        if (!slot.itemType.HasValue || slot.count <= 0) return;

        // ★ 아이템 효과 실행
        UseItem(slot.itemType.Value);

        // 수량 감소
        slot.count -= 1;

        if (slot.count <= 0)
        {
            slot.Clear();
            CompactSlotsUp(); // 빈칸 생기면 위로 정렬
        }
        else slot.RefreshCountText();
    }

    /// <summary>
    /// 아이템 효과 실행 (Factory Pattern 사용)
    /// </summary>
    private void UseItem(ItemType type)
    {
        Debug.Log($"[InventoryManager] {type} 아이템 사용!");

        // GameManager 턴 처리 (선택사항)
        if (GameManager.Instance != null) GameManager.Instance.NotifyTurnProcessed();

        // ★ Factory에서 효과 객체 가져와서 실행
        IItemEffect effect = ItemEffectFactory.GetEffect(type);

        if (effect != null)
        {
            if (playerObject != null) effect.Execute(playerObject);
            else Debug.LogWarning("[InventoryManager] playerObject가 null입니다. 아이템 효과가 적용되지 않습니다.");
        }
        else
        {
            Debug.LogWarning($"[InventoryManager] {type}에 대한 효과가 정의되지 않았습니다.");
        }
    }

    /// <summary>
    /// 아래로 밀린 순서 유지하면서 빈칸 제거
    /// </summary>
    private void CompactSlotsUp()
    {
        for (int i = 0; i < slots.Length - 1; i++)
        {
            if (slots[i] == null) continue;
            if (slots[i].itemType.HasValue) continue;

            int j = i + 1;
            while (j < slots.Length && (slots[j] == null || !slots[j].itemType.HasValue)) j++;

            if (j >= slots.Length) break;

            var t = slots[j].itemType.Value;
            var c = slots[j].count;
            var icon = GetIcon(t);

            slots[i].Set(t, icon, c);
            slots[j].Clear();
        }
    }

    private void RefreshAllUI()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            if (!slots[i].itemType.HasValue) slots[i].Clear();
        }
    }

    /// <summary>
    /// 특정 타입 아이템 개수 확인
    /// </summary>
    public int GetItemCount(ItemType type)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            if (slots[i].itemType.HasValue && slots[i].itemType.Value == type) return slots[i].count;
        }
        return 0;
    }

    /// <summary>
    /// 특정 타입 아이템 보유 여부
    /// </summary>
    public bool HasItem(ItemType type) { return GetItemCount(type) > 0; }
}
