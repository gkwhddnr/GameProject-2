using UnityEngine;

/// <summary>
/// 2D 맵용 카메라 컨트롤러
/// - boundsCollider: 카메라 이동 제한(카메라가 영역 밖으로 나가지 않도록)
/// - fitBoundsCollider: 이 콜라이더를 할당하면 그 크기만큼 viewWidth/viewHeight를 맞춰 카메라 뷰를 자동으로 설정
/// - fitPadding: fit 시 추가 여백 (월드 단위)
/// - fitEveryFrame: fitBoundsCollider가 런타임에 움직이면 true로 두어 매 프레임 재적용 가능
/// </summary>
[RequireComponent(typeof(Camera))]
public class MapCamera : MonoBehaviour
{
    [Header("카메라 움직임")]
    public float panSpeed = 10f;
    public float panSmooth = 0.08f;

    [Header("전체 배경에 맞게 자동 카메라 영역 설정")]
    [Tooltip("카메라 이동 제한에 사용할 BoxCollider2D (선택). 비어있어도 동작합니다.")]
    public BoxCollider2D boundsCollider; // 카메라 이동 클램프용

    [Header("Zoom")]
    public bool allowZoom = false;        // 마우스 휠 줌 허용 (useFixedViewSize가 켜져있으면 무시됨)
    public float zoomSpeed = 5f;
    public float minOrthoSize = 3f;
    public float maxOrthoSize = 20f;

    [Header("카메라 영역 설정")]
    public bool useFixedViewSize = false;
    public float viewWidth = 20f;
    public float viewHeight = 10f;

    Vector3 targetPos;
    Vector3 velocity = Vector3.zero;
    Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        targetPos = transform.position;

        // 안전값
        viewWidth = Mathf.Max(0.01f, viewWidth);
        viewHeight = Mathf.Max(0.01f, viewHeight);
        minOrthoSize = Mathf.Max(0.0001f, minOrthoSize);
        maxOrthoSize = Mathf.Max(minOrthoSize, maxOrthoSize);

        // 시작 시 bounds가 있으면 카메라 위치 클램프
        if (boundsCollider != null)
        {
            ClampTargetToBounds();
            transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z);
        }
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

        if (cam != null && cam.orthographic && useFixedViewSize)
        {
            ApplyFixedViewSize();
        }
        else
        {
            // 줌 (오쏘그래픽 전제, useFixedViewSize가 켜져 있으면 실행되지 않음)
            if (allowZoom && cam != null && cam.orthographic)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.0001f)
                {
                    cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - scroll * zoomSpeed, minOrthoSize, maxOrthoSize);
                }
            }
        }

        // Bounds가 있으면 카메라 뷰를 고려해 clamp
        if (boundsCollider != null && cam != null && cam.orthographic)
        {
            float verticalExtent = cam.orthographicSize;
            float horizontalExtent = cam.orthographicSize * cam.aspect;

            Bounds b = boundsCollider.bounds;

            float minX = b.min.x + horizontalExtent;
            float maxX = b.max.x - horizontalExtent;
            float minY = b.min.y + verticalExtent;
            float maxY = b.max.y - verticalExtent;

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

    /// <summary>
    /// viewWidth x viewHeight 를 기준으로 orthographicSize 계산 후 적용.
    /// ignoreMaxOrtho = true 이면 maxOrthoSize 제한을 무시하고 필요한 ortho를 강제 설정.
    /// </summary>
    void ApplyFixedViewSize(bool ignoreMaxOrtho = false)
    {
        if (cam == null || !cam.orthographic) return;

        // 사용자가 요청한 view -> ortho 계산 (viewWidth/viewHeight 사용)
        float orthoFromHeight = viewHeight * 0.5f;
        float orthoFromWidth = (viewWidth / cam.aspect) * 0.5f;
        float targetOrtho = Mathf.Max(orthoFromHeight, orthoFromWidth);

        // bounds가 있으면 bounds에 맞춰 축소(또는 맞춤)
        if (boundsCollider != null)
        {
            Bounds b = boundsCollider.bounds;
            float boundsWidth = Mathf.Max(0.0001f, b.size.x);
            float boundsHeight = Mathf.Max(0.0001f, b.size.y);

            float allowedOrthoFromHeight = boundsHeight * 0.5f;
            float allowedOrthoFromWidth = (boundsWidth / cam.aspect) * 0.5f;
            float allowedOrtho = Mathf.Min(allowedOrthoFromHeight, allowedOrthoFromWidth);
            allowedOrtho = Mathf.Max(0.0001f, allowedOrtho);

            // 만약 targetOrtho가 bounds보다 크다면 bounds에 맞추어 줄인다
            if (targetOrtho > allowedOrtho)
            {
                targetOrtho = allowedOrtho;

                // 실제 적용되는 화면 크기 계산 (실제 카메라에 적용될 크기)
                float appliedHeight = targetOrtho * 2f;
                float appliedWidth = appliedHeight * cam.aspect;

                // 인스펙터에 보이는 viewWidth/viewHeight를 실제 적용값으로 갱신
                viewWidth = appliedWidth;
                viewHeight = appliedHeight;
            }
        }

        // 이제 maxOrthoSize 제한 적용 여부 판단
        if (!ignoreMaxOrtho)
        {
            targetOrtho = Mathf.Clamp(targetOrtho, 0.0001f, maxOrthoSize);
        }
        else
        {
            // ignoreMaxOrtho일 때에도 최소값은 보장
            targetOrtho = Mathf.Max(0.0001f, targetOrtho);
        }

        cam.orthographicSize = targetOrtho;
    }

    // targetPos가 bounds 내부가 되도록 클램프(필요 시 가운데 고정)
    void ClampTargetToBounds()
    {
        if (boundsCollider == null || cam == null || !cam.orthographic) return;

        Bounds b = boundsCollider.bounds;
        float verticalExtent = cam.orthographicSize;
        float horizontalExtent = cam.orthographicSize * cam.aspect;

        float minX = b.min.x + horizontalExtent;
        float maxX = b.max.x - horizontalExtent;
        float minY = b.min.y + verticalExtent;
        float maxY = b.max.y - verticalExtent;

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

        if (useFixedViewSize)
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.15f);
            Vector3 size = new Vector3(viewWidth, viewHeight, 0.01f);
            Gizmos.DrawCube(new Vector3(transform.position.x, transform.position.y, 0f), size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(new Vector3(transform.position.x, transform.position.y, 0f), size);
        }
    }
}
