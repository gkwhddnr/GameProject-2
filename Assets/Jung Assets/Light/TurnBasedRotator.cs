using UnityEngine;
using System.Collections;

public class TurnBasedRotator : MonoBehaviour
{
    [Header("회전 설정")]
    [Tooltip("몇 턴마다 회전할지 설정 (예: 1 = 매 턴, 2 = 2턴마다)")]
    [Min(1)] public int period = 2; // 주기

    [Tooltip("한 번에 회전할 각도 (양수: 반시계, 음수: 시계)")]
    public float rotationAngle = 90f; // 회전량

    [Tooltip("회전하는 데 걸리는 시간 (0이면 즉시 회전)")]
    public float smoothDuration = 0.3f;

    // 내부 카운트 변수
    private int currentTurnCount = 0;

    private void Start()
    {
        // GameManager의 턴 종료 이벤트 구독
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerTurnEnd += CheckAndRotate;
        }
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerTurnEnd -= CheckAndRotate;
        }
    }

    // ★ 핵심 함수: 턴이 끝날 때마다 호출됨
    private void CheckAndRotate()
    {
        currentTurnCount++;

        // 현재 턴을 주기로 나눈 나머지가 0이면 행동할 차례 (예: 2턴, 4턴, 6턴...)
        if (currentTurnCount % period == 0)
        {
            StartCoroutine(RotateRoutine());
        }
    }

    // 부드럽게 회전시키는 코루틴
    private IEnumerator RotateRoutine()
    {
        Quaternion startRotation = transform.rotation;
        // 현재 각도에서 rotationAngle만큼 더 돌린 목표 각도 계산
        Quaternion targetRotation = startRotation * Quaternion.Euler(0, 0, rotationAngle);

        float elapsedTime = 0f;

        while (elapsedTime < smoothDuration)
        {
            // 시간 진행률 (0 ~ 1)
            float t = elapsedTime / smoothDuration;

            // 부드러운 움직임 (Ease Out: 끝에서 천천히)
            t = t * (2f - t);

            transform.rotation = Quaternion.Lerp(startRotation, targetRotation, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 오차 없이 확실하게 목표 각도로 고정
        transform.rotation = targetRotation;
    }
}