using UnityEngine;

public class DeadlyLight : MonoBehaviour
{
    [Header("Detection Settings")]
    public LayerMask obstacleLayer; // 벽 레이어 (Obstacle)
    public LayerMask playerLayer;   // 플레이어 레이어 (Player - 설정 필요!)

    private void OnTriggerStay2D(Collider2D collision)
    {
        // [디버깅 1] 무엇이든 들어오면 일단 로그를 찍어봄
        // 이 로그조차 안 뜨면 Rigidbody 2D 문제임!
        Debug.Log($"무언가 들어옴: {collision.name}");

        if (collision.CompareTag("Player"))
        {
            // [디버깅 2] 태그가 Player인 게 확인됨
            Debug.Log("플레이어 태그 확인됨. 레이캐스트 시도...");
            CheckLineOfSight(collision.transform);
        }
    }

    private void CheckLineOfSight(Transform player)
    {
        // 빛의 원점(전구 위치)에서 플레이어까지의 방향과 거리 계산
        Vector2 direction = player.position - transform.position;
        float distance = direction.magnitude;

        // 빛의 위치에서 플레이어 쪽으로 레이저를 쏩니다.
        // obstacleLayer(벽)와 playerLayer(플레이어) 둘 다 감지하도록 설정
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, obstacleLayer | playerLayer);

        // 레이저가 무언가에 맞았는데
        if (hit.collider != null)
        {
            // 그게 벽(Obstacle)이라면? -> 숨었다! (안 죽음)
            if (((1 << hit.collider.gameObject.layer) & obstacleLayer) != 0)
            {
                // 벽 뒤에 숨음. 안전함. (디버그용 초록선)
                Debug.DrawRay(transform.position, direction, Color.green);
            }
            // 그게 플레이어라면? -> 들켰다! (사망)
            else if (hit.collider.gameObject == player.gameObject)
            {
                Debug.DrawRay(transform.position, direction, Color.red);
                Debug.Log("플레이어 감지됨! 게임 오버!");

                // 여기에 게임 오버 로직 연결
                // GameManager.Instance.GameOver(); 
            }
            else
            {
                Debug.Log("감지 실패");
            }
        }
    }
}