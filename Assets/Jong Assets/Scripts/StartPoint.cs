using UnityEngine;

/// <summary>
/// 씬에서 플레이어 시작 위치를 표시하는 마커 컴포넌트.
/// 빈 오브젝트에 붙여 사용. (Inspector에서 위치로 드래그할 수 있음)
/// </summary>
[DisallowMultipleComponent]
public class StartPoint : MonoBehaviour
{
    void Reset()
    {
        // 기본 이름 설정 (원하면 변경 가능)
        if (string.IsNullOrEmpty(gameObject.name) || gameObject.name.StartsWith("GameObject"))
            gameObject.name = "StartPoint";
    }

    // 유틸리티: 코드에서 쉽게 참조하기 위한 정적 찾기 함수
    public static StartPoint FindFirst()
    {
        return FindFirstObjectByType<StartPoint>();
    }
}
