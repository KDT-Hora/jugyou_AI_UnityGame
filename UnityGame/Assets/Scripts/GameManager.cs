using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public PlayerController player;
    public List<Collectible> items = new List<Collectible>();
    public Text countText;
    public Text clearText;

    public int CollectedCount { get; private set; }
    public bool IsCleared { get; private set; }

    void Start() => ResetGame();

    public void Collect(Collectible item)
    {
        if (item == null || item.Collected) return;
        item.MarkCollected();
        CollectedCount++;
        if (CollectedCount >= items.Count) IsCleared = true;
        UpdateUI();
    }

    public void ResetGame()
    {
        CollectedCount = 0;
        IsCleared = false;
        foreach (var i in items) i.ResetItem();
        if (player != null) player.ResetPlayer();
        UpdateUI();
    }

    void UpdateUI()
    {
        if (countText != null) countText.text = $"{CollectedCount}/{items.Count}";
        if (clearText != null) clearText.gameObject.SetActive(IsCleared);
    }
}
