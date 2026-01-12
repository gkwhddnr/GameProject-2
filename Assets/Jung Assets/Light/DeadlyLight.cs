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
        Vector2 direction = player.position - transform.position;
        float distance = direction.magnitude;

        // 플레이어까지 레이저를 쏩니다.
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, obstacleLayer | playerLayer);

        if (hit.collider != null)
        {
            if (((1 << hit.collider.gameObject.layer) & obstacleLayer) != 0)
            {

            }
            // 벽 없이 바로 플레이어를 맞췄다면?
            else if (hit.collider.gameObject == player.gameObject)
            {
                Debug.DrawRay(transform.position, direction, Color.red);
                Debug.Log("플레이어 사망!");
                // GameManager.Instance.GameOver();
            }
        }
    }
}