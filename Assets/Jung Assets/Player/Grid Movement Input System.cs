using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class GridMovementSystem : MonoBehaviour
{
    [Header("Input Settings")]
    public InputActionReference moveInputRef;

    [Header("Movement Settings")]
    public float moveSpeed = 0.15f;
    public float gridSize = 1f;
    public LayerMask obstacleLayer;

    [Header("Animation Settings")]
    public string bumpTriggerName = "Bump";
    public string isMovingBoolName = "IsMoving";
    // ★ 추가됨: 블렌드 트리 제어를 위한 파라미터 이름
    public string inputXFloatName = "InputX";
    public string inputYFloatName = "InputY";
    public string walkOffsetFloatName = "WalkOffset";

    private bool isMoving = false;
    private bool isInputProcessed = false;

    private float _nextStepOffset = 0.0f;
    private int _walkOffsetHash;

    private Animator _animator;
    private SpriteRenderer _spriteRenderer;

    // 해시값 최적화
    private int _bumpHash;
    private int _isMovingHash;
    private int _inputXHash;
    private int _inputYHash;

    private void Awake()
    {
        // 1. 컴포넌트 안전하게 찾기 (자식 오브젝트 포함)
        _animator = GetComponent<Animator>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();

        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // 2. 해시값 미리 계산
        _bumpHash = Animator.StringToHash(bumpTriggerName);
        _isMovingHash = Animator.StringToHash(isMovingBoolName);
        _inputXHash = Animator.StringToHash(inputXFloatName);
        _inputYHash = Animator.StringToHash(inputYFloatName);
        _walkOffsetHash = Animator.StringToHash(walkOffsetFloatName);
    }

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
        Vector2 input = moveInputRef.action.ReadValue<Vector2>();

        if (input == Vector2.zero)
        {
            isInputProcessed = false;
            return;
        }

        if (isMoving || isInputProcessed) return;

        // 대각선 이동 방지
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y)) input.y = 0;
        else input.x = 0;

        Vector3 direction = new Vector3(Mathf.Round(input.x), Mathf.Round(input.y), 0);

        if (direction != Vector3.zero)
        {
            // ★ 수정됨: FlipX 대신 애니메이터 파라미터 업데이트
            UpdateAnimationDirection(direction);

            Vector3 targetPosition = transform.position + (direction * gridSize);

            if (IsBlocked(targetPosition))
            {
                StartCoroutine(BumpAnimation(direction));
            }
            else
            {
                isInputProcessed = true;
                StartCoroutine(MoveRoutine(targetPosition));
            }

            GameManager.Instance.NotifyTurnProcessed();
        }
    }

    // ★ 새로운 함수: 방향을 애니메이터에 전달
    private void UpdateAnimationDirection(Vector3 direction)
    {
        if (_animator == null) return;

        // X, Y 값을 애니메이터에 전달 -> 블렌드 트리가 알아서 해당 애니메이션 재생
        _animator.SetFloat(_inputXHash, direction.x);
        _animator.SetFloat(_inputYHash, direction.y);
    }

    private IEnumerator BumpAnimation(Vector3 direction)
    {
        isMoving = true;

        // 부딪힐 때도 방향을 봐라보게 하려면 여기서 업데이트
        // UpdateAnimationDirection(direction); 

        if (_animator) _animator.SetTrigger(_bumpHash);

        Vector3 startPosition = transform.position;
        Vector3 bumpPosition = startPosition + (direction * 0.2f);
        float bumpSpeed = 0.1f;
        float elapsedTime = 0;

        while (elapsedTime < bumpSpeed)
        {
            transform.position = Vector3.Lerp(startPosition, bumpPosition, (elapsedTime / bumpSpeed));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        elapsedTime = 0;
        while (elapsedTime < bumpSpeed)
        {
            transform.position = Vector3.Lerp(bumpPosition, startPosition, (elapsedTime / bumpSpeed));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = startPosition;
        isMoving = false;
        isInputProcessed = true;
    }

    private IEnumerator MoveRoutine(Vector3 targetPosition)
    {
        isMoving = true;

        if (_animator)
        {
            // ★ 핵심 로직: 걷기 시작할 때 이번 발걸음의 시작 위치(Offset)를 지정
            _animator.SetFloat(_walkOffsetHash, _nextStepOffset);

            // 애니메이션 켜기
            _animator.SetBool(_isMovingHash, true);

            // ★ 다음번 걸음을 위해 오프셋 뒤집기 (0.0 -> 0.5 -> 0.0 ...)
            // Mathf.Repeat는 값을 0~1 사이로 반복시킵니다. (0.5를 더해서 발을 바꿈)
            _nextStepOffset = Mathf.Repeat(_nextStepOffset + 0.5f, 1.0f);
        }

        Vector3 startPosition = transform.position;
        float elapsedTime = 0;

        while (elapsedTime < moveSpeed)
        {
            transform.position = Vector3.Lerp(startPosition, targetPosition, (elapsedTime / moveSpeed));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPosition;

        if (_animator) _animator.SetBool(_isMovingHash, false);
        isMoving = false;
    }

    private bool IsBlocked(Vector3 targetPos)
    {
        return Physics2D.OverlapCircle(targetPos, 0.2f, obstacleLayer) != null;
    }

    public bool GetMoving()
    {
        return isMoving;
    }

    public void ResetMovement()
    {
        // 1. 실행 중인 모든 이동/충돌 코루틴 중지
        StopAllCoroutines();

        // 2. 내부 플래그 초기화
        isMoving = false;
        isInputProcessed = false;
        _nextStepOffset = 0.0f; // 발걸음 순서도 초기화

        // 3. 애니메이션 상태 초기화 (걷기 모션 끄기)
        if (_animator != null)
        {
            _animator.SetBool(_isMovingHash, false);
            // 필요하다면 Idle 상태로 강제 전환하는 Trigger를 쏠 수도 있습니다.
            _animator.Play("Idle", -1, 0f);
        }
    }

}