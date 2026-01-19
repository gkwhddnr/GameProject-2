using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    private ItemCollector collector;

    void Awake()
    {
        collector = FindFirstObjectByType<ItemCollector>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        collector?.CollectBy(gameObject);
    }
}
