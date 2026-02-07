using UnityEngine;
using System.Collections.Generic;

public class ItemPickup : MonoBehaviour
{
    private ItemCollector collector;
    private BoxCollider2D myCollider;
    private List<GameObject> childObjects = new List<GameObject>();
    private List<ParticleSystem> childParticles = new List<ParticleSystem>();
    private bool previousColliderState = false;
    private bool particlesEverActivated = false; // 파티클이 한 번이라도 활성화되었는지 추적

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
            // 시작할 때 콜라이더 상태에 따라 자식들 설정
            UpdateChildrenState(myCollider.enabled);
        }
    }

    void Update()
    {
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
        // 콜라이더가 활성화되면
        if (colliderEnabled)
        {
            // 일반 자식들 활성화
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
        // 콜라이더가 비활성화될 때
        else
        {
            // 파티클이 아직 활성화되지 않았으면 자식들 전부 비활성화
            if (!particlesEverActivated)
            {
                SetChildrenActive(false);
            }
            // 파티클이 이미 활성화되었으면 파티클만 켜진 채로 유지
            else
            {
                SetChildrenActiveExceptParticles(false);
            }
        }
    }

    private void SetChildrenActive(bool active)
    {
        // 모든 자식 오브젝트 활성화/비활성화
        foreach (var child in childObjects)
        {
            if (child != null)
            {
                child.SetActive(active);
            }
        }
    }

    private void SetChildrenActiveExceptParticles(bool active)
    {
        // 파티클을 제외한 자식들만 활성화/비활성화
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

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (collector != null) collector.CollectBy(gameObject);
    }
}