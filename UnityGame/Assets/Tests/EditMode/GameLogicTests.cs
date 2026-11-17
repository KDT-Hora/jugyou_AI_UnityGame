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

    [Test]
    public void Collect_Null_DoesNothing()
    {
        gm.Collect(null);
        Assert.AreEqual(0, gm.CollectedCount);
    }
}
