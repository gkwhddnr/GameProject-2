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

    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private Image _fadeImage;

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

    public void FadeToScene(string sceneName)
    {
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(sceneName));
    }

    IEnumerator FadeRoutine(string sceneName)
    {
        SetupFadeUI();
        transform.SetAsLastSibling();
        _canvasGroup.blocksRaycasts = true;

        // [Fade Out] 화면 덮기 (0 -> 1)
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            // Mathf.Saturate 대신 Mathf.Clamp01 사용
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
            // Mathf.Saturate 대신 Mathf.Clamp01 사용
            _canvasGroup.alpha = Mathf.Clamp01(1f - (t / fadeDuration));
            yield return null;
        }

        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
    }
}