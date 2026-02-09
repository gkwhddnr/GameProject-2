using UnityEngine;
using UnityEngine.Rendering.Universal;

public class DeadlyLight : MonoBehaviour
{
    [Header("Detection Settings")]
    public LayerMask obstacleLayer;
    public LayerMask playerLayer;

    [Header("Light Settings")]
    public float maxDistance = 5f;
    private Light2D thisLight;
    private void Awake()
    {
        thisLight = GetComponent<Light2D>();
        if (thisLight != null) thisLight.pointLightOuterRadius = maxDistance;
    }

    void Update()
    {
        BreathingEffect();
    }

    private void BreathingEffect()
    {
        if (thisLight != null)
        {
            thisLight.intensity = 1.0f + Mathf.PingPong(Time.time, 0.5f);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            float distanceToPlayer = Vector2.Distance(transform.position, collision.transform.position);
            if (distanceToPlayer > maxDistance) return;

            CheckLineOfSight(collision.transform);
        }
    }

    private void CheckLineOfSight(Transform player)
    {
        // ★ 핵심: 게임매니저가 리스폰 중이라면 아예 로직을 돌리지 않음
        if (GameManager.Instance.IsRespawning) return;

        Vector2 direction = player.position - transform.position;
        float distance = direction.magnitude;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, obstacleLayer | playerLayer);

        if (hit.collider != null)
        {
            // 장애물(벽)에 가려졌는지 확인
            if (((1 << hit.collider.gameObject.layer) & obstacleLayer) != 0)
            {
                // 벽에 맞음 -> 플레이어 못 봄
            }
            // 플레이어를 직접 맞췄다면?
            else if (hit.collider.gameObject == player.gameObject)
            {
                Debug.DrawRay(transform.position, direction, Color.red);

                GameManager.Instance.DieAndRespawn();
            }
        }
    }
}
