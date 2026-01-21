using UnityEngine;

[RequireComponent(typeof(Renderer))]
[DisallowMultipleComponent]
public class BackgroundMover : MonoBehaviour
{
    [Header("Movement")]
    public Vector2 speed = new Vector2(0.1f, 0f); // 텍스처 오프셋속도 (1 == 텍스처 전체 길이 / 초)
    public bool moveX = true;
    public bool moveY = false;
    public bool useUnscaledTime = false;

    [Header("PingPong (대체 움직임)")]
    public bool pingPongX = false;
    public bool pingPongY = false;
    public float pingPeriodX = 2f;
    public float pingPeriodY = 2f;

    [Header("Runtime")]
    public bool autoStart = true;

    // 내부
    Renderer rend;
    MaterialPropertyBlock mpb;
    Vector2 offset = Vector2.zero;
    float startTime = 0f;
    bool running = false;

    BoxCollider2D bounds;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        startTime = useUnscaledTime ? Time.realtimeSinceStartup : Time.time;
        if (autoStart) StartMove();
    }

    void OnDisable()
    {
        StopMove();
    }

    void Update()
    {
        if (!running) return;
        float realDt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float t = (useUnscaledTime ? Time.realtimeSinceStartup : Time.time) - startTime;

        // X
        if (pingPongX)
        {
            float p = Mathf.PingPong(t / Mathf.Max(0.0001f, pingPeriodX), 1f);
            offset.x = p * Mathf.Sign(speed.x);
        }
        else
        {
            if (moveX) offset.x += speed.x * realDt;
        }

        // Y
        if (pingPongY)
        {
            float p = Mathf.PingPong(t / Mathf.Max(0.0001f, pingPeriodY), 1f);
            offset.y = p * Mathf.Sign(speed.y);
        }
        else
        {
            if (moveY) offset.y += speed.y * realDt;
        }

        offset.x = Wrap01(offset.x);
        offset.y = Wrap01(offset.y);

        ApplyOffsetToMaterial(offset);
    }

    float Wrap01(float v)
    {
        if (v >= 1f || v < 0f) v = v - Mathf.Floor(v);
        return v;
    }

    void ApplyOffsetToMaterial(Vector2 offs)
    {
        if (rend == null) return;
        rend.GetPropertyBlock(mpb);
        mpb.SetVector("_MainTex_ST", new Vector4(1, 1, offs.x, offs.y));
        rend.SetPropertyBlock(mpb);
    }

    public void SetBounds(BoxCollider2D newBounds){ bounds = newBounds; }
    public void StopMove() { running = false; }

    public void StartMove()
    {
        running = true;
        startTime = useUnscaledTime ? Time.realtimeSinceStartup : Time.time;
    }

    // 유틸: 즉시 오프셋 초기화
    public void ResetOffset()
    {
        offset = Vector2.zero;
        ApplyOffsetToMaterial(offset);
    }
}
