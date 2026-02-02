using System.Collections;
using UnityEngine;

public class ShootingStarTest : MonoBehaviour
{
    private SpriteRenderer sr;

    [Header("설정")]
    public float visibleTime = 1.0f; // 완전히 보여지는 시간
    public float fadeTime = 0.5f;    // 서서히 사라지는 시간

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        // 1. 시작하자마자 무조건 보이게 설정
        gameObject.SetActive(true);
        Color c = sr.color;
        c.a = 1f;
        sr.color = c;

        Debug.Log(gameObject.name + " 가 보입니다. " + visibleTime + "초 뒤 사라집니다.");

        // 2. 사라지는 로직 시작
        StartCoroutine(HideRoutine());
    }

    IEnumerator HideRoutine()
    {
        // visibleTime 동안은 가만히 보임
        yield return new WaitForSeconds(visibleTime);

        // fadeTime 동안 서서히 투명해짐 (Alpha 1 -> 0)
        float t = 0;
        Color origColor = sr.color;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, t / fadeTime);
            sr.color = new Color(origColor.r, origColor.g, origColor.b, alpha);
            yield return null;
        }

        // 완전히 사라지면 비활성화
        gameObject.SetActive(false);
        Debug.Log(gameObject.name + " 가 사라졌습니다.");
    }
}