using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class GameLogicTests
{
    GameObject root;
    GameManager gm;
    PlayerController pc;
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
        gm.ResetGame();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    [Test]
    public void InitialState_IsZeroOfFive_NotCleared()
    {
        Assert.AreEqual(0, gm.CollectedCount);
        Assert.IsFalse(gm.IsCleared);
        Assert.AreEqual("0/5", countText.text);
        Assert.IsFalse(clearText.gameObject.activeSelf);
    }

    [Test]
    public void CollectSameItemTwice_CountsOnce()
    {
        gm.Collect(gm.items[0]);
        gm.Collect(gm.items[0]);
        Assert.AreEqual(1, gm.CollectedCount);
        Assert.AreEqual("1/5", countText.text);
    }

    [Test]
    public void CollectFour_NotCleared_CollectFive_Cleared()
    {
        for (int i = 0; i < 4; i++) gm.Collect(gm.items[i]);
        Assert.IsFalse(gm.IsCleared);
        Assert.IsFalse(clearText.gameObject.activeSelf);
        gm.Collect(gm.items[4]);
        Assert.IsTrue(gm.IsCleared);
        Assert.IsTrue(clearText.gameObject.activeSelf);
    }

    [Test]
    public void ResetGame_RestoresEverything()
    {
        for (int i = 0; i < 5; i++) gm.Collect(gm.items[i]);
        pc.transform.position = new Vector3(3, 2, 4);
        gm.ResetGame();
        Assert.AreEqual(0, gm.CollectedCount);
        Assert.IsFalse(gm.IsCleared);
        Assert.IsFalse(clearText.gameObject.activeSelf);
        Assert.AreEqual("0/5", countText.text);
        Assert.IsTrue(gm.items.TrueForAll(i => i.gameObject.activeSelf && !i.Collected));
        Assert.AreEqual(pc.startPosition, pc.transform.position);
    }

    // ---- 30秒制限 (A1〜A5) ----

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
    public void A2_CollectAfterTimeUp_IsIgnored()
    {
        gm.Tick(31f);
        gm.Collect(gm.items[0]);
        Assert.AreEqual(0, gm.CollectedCount);
        Assert.IsFalse(gm.items[0].Collected);
    }

    [Test]
    public void A3_ClearStopsClock()
    {
        gm.Tick(5f);
        for (int i = 0; i < 5; i++) gm.Collect(gm.items[i]);
        float t = gm.RemainingTime;
        gm.Tick(3f);
        Assert.AreEqual(t, gm.RemainingTime, 0.0001f);
    }

    [Test]
    public void A4_AfterClear_NeverBecomesTimeUp()
    {
        for (int i = 0; i < 5; i++) gm.Collect(gm.items[i]);
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
    public void Priority_FifthCollectBeforeTimeUp_Clears()
    {
        gm.Tick(29.99f);
        for (int i = 0; i < 5; i++) gm.Collect(gm.items[i]);   // 物理判定はUpdateより先
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
