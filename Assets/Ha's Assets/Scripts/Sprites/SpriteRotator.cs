using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))] // SpriteRenderer가 필수임을 명시
public class SpriteRotator : MonoBehaviour
{
    public enum SpinDirection { Left = -1, Right = 1, Random = 0 }

    [Header("Rotation Settings")]
    [SerializeField] private SpinDirection spinDirection = SpinDirection.Right;
    [SerializeField] private float baseRotationSpeed = 18f;
    [SerializeField] private float idleSpeedVariance = 2f;
    [SerializeField] private bool randomizeDirectionOnStart = false;

    [Header("Visual Effects")]
    [Range(0f, 1f)][SerializeField] private float ease = 0.85f;
    [SerializeField] private bool fadeOnDisappear = true;
    [SerializeField] private bool disableOnComplete = false;

    [Header("Animation States")]
    public Vector3 approachScale = new Vector3(0.85f, 0.85f, 1f);
    public float approachRotationMultiplier = 1.2f;
    public float disappearSpinSpeed = 720f;
    public Vector3 targetDisappearScale = new Vector3(0.05f, 0.05f, 1f);
    public float disappearDuration = 0.45f;
    [Range(0f, 1f)] public float targetAlpha = 0f;

    // 캐싱 및 상태 변수
    private SpriteRenderer _spriteRenderer;
    private Vector3 _initialScale;
    private Color _initialColor;
    private float _currentRotationSpeed;
    private bool _isDisappearing;
    private Coroutine _activeAnimation;

    // Property: 레이어 이름을 확인하여 NextPoint 여부 판단
    private bool IsNextPoint => gameObject.layer == LayerMask.NameToLayer("next");

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _initialScale = transform.localScale;
        _initialColor = _spriteRenderer != null ? _spriteRenderer.color : Color.white;
        InitializeRotation();
    }

    void OnEnable() => ResetToInitialState();

    void Update()
    {
        if (!_isDisappearing)
        {
            transform.Rotate(0, 0, _currentRotationSpeed * Time.deltaTime);
        }
    }

    private void InitializeRotation()
    {
        int dirSign = (spinDirection == SpinDirection.Random || randomizeDirectionOnStart)
            ? (Random.value < 0.5f ? -1 : 1)
            : (int)spinDirection;

        _currentRotationSpeed = (baseRotationSpeed * dirSign) + Random.Range(-idleSpeedVariance, idleSpeedVariance);
    }

    #region Animation Methods

    public void StartApproach(float duration)
    {
        if (!IsNextPoint || _isDisappearing) return;
        ReplaceAnimation(ApproachRoutine(duration));
    }

    public void TriggerDisappear()
    {
        if (_isDisappearing) return;
        _isDisappearing = true;
        ReplaceAnimation(DisappearRoutine());
    }

    private void ReplaceAnimation(IEnumerator newRoutine)
    {
        if (_activeAnimation != null) StopCoroutine(_activeAnimation);
        _activeAnimation = StartCoroutine(newRoutine);
    }

    #endregion

    #region Coroutines

    private IEnumerator ApproachRoutine(float duration)
    {
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        float startSpeed = _currentRotationSpeed;
        float targetSpeed = startSpeed * approachRotationMultiplier;

        while (elapsed < duration)
        {
            float t = GetEaseT(ref elapsed, duration);
            transform.localScale = Vector3.Lerp(startScale, approachScale, t);
            _currentRotationSpeed = Mathf.Lerp(startSpeed, targetSpeed, t);
            yield return null;
        }

        transform.localScale = approachScale;
        _currentRotationSpeed = targetSpeed;
    }

    private IEnumerator DisappearRoutine()
    {
        Vector3 startScale = transform.localScale;
        Color startColor = _spriteRenderer.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, targetAlpha);

        float startSpeed = _currentRotationSpeed;
        float targetSpeed = Mathf.Abs(disappearSpinSpeed) * Mathf.Sign(startSpeed == 0 ? 1 : startSpeed);
        if (!IsNextPoint) targetSpeed *= 0.5f;

        float elapsed = 0f;
        while (elapsed < disappearDuration)
        {
            float t = GetEaseT(ref elapsed, disappearDuration);

            _currentRotationSpeed = Mathf.Lerp(startSpeed, targetSpeed, t);
            transform.Rotate(0, 0, _currentRotationSpeed * Time.deltaTime);
            transform.localScale = Vector3.Lerp(startScale, targetDisappearScale, t);

            if (fadeOnDisappear)
                _spriteRenderer.color = Color.Lerp(startColor, endColor, t);

            yield return null;
        }

        transform.localScale = targetDisappearScale;
        if (fadeOnDisappear) _spriteRenderer.color = endColor;
        if (disableOnComplete) gameObject.SetActive(false);

        _isDisappearing = false;
        _activeAnimation = null;
    }

    // 보간용 T값 계산 유틸리티
    private float GetEaseT(ref float elapsed, float duration)
    {
        elapsed += Time.deltaTime;
        float rawT = Mathf.Clamp01(elapsed / duration);
        return Mathf.Lerp(rawT, Mathf.SmoothStep(0, 1, rawT), ease);
    }

    public IEnumerator WaitForDisappear()
    {
        while (_isDisappearing) yield return null;
    }

    #endregion

    public void ResetToInitialState()
    {
        if (_activeAnimation != null) StopCoroutine(_activeAnimation);
        _activeAnimation = null;

        _isDisappearing = false;
        transform.localScale = _initialScale;
        if (_spriteRenderer != null) _spriteRenderer.color = _initialColor;
        InitializeRotation();
    }
}