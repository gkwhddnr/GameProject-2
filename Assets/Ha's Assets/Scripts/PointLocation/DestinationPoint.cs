using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider2D))]
[DisallowMultipleComponent]
public class DestinationPoint : MonoBehaviour
{
    [Tooltip("플레이어가 도착했을 때 호출되는 이벤트입니다. (인스펙터에서 GameManager 함수 연결 가능)")]
    public UnityEvent onReached;

    public string playerTag = "Player";

    [Header("캐릭터가 목표지점에 도착 시 딜레이 후 이동")]
    public float delaySeconds = 0.6f;

    [Tooltip("대기 시간 동안 플레이어 이동 스크립트(GridMovementSystem)를 비활성화합니다(있을 경우).")]
    public bool disablePlayerMovementDuringDelay = true;

    [Tooltip("disablePlayerMovementDuringDelay가 true일 때 이동 코루틴들을 StopAllCoroutines()로 강제 중지합니다.")]
    public bool stopMoveCoroutines = true;

    [Tooltip("대기 시간 동안 Rigidbody2D의 시뮬레이션을 끌지 여부 (있을 경우). 물리 충돌/관성을 멈춤.")]
    public bool disableRigidbodySimulationDuringDelay = true;

    // 내부 플래그: 중복 트리거 방지
    bool triggered = false;

    void Reset()
    {
        if (string.IsNullOrEmpty(gameObject.name) || gameObject.name.StartsWith("GameObject"))
            gameObject.name = "Destination";

        var bc = GetComponent<BoxCollider2D>();
        if (bc == null) bc = gameObject.AddComponent<BoxCollider2D>();
    }

    void Start()
    {
    }

    // Collision 방식 (Collider.isTrigger == false 상태에서 호출)
    void OnCollisionEnter2D(Collision2D collision)
    {
        var otherGO = collision.collider?.gameObject;
        if (otherGO == null) return;

        if (IsPlayerObject(otherGO)) HandleReached(otherGO);
    }

    // Trigger 방식 (Collider.isTrigger == true 상태에서 호출)
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        var otherGO = other.gameObject;
        if (IsPlayerObject(otherGO)) HandleReached(otherGO);
    }

    bool IsPlayerObject(GameObject go)
    {
        if (go == null) return false;

        if (!string.IsNullOrEmpty(playerTag) && go.CompareTag(playerTag)) return true;
        if (go.GetComponent<Rigidbody2D>() != null && string.IsNullOrEmpty(playerTag)) return true;

        return false;
    }

    void HandleReached(GameObject playerGO)
    {
        if (triggered) return; // 중복 방지
        triggered = true;

        Debug.Log($"Destination reached by {playerGO.name} — will process after {delaySeconds} s");

        // 코루틴으로 지연 처리 및 안전 조치
        StartCoroutine(DelayAndHandle(playerGO));
    }

    IEnumerator DelayAndHandle(GameObject playerGO)
    {
        if (playerGO == null) yield break;
        
        // 1) 선택적: 플레이어 이동/물리 비활성화하고 현재 속도 0으로
        Component moveSysComp = null;
        MonoBehaviour moveSysMB = null;
        bool moveSysWasEnabled = false;

        Rigidbody2D rb = null;
        bool rbSimulatedWas = true;

        if (disablePlayerMovementDuringDelay)
        {
            // GridMovementSystem을 구체 타입으로 참조하지 않고 컴포넌트 이름으로 찾음
            moveSysComp = playerGO.GetComponent("GridMovementSystem");
            moveSysMB = moveSysComp as MonoBehaviour;
            if (moveSysMB != null)
            {
                moveSysWasEnabled = moveSysMB.enabled;
                if (stopMoveCoroutines)
                {
                    try { moveSysMB.StopAllCoroutines(); } catch { }
                }
                try { moveSysMB.enabled = false; } catch { }
                // 리플렉션으로 내부 bool 필드 리셋 시도 (중요)
                ResetMovementFlagsViaReflection(moveSysMB);
            }
        }

        if (disableRigidbodySimulationDuringDelay)
        {
            rb = playerGO.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rbSimulatedWas = rb.simulated;
                try
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                    rb.simulated = false;
                }
                catch { }
            }
        }
        else
        {
            rb = playerGO.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                try
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
                catch { }
            }
        }

        // 2) 대기 (실시간)
        if (delaySeconds > 0f) yield return new WaitForSecondsRealtime(delaySeconds);
        else yield return null;

        // 4) onReached 이벤트 호출 (원하면 텔레포트 전 호출로 바꿀 수 있음)
        try
        {
            onReached?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        // 5) 복구: 물리/이동 다시 활성화 (playerGO가 바뀌었을 수 있으므로 안전 체크)
        if (playerGO != null)
        {
            // Rigidbody 복구
            if (rb != null)
            {
                try
                {
                    rb.simulated = rbSimulatedWas;
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
                catch { }
            }

            // movement component 복구 및 내부 플래그 초기화
            if (moveSysMB != null)
            {
                ResetMovementFlagsViaReflection(moveSysMB);
                try { moveSysMB.enabled = moveSysWasEnabled; } catch { }
                try { moveSysMB.StopAllCoroutines(); } catch { }
            }
        }
    }

    /// <summary>
    /// GridMovementSystem(또는 유사 이동 컴포넌트)의 흔한 내부 bool 필드들을 reflection으로 false로 만듭니다.
    /// 실패해도 예외는 무시합니다.
    /// </summary>
    void ResetMovementFlagsViaReflection(MonoBehaviour moveSysMB)
    {
        if (moveSysMB == null) return;

        Type t = moveSysMB.GetType();

        string[] boolFieldCandidates = new string[] { "isMoving", "isInputProcessed", "_isMoving", "_isInputProcessed", "moving", "inputProcessed" };

        foreach (var name in boolFieldCandidates)
        {
            try
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null && f.FieldType == typeof(bool))
                {
                    f.SetValue(moveSysMB, false);
                }
            }
            catch { }
        }

        string[] propCandidates = new string[] { "IsMoving", "IsInputProcessed" };
        foreach (var name in propCandidates)
        {
            try
            {
                var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (p != null && p.PropertyType == typeof(bool) && p.CanWrite)
                {
                    p.SetValue(moveSysMB, false, null);
                }
            }
            catch { }
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
