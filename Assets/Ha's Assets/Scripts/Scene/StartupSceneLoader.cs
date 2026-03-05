using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;


static class StartupSceneLoader
{
    private const string MainSceneName = "Choi_MainScreen";

    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureMainMenuShown()
    {
        // �̹� ���� ���̸� �ƹ��͵� ����
        if (SceneManager.GetActiveScene().name == MainSceneName) return;

        // Build Settings�� �ش� ���� �ִ��� Ȯ��
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

        // ���� ������ �ε� (����)
        SceneManager.LoadScene(MainSceneName);
    }
    
}
