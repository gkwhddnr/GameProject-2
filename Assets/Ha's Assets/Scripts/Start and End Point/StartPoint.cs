using UnityEngine;

[DisallowMultipleComponent]
public class StartPoint : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("게임 시작 시 스폰할 플레이어 프리팹. 비어있으면 씬의 'Player' 태그 오브젝트를 찾아 이동시킵니다.")]
    public GameObject playerPrefab;

    [Tooltip("게임 시작 시 자동으로 스폰/이동할지 여부")]
    public bool spawnOnStart = true;

    [Tooltip("씬에서 기존 플레이어를 찾을 때 사용할 태그 (기본: Player)")]
    public string playerTag = "Player";

    void Reset()
    {
        if (string.IsNullOrEmpty(gameObject.name) || gameObject.name.StartsWith("GameObject"))
            gameObject.name = "StartPoint";
    }

    void Start()
    {
        if (!spawnOnStart) return;

        if (playerPrefab != null)
        {
            // 프리팹이 지정되어 있으면 새로 Instantiate
            var spawned = Instantiate(playerPrefab, transform.position, Quaternion.identity);
            spawned.name = playerPrefab.name; // 원하면 이름 통일
            // 태그가 비어있다면 playerTag로 설정해주기 (프리팹이 태그가 이미 있으면 건드리지 않음)
            if (string.IsNullOrEmpty(spawned.tag) || spawned.tag == "Untagged")
            {
                try { spawned.tag = playerTag; } catch { /* 태그가 없으면 무시 */ }
            }

            // Rigidbody2D가 있으면 속도 초기화
            var rb = spawned.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }
        else
        {
            // 프리팹이 없으면 씬에서 Player 태그 객체를 찾아서 위치를 이동
            var existing = GameObject.FindWithTag(playerTag);
            if (existing != null)
            {
                existing.transform.position = transform.position;
                var rb = existing.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }
            else
            {
                Debug.LogWarning($"StartPoint: playerPrefab이 지정되지 않았고, 씬에서 태그 '{playerTag}'인 오브젝트를 찾지 못했습니다.");
            }
        }
    }

    // 유틸리티: 코드에서 쉽게 참조하기 위한 정적 찾기 함수
    public static StartPoint FindFirst()
    {
        return FindFirstObjectByType<StartPoint>();
    }
}
