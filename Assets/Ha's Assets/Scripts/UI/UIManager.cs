using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// UIManager - UI 전용 씬(Choi_UIRoot)을 Additive 방식으로 로드하여 관리합니다.
/// EventSystem 중복도 자동으로 처리합니다.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI Scene Settings")]
    [Tooltip("로드할 UI 씬 이름")]
    public string uiSceneName = "Choi_UIRoot";

    [Tooltip("게임 시작 시 자동으로 UI 씬 로드")]
    public bool loadOnStart = true;

    [Tooltip("UI 씬 로드 완료 후 콜백")]
    public UnityEngine.Events.UnityEvent onUILoaded;

    private bool isUISceneLoaded = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start(){ if (loadOnStart) LoadUIScene(); }

    public void LoadUIScene()
    {
        if (isUISceneLoaded) return;
        if (!IsSceneInBuildSettings(uiSceneName)) return;
        StartCoroutine(LoadUISceneAsync());
    }

    private IEnumerator LoadUISceneAsync()
    {
        Debug.Log($"[UIManager] UI 씬 로드 시작: {uiSceneName}");
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(uiSceneName, LoadSceneMode.Additive);

        while (!asyncLoad.isDone) yield return null;

        isUISceneLoaded = true;

        // 1. 현재 씬에서 진짜 메인 카메라(MapCamera가 붙은 것)를 찾음
        Camera mainCam = Camera.main;

        // 2. UI 씬 내부의 오브젝트들을 뒤져서 처리
        Scene uiScene = SceneManager.GetSceneByName(uiSceneName);
        GameObject[] rootObjects = uiScene.GetRootGameObjects();

        foreach (GameObject root in rootObjects)
        {
            // UI 씬의 카메라가 보이면 바로 끄거나 삭제
            Camera uiCam = root.GetComponentInChildren<Camera>(true);
            if (uiCam != null && uiCam != mainCam)
            {
                Debug.Log($"[UIManager] UI 씬의 불필요한 카메라 제거: {uiCam.name}");
                uiCam.gameObject.SetActive(false); // 혹은 Destroy(uiCam.gameObject);
            }

            // Canvas를 찾아 메인 카메라와 연결
            Canvas canvas = root.GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; // 혹은 Overlay
                canvas.worldCamera = mainCam;
                canvas.planeDistance = 1; // UI가 카메라 바로 앞에 오도록 설정
            }
        }
        RemoveDuplicateEventSystems();

        onUILoaded?.Invoke();
    }

    /// 중복된 EventSystem을 제거
    private void RemoveDuplicateEventSystems()
    {
        EventSystem[] eventSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);

        if (eventSystems.Length > 1)
        {
            Debug.LogWarning($"[UIManager] EventSystem이 {eventSystems.Length}개 발견됨. 중복 제거 중...");

            // 첫 번째 EventSystem만 유지하고 나머지 삭제
            for (int i = 1; i < eventSystems.Length; i++)
            {
                Debug.Log($"[UIManager] 중복 EventSystem 제거: {eventSystems[i].gameObject.name}");
                Destroy(eventSystems[i].gameObject);
            }

            // 첫 번째 EventSystem을 DontDestroyOnLoad로 유지
            DontDestroyOnLoad(eventSystems[0].gameObject);
            Debug.Log($"[UIManager] EventSystem 유지: {eventSystems[0].gameObject.name}");
        }
        else if (eventSystems.Length == 1)
        {
            DontDestroyOnLoad(eventSystems[0].gameObject);
            Debug.Log($"[UIManager] EventSystem 확인: {eventSystems[0].gameObject.name}");
        }
        else
             Debug.LogError("[UIManager] EventSystem을 찾을 수 없습니다! UI 입력이 작동하지 않을 수 있습니다.");
    }

    public void UnloadUIScene()
    {
        if (!isUISceneLoaded) return;
        StartCoroutine(UnloadUISceneAsync());
    }

    private IEnumerator UnloadUISceneAsync()
    {
        Debug.Log($"[UIManager] UI 씬 언로드 시작: {uiSceneName}");

        AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(uiSceneName);

        while (!asyncUnload.isDone) yield return null;
        

        isUISceneLoaded = false;
        Debug.Log($"[UIManager] UI 씬 언로드 완료: {uiSceneName}");
    }

    private bool IsSceneInBuildSettings(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneNameInBuild = System.IO.Path.GetFileNameWithoutExtension(scenePath);

            if (sceneNameInBuild == sceneName) return true;
        }
        return false;
    }

    public bool IsUISceneLoaded(){ return isUISceneLoaded; }

    void OnDestroy()
    {
        if (isUISceneLoaded && Instance == this) SceneManager.UnloadSceneAsync(uiSceneName);
    }
}