    using System;
    using System.Collections;
    using System.Reflection;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.SceneManagement;

    [RequireComponent(typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public class DestinationPoint : MonoBehaviour
    {
        [Tooltip("플레이어가 도착했을 때 호출되는 이벤트입니다.")]
        public UnityEvent onReached;

        [Header("캐릭터가 목표지점에 도착 시 딜레이 후 이동")]
        public float delaySeconds = 0.6f;

        [Header("설정")]
        [Tooltip("이동하는 동안 플레이어의 조작을 막을지 여부")]
        private bool disablePlayerMovementDuringDelay = true;

        [Tooltip("물리 연산을 멈출지 여부 (밀림 방지)")]
        private readonly bool disableRigidbodySimulationDuringDelay = true;

        private readonly string playerTag = "Player";
        private bool triggered = false;

        public bool DisablePlayerMovementDuringDelay
        {
            get => disablePlayerMovementDuringDelay;
            set => disablePlayerMovementDuringDelay = value;
        }

        void Reset()
        {
            if (string.IsNullOrEmpty(gameObject.name) || gameObject.name.StartsWith("GameObject")) gameObject.name = "Destination";

            if (!TryGetComponent<BoxCollider2D>(out var bc)) bc = gameObject.AddComponent<BoxCollider2D>();

            bc.isTrigger = true;
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
            GridMovementSystem gms = otherGO.GetComponent<GridMovementSystem>();
            bool isPlayerTag = !string.IsNullOrEmpty(playerTag) && otherGO.CompareTag(playerTag);

            if (gms != null || isPlayerTag) HandleReached(otherGO, gms);
        }

        void HandleReached(GameObject playerGO, GridMovementSystem gms)
        {
            if (triggered) return;
            triggered = true;

            Debug.Log($"[DestinationPoint] Destination reached by {playerGO.name}");

            StartCoroutine(DelayAndHandle(playerGO, gms));
        }

        IEnumerator DelayAndHandle(GameObject playerGO, GridMovementSystem gms)
        {
            if (playerGO == null) yield break;

            // 1. 즉시 이동 멈추기
            bool moveSysWasEnabled = false;
            bool rbSimulatedWas = true;
            Rigidbody2D rb = playerGO.GetComponent<Rigidbody2D>();
            Animator anim = playerGO.GetComponent<Animator>();

            if (gms != null)
            {
                moveSysWasEnabled = gms.enabled;
                gms.StopAllCoroutines();
                ResetPrivateMovementFlags(gms);

                if (DisablePlayerMovementDuringDelay)
                    gms.enabled = false;
            }

            // 애니메이션 Idle 전환
            if (anim != null) anim.SetBool("IsMoving", false);

            // 물리 연산 중단
            if (disableRigidbodySimulationDuringDelay && rb != null)
            {
                rbSimulatedWas = rb.simulated;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = false;
            }

            // 2. 연출 대기
            if (delaySeconds > 0f) yield return new WaitForSecondsRealtime(delaySeconds);

            // 3. 이벤트 실행
            try
            {
                onReached?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            // 4. Planet 레이어 체크 → 씬 전환
            int planetLayer = LayerMask.NameToLayer("Planet");
        if (planetLayer != -1 && gameObject.layer == planetLayer)
        {
            Debug.Log("[DestinationPoint] Planet 레이어 감지! 'Choi_MainScreen' 씬으로 이동...");

            // 플레이어 상태 복원
            RestorePlayerState(playerGO, rb, gms, rbSimulatedWas, moveSysWasEnabled);

            // ★ SceneFader를 사용한 씬 전환
            if (SceneFader.Instance != null)
            {
                SceneFader.Instance.FadeToScene("Choi_MainScreen");
                Debug.Log("[DestinationPoint] SceneFader를 통한 씬 전환 시작!");
            }
            else
            {
                // SceneFader가 없으면 기본 방식으로 전환
                Debug.LogWarning("[DestinationPoint] SceneFader.Instance가 없습니다. 기본 씬 전환 사용.");
                AsyncOperation op = SceneManager.LoadSceneAsync("Choi_MainScreen");
                if (op != null)
                {
                    while (!op.isDone) yield return null;
                    Debug.Log("[DestinationPoint] 씬 전환 완료!");
                }
            }

            yield break;
        }

        // 5. 일반 도착지점 - 플레이어 상태 복원
        RestorePlayerState(playerGO, rb, gms, rbSimulatedWas, moveSysWasEnabled);
    }

        /// <summary>
        /// 플레이어 상태 복원
        /// </summary>
        private void RestorePlayerState(GameObject playerGO, Rigidbody2D rb, GridMovementSystem gms,
            bool rbSimulatedWas, bool moveSysWasEnabled)
        {
            if (playerGO == null) return;

            // Rigidbody2D 복원
            if (rb != null && disableRigidbodySimulationDuringDelay) rb.simulated = rbSimulatedWas;

            // GridMovementSystem 복원
            if (gms != null && DisablePlayerMovementDuringDelay)
            {
                ResetPrivateMovementFlags(gms);
                gms.enabled = moveSysWasEnabled;
            }
        }

        /// <summary>
        /// GridMovementSystem의 private 플래그 리셋 (Reflection)
        /// </summary>
        void ResetPrivateMovementFlags(GridMovementSystem gms)
        {
            if (gms == null) return;

            Type t = typeof(GridMovementSystem);
            string[] fieldNames = new string[] { "isMoving", "isInputProcessed" };

            foreach (var name in fieldNames)
            {
                try
                {
                    FieldInfo field = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null && field.FieldType == typeof(bool)) field.SetValue(gms, false);
                }
                catch { /* 무시 */ }
            }
        }

        void OnDrawGizmos()
        {
            if (TryGetComponent<BoxCollider2D>(out var col))
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
                Gizmos.DrawCube(transform.position + (Vector3)col.offset, col.size);
                Gizmos.color = new Color(0f, 1f, 1f, 1f);
                Gizmos.DrawWireCube(transform.position + (Vector3)col.offset, col.size);
            }
        }
    }   