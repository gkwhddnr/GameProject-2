using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainScreen : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnclickStartButton()
    {
        Debug.Log("start button clicked");
        if (SceneFader.Instance != null) SceneFader.Instance.FadeToScene("Ha");
        else
        {
            // SceneFader가 아직 없다면 생성 후 호출
            GameObject go = new GameObject("SceneFader");
            SceneFader f = go.AddComponent<SceneFader>();
            f.FadeToScene("Ha");
        }
    }

    public void OnclickcontinueButton()
    {
        Debug.Log("continue button clicked");
    }

    public void OnclickSettingsButton()
    {
        Debug.Log("settings button clicked");
        SceneManager.LoadScene("Choi_SettingScreen");
    }

    public void OnclickExitButton()
    {
        Debug.Log("exit button clicked");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
