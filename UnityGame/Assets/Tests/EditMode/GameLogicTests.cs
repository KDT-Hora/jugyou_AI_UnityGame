using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

// ルール:
//  メダルを拾う → 玉が発射されるだけ(カウントは増えない)
//  玉が得点の穴 → +1 (5回でCLEAR) / ボーナスの穴 → メダルが大量に出る / どこにも入らない → メダルが1枚出る
public class GameLogicTests
{
    GameObject root;
    GameManager gm;
    PlayerController pc;
    MedalField field;
    Text countText, clearText;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("TestRoot");
        var p = new GameObject("Player");
        p.transform.SetParent(root.transform);
        p.AddComponent<Rigidbody>();
        pc = p.AddComponent<PlayerController>();

        var g = new GameObject("GM");
        g.transform.SetParent(root.transform);
        gm = g.AddComponent<GameManager>();
        gm.player = pc;
        pc.manager = gm;

        countText = new GameObject("Count").AddComponent<Text>();
        clearText = new GameObject("Clear").AddComponent<Text>();
        countText.transform.SetParent(root.transform);
        clearText.transform.SetParent(root.transform);
        gm.countText = countText;
        gm.clearText = clearText;
        gm.timeText = new GameObject("Time").AddComponent<Text>();
        gm.timeUpText = new GameObject("TimeUp").AddComponent<Text>();
        gm.timeText.transform.SetParent(root.transform);
        gm.timeUpText.transform.SetParent(root.transform);

        for (int i = 0; i < 5; i++)
        {
            var it = new GameObject("Item" + i);
            it.transform.SetParent(root.transform);
            var c = it.AddComponent<Collectible>();
            c.manager = gm;
            gm.items.Add(c);
        }

        // 追加メダルの複製元(非アクティブ)
        var tmplGo = new GameObject("MedalTemplate");
        tmplGo.transform.SetParent(root.transform);
        var tmpl = tmplGo.AddComponent<Collectible>();
        tmplGo.SetActive(false);
        field = new GameObject("Field").AddComponent<MedalField>();
        field.transform.SetParent(root.transform);
        field.gm = gm;
        field.template = tmpl;
        gm.field = field;

        gm.ResetGame();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    void ScoreTimes(int n) { for (int i = 0; i < n; i++) gm.ReportBall(BallResult.Score); }

    // ---- 初期状態 ----

    [Test]
    public void InitialState_IsZeroOfFive_NotCleared_FiveMedalsOnField()
    {
        Assert.AreEqual(0, gm.CollectedCount);
        Assert.IsFalse(gm.IsCleared);
        Assert.AreEqual("0/5", countText.text);
        Assert.IsFalse(clearText.gameObject.activeSelf);
        Assert.AreEqual(5, field.AliveCount);
    }

    // ---- メダルを拾う: 玉が出るだけ ----

    [Test]
    public void PickingAMedal_DoesNotCount_ButLaunchesABall()
    {
        int launched = 0;
        gm.ItemCollected += _ => launched++;
        gm.Collect(gm.items[0]);
        Assert.AreEqual(0, gm.CollectedCount, "拾っただけではカウントされない");
        Assert.AreEqual("0/5", countText.text);
        Assert.IsTrue(gm.items[0].Collected);
        Assert.IsFalse(gm.items[0].gameObject.activeSelf, "メダルは消える");
        Assert.AreEqual(1, launched, "玉が1つ発射される");
    }

    [Test]
    public void PickingSameMedalTwice_LaunchesOnlyOneBall()
    {
        int launched = 0;
        gm.ItemCollected += _ => launched++;
        gm.Collect(gm.items[0]);
        gm.Collect(gm.items[0]);
        Assert.AreEqual(1, launched);
    }

    [Test]
    public void PickingAllFiveMedals_DoesNotClear()
    {
        foreach (var i in gm.items) gm.Collect(i);
        Assert.AreEqual(0, gm.CollectedCount);
        Assert.IsFalse(gm.IsCleared);
        Assert.AreEqual(0, field.AliveCount);
    }

    // ---- 得点の穴 ----

    [Test]
    public void ScoreHole_AddsOne_UpdatesText()
    {
        gm.ReportBall(BallResult.Score);
        Assert.AreEqual(1, gm.CollectedCount);
        Assert.AreEqual("1/5", countText.text);
    }

    [Test]
    public void FourScores_NotCleared_FifthScore_Cleared()
    {
        ScoreTimes(4);
        Assert.IsFalse(gm.IsCleared);
        Assert.IsFalse(clearText.gameObject.activeSelf);
        gm.ReportBall(BallResult.Score);
        Assert.IsTrue(gm.IsCleared);
        Assert.IsTrue(clearText.gameObject.activeSelf);
        Assert.AreEqual(5, gm.CollectedCount);
    }

    [Test]
    public void ScoreEvent_FiresOncePerScore_ClearEventOnFifth()
    {
        int scored = 0, cleared = 0;
        gm.ScoreAwarded += () => scored++;
        gm.GameCleared += () => cleared++;
        ScoreTimes(5);
        Assert.AreEqual(5, scored);
        Assert.AreEqual(1, cleared);
    }

    // ---- ボーナスの穴 / はずれ ----

    [Test]
    public void BonusHole_SpawnsManyMedals_WithoutScoring()
    {
        gm.ReportBall(BallResult.Bonus);
        Assert.AreEqual(gm.bonusMedals, field.SpawnedCount);
        Assert.AreEqual(5 + gm.bonusMedals, field.AliveCount);
        Assert.AreEqual(0, gm.CollectedCount, "ボーナスの穴ではカウントは増えない");
        Assert.Greater(gm.bonusMedals, 1, "「大量」= 複数枚");
    }

    [Test]
    public void BonusMedals_AppearOnTheFloor_AwayFromThePlayer()
    {
        gm.ReportBall(BallResult.Bonus);
        var playerPos = pc.transform.position;
        foreach (var c in field.GetComponentsInChildren<Collectible>())
        {
            var p = c.transform.position;
            Assert.LessOrEqual(Mathf.Abs(p.x), field.half + 0.01f);
            Assert.LessOrEqual(Mathf.Abs(p.z), field.half + 0.01f);
            Assert.AreEqual(0.5f, p.y, 0.001f);
            Assert.IsTrue(c.gameObject.activeSelf && !c.Collected);
            Assert.GreaterOrEqual(Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerPos.x, playerPos.z)), field.minPlayerDistance - 0.01f);
        }
    }

    [Test]
    public void Miss_SpawnsExactlyOneMedal()
    {
        gm.ReportBall(BallResult.Miss);
        Assert.AreEqual(1, field.SpawnedCount);
        Assert.AreEqual(6, field.AliveCount);
        Assert.AreEqual(0, gm.CollectedCount);
    }

    [Test]
    public void SpawnedMedals_CanBePickedUpToo_AndLaunchBalls()
    {
        gm.ReportBall(BallResult.Miss);
        var spawned = field.GetComponentInChildren<Collectible>();
        int launched = 0;
        gm.ItemCollected += _ => launched++;
        gm.Collect(spawned);
        Assert.AreEqual(1, launched);
        Assert.IsTrue(spawned.Collected);
    }

    // ---- クリア後・時間切れ後は無視 ----

    [Test]
    public void AfterClear_BallResultsAndPickupsAreIgnored()
    {
        ScoreTimes(5);
        gm.ReportBall(BallResult.Bonus);
        gm.ReportBall(BallResult.Miss);
        gm.ReportBall(BallResult.Score);
        Assert.AreEqual(0, field.SpawnedCount);
        Assert.AreEqual(5, gm.CollectedCount);
        int launched = 0;
        gm.ItemCollected += _ => launched++;
        gm.Collect(gm.items[0]);
        Assert.AreEqual(0, launched);
    }

    [Test]
    public void AfterTimeUp_BallResultsAndPickupsAreIgnored()
    {
        gm.Tick(31f);
        gm.ReportBall(BallResult.Score);
        gm.ReportBall(BallResult.Bonus);
        Assert.AreEqual(0, gm.CollectedCount);
        Assert.AreEqual(0, field.SpawnedCount);
        gm.Collect(gm.items[0]);
        Assert.IsFalse(gm.items[0].Collected);
    }

    // ---- リセット ----

    [Test]
    public void Reset_RemovesSpawnedMedals_RestoresInitialMedals_ZeroesCount()
    {
        gm.Collect(gm.items[0]);
        gm.Collect(gm.items[1]);
        gm.ReportBall(BallResult.Bonus);
        ScoreTimes(2);
        gm.ResetGame();
        Assert.AreEqual(0, gm.CollectedCount);
        Assert.AreEqual("0/5", countText.text);
        Assert.AreEqual(0, field.SpawnedCount);
        Assert.AreEqual(5, field.AliveCount);
        Assert.IsTrue(gm.items.TrueForAll(i => i.gameObject.activeSelf && !i.Collected));
        Assert.AreEqual(pc.startPosition, pc.transform.position);
    }

    [Test]
    public void Reset_AfterClear_RestoresEverything()
    {
        ScoreTimes(5);
        gm.ResetGame();
        Assert.IsFalse(gm.IsCleared);
        Assert.IsFalse(clearText.gameObject.activeSelf);
        Assert.AreEqual("0/5", countText.text);
        Assert.IsTrue(gm.IsPlaying);
    }

    // ---- 行き詰まらない(詰みがない)こと ----

    // 5回得点するまで、フィールドのメダルが尽きて進めなくなることはない
    [Test]
    public void NeverRunsOutOfMedals_BeforeTheGoalIsReached()
    {
        for (int seed = 0; seed < 300; seed++)
        {
            Random.InitState(seed);
            gm.ResetGame();
            int guard = 0;
            while (!gm.IsCleared && guard++ < 500)
            {
                Collectible next = null;
                foreach (var i in gm.items) if (!i.Collected) { next = i; break; }
                if (next == null) foreach (var c in field.GetComponentsInChildren<Collectible>()) if (!c.Collected) { next = c; break; }
                Assert.IsNotNull(next, $"seed {seed}: ゴール前にメダルが尽きた(詰み) count={gm.CollectedCount}");
                gm.Collect(next);
                // 玉の行き先はランダム(得点・ボーナス・はずれのどれでも)
                gm.ReportBall((BallResult)Random.Range(0, 3));
            }
            Assert.IsTrue(gm.IsCleared, $"seed {seed}: クリアに到達しない");
        }
    }

    // ---- 30秒制限 ----

    [Test]
    public void A1_InitialTimeIs30_AndDecreasesWithTick()
    {
        Assert.AreEqual(30f, gm.RemainingTime, 0.001f);
        gm.Tick(10f);
        Assert.AreEqual(20f, gm.RemainingTime, 0.001f);
    }

    [Test]
    public void A2_TimeUpAtZero_StopsPlaying_ShowsTimeUp()
    {
        gm.Tick(29f);
        Assert.IsFalse(gm.IsTimeUp);
        gm.Tick(1.5f);
        Assert.IsTrue(gm.IsTimeUp);
        Assert.AreEqual(0f, gm.RemainingTime);
        Assert.IsTrue(gm.timeUpText.gameObject.activeSelf);
        Assert.IsFalse(gm.IsPlaying);
    }

    [Test]
    public void A3_ClearStopsClock()
    {
        gm.Tick(5f);
        ScoreTimes(5);
        float t = gm.RemainingTime;
        gm.Tick(3f);
        Assert.AreEqual(t, gm.RemainingTime, 0.0001f);
    }

    [Test]
    public void A4_AfterClear_NeverBecomesTimeUp()
    {
        ScoreTimes(5);
        gm.Tick(100f);
        Assert.IsTrue(gm.IsCleared);
        Assert.IsFalse(gm.IsTimeUp);
        Assert.IsFalse(gm.timeUpText.gameObject.activeSelf);
    }

    [Test]
    public void A5_ResetRestoresTimeTo30_AndClearsTimeUp()
    {
        gm.Tick(31f);
        gm.ResetGame();
        Assert.AreEqual(30f, gm.RemainingTime, 0.001f);
        Assert.IsFalse(gm.IsTimeUp);
        Assert.IsFalse(gm.timeUpText.gameObject.activeSelf);
        Assert.AreEqual("TIME 30", gm.timeText.text);
    }

    [Test]
    public void Priority_FifthScoreBeforeTimeUp_Clears()
    {
        ScoreTimes(4);
        gm.Tick(29.99f);
        gm.ReportBall(BallResult.Score);   // 物理の判定はUpdateより先
        gm.Tick(1f);
        Assert.IsTrue(gm.IsCleared);
        Assert.IsFalse(gm.IsTimeUp);
    }

    [Test]
    public void Collect_Null_DoesNothing()
    {
        gm.Collect(null);
        Assert.AreEqual(0, gm.CollectedCount);
    }
}
