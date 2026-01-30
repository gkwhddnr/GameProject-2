using UnityEngine;
using TMPro;

public class TimerManager : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;
    private float elapsed;

    private void Update()
    {
        elapsed += Time.deltaTime;

        int m = Mathf.FloorToInt(elapsed / 60f);
        int s = Mathf.FloorToInt(elapsed % 60f);

        timerText.text = $"{m:00}:{s:00}";
    }

    public void ResetTimer()
    {
        elapsed = 0f;
    }
}
