using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider2D))]
[DisallowMultipleComponent]
public class DestinationPoint : MonoBehaviour
{
    [Tooltip("플레이어가 도착했을 때 호출되는 이벤트입니다. GameManager.PlayerReachedDestination 같은 함수 연결.")]
    public UnityEvent onReached;

    [Tooltip("플레이어 판별: Tag 'Player' 또는 PlayerController 컴포넌트가 있으면 도착으로 간주합니다.")]
    public string playerTag = "Player";

    void Reset()
    {
        // 기본 이름 설정
        if (string.IsNullOrEmpty(gameObject.name) || gameObject.name.StartsWith("GameObject"))
            gameObject.name = "Destination";

        // BoxCollider2D 추가 및 Trigger로 설정
        var bc = GetComponent<BoxCollider2D>();
        if (bc == null) bc = gameObject.AddComponent<BoxCollider2D>();
        bc.isTrigger = true;
    }

    void Start()
    {
        // 안전 체크: Collider가 Trigger인지 확실히
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"{name}: Collider가 Trigger로 설정되지 않았습니다. 자동으로 isTrigger=true 로 변경합니다.");
            col.isTrigger = true;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        // 태그 검사 또는 PlayerController 등의 유무로 플레이어 판별
        bool isPlayer = false;
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag)) isPlayer = true;
        if (!isPlayer && other.GetComponent<Rigidbody2D>() != null) isPlayer = true;

        if (isPlayer)
        {
            Debug.Log($"Destination reached by {other.name}");
            onReached?.Invoke();
        }
    }

    // 씬에서 시각화
    void OnDrawGizmosSelected()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position, 0.2f);
        }
    }
}
