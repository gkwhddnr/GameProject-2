using System.ComponentModel.Design;
using TMPro;
using UnityEditor;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI References")]
    public TextMeshProUGUI countText;

    private int currentMoveCount = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    public void AddMoveCount()
    {
        currentMoveCount++;
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (countText != null)
        {
            countText.text = "Count: " + currentMoveCount.ToString();
        }
    }
}
