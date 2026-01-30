using UnityEngine;

public class UIRoot : MonoBehaviour
{
    public static UIRoot Instance { get; private set; }

    private void Awake()
    {
        // 씬마다 UIRoot가 또 생기면 중복 제거
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // 씬 바뀌어도 유지
    }
}
