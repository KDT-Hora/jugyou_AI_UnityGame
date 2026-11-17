using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 玉の行き先
public enum BallResult
{
    Score,   // 得点の穴: アイテム獲得(カウント+1)
    Bonus,   // ボーナスの穴: フィールドにメダルが大量に出現
    Miss     // どの穴にも入らなかった: フィールドにメダルが1枚出現
}

// ルール:
//  - メダル(フィールドのコイン)に触れると、メダルが消えて玉がパチンコ台へ発射される(カウントは増えない)
//  - 玉が得点の穴に入ると、アイテム獲得(+1)。goal個でCLEAR
//  - 玉がボーナスの穴に入ると、フィールドにメダルが大量に出現
//  - 玉がどこにも入らなければ、フィールドにメダルが1枚出現
public class GameManager : MonoBehaviour
{
    public PlayerController player;
    public List<Collectible> items = new List<Collectible>();   // 最初から置いてあるメダル
    public MedalField field;                                     // メダルの追加出現(省略可)
    public Text countText;
    public Text clearText;
    public Text timeText;
    public Text timeUpText;
    public float timeLimit = 30f;
    public int goal = 5;                  // 得点の穴に入れる回数(CLEARの条件)
    public int bonusMedals = 8;           // ボーナスの穴で出るメダルの数
    public int missMedals = 1;            // はずれで出るメダルの数

    // 演出用イベント(ロジックは演出を知らない)
    public event System.Action<Collectible> ItemCollected;   // メダルを拾った(=玉を発射)
    public event System.Action ScoreAwarded;                 // 得点の穴に入った(アイテム獲得)
    public event System.Action BonusTriggered;               // ボーナスの穴に入った
    public event System.Action MissTriggered;                // どの穴にも入らなかった
    public event System.Action GameCleared;
    public event System.Action TimeUpReached;
    public event System.Action GameReset;

    public int CollectedCount { get; private set; }          // 得点の穴に入れた回数
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
        if (!IsPlaying) return;           // クリア後・時間切れ後は時計を進めない
        RemainingTime -= dt;
        if (RemainingTime <= 0f)
        {
            RemainingTime = 0f;
            IsTimeUp = true;
            TimeUpReached?.Invoke();
        }
        UpdateUI();
    }

    // メダルを拾った。カウントは増えず、玉が発射されるだけ。
    public void Collect(Collectible item)
    {
        if (item == null || item.Collected) return;
        if (!IsPlaying) return;              // 時間切れ後・クリア後は拾えない
        item.MarkCollected();
        ItemCollected?.Invoke(item);
    }

    // 玉の結果を受け取る(パチンコ台から呼ばれる)
    public void ReportBall(BallResult result)
    {
        if (!IsPlaying) return;              // クリア後・時間切れ後に穴へ入っても何も起きない
        switch (result)
        {
            case BallResult.Score:
                AwardItem();
                break;
            case BallResult.Bonus:
                if (field != null) field.Spawn(bonusMedals);
                BonusTriggered?.Invoke();
                break;
            default:
                if (field != null) field.Spawn(missMedals);
                MissTriggered?.Invoke();
                break;
        }
    }

    void AwardItem()
    {
        CollectedCount++;
        // 5回目の得点と時間切れが同じ更新で重なった場合は得点を優先
        // (物理の判定はUpdateより先に走るため、Tickで時間切れになる前にここへ来る)
        if (CollectedCount >= goal) IsCleared = true;
        UpdateUI();
        ScoreAwarded?.Invoke();
        if (IsCleared) GameCleared?.Invoke();
    }

    public void ResetGame()
    {
        CollectedCount = 0;
        IsCleared = false;
        IsTimeUp = false;
        RemainingTime = timeLimit;
        shownCount = -1; shownSec = -1;      // リセット時は必ず再描画
        if (field != null) field.ClearSpawned();   // 追加で出たメダルは消す
        foreach (var i in items) i.ResetItem();    // 最初のメダルは元の位置へ
        if (player != null) player.ResetPlayer();
        UpdateUI();
        GameReset?.Invoke();
    }

    int shownCount = -1, shownSec = -1;

    void UpdateUI()
    {
        // 毎フレーム呼ばれるため、値が変わった時だけ文字列を作る(GC削減)
        if (countText != null && shownCount != CollectedCount)
        {
            shownCount = CollectedCount;
            countText.text = $"{CollectedCount}/{goal}";
        }
        if (clearText != null) clearText.gameObject.SetActive(IsCleared);
        int sec = Mathf.CeilToInt(RemainingTime);
        if (timeText != null && shownSec != sec)
        {
            shownSec = sec;
            timeText.text = $"TIME {sec}";
        }
        if (timeUpText != null) timeUpText.gameObject.SetActive(IsTimeUp && !IsCleared);
    }
}
