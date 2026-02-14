using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

static class StartupSceneLoader
{
    private const string MainSceneName = "Choi_MainScreen";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureMainMenuShown()
    {
        // 이미 메인 씬이면 아무것도 안함
        if (SceneManager.GetActiveScene().name == MainSceneName) return;

        // Build Settings에 해당 씬이 있는지 확인
        bool found = false;
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(name, MainSceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                break;
            }
        }

        if (!found) return;

        // 메인 씬으로 로드 (동기)
        SceneManager.LoadScene(MainSceneName);
    }
}
