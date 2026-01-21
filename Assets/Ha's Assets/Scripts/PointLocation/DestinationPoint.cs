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

    [Header("캐릭터가 목표지점에 도착 시 딜레이 후 이동")]
    public float delaySeconds = 0.6f;

    private bool disablePlayerMovementDuringDelay = true;
    private bool stopMoveCoroutines = true;
    private bool disableRigidbodySimulationDuringDelay = true;

    private string playerTag = "Player";

    // 내부 플래그: 중복 트리거 방지
    bool triggered = false;

    void Reset()
    {
        if (string.IsNullOrEmpty(gameObject.name) || gameObject.name.StartsWith("GameObject")) gameObject.name = "Destination";

        var bc = GetComponent<BoxCollider2D>();
        if (bc == null) bc = gameObject.AddComponent<BoxCollider2D>();
    }

    void Start()
    {
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        var otherGO = collision.collider?.gameObject;
        if (otherGO == null) return;

        if (IsPlayerObject(otherGO)) HandleReached(otherGO);
    }

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

        // --- 0) 가능한 경우: 현재 이동(픽셀 보간)이 끝날 때까지 기다려서 mid-move teleport 방지 ---
        GridMovementSystem gms = playerGO.GetComponent<GridMovementSystem>();
        if (gms != null)
        {
            // 최대 대기 타임(안전망)
            float waitTimeout = 1.5f;
            float waited = 0f;
            while (gms.GetMoving() && waited < waitTimeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }

        // --- 1) 이동 시스템 안전 차단 ---
        MonoBehaviour moveSysMB = null;
        bool moveSysWasEnabled = false;

        Rigidbody2D rb = null;
        bool rbSimulatedWas = true;

        if (disablePlayerMovementDuringDelay && playerGO != null)
        {
            var comp = playerGO.GetComponent("GridMovementSystem");
            moveSysMB = comp as MonoBehaviour;
            if (moveSysMB != null)
            {
                moveSysWasEnabled = moveSysMB.enabled;
                if (stopMoveCoroutines)
                {
                    try { moveSysMB.StopAllCoroutines(); } catch { }
                }
                try { moveSysMB.enabled = false; } catch { }
                // 추가 안전: 내부 상태 초기화
                ResetMovementFlagsViaReflection(moveSysMB);
            }
        }

        if (disableRigidbodySimulationDuringDelay && playerGO != null)
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
        else if (playerGO != null)
        {
            rb = playerGO.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                try { rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f; } catch { }
            }
        }

        // --- 2) 실제 대기 ---
        if (delaySeconds > 0f) yield return new WaitForSecondsRealtime(delaySeconds);
        else yield return null;

        // --- 3) 이벤트 호출(텔레포트/전환 등은 onReached 리스너에서 처리) ---
        try{ onReached?.Invoke(); }
        catch (Exception ex){ Debug.LogException(ex); }

        // --- 4) 복구: 이동/물리 다시 활성화 ---
        if (playerGO != null)
        {
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

            if (moveSysMB != null)
            {
                // 내부 플래그 초기화
                ResetMovementFlagsViaReflection(moveSysMB);
                try { moveSysMB.enabled = moveSysWasEnabled; } catch { }
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
                if (f != null && f.FieldType == typeof(bool)) f.SetValue(moveSysMB, false);
            }
            catch { }
        }

        string[] propCandidates = new string[] { "IsMoving", "IsInputProcessed" };
        foreach (var name in propCandidates)
        {
            try
            {
                var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (p != null && p.PropertyType == typeof(bool) && p.CanWrite) p.SetValue(moveSysMB, false, null);
            }
            catch { }
        }
    }

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
