using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light2D), typeof(PolygonCollider2D))]
public class DynamicLightCollider : MonoBehaviour
{
    private Light2D myLight;
    private PolygonCollider2D myCollider;

    [Tooltip("부채꼴의 부드러움 정도")]
    public int segments = 20;

    // 현재 각도를 기억해서 중복 계산 방지
    private float currentAngle = -1f;

    private void Awake()
    {
        myLight = GetComponent<Light2D>();
        myCollider = GetComponent<PolygonCollider2D>();
        ForceUpdate(); // 시작할 때 한 번 모양 잡기
    }

    // 외부(지휘관)에서 이 함수를 부를 때만 모양을 바꿈
    public void SetLightAngle(float newAngle)
    {
        // 이미 그 각도면 무시 (메모리 절약)
        if (Mathf.Abs(currentAngle - newAngle) < 0.01f) return;

        myLight.pointLightOuterAngle = newAngle;
        myLight.pointLightInnerAngle = newAngle - 10f;

        UpdateShape();
    }

    // 강제로 모양 맞추기 (초기화용)
    public void ForceUpdate()
    {
        if (myLight != null) UpdateShape();
    }

    private void UpdateShape()
    {
        if (myLight == null || myCollider == null) return;

        float radius = myLight.pointLightOuterRadius;
        float angle = myLight.pointLightOuterAngle;
        currentAngle = angle;

        int pointCount = segments + 2;
        Vector2[] points = new Vector2[pointCount];
        points[0] = Vector2.zero;

        float startAngle = -angle / 2f;
        float angleStep = angle / segments;

        for (int i = 0; i <= segments; i++)
        {
            float rad = (startAngle + (angleStep * i)) * Mathf.Deg2Rad;
            points[i + 1] = new Vector2(-Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius);
        }
        myCollider.points = points;
    }
}