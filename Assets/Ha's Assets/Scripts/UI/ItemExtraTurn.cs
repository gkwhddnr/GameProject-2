using UnityEngine;

/// <summary>
/// 플레이어가 아이템을 먹었을 때 스테이지별로 추가 턴을 지급하는 컴포넌트.
/// - Inspector에서 enableExtraTurns와 extraTurnsPerStage를 설정한다.
/// - 플레이어가 Collider2D(Trigger)로 닿으면 자동으로 GrantExtraTurns() 호출.
/// - 또는 다른 수거 시스템(예: ItemCollector)에서 GrantExtraTurns()를 직접 호출해도 된다.
/// </summary>
[DisallowMultipleComponent]
public class ItemExtraTurn : MonoBehaviour
{
    [Header("Extra Turn Settings")]
    [Tooltip("아이템 획득 시 추가 턴을 부여할지 여부")]
    public bool enableExtraTurns = true;

    [Tooltip("각 스테이지(인덱스)에 대해 부여할 추가 턴 수. (배열 길이는 GameManager의 스테이지 수와 맞추면 편함)")]
    public int[] extraTurnsPerStage;

    [Tooltip("배열에 해당 인덱스가 없다면 사용하는 기본값")]
    public int defaultExtraTurns = 1;


    // 이미 수거되었는지 방지
    private bool consumed = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        if (other == null) return;

        // 플레이어 태그로 감지 (플레이어 오브젝트에 "Player" 태그를 붙여두는 것을 권장)
        if (other.CompareTag("Player"))
        {
            GrantExtraTurns();
        }
    }

    /// <summary>
    /// 외부에서 명시적으로 호출하여 추가 턴을 지급하도록 허용.
    /// 예: ItemCollector.TryCollect() 내에서 item.GetComponentInChildren&lt;ItemExtraTurn&gt;()?.GrantExtraTurns();
    /// </summary>
    public void GrantExtraTurns()
    {
        if (consumed) return;
        consumed = true;

        if (!enableExtraTurns)
        {
            return;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[ItemExtraTurn] GameManager.Instance is null. Cannot add extra turns.");
            return;
        }
    }
}
