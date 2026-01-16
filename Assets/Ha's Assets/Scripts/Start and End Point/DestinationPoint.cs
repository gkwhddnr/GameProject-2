using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider2D))]
[DisallowMultipleComponent]
public class DestinationPoint : MonoBehaviour
{
    [Tooltip("플레이어가 도착했을 때 호출되는 이벤트입니다. (인스펙터에서 GameManager 함수 연결 가능)")]
    public UnityEvent onReached;

    [Tooltip("플레이어 판별: Tag 'Player' 또는 PlayerController 컴포넌트가 있으면 도착으로 간주합니다.")]
    public string playerTag = "Player";

    public bool useCollision = true;

    void Reset()
    {
        if (string.IsNullOrEmpty(gameObject.name) || gameObject.name.StartsWith("GameObject"))
            gameObject.name = "Destination";

        var bc = GetComponent<BoxCollider2D>();
        if (bc == null) bc = gameObject.AddComponent<BoxCollider2D>();
        // Reset 시점에는 useCollision 값이 인스펙터 기본값(true)일 수 있으므로 일단 설정
        bc.isTrigger = !useCollision;
    }

    void Start()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            // 시작 시점에 옵션에 따라 isTrigger를 맞춤
            col.isTrigger = !useCollision;
        }
    }

    // Collision 방식 (Collider.isTrigger == false 상태에서 호출)
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!useCollision) return;

        var otherGO = collision.collider?.gameObject;
        if (otherGO == null) return;

        if (IsPlayerObject(otherGO))
        {
            HandleReached(otherGO);
        }
    }

    // Trigger 방식 (Collider.isTrigger == true 상태에서 호출)
    void OnTriggerEnter2D(Collider2D other)
    {
        if (useCollision) return;

        if (other == null) return;

        var otherGO = other.gameObject;
        if (IsPlayerObject(otherGO))
        {
            HandleReached(otherGO);
        }
    }

    bool IsPlayerObject(GameObject go)
    {
        if (go == null) return false;

        // 태그 우선 검사
        if (!string.IsNullOrEmpty(playerTag) && go.CompareTag(playerTag)) return true;
        if (go.GetComponent<Rigidbody2D>() != null && string.IsNullOrEmpty(playerTag)) return true;

        return false;
    }

    void HandleReached(GameObject playerGO)
    {
        Debug.Log($"Destination reached by {playerGO.name}");

        // 1) UnityEvent 호출 (인스펙터에서 GameManager의 PlayerReachedDestination 등 연결 가능)
        onReached?.Invoke();

        // 2) 테스트용 종료 처리(명시적 GameManager 의존성 없이 동작하게 함)
        EndGameForTest(playerGO);
    }

    void EndGameForTest(GameObject playerGO)
    {
        // 게임 일시정지 (테스트용)
        Time.timeScale = 0f;


        // Rigidbody2D 속도 초기화
        var rb = playerGO.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        Debug.Log("EndGameForTest: 게임을 일시정지하고 플레이어 입력을 비활성화했습니다. (테스트 동작)");
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
