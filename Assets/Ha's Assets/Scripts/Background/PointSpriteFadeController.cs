using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PointSpriteFadeController : MonoBehaviour
{
    [Header("Fade Settings")]
    public float fadeOutDuration = 0.8f;
    public bool disableAfterFade = true;

    SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// 외부에서 호출
    /// </summary>
    public void FadeOut()
    {
        StopAllCoroutines();
        StartCoroutine(FadeOutRoutine());
    }

    IEnumerator FadeOutRoutine()
    {
        float t = 0f;
        Color c = spriteRenderer.color;
        c.a = 1f;
        spriteRenderer.color = c;

        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, t / fadeOutDuration);
            spriteRenderer.color = c;
            yield return null;
        }

        c.a = 0f;
        spriteRenderer.color = c;

        if (disableAfterFade)
            spriteRenderer.enabled = false;
    }
}
