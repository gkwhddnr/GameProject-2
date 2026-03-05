using UnityEngine;
using UnityEngine.Playables;

public class TimelineToGameBridge : MonoBehaviour
{
    public PlayableDirector director;
    public string nextSceneName = "Ha"; // 타임라인 종료 후 이동할 씬 이름

    void OnEnable()
    {
        director.stopped += OnTimelineStopped;
    }

    void OnDisable()
    {
        director.stopped -= OnTimelineStopped;
    }

    private void OnTimelineStopped(PlayableDirector aDirector)
    {
        if (director == aDirector)
        {
            Debug.Log("타임라인 종료: 게임 씬으로 이동합니다.");
            // 타임라인이 끝나면 SceneFader를 통해 실제 게임 씬으로 이동
            if (SceneFader.Instance != null)
            {
                SceneFader.Instance.FadeToScene(nextSceneName);
            }
        }
    }
}