using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class GridMovementSystem : MonoBehaviour
{
    [Header("Input Settings")]
    public InputActionReference moveInputRef;
    [Header("Movement Settings")]
    public float moveSpeed = 0.15f; // 한 칸씩 움직일 땐 약간 느린 게 더 느낌이 좋습니다.
    public float gridSize = 1f;
    public LayerMask obstacleLayer;
    public int moveCount = 0;
    private bool isMoving = false;

    // 핵심 변수 추가: 현재 눌린 키에 대한 이동 처리가 끝났는지 확인
    private bool isInputProcessed = false;

    private void OnEnable()
    {
        if (moveInputRef != null) moveInputRef.action.Enable();
    }

    private void OnDisable()
    {
        if (moveInputRef != null) moveInputRef.action.Disable();
    }

    void Update()
    {
        // 1. 현재 입력값 읽기
        Vector2 input = moveInputRef.action.ReadValue<Vector2>();

        // [핵심 로직] 키를 뗐는지 확인 (입력값이 0이면 리셋)
        if (input == Vector2.zero)
        {
            isInputProcessed = false; // 키를 뗐으니 다음 입력 받을 준비 완료!
            return;
        }

        // 2. 이미 이동 중이거나, 현재 누르고 있는 키에 대해 이미 이동 명령을 내렸다면 무시
        if (isMoving || isInputProcessed) return;

        // --- 아래는 이전과 동일한 이동 계산 로직 ---

        if (Mathf.Abs(input.x) > Mathf.Abs(input.y)) input.y = 0;
        else input.x = 0;

        Vector3 direction = new Vector3(Mathf.Round(input.x), Mathf.Round(input.y), 0);

        if (direction != Vector3.zero)
        {
            // [핵심 로직] 이동 시작 전에 "이 입력은 처리했다"고 표시
            isInputProcessed = true;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddMoveCount();
            }

            StartCoroutine(MoveRoutine(direction));
        }
    }

    private IEnumerator MoveRoutine(Vector3 direction)
    {
        isMoving = true;

        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + (direction * gridSize);

        if (IsBlocked(targetPosition))
        {
            // 막혔을 때의 처리 (예: 살짝 부딪히는 연출)
            // 막혔더라도 isInputProcessed는 true이므로, 키를 뗐다 눌러야 다시 시도 가능
        }
        else
        {
            float elapsedTime = 0;
            while (elapsedTime < moveSpeed)
            {
                transform.position = Vector3.Lerp(startPosition, targetPosition, (elapsedTime / moveSpeed));
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            transform.position = targetPosition;
        }

        isMoving = false;
    }

    private bool IsBlocked(Vector3 targetPos)
    {
        return Physics2D.OverlapCircle(targetPos, 0.2f, obstacleLayer);
    }
}