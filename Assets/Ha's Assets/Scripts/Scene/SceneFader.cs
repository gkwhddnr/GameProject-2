using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    [Header("Fade settings")]
    public float fadeDuration = 0.6f;
    public Color fadeColor = Color.black;

    [Tooltip("Fade 중 플레이어 이동 차단")]
    private bool blockPlayerDuringFade = true;

    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private Image _fadeImage;

    private bool _isFading = false;
    public bool IsFading => _isFading;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        transform.SetParent(null);
        SetupFadeUI();
    }

    void SetupFadeUI()
    {
        _canvas = GetComponent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();

        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 32767; // 최상단 유지

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;

        if (_fadeImage == null)
        {
            Transform child = transform.Find("FadeOverlay");
            GameObject imgGO = child ? child.gameObject : new GameObject("FadeOverlay");
            imgGO.transform.SetParent(transform, false);

            _fadeImage = imgGO.GetComponent<Image>();
            if (_fadeImage == null) _fadeImage = imgGO.AddComponent<Image>();

            _fadeImage.color = fadeColor;
            _fadeImage.raycastTarget = true;

            RectTransform rt = _fadeImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    // ★ 추가: 페이드 아웃만 실행
    public void StartFadeOut()
    {
        StopAllCoroutines();
        StartCoroutine(FadeOutRoutine());
    }

    // ★ 추가: 페이드 인만 실행
    public void StartFadeIn()
    {
        StopAllCoroutines();
        StartCoroutine(FadeInRoutine());
    }

    public void FadeToScene(string sceneName)
    {
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(sceneName));
    }

    IEnumerator FadeRoutine(string sceneName)
    {
        _isFading = true;

        SetupFadeUI();
        transform.SetAsLastSibling();
        _canvasGroup.blocksRaycasts = true;

        if (blockPlayerDuringFade) DisablePlayerMovement();
        

        // [Fade Out] 화면 덮기 (0 -> 1)
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        _canvasGroup.alpha = 1f;

        // 씬 로딩
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f) yield return null;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        yield return new WaitForEndOfFrame();
        transform.SetAsLastSibling();
        _canvas.sortingOrder = 32767;

        // [Fade In] 화면 밝히기 (1 -> 0)
        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Clamp01(1f - (t / fadeDuration));
            yield return null;
        }

        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;

        _isFading = false;
    }

    IEnumerator FadeInRoutine()
    {
        SetupFadeUI();
        transform.SetAsLastSibling();

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Clamp01(1f - (t / fadeDuration));
            yield return null;
        }

        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;

        _isFading = false; 

        if (blockPlayerDuringFade) EnablePlayerMovement();
    }

    IEnumerator FadeOutRoutine()
    {
        _isFading = true; 

        SetupFadeUI();
        transform.SetAsLastSibling();
        _canvasGroup.blocksRaycasts = true;

        // ★ 플레이어 이동 차단
        if (blockPlayerDuringFade) DisablePlayerMovement();
        
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        _canvasGroup.alpha = 1f;

        // Fade Out 완료 (아직 _isFading = true 유지)
    }

    /// <summary>
    /// 플레이어 이동 차단
    /// </summary>
    private void DisablePlayerMovement()
    {
        // Player 태그를 가진 오브젝트 찾기
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        // GridMovementSystem 비활성화
        GridMovementSystem gms = player.GetComponent<GridMovementSystem>();
        if (gms != null)
        {
            gms.enabled = false;
            Debug.Log("[SceneFader] 플레이어 이동 차단");
        }

        // Rigidbody2D 정지
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // Animator Idle 전환
        Animator anim = player.GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetBool("IsMoving", false);
        }
    }

    /// <summary>
    /// 플레이어 이동 재활성화
    /// </summary>
    private void EnablePlayerMovement()
    {
        // Player 태그를 가진 오브젝트 찾기
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        // GridMovementSystem 활성화
        GridMovementSystem gms = player.GetComponent<GridMovementSystem>();
        if (gms != null)
        {
            gms.enabled = true;
            Debug.Log("[SceneFader] 플레이어 이동 재활성화");
        }
    }
}