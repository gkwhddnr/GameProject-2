using UnityEngine;
using System.Collections;
using UnityEngine.Rendering.Universal; // 현재 빛의 각도를 읽어오기 위해 필요

public class TurnBasedLightController : MonoBehaviour
{
    [Header("Component Link")]
    public DynamicLightCollider lightColliderTool; // 기술자
    private Light2D myLight; // 현재 각도를 읽기 위해 내 빛 컴포넌트도 필요

    [Header("Pattern Settings")]
    public float[] anglePattern = { 30f, 90f };
    [Tooltip("각도가 변하는 데 걸리는 시간 (초)")]
    public float changeDuration = 0.3f; // 플레이어 이동 속도랑 비슷하게 맞추세요

    private int currentPatternIndex = 0;
    private Coroutine currentRoutine; // 실행 중인 코루틴 저장 (중복 방지)

    private void Start()
    {
        if (lightColliderTool == null)
            lightColliderTool = GetComponent<DynamicLightCollider>();

        myLight = GetComponent<Light2D>();

        // GameManager 방송 구독
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerTurnEnd += NextPattern;
        }

        // 시작할 때는 부드럽게 말고 즉시 적용 (초기화)
        if (anglePattern.Length > 0)
            lightColliderTool.SetLightAngle(anglePattern[0]);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerTurnEnd -= NextPattern;
        }
    }

    private void NextPattern()
    {
        // 다음 패턴 인덱스 계산
        currentPatternIndex = (currentPatternIndex + 1) % anglePattern.Length;
        float targetAngle = anglePattern[currentPatternIndex];

        // 이전에 돌아가던 코루틴이 있다면 멈춤 (안전장치)
        if (currentRoutine != null) StopCoroutine(currentRoutine);

        // 부드럽게 변경 시작!
        currentRoutine = StartCoroutine(SmoothAngleRoutine(targetAngle));
    }

    private IEnumerator SmoothAngleRoutine(float targetAngle)
    {
        // 현재 빛의 각도에서 시작
        float startAngle = myLight.pointLightOuterAngle;
        float elapsedTime = 0;

        while (elapsedTime < changeDuration)
        {
            // Lerp로 중간값 계산 (부드러운 전환)
            float t = elapsedTime / changeDuration;
            // Easing을 넣고 싶다면 Mathf.SmoothStep(startAngle, targetAngle, t) 사용 가능
            float currentAngle = Mathf.Lerp(startAngle, targetAngle, t);

            // 기술자에게 명령 -> 기술자가 알아서 콜라이더도 같이 바꿈!
            lightColliderTool.SetLightAngle(currentAngle);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 오차 없이 최종 각도로 확정
        lightColliderTool.SetLightAngle(targetAngle);
        currentRoutine = null;
    }
}