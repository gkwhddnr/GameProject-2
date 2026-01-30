using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;              // 자식 Icon
    public TMP_Text countText;           // 자식 CountText

    [HideInInspector] public ItemType? itemType = null;
    [HideInInspector] public int count = 0;

    public void Set(ItemType type, Sprite icon, int newCount)
    {
        itemType = type;
        count = newCount;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.color = new Color(1, 1, 1, 1); // 보이게
        }

        RefreshCountText();
    }

    public void Clear()
    {
        itemType = null;
        count = 0;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.color = new Color(1, 1, 1, 0); // 안 보이게(투명)
        }

        if (countText != null) countText.text = "";
    }

    public void RefreshCountText()
    {
        if (countText == null) return;

        // 마인크래프트처럼 1개면 숫자 숨기고, 2개 이상이면 표시
        countText.text = (count >= 2) ? count.ToString() : "";
    }
}
