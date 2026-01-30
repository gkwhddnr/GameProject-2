using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UISlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image bg;
    [SerializeField] private Outline outline;

    private Color normalColor;
    private Color hoverColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    private void Awake()
    {
        if (bg == null) bg = GetComponent<Image>();
        if (outline == null) outline = GetComponent<Outline>();

        normalColor = bg.color;

        // 시작할 때 테두리 끄기
        if (outline != null) outline.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        bg.color = hoverColor;
        if (outline != null) outline.enabled = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        bg.color = normalColor;
        if (outline != null) outline.enabled = false;
    }
}
