using UnityEngine;
using System.Collections.Generic;

public class ItemPickup : MonoBehaviour
{
    private ItemCollector collector;
    private BoxCollider2D myCollider;
    private List<GameObject> childObjects = new List<GameObject>();
    private List<ParticleSystem> childParticles = new List<ParticleSystem>();
    private bool previousColliderState = false;
    private bool particlesEverActivated = false;

    // 중복 수집 방지 플래그 (충돌/트리거가 여러 번 들어오는 걸 막음)
    private bool hasCollected = false;

    void Awake()
    {
        collector = FindFirstObjectByType<ItemCollector>();
        myCollider = GetComponent<BoxCollider2D>();

        // 자식 오브젝트와 파티클 시스템을 모두 찾아서 리스트에 저장
        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject child = transform.GetChild(i).gameObject;
            childObjects.Add(child);
            ParticleSystem ps = child.GetComponent<ParticleSystem>();
            if (ps != null) childParticles.Add(ps);
        }

        // 초기 콜라이더 상태 저장
        if (myCollider != null)
        {
            previousColliderState = myCollider.enabled;
            UpdateChildrenState(myCollider.enabled);
        }
    }

    void Update()
    {
        // ★ 수정: 수집된 후에는 더 이상 자식 상태 업데이트 안함!
        if (hasCollected) return;

        if (myCollider != null)
        {
            // 콜라이더 상태가 변경되었을 때만 업데이트
            if (myCollider.enabled != previousColliderState)
            {
                previousColliderState = myCollider.enabled;
                UpdateChildrenState(myCollider.enabled);
            }
        }
    }

    private void UpdateChildrenState(bool colliderEnabled)
    {
        if (colliderEnabled)
        {
            SetChildrenActive(true);

            // 파티클은 처음 한 번만 활성화
            if (!particlesEverActivated)
            {
                foreach (var ps in childParticles)
                {
                    if (ps != null)
                    {
                        ps.gameObject.SetActive(true);
                        ps.Play();
                    }
                }
                particlesEverActivated = true;
            }
        }
        else
        {
            // 파티클이 아직 활성화되지 않았으면 자식들 전부 비활성화
            if (!particlesEverActivated)
                SetChildrenActive(false);

            // 파티클이 이미 활성화되었으면 파티클만 켜진 채로 유지
            else
                SetChildrenActiveExceptParticles(false);
        }
    }

    private void SetChildrenActive(bool active)
    {
        foreach (var child in childObjects)
        {
            if (child != null) child.SetActive(active);
        }
    }

    private void SetChildrenActiveExceptParticles(bool active)
    {
        foreach (var child in childObjects)
        {
            if (child != null)
            {
                // 파티클 시스템이 있는 자식은 건너뛰기
                if (child.GetComponent<ParticleSystem>() == null)
                {
                    child.SetActive(active);
                }
            }
        }
    }

    // 트리거로 들어왔을 때 (Player 태그 검사)
    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasCollected) return;
        if (!other.CompareTag("Player")) return;

        hasCollected = true;

        if (myCollider != null) myCollider.enabled = false;
        if (collector != null) collector.CollectBy(gameObject);
    }

    // 물리 충돌(트리거가 아닌 경우)도 동일 처리
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasCollected) return;
        if (!collision.collider.CompareTag("Player")) return;

        hasCollected = true;

        if (myCollider != null) myCollider.enabled = false;
        if (collector != null) collector.CollectBy(gameObject);
    }
}