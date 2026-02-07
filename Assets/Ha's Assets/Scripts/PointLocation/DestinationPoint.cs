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
        if (string.IsNullOrEmpty(gameObject.name) || gameObject.name.StartsWith("GameObject")) gameObject.name = "Destination";

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

        if (gms != null || isPlayerTag) HandleReached(otherGO, gms);   
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

        // 1. 즉시 이동 멈추기 (이동 코루틴 강제 중단)
        bool moveSysWasEnabled = false;
        bool rbSimulatedWas = true;
        Rigidbody2D rb = playerGO.GetComponent<Rigidbody2D>();
        Animator anim = playerGO.GetComponent<Animator>();

        if (gms != null)
        {
            moveSysWasEnabled = gms.enabled;

            // 현재 실행 중인 모든 이동 코루틴 즉시 중단
            gms.StopAllCoroutines();

            // 내부 상태 강제 리셋
            ResetPrivateMovementFlags(gms);

            if (DisablePlayerMovementDuringDelay) gms.enabled = false;     // Update 및 입력 차단
        }

        // 애니메이션 강제 Idle 전환 (걷는 모션으로 굳는 것 방지)
        if (anim != null) anim.SetBool("IsMoving", false);
        if (disableRigidbodySimulationDuringDelay && rb != null)
        {
            rbSimulatedWas = rb.simulated;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false; // 물리 연산 중단 (미끄러짐 방지)
        }

        // 2. 연출 대기
        if (delaySeconds > 0f) yield return new WaitForSecondsRealtime(delaySeconds);

        // 3. 이벤트 실행
        try{ onReached?.Invoke(); }
        catch (Exception ex){ Debug.LogException(ex); }

        if (playerGO != null)
        {
            if (rb != null && disableRigidbodySimulationDuringDelay) rb.simulated = rbSimulatedWas;
            if (gms != null && DisablePlayerMovementDuringDelay)
            {
                // 다시 켜기 전에 플래그 확실히 초기화
                ResetPrivateMovementFlags(gms);
                gms.enabled = moveSysWasEnabled;
            }
        }
    }


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