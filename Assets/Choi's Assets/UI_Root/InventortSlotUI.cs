using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인벤토리 슬롯 UI (수정됨 - SetIcon 메서드 추가)
/// </summary>
public class InventorySlotUI : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;
    public TextMeshProUGUI countText;
    public Button button;

    [Header("Visual Settings")]
    public Color emptyColor = new Color(1, 1, 1, 0.3f);
    public Color filledColor = new Color(1, 1, 1, 1f);

    [HideInInspector] public ItemType? itemType;
    [HideInInspector] public int count;

    private int slotIndex;

    private void Awake()
    {
        // 슬롯 인덱스 자동 설정 (부모의 자식 순서)
        slotIndex = transform.GetSiblingIndex();

        // 버튼 클릭 이벤트 연결
        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }
    }

    /// <summary>
    /// 슬롯에 아이템 설정
    /// </summary>
    public void Set(ItemType type, Sprite icon, int amount)
    {
        itemType = type;
        count = amount;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.color = filledColor;
            iconImage.enabled = icon != null;
        }

        RefreshCountText();
    }

    /// <summary>
    /// 아이콘만 업데이트 (동적 Sprite 변경용)
    /// </summary>
    public void SetIcon(Sprite icon)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            if (icon != null)
            {
                iconImage.color = filledColor;
                iconImage.enabled = true;
            }
        }
    }

    /// <summary>
    /// 슬롯 비우기
    /// </summary>
    public void Clear()
    {
        itemType = null;
        count = 0;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.color = emptyColor;
            iconImage.enabled = false;
        }

        if (countText != null)
        {
            countText.text = "";
        }
    }

    /// <summary>
    /// 수량 텍스트 갱신
    /// </summary>
    public void RefreshCountText()
    {
        if (countText != null)
        {
            if (itemType.HasValue && count > 0)
            {
                countText.text = count > 1 ? count.ToString() : "";
            }
            else
            {
                countText.text = "";
            }
        }
    }

    /// <summary>
    /// 슬롯 클릭 이벤트
    /// </summary>
    private void OnClick()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.TryUseSlot(slotIndex);
        }
    }
}