using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider2D))]
[DisallowMultipleComponent]
public class DestinationPoint : MonoBehaviour
{
    private const bool V = true;
    private const string V1 = "Player";
    [Tooltip("플레이어가 도착했을 때 호출되는 이벤트입니다. (인스펙터에서 GameManager 함수 연결 가능)")]
    public UnityEvent onReached;

    [Header("캐릭터가 목표지점에 도착 시 딜레이 후 이동")]
    public float delaySeconds = 0.6f;

    [Header("설정")]
    [Tooltip("이동하는 동안 플레이어의 조작을 막을지 여부")]
    private bool disablePlayerMovementDuringDelay = true;
    [Tooltip("물리 연산을 멈출지 여부 (밀림 방지)")]
    private readonly bool disableRigidbodySimulationDuringDelay = V;

    private readonly string playerTag = V1;
    private bool triggered = false; // 중복 트리거 방지 플래그

    public bool DisablePlayerMovementDuringDelay { get => disablePlayerMovementDuringDelay; set => disablePlayerMovementDuringDelay = value; }

    void Reset()
    {
        if (string.IsNullOrEmpty(gameObject.name) || gameObject.name.StartsWith("GameObject"))
            gameObject.name = "Destination";

        if (!TryGetComponent<BoxCollider2D>(out var bc)) bc = gameObject.AddComponent<BoxCollider2D>();
        bc.isTrigger = true; // 목적지는 보통 트리거로 설정
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        HandleCollision(other.gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider == null) return;
        HandleCollision(collision.collider.gameObject);
    }

    void HandleCollision(GameObject otherGO)
    {
        // GridMovementSystem이 있는 오브젝트를 플레이어로 간주
        GridMovementSystem gms = otherGO.GetComponent<GridMovementSystem>();

        // 혹은 태그로 확인
        bool isPlayerTag = !string.IsNullOrEmpty(playerTag) && otherGO.CompareTag(playerTag);

        if (gms != null || isPlayerTag)
        {
            HandleReached(otherGO, gms);
        }
    }

    void HandleReached(GameObject playerGO, GridMovementSystem gms)
    {
        if (triggered) return;
        triggered = true;

        Debug.Log($"Destination reached by {playerGO.name}");

        StartCoroutine(DelayAndHandle(playerGO, gms));
    }

    IEnumerator DelayAndHandle(GameObject playerGO, GridMovementSystem gms)
    {
        if (playerGO == null) yield break;

        // 1. 현재 타일 이동이 끝날 때까지 대기 (중요: 중간에 끊기면 어색함)
        if (gms != null)
        {
            float waitTimeout = 1.5f; // 무한 루프 방지 안전장치
            float waited = 0f;

            // GridMovementSystem의 public 메서드 활용
            while (gms.GetMoving() && waited < waitTimeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }

        // 2. 이동 시스템 및 물리 비활성화
        bool moveSysWasEnabled = false;
        bool rbSimulatedWas = true;
        Rigidbody2D rb = playerGO.GetComponent<Rigidbody2D>();
        Animator anim = playerGO.GetComponent<Animator>();

        if (DisablePlayerMovementDuringDelay && gms != null)
        {
            moveSysWasEnabled = gms.enabled;
            gms.StopAllCoroutines(); // 이동 코루틴 강제 종료
            gms.enabled = false;     // Update 및 입력 차단

            // 내부 상태 강제 리셋 (private 변수라 리플렉션 사용)
            ResetPrivateMovementFlags(gms);
        }

        // 애니메이션 강제 Idle 전환 (걷는 모션으로 굳는 것 방지)
        if (anim != null)
        {
            anim.SetBool("IsMoving", false);
        }

        if (disableRigidbodySimulationDuringDelay && rb != null)
        {
            rbSimulatedWas = rb.simulated;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false; // 물리 연산 중단 (미끄러짐 방지)
        }

        // 3. 연출 대기
        if (delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);

        // 4. 이벤트 실행 (씬 전환 등)
        try
        {
            onReached?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        // 5. (옵션) 씬이 전환되지 않고 계속 진행될 경우를 대비한 복구 로직
        // 만약 씬이 바뀌면 이 아래 코드는 실행되지 않거나 의미가 없습니다.
        if (playerGO != null)
        {
            if (rb != null && disableRigidbodySimulationDuringDelay)
            {
                rb.simulated = rbSimulatedWas;
            }

            if (gms != null && DisablePlayerMovementDuringDelay)
            {
                // 다시 켜기 전에 플래그 확실히 초기화
                ResetPrivateMovementFlags(gms);
                gms.enabled = moveSysWasEnabled;
            }
        }

        // 트리거 리셋 (재사용 가능하게 하려면)
        // triggered = false; 
    }

    /// <summary>
    /// GridMovementSystem의 private 변수(isMoving, isInputProcessed)를 강제로 초기화합니다.
    /// 스크립트를 껐다 켜도 내부 상태가 남아 입력이 먹통되는 것을 방지합니다.
    /// </summary>
    void ResetPrivateMovementFlags(GridMovementSystem gms)
    {
        if (gms == null) return;

        Type t = typeof(GridMovementSystem);

        // 앞서 작성한 스크립트의 변수명에 맞춰 설정
        string[] fieldNames = new string[] { "isMoving", "isInputProcessed" };

        foreach (var name in fieldNames)
        {
            try
            {
                FieldInfo field = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(bool))
                {
                    field.SetValue(gms, false);
                }
            }
            catch { /* 무시 */ }
        }
    }

    void OnDrawGizmos()
    {
        if (TryGetComponent<BoxCollider2D>(out var col))
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f); // 하늘색 반투명
            Gizmos.DrawCube(transform.position + (Vector3)col.offset, col.size);
            Gizmos.color = new Color(0f, 1f, 1f, 1f);
            Gizmos.DrawWireCube(transform.position + (Vector3)col.offset, col.size);
        }
    }
}