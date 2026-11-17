using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public PlayerController player;
    public List<Collectible> items = new List<Collectible>();
    public Text countText;
    public Text clearText;
    public Text timeText;
    public Text timeUpText;
    public float timeLimit = 30f;

    // 演出用イベント(ロジックは演出を知らない)
    public event System.Action<Collectible> ItemCollected;
    public event System.Action GameCleared;
    public event System.Action TimeUpReached;
    public event System.Action GameReset;

    public int CollectedCount { get; private set; }
    public bool IsCleared { get; private set; }
    public bool IsTimeUp { get; private set; }
    public float RemainingTime { get; private set; }

    // 操作を受け付けるのは、クリアでも時間切れでもない間だけ
    public bool IsPlaying => !IsCleared && !IsTimeUp;

    void Start() => ResetGame();

    void Update() => Tick(Time.deltaTime);

    // テストから時間を進められるよう分離
    public void Tick(float dt)
    {
        if (!IsPlaying) return;           // A3/A4: クリア後・時間切れ後は時計を進めない
        RemainingTime -= dt;
        if (RemainingTime <= 0f)
        {
            RemainingTime = 0f;
            IsTimeUp = true;              // A2
            TimeUpReached?.Invoke();
        }
        UpdateUI();
    }

    public void Collect(Collectible item)
    {
        if (item == null || item.Collected) return;
        if (IsTimeUp || IsCleared) return;   // 時間切れ後・クリア後は回収しない
        item.MarkCollected();
        CollectedCount++;
        // 5個目と時間切れが同じ更新で重なった場合は回収完了を優先
        // (物理の回収判定はUpdateより先に走るため、Tickで時間切れになる前にここへ来る)
        if (CollectedCount >= items.Count) IsCleared = true;
        UpdateUI();
        ItemCollected?.Invoke(item);
        if (IsCleared) GameCleared?.Invoke();
    }

    public void ResetGame()
    {
        CollectedCount = 0;
        IsCleared = false;
        IsTimeUp = false;
        RemainingTime = timeLimit;           // A5
        foreach (var i in items) i.ResetItem();
        if (player != null) player.ResetPlayer();
        UpdateUI();
        GameReset?.Invoke();
    }

    void UpdateUI()
    {
        if (countText != null) countText.text = $"{CollectedCount}/{items.Count}";
        if (clearText != null) clearText.gameObject.SetActive(IsCleared);
        if (timeText != null) timeText.text = $"TIME {Mathf.CeilToInt(RemainingTime)}";
        if (timeUpText != null) timeUpText.gameObject.SetActive(IsTimeUp && !IsCleared);
    }
}
