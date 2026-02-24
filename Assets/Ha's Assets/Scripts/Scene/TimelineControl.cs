using UnityEngine;
using UnityEngine.Playables;

public class TimelineControl : MonoBehaviour
{
    public PlayableDirector director;
    public GameObject nextIcon; // "▶" 아이콘 오브젝트 (Canvas 내부에 있는 것)
    private bool isWaitingForClick = false;

    void Start()
    {
        // 시작할 때는 아이콘이 안 보이게 설정
        if (nextIcon != null) nextIcon.SetActive(false);
    }

    void Update()
    {
        // 멈춰있는 상태에서 마우스 좌클릭이 들어오면
        if (isWaitingForClick && Input.GetMouseButtonDown(0))
        {
            isWaitingForClick = false;

            // 아이콘 다시 숨기기
            if (nextIcon != null) nextIcon.SetActive(false);

            director.Resume(); // 타임라인 재생
            Debug.Log("클릭 감지: 타임라인을 재개합니다.");
        }
    }

    // Signal에 의해 호출될 함수
    public void PauseTimeline()
    {
        isWaitingForClick = true;

        // 아이콘 나타나게 하기
        if (nextIcon != null) nextIcon.SetActive(true);

        director.Pause(); // 타임라인 일시정지
        Debug.Log("타임라인 일시정지: 클릭 대기 중...");
    }
}