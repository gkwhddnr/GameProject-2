using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class StageBoundsUIUpdater : MonoBehaviour
{
    [System.Serializable]
    public class StageEntry
    {
        public BoxCollider2D[] bounds;
        [TextArea] public string message;
    }

    [Header("References")]
    public StageEntry[] stageEntries;
    public Transform playerTransform;
    public TextMeshProUGUI uiText;

    [Header("Settings")]
    public float pollInterval = 0.15f;
    [Header("Fade")]
    public float fadeDuration = 0.5f;

    private float pollTimer = 0f;
    private int prevPlayerStageIndex = -2;
    private CanvasGroup cg;
    private Coroutine fadeCoroutine;

    // 최적화: 매 프레임 생성을 방지하기 위한 캐싱
    private WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();

    void Start()
    {
        if (playerTransform == null && GameManager.Instance != null)
            playerTransform = GameManager.Instance.playerTransform;

        // 시작 시점에 딱 한 번만 캐싱 (과부하 방지)
        InitCanvasGroup();
        RefreshUIImmediate();
    }

    private void InitCanvasGroup()
    {
        if (uiText != null)
        {
            cg = uiText.GetComponent<CanvasGroup>();
            if (cg == null) cg = uiText.gameObject.AddComponent<CanvasGroup>();
        }
    }

    void Update()
    {
        if (playerTransform == null || uiText == null) return;

        pollTimer += Time.deltaTime;
        if (pollTimer < pollInterval) return;
        pollTimer = 0f;

        int currentPlayerStageIndex = GetPlayerStageIndex();

        if (currentPlayerStageIndex != prevPlayerStageIndex)
        {
            prevPlayerStageIndex = currentPlayerStageIndex;
            UpdateUITextWithFade(currentPlayerStageIndex);
        }
    }

    private int GetPlayerStageIndex()
    {
        if (stageEntries == null) return -1;
        Vector3 p = playerTransform.position;

        for (int i = 0; i < stageEntries.Length; ++i)
        {
            var entry = stageEntries[i];
            if (entry == null || entry.bounds == null) continue;
            for (int j = 0; j < entry.bounds.Length; ++j)
            {
                if (entry.bounds[j] != null && entry.bounds[j].bounds.Contains(p))
                    return i;
            }
        }
        return -1;
    }

    private void UpdateUITextWithFade(int idx)
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

        if (idx >= 0 && idx < stageEntries.Length)
        {
            // 부드러운 전환을 위해 FadeOut 후 FadeIn 하거나, 
            // 현재 요청하신 대로 즉시 끄고 페이드 인 하는 로직 유지
            cg.alpha = 0f;
            uiText.text = stageEntries[idx].message ?? string.Empty;
            if (!uiText.gameObject.activeSelf) uiText.gameObject.SetActive(true);

            fadeCoroutine = StartCoroutine(FadeInRoutine(fadeDuration));
        }
        else
        {
            // 구역을 벗어났을 때도 부드럽게 사라지게 하고 싶다면 FadeOutRoutine을 호출
            if (uiText.gameObject.activeSelf) uiText.gameObject.SetActive(false);
        }
    }

    private IEnumerator FadeInRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(0f, 1f, t / duration);
            yield return _waitForEndOfFrame; // 캐싱된 객체 사용으로 GC 방지
        }
        cg.alpha = 1f;
        fadeCoroutine = null;
    }

    public void RefreshUIImmediate()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

        prevPlayerStageIndex = GetPlayerStageIndex();
        if (uiText == null) return;

        if (prevPlayerStageIndex >= 0 && prevPlayerStageIndex < stageEntries.Length)
        {
            cg.alpha = 1f;
            uiText.text = stageEntries[prevPlayerStageIndex].message;
            uiText.gameObject.SetActive(true);
        }
        else
        {
            uiText.gameObject.SetActive(false);
        }
    }
}