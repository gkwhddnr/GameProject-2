using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class KeyActivator : MonoBehaviour
{
    [Header("Key에 매핑된 오브젝트들 (키 수거 시 페이드 아웃 호출)")]
    public GameObject[] targets;

    // 한 번만 동작하도록 기본 설정
    private bool _used = false;

    /// <summary>
    /// ItemCollector에서 이 키를 수거할 때 호출하세요.
    /// 매핑된 오브젝트들을 ItemCollector의 페이드 루틴으로 제거 호출합니다.
    /// ItemCollector가 없으면 즉시 Destroy합니다.
    /// </summary>
    public void Activate(int stageIndex = -1)
    {
        if (_used) return;
        _used = true;

        if (targets == null) return;

        // 가능한 경우 ItemCollector 인스턴스 사용
        var collector = FindAnyObjectByType<ItemCollector>();

        for (int i = 0; i < targets.Length; i++)
        {
            var t = targets[i];
            if (t == null) continue;

            if (collector != null)
            {
                // ItemCollector가 있으면 그 쪽의 페이드 아웃 루틴으로 처리
                collector.FadeOutTarget(t);
            }
            else
            {
                Destroy(t);
            }
        }
    }
}
