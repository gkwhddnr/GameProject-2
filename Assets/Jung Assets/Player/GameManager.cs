using TMPro;
using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public event Action OnPlayerTurnEnd;

    [Header("UI References")]
    public TextMeshProUGUI countText;

    private int MoveCount = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    public void NotifyTurnProcessed()
    {
        AddMoveCount(1);
        OnPlayerTurnEnd?.Invoke();
    }
    public void AddMoveCount(int i)
    {
        MoveCount += i;
        UpdateUI();
    }

    public void ResetMoveCount()
    {
        MoveCount = 0;
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (countText != null)
        {
            countText.text = "Count: " + MoveCount.ToString();
        }
    }
}
