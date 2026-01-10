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
            Vector3 targetPosition = transform.position + (direction * gridSize);

            if (IsBlocked(targetPosition))
            {
                StartCoroutine(BumpAnimation(direction));                
            }
            else
            {
                isInputProcessed = true; // 입력 처리 완료
                StartCoroutine(MoveRoutine(targetPosition)); // 이동 코루틴
            }
            GameManager.Instance.AddMoveCount(1);
        }
    }
    private IEnumerator BumpAnimation(Vector3 direction)
    {
        isMoving = true; // 애니메이션 중 입력 막기

        Vector3 startPosition = transform.position;
        // 벽 쪽으로 아주 조금(0.2칸)만 움직였다가 돌아옴
        Vector3 bumpPosition = startPosition + (direction * 0.2f);

        float bumpSpeed = 0.1f; // 들이박는 속도
        float elapsedTime = 0;

        // 1. 벽 쪽으로 쾅!
        while (elapsedTime < bumpSpeed)
        {
            transform.position = Vector3.Lerp(startPosition, bumpPosition, (elapsedTime / bumpSpeed));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 2. 제자리로 복귀
        elapsedTime = 0;
        while (elapsedTime < bumpSpeed)
        {
            transform.position = Vector3.Lerp(bumpPosition, startPosition, (elapsedTime / bumpSpeed));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = startPosition; // 위치 보정
        isMoving = false;
        isInputProcessed = true; // 연출이 끝나면 입력 처리 완료로 간주 (키를 떼야 다시 입력 가능)
    }
    private IEnumerator MoveRoutine(Vector3 targetPosition)
    {
        isMoving = true;
        Vector3 startPosition = transform.position;
        float elapsedTime = 0;

        while (elapsedTime < moveSpeed)
        {
            transform.position = Vector3.Lerp(startPosition, targetPosition, (elapsedTime / moveSpeed));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPosition;
        isMoving = false;
    }

    private bool IsBlocked(Vector3 targetPos)
    {

        Collider2D collider = Physics2D.OverlapCircle(targetPos, 0.2f, obstacleLayer);

        return collider != null;
    }
}