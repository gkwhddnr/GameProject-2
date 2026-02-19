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
    public ItemDef[] itemDefs;

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
        RefreshAllUI();
    }

    private void Start()
    {
        if (playerObject != null) return;

        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            playerObject = GameManager.Instance.playerTransform.gameObject;
            Debug.Log("[InventoryManager] playerObject를 GameManager.playerTransform에서 설정했습니다.");
            return;
        }

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
            Debug.LogWarning("[InventoryManager] GameManager 또는 playerTransform을 찾지 못했습니다. playerObject가 null 상태입니다.");
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

    public void GiveRandomItem()
    {
        var values = (ItemType[])Enum.GetValues(typeof(ItemType));
        var validTypes = new List<ItemType>();
        foreach (var v in values) if (v != ItemType.None) validTypes.Add(v);

        if (validTypes.Count == 0) return;

        var randomType = validTypes[UnityEngine.Random.Range(0, validTypes.Count)];
        AddItem(randomType, 1);
    }

    public void AddItem(ItemType type, int amount)
    {
        Debug.Log($"[InventoryManager] {type} 아이템 {amount}개 추가");

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
        if (!slot.itemType.HasValue || slot.count <= 0) return;

        // ★ 아이템 사용 시도 (UseItem이 false를 반환하면 사용 실패)
        bool success = UseItem(slot.itemType.Value);

        // ★ 사용 성공한 경우에만 아이템 소모
        if (success)
        {
            slot.count -= 1;

            if (slot.count <= 0)
            {
                slot.Clear();
                CompactSlotsUp();
            }
            else
            {
                slot.RefreshCountText();
            }
        }
        else
        {
            Debug.Log($"[InventoryManager] {slot.itemType.Value} 아이템을 사용할 수 없습니다. (조건 불충족)");
        }
    }

    /// <summary>
    /// 아이템 효과 실행 (Factory Pattern 사용)
    /// ★ 반환값: true = 사용 성공, false = 사용 실패
    /// </summary>
    private bool UseItem(ItemType type)
    {
        Debug.Log($"[InventoryManager] {type} 아이템 사용 시도...");

        // ★ Shield 특수 처리: 이미 활성화 중이면 사용 불가
        if (type == ItemType.Shield)
        {
            ShieldEffectController controller = FindFirstObjectByType<ShieldEffectController>();
            if (controller != null && controller.IsShieldActive)
            {
                Debug.LogWarning("[InventoryManager] Shield가 이미 활성화되어 있어 사용할 수 없습니다!");
                return false; // ★ 사용 실패
            }
        }

        // GameManager 턴 처리
        if (GameManager.Instance != null)
            GameManager.Instance.NotifyTurnProcessed();

        // Factory에서 효과 객체 가져와서 실행
        IItemEffect effect = ItemEffectFactory.GetEffect(type);

        if (effect != null)
        {
            if (playerObject != null)
            {
                effect.Execute(playerObject);
                return true; // ★ 사용 성공
            }
            else
            {
                Debug.LogWarning("[InventoryManager] playerObject가 null입니다. 아이템 효과가 적용되지 않습니다.");
                return false; // ★ 사용 실패
            }
        }
        else
        {
            Debug.LogWarning($"[InventoryManager] {type}에 대한 효과가 정의되지 않았습니다.");
            return false; // ★ 사용 실패
        }
    }

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

    public int GetItemCount(ItemType type)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            if (slots[i].itemType.HasValue && slots[i].itemType.Value == type)
                return slots[i].count;
        }
        return 0;
    }

    public bool HasItem(ItemType type)
    {
        return GetItemCount(type) > 0;
    }
}