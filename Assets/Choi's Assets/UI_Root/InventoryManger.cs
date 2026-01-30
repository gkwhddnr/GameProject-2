using System;
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

    [Header("Item Definitions (3 items)")]
    public ItemDef[] itemDefs; // Bomb/Shuffle/Hint 아이콘 등록

    private Dictionary<ItemType, Sprite> iconMap = new();

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

    private void BuildIconMap()
    {
        iconMap.Clear();
        if (itemDefs == null) return;

        foreach (var def in itemDefs)
        {
            if (!iconMap.ContainsKey(def.type))
                iconMap.Add(def.type, def.icon);
        }
    }

    // ✅ 버튼에서 호출: 랜덤 지급
    public void GiveRandomItem()
    {
        var values = (ItemType[])Enum.GetValues(typeof(ItemType));
        var randomType = values[UnityEngine.Random.Range(0, values.Length)];
        AddItem(randomType, 1);
    }

    // ✅ 아이템 추가(스택 + 순서 유지)
    public void AddItem(ItemType type, int amount)
    {
        // 1) 이미 가지고 있으면 수량만 증가(순서 유지)
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].itemType.HasValue && slots[i].itemType.Value == type)
            {
                slots[i].count += amount;
                slots[i].RefreshCountText();
                return;
            }
        }

        // 2) 없으면 "첫 빈 슬롯"에 추가 (획득 순서대로 위->아래)
        int emptyIndex = FindFirstEmptySlot();
        if (emptyIndex == -1)
        {
            Debug.Log("인벤토리 가득 참!");
            return;
        }

        var icon = GetIcon(type);
        slots[emptyIndex].Set(type, icon, amount);
    }

    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].itemType.HasValue)
                return i;
        }
        return -1;
    }

    private Sprite GetIcon(ItemType type)
    {
        if (iconMap.TryGetValue(type, out var icon))
            return icon;
        return null;
    }

    // ✅ 슬롯 클릭 시 호출: 아이템 사용
    public void TryUseSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        var slot = slots[slotIndex];

        // 아이템 없으면 사용 불가
        if (!slot.itemType.HasValue || slot.count <= 0)
        {
            Debug.Log("빈 슬롯: 사용할 아이템 없음");
            return;
        }

        // 아이템 사용 처리 (여기에서 퍼즐 로직으로 연결)
        UseItem(slot.itemType.Value);

        // 수량 감소
        slot.count -= 1;

        if (slot.count <= 0)
        {
            slot.Clear();
            CompactSlotsUp(); // 빈칸 생기면 아래 아이템들 위로 당기기(정렬 유지)
        }
        else
        {
            slot.RefreshCountText();
        }
    }

    private void UseItem(ItemType type)
    {
        // TODO: 퍼즐 기능에 연결하면 됨
        switch (type)
        {
            case ItemType.Bomb:
                Debug.Log("Bomb 사용! (예: 블록 제거)");
                break;
            case ItemType.Shuffle:
                Debug.Log("Shuffle 사용! (예: 보드 섞기)");
                break;
            case ItemType.Hint:
                Debug.Log("Hint 사용! (예: 힌트 표시)");
                break;
        }
    }

    // ✅ 아래로 밀린 순서 유지하면서 빈칸 제거 (위->아래 꽉 채우기)
    private void CompactSlotsUp()
    {
        for (int i = 0; i < slots.Length - 1; i++)
        {
            if (slots[i].itemType.HasValue) continue;

            // i가 비었으면 아래에서 첫 아이템을 찾아 당겨옴
            int j = i + 1;
            while (j < slots.Length && !slots[j].itemType.HasValue) j++;

            if (j >= slots.Length) break; // 아래에도 없음

            // j의 내용을 i로 복사
            var t = slots[j].itemType.Value;
            var c = slots[j].count;
            var icon = GetIcon(t);

            slots[i].Set(t, icon, c);
            slots[j].Clear();
        }
    }

    private void RefreshAllUI()
    {
        // 초기화 시 slots가 연결돼 있으면 UI 안정화용
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            if (!slots[i].itemType.HasValue) slots[i].Clear();
        }
    }
}
