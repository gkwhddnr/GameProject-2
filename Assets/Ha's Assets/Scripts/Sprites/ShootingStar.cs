using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ShootingStar : MonoBehaviour
{
    [Header("Path")]
    public Vector3 startWorldPos = Vector3.zero;
    public Vector3 endWorldPos = Vector3.right;
    [Tooltip("머리가 start->end 를 이동하는 총 시간")]
    public float travelDuration = 0.5f;

    [Header("TrailRenderer settings")]
    [Tooltip("트레일이 남아있을 시간(초) — 이동이 끝난 뒤 이 시간 동안 트레일이 서서히 사라짐")]
    public float trailTime = 0.6f;
    [Tooltip("트레일이 생성될 때 점 사이 최소 거리 (작을수록 부드럽게)")]
    public float minVertexDistance = 0.01f;
    [Tooltip("머리 앞쪽 너비")]
    public float startWidth = 0.28f;
    [Tooltip("꼬리 끝 너비")]
    public float endWidth = 0.06f;
    [Tooltip("트레일 컬러 (알파는 그라데이션으로 제어됨)")]
    public Color trailColor = new Color(1f, 0.6f, 0.9f, 1f);
    [Tooltip("사용할 머티리얼(없으면 기본 Sprite/Default로 생성). 만약 텍스처 사용이면 assign.")]
    public Material trailMaterial;
    [Tooltip("선 텍스처로 사용하고 싶다면 sprite 할당 가능 (optional)")]
    public Sprite trailSprite;

    [Header("Star (끝 지점)")]
    public Sprite starSprite; // 별 스프라이트
    [Tooltip("트레일이 끝난 뒤 잠깐 대기하고 별을 보여주기")]
    public float starShowDelayAfterTravel = 0.04f;
    [Tooltip("별이 보여지는 시간")]
    public float starHold = 0.7f;
    [Tooltip("별 페이드아웃 시간")]
    public float starFade = 0.4f;
    public Vector2 starScale = new Vector2(0.5f, 0.5f);

    [Header("Misc")]
    [Tooltip("기본 정렬 순서 (트레일/별)")]
    public int sortingOrderBase = 500;
    [Tooltip("자동 재생 여부 (PlayRoutine)")]
    public bool playOnStart = false;
    [Tooltip("풀링을 사용하는 경우 파괴하지 말 것 (풀 사용 시 false)")]
    public bool destroyAfterPlay = true;

    // internal
    private TrailRenderer tr;
    private Coroutine playCoroutine;

    void Start()
    {
        if (playOnStart) PlayOnce(startWorldPos, endWorldPos, travelDuration);
    }

    /// <summary>
    /// 즉시 재생 시작 (외부에서 호출). 내부적으로 코루틴으로 재생함.
    /// </summary>
    public void PlayOnce(Vector3 from, Vector3 to, float travelTime = -1f)
    {
        startWorldPos = from;
        endWorldPos = to;
        if (travelTime > 0f) travelDuration = travelTime;

        // 중복 실행 방지: 이전 코루틴이 있으면 멈추고 재시작
        if (playCoroutine != null) StopCoroutine(playCoroutine);
        playCoroutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        // 위치 보정
        transform.position = startWorldPos;

        // TrailRenderer 세팅(존재하면 재사용, 없으면 추가)
        SetupTrailRenderer();

        // 시작 emitting
        tr.emitting = true;

        // 이동 (Linear)
        float t = 0f;
        while (t < travelDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, travelDuration));
            transform.position = Vector3.Lerp(startWorldPos, endWorldPos, a);
            yield return null;
        }
        transform.position = endWorldPos;

        // 더 이상 새 트레일 포인트 생성 안함 -> 기존 트레일은 tr.time 동안 뒤에서부터 사라짐
        tr.emitting = false;

        // optional small delay before star shows
        yield return new WaitForSeconds(Mathf.Max(0f, starShowDelayAfterTravel));

        // 별 출력
        if (starSprite != null)
        {
            yield return StartCoroutine(SpawnAndFadeStar(endWorldPos, starHold, starFade));
        }

        // 트레일이 완전히 사라질 시간을 기다리기 (optional)
        yield return new WaitForSeconds(Mathf.Max(0f, trailTime));

        // 재사용/종료 처리
        playCoroutine = null;
        if (destroyAfterPlay) Destroy(gameObject);
        else
        {
            // 만약 풀링이면 시각 상태 초기화(트레일 완전 초기화)
            // ensure trail cleared
            if (tr != null)
            {
                tr.Clear();
                tr.emitting = false;
            }
        }
    }

    private void SetupTrailRenderer()
    {
        tr = GetComponent<TrailRenderer>();
        if (tr == null) tr = gameObject.AddComponent<TrailRenderer>();

        tr.time = Mathf.Max(0.01f, trailTime);
        tr.minVertexDistance = Mathf.Max(0.0001f, minVertexDistance);

        // width curve
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, startWidth);
        curve.AddKey(1f, endWidth);
        tr.widthCurve = curve;

        // material: 만들거나 사용
        if (trailMaterial != null)
        {
            tr.material = trailMaterial;
        }
        else
        {
            Material m = new Material(Shader.Find("Sprites/Default"));
            if (trailSprite != null && trailSprite.texture != null)
            {
                m.mainTexture = trailSprite.texture;
            }
            tr.material = m;
        }

        // color gradient: 앞은 trailColor alpha 1, 끝은 alpha 0
        Gradient g = new Gradient();
        GradientColorKey[] cols = new GradientColorKey[2];
        cols[0].color = new Color(trailColor.r, trailColor.g, trailColor.b);
        cols[0].time = 0f;
        cols[1].color = new Color(trailColor.r, trailColor.g, trailColor.b);
        cols[1].time = 1f;
        GradientAlphaKey[] alphas = new GradientAlphaKey[2];
        alphas[0].alpha = 1f; alphas[0].time = 0f;
        alphas[1].alpha = 0f; alphas[1].time = 1f;
        g.SetKeys(cols, alphas);
        tr.colorGradient = g;

        tr.sortingOrder = sortingOrderBase;
        tr.numCapVertices = 4;
        tr.numCornerVertices = 4;
        tr.emitting = true;
    }

    private IEnumerator SpawnAndFadeStar(Vector3 pos, float hold, float fade)
    {
        GameObject star = new GameObject("ShootingStar_Star");
        star.transform.position = pos;
        star.transform.localScale = new Vector3(starScale.x, starScale.y, 1f);

        var sr = star.AddComponent<SpriteRenderer>();
        sr.sprite = starSprite;
        sr.sortingOrder = sortingOrderBase + 1;
        Color orig = sr.color;
        sr.color = new Color(orig.r, orig.g, orig.b, 0f);

        // 빠른 페이드인
        float tin = 0f;
        float inDur = Mathf.Min(0.12f, Mathf.Max(0.02f, fade * 0.2f));
        while (tin < inDur)
        {
            tin += Time.deltaTime;
            float a = Mathf.Clamp01(tin / inDur);
            sr.color = new Color(orig.r, orig.g, orig.b, a);
            yield return null;
        }
        sr.color = new Color(orig.r, orig.g, orig.b, 1f);

        // 보관
        yield return new WaitForSeconds(Mathf.Max(0f, hold));

        // fade out
        float t = 0f;
        while (t < fade)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / Mathf.Max(0.0001f, fade));
            sr.color = new Color(orig.r, orig.g, orig.b, a);
            yield return null;
        }

        Destroy(star);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        travelDuration = Mathf.Max(0.01f, travelDuration);
        trailTime = Mathf.Max(0.01f, trailTime);
    }
#endif
}
