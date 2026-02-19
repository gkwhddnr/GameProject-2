using UnityEngine;
using System.Collections;

/// <summary>
/// Shield 이펙트 컨트롤러
/// 영구적으로 Shield를 활성화/비활성화
/// </summary>
public class ShieldEffectController : MonoBehaviour
{
    public static ShieldEffectController Instance { get; private set; }

    [Header("Shield Prefab")]
    [SerializeField] private GameObject shieldEffectPrefab;

    [Header("Visual Effects")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private bool pulseEffect = true;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseAmount = 0.1f;

    // 내부 상태
    private GameObject activeShieldInstance;
    private GameObject currentPlayer;
    private bool isShieldActive = false;

    // 이펙트 컴포넌트 캐싱
    private SpriteRenderer shieldVisual;
    private ParticleSystem shieldParticle;
    private Vector3 originalScale;
    private Color originalColor;

    public bool IsShieldActive => isShieldActive;

    private void Awake()
    {
        // 싱글톤 패턴
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // ShieldEffect Prefab이 없으면 Resources에서 로드
        if (shieldEffectPrefab == null)
        {
            shieldEffectPrefab = Resources.Load<GameObject>("Effects/ShieldEffect");

            if (shieldEffectPrefab == null)
            {
                Debug.LogWarning("[ShieldEffectController] ShieldEffect Prefab을 찾을 수 없습니다. " +
                    "Resources/Effects/ShieldEffect.prefab을 생성하세요.");
            }
        }
    }

    private void Update()
    {
        // Shield 활성화 중일 때만 펄스 효과
        if (isShieldActive && pulseEffect && shieldVisual != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            shieldVisual.transform.localScale = originalScale * pulse;
        }
    }

    /// <summary>
    /// Shield 활성화 (영구)
    /// </summary>
    public void ActivateShield(GameObject player)
    {
        // 이미 활성화 중이면 무시
        if (isShieldActive)
        {
            Debug.Log("[ShieldEffectController] Shield가 이미 활성화되어 있습니다!");
            return;
        }

        if (player == null)
        {
            Debug.LogError("[ShieldEffectController] Player가 null입니다!");
            return;
        }

        if (shieldEffectPrefab == null)
        {
            Debug.LogError("[ShieldEffectController] shieldEffectPrefab이 설정되지 않았습니다!");
            return;
        }

        currentPlayer = player;

        // Shield 이펙트 생성
        activeShieldInstance = Instantiate(shieldEffectPrefab, player.transform);
        activeShieldInstance.transform.localPosition = Vector3.zero;
        activeShieldInstance.transform.localRotation = Quaternion.identity;

        // 컴포넌트 캐싱
        shieldVisual = activeShieldInstance.GetComponentInChildren<SpriteRenderer>();
        shieldParticle = activeShieldInstance.GetComponentInChildren<ParticleSystem>();

        if (shieldVisual != null)
        {
            originalScale = shieldVisual.transform.localScale;
            originalColor = shieldVisual.color;
        }

        // 파티클 시작
        if (shieldParticle != null)
        {
            shieldParticle.Play();
        }

        isShieldActive = true;

        // 페이드 인
        StartCoroutine(FadeInCoroutine());

        Debug.Log("[ShieldEffectController] Shield 활성화! (영구)");
    }

    /// <summary>
    /// Shield 비활성화
    /// </summary>
    public void DeactivateShield()
    {
        if (!isShieldActive)
        {
            Debug.Log("[ShieldEffectController] Shield가 활성화되어 있지 않습니다!");
            return;
        }

        isShieldActive = false;

        // 페이드 아웃 후 제거
        StartCoroutine(FadeOutAndDestroyCoroutine());

        Debug.Log("[ShieldEffectController] Shield 비활성화!");
    }

    /// <summary>
    /// Shield 토글 (활성화 ↔ 비활성화)
    /// </summary>
    public void ToggleShield(GameObject player)
    {
        if (isShieldActive)
        {
            DeactivateShield();
        }
        else
        {
            ActivateShield(player);
        }
    }

    /// <summary>
    /// 페이드 인 코루틴
    /// </summary>
    private IEnumerator FadeInCoroutine()
    {
        if (shieldVisual == null) yield break;

        Color color = shieldVisual.color;
        float targetAlpha = originalColor.a;
        color.a = 0f;
        shieldVisual.color = color;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeInDuration;

            color.a = Mathf.Lerp(0f, targetAlpha, t);
            shieldVisual.color = color;

            yield return null;
        }

        color.a = targetAlpha;
        shieldVisual.color = color;
    }

    /// <summary>
    /// 페이드 아웃 후 제거 코루틴
    /// </summary>
    private IEnumerator FadeOutAndDestroyCoroutine()
    {
        if (shieldVisual != null)
        {
            Color color = shieldVisual.color;
            float startAlpha = color.a;

            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOutDuration;

                color.a = Mathf.Lerp(startAlpha, 0f, t);
                shieldVisual.color = color;

                yield return null;
            }
        }

        // 파티클 중지
        if (shieldParticle != null)
        {
            shieldParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // 오브젝트 제거
        if (activeShieldInstance != null)
        {
            Destroy(activeShieldInstance);
            activeShieldInstance = null;
        }

        currentPlayer = null;
    }

    private void OnDestroy()
    {
        // 정리
        if (activeShieldInstance != null)
        {
            Destroy(activeShieldInstance);
        }
    }
}