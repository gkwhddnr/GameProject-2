using UnityEngine;

/// <summary>
/// 씬의 아이템 오브젝트들의 Sprite를 자동으로 InventoryManager에 등록
/// Kit, Shield 등의 아이템 오브젝트에 부착하여 사용
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ItemSpriteRegistrar : MonoBehaviour
{
    [Header("아이템 타입")]
    [Tooltip("이 오브젝트가 나타내는 아이템 타입")]
    public ItemType itemType;

    [Header("인벤토리 아이콘")]
    [Tooltip("인벤토리에 표시될 아이콘 (비어있으면 SpriteRenderer의 Sprite 사용)")]
    public Sprite inventoryIcon;

    [Header("자동 등록 설정")]
    [Tooltip("게임 시작 시 자동으로 InventoryManager에 등록")]
    public bool registerOnStart = true;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (registerOnStart)
        {
            RegisterSprite();
        }
    }

    /// <summary>
    /// 이 아이템의 Sprite를 InventoryManager에 등록
    /// </summary>
    public void RegisterSprite()
    {
        // 사용할 Sprite 결정: inventoryIcon이 있으면 그것 사용, 없으면 SpriteRenderer 사용
        Sprite spriteToUse = inventoryIcon != null ? inventoryIcon : GetSpriteFromRenderer();

        if (spriteToUse == null)
        {
            Debug.LogError($"[ItemSpriteRegistrar] {gameObject.name}에 등록할 Sprite가 없습니다! " +
                "Inventory Icon을 설정하거나 SpriteRenderer에 Sprite를 설정해주세요.");
            return;
        }

        if (itemType == ItemType.None)
        {
            Debug.LogWarning($"[ItemSpriteRegistrar] {gameObject.name}의 ItemType이 None입니다. 타입을 설정해주세요.");
            return;
        }

        // InventoryManager에 Sprite 등록
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.RegisterItemSprite(itemType, spriteToUse);
            Debug.Log($"[ItemSpriteRegistrar] {itemType} 아이템 Sprite 등록 완료: {spriteToUse.name}");
        }
        else
        {
            Debug.LogWarning("[ItemSpriteRegistrar] InventoryManager를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// SpriteRenderer에서 Sprite 가져오기
    /// </summary>
    private Sprite GetSpriteFromRenderer()
    {
        if (spriteRenderer == null)
        {
            Debug.LogError($"[ItemSpriteRegistrar] {gameObject.name}에 SpriteRenderer가 없습니다!");
            return null;
        }

        if (spriteRenderer.sprite == null)
        {
            Debug.LogError($"[ItemSpriteRegistrar] {gameObject.name}의 SpriteRenderer에 Sprite가 설정되지 않았습니다!");
            return null;
        }

        return spriteRenderer.sprite;
    }

    /// <summary>
    /// Inspector에서 아이템 타입 자동 감지 (레이어 기반)
    /// </summary>
    private void OnValidate()
    {
        // 레이어 이름으로 자동 감지
        string layerName = LayerMask.LayerToName(gameObject.layer);
        ItemType detectedType = ItemLayers.GetItemType(layerName);

        if (detectedType != ItemType.None && itemType == ItemType.None)
        {
            itemType = detectedType;
            Debug.Log($"[ItemSpriteRegistrar] {gameObject.name}의 ItemType을 자동으로 {detectedType}(으)로 설정했습니다.");
        }
    }
}