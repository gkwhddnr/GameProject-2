using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryItemCollector : MonoBehaviour
{
    [Header("수집 설정")]
    [Tooltip("수집 가능한 아이템 레이어들")]
    public LayerMask collectibleLayerMask;

    [Tooltip("수집 시 아이템 페이드아웃")]
    public bool fadeOutOnCollect = true;

    [Tooltip("페이드아웃 지속시간")]
    public float fadeDuration = 0.5f;

    [Tooltip("페이드아웃 후 파괴")]
    public bool destroyAfterFade = true;

    [Header("디버그")]
    [Tooltip("디버그 로그 출력")]
    public bool debugLog = true;

    // 이미 수집한 아이템 추적
    private HashSet<int> collectedItemIds = new HashSet<int>();

    [Tooltip("옵션: 씬의 InventoryManager를 수동 연결하려면 드래그")]
    public InventoryManager inventoryManagerReference;

    // 내부: 플레이어에 붙일 relay 컴포넌트 (런타임에 추가)
    private PlayerCollisionRelay _relay;

    #region 런타임 플레이어 리스너(전달자)
    // 플레이어 오브젝트에 붙여져 충돌 이벤트를 전달하는 간단 컴포넌트
    private class PlayerCollisionRelay : MonoBehaviour
    {
        public InventoryItemCollector owner;

        private void OnTriggerEnter2D(Collider2D other)
        {
            owner?.TryCollect(other.gameObject);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            owner?.TryCollect(collision.gameObject);
        }
    }
    #endregion

    private void Awake()
    {
        if (inventoryManagerReference == null && InventoryManager.Instance != null) inventoryManagerReference = InventoryManager.Instance;
    }

    private void Start()
    {

        Transform playerTf = null;
        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
            playerTf = GameManager.Instance.playerTransform;
        else
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) playerTf = go.transform;
        }

        if (playerTf == null)
        {
            if (debugLog) Debug.LogWarning("[InventoryItemCollector] 플레이어를 찾지 못했습니다. (GameManager.playerTransform 또는 Tag 'Player' 필요)");
            return;
        }

        // 플레이어에 전달자 컴포넌트 붙이기 (이미 붙어있으면 재사용)
        _relay = playerTf.GetComponent<PlayerCollisionRelay>();
        if (_relay == null)
        {
            _relay = playerTf.gameObject.AddComponent<PlayerCollisionRelay>();
            _relay.owner = this;
            if (debugLog) Debug.Log("[InventoryItemCollector] 플레이어에 PlayerCollisionRelay를 추가하여 충돌을 수신합니다.");
        }
        else
        {
            _relay.owner = this;
            if (debugLog) Debug.Log("[InventoryItemCollector] 기존 PlayerCollisionRelay를 재사용합니다.");
        }
    }

    /// <summary>
    /// 외부에서 직접 호출할 수도 있게 public으로 열어둠.
    /// ItemCollector.TryCollect과 비슷한 흐름으로 동작하도록 구현.
    /// </summary>
    public void TryCollect(GameObject candidate)
    {
        if (candidate == null) return;

        // 이미 수집된 인스턴스인지 체크
        int id = candidate.GetInstanceID();
        if (collectedItemIds.Contains(id)) return;

        // 아이템 레이어 검사 (collectibleLayerMask)
        if (!IsCollectibleLayer(candidate.layer)) return;

        // 레이어 이름 -> ItemType 변환 (ItemLayers 클래스 사용)
        string layerName = LayerMask.LayerToName(candidate.layer);
        ItemType itemType = ItemLayers.GetItemType(layerName);

        if (itemType == ItemType.None)
        {
            // 수집 가능한 레이어에 있지만 매핑이 없으면 로그만 남기고 제거 처리
            if (debugLog) Debug.LogWarning($"[InventoryItemCollector] 수집 레이어지만 ItemType 매핑 없음: {layerName}, 오브젝트: {candidate.name}");
            collectedItemIds.Add(id);
            StartRemoval(candidate);
            return;
        }

        // 중복 방지로 ID 추가
        collectedItemIds.Add(id);

        if (debugLog) Debug.Log($"[InventoryItemCollector] 수집: {candidate.name} (Layer:{layerName}) => {itemType}");

        // 인벤토리에 추가 (InventoryManager 우선)
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(itemType, 1);
        }
        else if (inventoryManagerReference != null)
        {
            inventoryManagerReference.AddItem(itemType, 1);
        }
        else
        {
            if (debugLog) Debug.LogWarning("[InventoryItemCollector] InventoryManager가 없어 인벤토리 추가가 이루어지지 않았습니다.");
        }

        // 사운드/플로팅 텍스트/게임매니저 알림(존재하면)
        SoundManager.Instance?.PlayCollect();
        FloatingTextSpawner.Instance?.ShowForCollectedItem(candidate);
        GameManager.Instance?.OnItemCollected(candidate);

        // 아이템 제거(페이드 or 비활성/파괴)
        StartRemoval(candidate);
    }

    private void StartRemoval(GameObject item)
    {
        if (fadeOutOnCollect)
            StartCoroutine(FadeOutAndDestroy(item));
        else
        {
            if (destroyAfterFade) Destroy(item);
            else item.SetActive(false);
        }
    }

    private IEnumerator FadeOutAndDestroy(GameObject item)
    {
        if (item == null) yield break;

        // 비활성 객체라도 렌더러를 얻기 위해 true로 검색
        var sprs = item.GetComponentsInChildren<SpriteRenderer>(true);
        var orig = new Color[sprs.Length];
        for (int i = 0; i < sprs.Length; i++) orig[i] = sprs[i] ? sprs[i].color : Color.white;

        var cols = item.GetComponentsInChildren<Collider2D>(true);
        foreach (var c in cols) if (c) c.enabled = false;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Clamp01(1f - (elapsed / fadeDuration));
            for (int i = 0; i < sprs.Length; i++)
            {
                if (sprs[i]) { var ccol = orig[i]; ccol.a = a; sprs[i].color = ccol; }
            }
            yield return null;
        }

        if (item != null)
        {
            if (destroyAfterFade) Destroy(item);
            else item.SetActive(false);
        }
    }

    private bool IsCollectibleLayer(int layer)
    {
        return ((1 << layer) & collectibleLayerMask.value) != 0;
    }

    /// <summary>
    /// 수집 기록 초기화
    /// </summary>
    public void ClearCollectionHistory()
    {
        collectedItemIds.Clear();
        if (debugLog) Debug.Log("[InventoryItemCollector] 수집 기록 초기화 완료");
    }

    private void OnDestroy()
    {
        // 플레이어에 붙인 relay를 제거하지는 않음(파괴시 안전), 다만 owner 참조 끊어둠
        if (_relay != null) _relay.owner = null;
        ClearCollectionHistory();
    }
}
