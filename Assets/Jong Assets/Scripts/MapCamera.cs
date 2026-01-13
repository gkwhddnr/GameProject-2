using UnityEngine;

/// <summary>
/// 2D 맵용 카메라 컨트롤러 (BoxCollider2D로 지정한 영역 안에서만 이동)
/// Attach to Camera GameObject. Save as MapCamera.cs
/// </summary>
[RequireComponent(typeof(Camera))]
public class MapCamera : MonoBehaviour
{
    [Header("Movement")]
    public float panSpeed = 10f;
    public float panSmooth = 0.08f;

    [Header("Bounds (BoxCollider2D)")]
    public BoxCollider2D boundsCollider; // 인스펙터에서 드래그해서 지정

    [Header("Zoom")]
    public bool allowZoom = false;
    public float zoomSpeed = 5f;
    public float minOrthoSize = 3f;
    public float maxOrthoSize = 20f;

    Vector3 targetPos;
    Vector3 velocity = Vector3.zero;
    Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        targetPos = transform.position;
    }

    void Update()
    {
        // 입력
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 inputDir = new Vector3(h, v, 0f);

        if (inputDir.sqrMagnitude > 0.0001f)
        {
            targetPos += inputDir.normalized * panSpeed * Time.deltaTime;
        }

        // 줌 (오쏘그래픽 전제)
        // 오쏘그래픽: 멀리 있는 물체도 작아지지 않고, 가까이 있는 물체도 커지지 않는 카메라 (2D 턴제 게임에 적합)
        if (allowZoom && cam != null && cam.orthographic)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - scroll * zoomSpeed, minOrthoSize, maxOrthoSize);
            }
        }

        // Bounds가 있으면 카메라 뷰를 고려해 clamp
        if (boundsCollider != null && cam != null && cam.orthographic)
        {
            // 카메라 절반 크기 (월드 단위)
            float verticalExtent = cam.orthographicSize;
            float horizontalExtent = cam.orthographicSize * cam.aspect;

            Bounds b = boundsCollider.bounds;

            float minX = b.min.x + horizontalExtent;
            float maxX = b.max.x - horizontalExtent;
            float minY = b.min.y + verticalExtent;
            float maxY = b.max.y - verticalExtent;

            // 만약 영역이 카메라보다 작아서 min > max 가 되면, 영역 중심으로 고정
            if (minX > maxX)
            {
                float centerX = (b.min.x + b.max.x) / 2f;
                minX = maxX = centerX;
            }
            if (minY > maxY)
            {
                float centerY = (b.min.y + b.max.y) / 2f;
                minY = maxY = centerY;
            }

            targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
            targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);
        }
        else if (boundsCollider != null && cam != null && !cam.orthographic)
        {
            // 비-orthographic 카메라일 경우 간단히 collider 내부로만 클램프 (뷰포트 고려 안 함)
            Bounds b = boundsCollider.bounds;
            targetPos.x = Mathf.Clamp(targetPos.x, b.min.x, b.max.x);
            targetPos.y = Mathf.Clamp(targetPos.y, b.min.y, b.max.y);
        }

        // Smooth 이동
        if (panSmooth > 0f)
            transform.position = Vector3.SmoothDamp(transform.position, new Vector3(targetPos.x, targetPos.y, transform.position.z), ref velocity, panSmooth);
        else
            transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z);
    }

    // 씬에서 영역 및 카메라 위치 시각화
    void OnDrawGizmosSelected()
    {
        if (boundsCollider != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawCube(boundsCollider.bounds.center, boundsCollider.bounds.size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(boundsCollider.bounds.center, boundsCollider.bounds.size);
        }

        // 카메라 뷰 박스(오쏘 경우)
        Camera c = (cam != null) ? cam : GetComponent<Camera>();
        if (c != null && c.orthographic)
        {
            float vExt = c.orthographicSize;
            float hExt = vExt * c.aspect;
            Vector3 center = transform.position;
            center.z = 0f;
            Gizmos.color = new Color(0f, 0f, 1f, 0.15f);
            Gizmos.DrawCube(new Vector3(center.x, center.y, 0f), new Vector3(hExt * 2f, vExt * 2f, 0.01f));
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0f), new Vector3(hExt * 2f, vExt * 2f, 0.01f));
        }
    }

    // 외부에서 영역을 바꿔야 할 때 호출(예: 런타임에 바운드 오브젝트 교체)
    public void SetBounds(BoxCollider2D newBounds)
    {
        boundsCollider = newBounds;
    }
}
