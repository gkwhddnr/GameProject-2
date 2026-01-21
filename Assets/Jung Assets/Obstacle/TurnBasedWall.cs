using System.Collections;
using UnityEngine;

public class TurnBasedWall : MonoBehaviour
{
    public enum DirectionType
    {
        Right,
        Left,
        Up,
        Down
    }

    [Header("Settings")]
    public DirectionType moveType = DirectionType.Right;
    public int distance = 3;
    public float moveSpeed = 0.15f;

    private Vector2 direction;
    private Vector3 startPos;
    // 목표 지점을 클래스 변수로 저장해서 언제든 '정위치'를 알 수 있게 함
    private Vector3 currentTargetPos;

    private bool isGoingToEnd = true;
    private int currentStep = 0;

    // 현재 실행 중인 코루틴을 저장할 변수
    private Coroutine currentMoveCoroutine;
    // 이동 중인지 확인하는 플래그
    private bool isMoving = false;

    private void Start()
    {
        UpdateDirection();
        startPos = transform.position;
        // 시작할 때 현재 위치를 목표 지점으로 초기화 (중요)
        currentTargetPos = transform.position;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerTurnEnd += OnTurnMove;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerTurnEnd -= OnTurnMove;
        }
    }

    private void OnValidate()
    {
        UpdateDirection();
    }

    private void UpdateDirection()
    {
        switch (moveType)
        {
            case DirectionType.Right: direction = Vector2.right; break;
            case DirectionType.Left: direction = Vector2.left; break;
            case DirectionType.Up: direction = Vector2.up; break;
            case DirectionType.Down: direction = Vector2.down; break;
        }
    }

    private void OnTurnMove()
    {
        if (!isActiveAndEnabled) return;

        // [핵심 로직]
        // 만약 이미 이동 중이라면? 
        // 1. 진행 중이던 이동 코루틴을 즉시 멈춤
        // 2. 캐릭터를 진행 중이던 목표 지점(Grid)으로 즉시 강제 이동 (좌표 보정)
        if (isMoving && currentMoveCoroutine != null)
        {
            StopCoroutine(currentMoveCoroutine);
            transform.position = currentTargetPos; // 텔레포트!
            isMoving = false;
        }

        // 이제 위치가 딱 정수 단위(그리드)에 맞춰졌으므로 다음 로직 계산이 안전함

        // 왕복 로직 (PingPong)
        if (isGoingToEnd)
        {
            currentStep++;
            // transform.position 대신 보정된 currentTargetPos를 기준으로 계산해야 안전
            currentTargetPos = transform.position + (Vector3)direction;

            if (currentStep >= distance) isGoingToEnd = false;
        }
        else
        {
            currentStep--;
            currentTargetPos = transform.position - (Vector3)direction;

            if (currentStep <= 0) isGoingToEnd = true;
        }

        // 이동 시작
        currentMoveCoroutine = StartCoroutine(MoveRoutine(currentTargetPos));
    }

    private IEnumerator MoveRoutine(Vector3 targetPosition)
    {
        isMoving = true; // 이동 시작 상태 표시
        Vector3 startPixel = transform.position;
        float elapsedTime = 0;

        while (elapsedTime < moveSpeed)
        {
            transform.position = Vector3.Lerp(startPixel, targetPosition, (elapsedTime / moveSpeed));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPosition;
        isMoving = false; // 이동 완료 상태 해제
    }
}