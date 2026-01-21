using UnityEngine;

[DisallowMultipleComponent]
public class StartPoint : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject playerPrefab;



    [Tooltip("게임 시작 시 자동으로 스폰/이동할지 여부")]
    private bool spawnOnStart = true;

    [Tooltip("씬에서 기존 플레이어를 찾을 때 사용할 태그")]
    private string playerTag = "Player";

    void Reset()
    {
        if (string.IsNullOrEmpty(gameObject.name) || gameObject.name.StartsWith("GameObject")) gameObject.name = "StartPoint";
    }

    void Start()
    {
        if (!spawnOnStart) return;

        var fade = GetComponentInChildren<PointSpriteFadeController>();
        if (fade != null) fade.FadeOut();

        // 씬에 이미 플레이어가 있는지 먼저 확인
        GameObject existingPlayer = null;

        if (!string.IsNullOrEmpty(playerTag))
        {
            try
            {
                existingPlayer = GameObject.FindWithTag(playerTag);
            }
            catch
            {
                existingPlayer = null;
            }
        }

        if (existingPlayer != null)
        {
            // 이미 배치된 캐릭터가 있으면 위치만 이동
            existingPlayer.transform.position = transform.position;

            var rb = existingPlayer.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;

            Debug.Log($"StartPoint: 기존 플레이어 '{existingPlayer.name}'를 시작 위치로 이동시켰습니다.");
            return;
        }

        // 씬에 플레이어가 없을 때만 프리팹 생성
        if (playerPrefab != null)
        {
            var spawned = Instantiate(playerPrefab, transform.position, Quaternion.identity);
            spawned.name = playerPrefab.name;

            if (string.IsNullOrEmpty(spawned.tag) || spawned.tag == "Untagged")
            {
                try { spawned.tag = playerTag; } catch { }
            }

            var rb = spawned.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;

            Debug.Log($"StartPoint: 플레이어 프리팹 '{spawned.name}'를 새로 스폰했습니다.");
        }
    }

    // 유틸리티
    public static StartPoint FindFirst()
    {
        return FindFirstObjectByType<StartPoint>();
    }
}
