using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

/// <summary>
/// EventSystem 중복 방지 및 자동 관리
/// 씬 전환 시 EventSystem이 하나만 존재하도록 보장
/// </summary>
public class EventSystemManager : MonoBehaviour
{
    public static EventSystemManager Instance { get; private set; }

    private EventSystem persistentEventSystem;

    private void Awake()
    {
        // 싱글톤 패턴
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 씬 로드 이벤트 구독
            SceneManager.sceneLoaded += OnSceneLoaded;

            // 현재 씬의 EventSystem 정리
            CleanupEventSystems();

            Debug.Log("[EventSystemManager] 초기화 완료");
        }
        else Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제
        if (Instance == this)SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// 씬이 로드될 때마다 호출되어 EventSystem 정리
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[EventSystemManager] 씬 로드됨: {scene.name} (모드: {mode})");

        // 약간의 딜레이 후 정리 (씬의 모든 오브젝트가 초기화될 시간 확보)
        StartCoroutine(DelayedCleanup());
    }

    private System.Collections.IEnumerator DelayedCleanup()
    {
        // 1프레임 대기
        yield return null;

        CleanupEventSystems();
    }

    /// <summary>
    /// EventSystem 중복 제거 및 하나만 유지
    /// </summary>
    private void CleanupEventSystems()
    {
        // 모든 EventSystem 찾기 (비활성화된 것도 포함)
        EventSystem[] allEventSystems = FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (allEventSystems.Length == 0)
        {
            // EventSystem이 하나도 없으면 생성
            Debug.LogWarning("[EventSystemManager] EventSystem이 없습니다. 새로 생성합니다.");
            CreatePersistentEventSystem();
            return;
        }

        if (allEventSystems.Length == 1)
        {
            // 하나만 있으면 그것을 영구 보존
            persistentEventSystem = allEventSystems[0];
            DontDestroyOnLoad(persistentEventSystem.gameObject);

            // 활성화 보장
            if (!persistentEventSystem.gameObject.activeInHierarchy)
            {
                persistentEventSystem.gameObject.SetActive(true);
            }

            Debug.Log($"[EventSystemManager] EventSystem 1개 유지: {persistentEventSystem.gameObject.name}");
            return;
        }

        // 2개 이상이면 중복 제거
        Debug.LogWarning($"[EventSystemManager] EventSystem {allEventSystems.Length}개 발견! 중복 제거 중...");

        EventSystem keepSystem = null;

        // 1순위: 이미 persistentEventSystem으로 지정된 것
        if (persistentEventSystem != null && System.Array.IndexOf(allEventSystems, persistentEventSystem) >= 0)
        {
            keepSystem = persistentEventSystem;
        }
        // 2순위: DontDestroyOnLoad 씬에 있는 것
        else
        {
            foreach (var es in allEventSystems)
            {
                if (es.gameObject.scene.name == null || es.gameObject.scene.name == "")
                {
                    keepSystem = es;
                    break;
                }
            }
        }
        // 3순위: 첫 번째 활성화된 것
        if (keepSystem == null)
        {
            foreach (var es in allEventSystems)
            {
                if (es.gameObject.activeInHierarchy)
                {
                    keepSystem = es;
                    break;
                }
            }
        }
        // 4순위: 그냥 첫 번째 것
        if (keepSystem == null)
        {
            keepSystem = allEventSystems[0];
        }

        // 선택된 EventSystem을 영구 보존
        persistentEventSystem = keepSystem;
        DontDestroyOnLoad(persistentEventSystem.gameObject);

        // 활성화 보장
        if (!persistentEventSystem.gameObject.activeInHierarchy) persistentEventSystem.gameObject.SetActive(true);

        Debug.Log($"[EventSystemManager] 유지할 EventSystem: {persistentEventSystem.gameObject.name}");

        // 나머지 중복 제거
        foreach (var es in allEventSystems)
        {
            if (es != persistentEventSystem)
            {
                Debug.Log($"[EventSystemManager] 중복 EventSystem 제거: {es.gameObject.name}");
                Destroy(es.gameObject);
            }
        }

        Debug.Log("[EventSystemManager] EventSystem 정리 완료!");
    }

    /// <summary>
    /// 영구 EventSystem 생성
    /// </summary>
    private void CreatePersistentEventSystem()
    {
        GameObject esGO = new GameObject("EventSystem (Persistent)");
        persistentEventSystem = esGO.AddComponent<EventSystem>();

        // StandaloneInputModule 추가 (InputModule이 없으면 입력이 작동하지 않음)
        if (esGO.GetComponent<StandaloneInputModule>() == null) esGO.AddComponent<StandaloneInputModule>();

        DontDestroyOnLoad(esGO);

        Debug.Log("[EventSystemManager] 새 EventSystem 생성 완료");
    }

    /// <summary>
    /// 현재 유지 중인 EventSystem 반환
    /// </summary>
    public EventSystem GetEventSystem(){ return persistentEventSystem; }

    /// <summary>
    /// EventSystem이 존재하는지 확인
    /// </summary>
    public bool HasEventSystem(){ return persistentEventSystem != null; }
}