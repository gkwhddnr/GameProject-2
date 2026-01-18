using System.Collections;
using UnityEngine;

/// <summary>
/// 도착 시(예: DestinationPoint.onReached) 호출해서
/// 1) 플레이어를 nextPoint(Transform)로 텔레포트
/// 2) MapCamera.SetBounds() 를 호출해 카메라의 bounds를 새 BoxCollider2D로 변경
/// 
/// 사용법:
/// - 이 컴포넌트를 Destination(GameObject) 또는 별도 빈 오브젝트에 붙임
/// - Inspector에 MapCamera, nextBounds(BoxCollider2D), nextPoint(Transform) 등 할당
/// - DestinationPoint.onReached 에 이 컴포넌트의 TeleportAndSwitch() 를 연결
/// </summary>
public class LevelTransition : MonoBehaviour
{
    [Header("References")]
    public MapCamera mapCamera;

    [Tooltip("다음 배경(영역)으로 사용할 BoxCollider2D")]
    public BoxCollider2D nextBounds;
    public Transform nextPoint;
    public GameObject playerObject;

    [Header("MapCamera.SetBounds options")]
    public bool snapCameraToBounds = true;
    public bool fitViewToBounds = true;

    [Header("Teleport options")]
    [Tooltip("텔레포트 시 플레이어 Rigidbody2D의 속도를 0으로 초기화")]
    public bool resetPlayerVelocity = true;

    /// <summary>
    /// DestinationPoint.onReached 에 연결
    /// </summary>
    public void TeleportAndSwitch()
    {
        // 1) 플레이어 확보
        GameObject player = ResolvePlayer();
        if (player == null) return;

        // 이동 시스템 강제 정지
        var moveSystem = player.GetComponent<GridMovementSystem>();

        if (moveSystem != null) moveSystem.enabled = false;

        // 2) 텔레포트
        if (nextPoint != null)
        {
            Vector3 teleportPos = nextPoint.position;

            var sr = nextPoint.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) teleportPos = sr.bounds.center;

            player.transform.position = teleportPos;
        }


        if (moveSystem != null) moveSystem.enabled = true;

        // 3) 카메라 전환
        if (mapCamera == null) mapCamera = FindFirstObjectByType<MapCamera>();

        if (mapCamera != null && nextBounds != null)
        {
            mapCamera.SetBounds(nextBounds, snapCameraToBounds, fitViewToBounds);
            mapCamera.playerTarget = player.transform;
        }

        // 텔레포트 후
        var fade = nextPoint.GetComponentInChildren<PointSpriteFadeController>();
        if (fade != null)
        {
            fade.gameObject.SetActive(true); // 혹시 비활성화돼 있다면
            fade.FadeOut();
        }
    }


    // playerObject 또는 tag로 플레이어 찾기
    GameObject ResolvePlayer()
    {
        if (playerObject != null) return playerObject;
        return null;
    }
}
