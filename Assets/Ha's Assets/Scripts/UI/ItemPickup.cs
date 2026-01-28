using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    private ItemCollector collector;

    void Awake()
    {
        // 씬에서 수집기를 한 번만 찾습니다.
        collector = FindFirstObjectByType<ItemCollector>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 플레이어 태그 확인 로직 원본 유지
        if (!other.CompareTag("Player")) return;

        // 수집 로직 실행
        if (collector != null) collector.CollectBy(gameObject);
    }
}