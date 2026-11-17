using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

// 実際に物理とInput Systemを動かすテスト(仮想キーボード使用)
public class GameplayTests : InputTestFixture
{
    GameObject root;
    GameManager gm;
    PlayerController pc;
    Keyboard kb;

    public override void Setup()
    {
        base.Setup();
        kb = InputSystem.AddDevice<Keyboard>();

        root = new GameObject("Root");
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.SetParent(root.transform);
        floor.transform.position = new Vector3(0, -0.1f, 0);
        floor.transform.localScale = new Vector3(20, 0.2f, 20);

        var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        p.transform.SetParent(root.transform);
        p.transform.position = new Vector3(0, 0.5f, -7);
        p.AddComponent<Rigidbody>();
        pc = p.AddComponent<PlayerController>();

        var g = new GameObject("GM");
        g.transform.SetParent(root.transform);
        gm = g.AddComponent<GameManager>();
        gm.player = pc;
        pc.manager = gm;
        var c = new GameObject("C");
        c.transform.SetParent(root.transform);
        gm.countText = c.AddComponent<Text>();
        gm.clearText = new GameObject("CL").AddComponent<Text>();
        gm.clearText.transform.SetParent(root.transform);

        foreach (var z in new[] { -4, -1, 2, 5, 8 })
        {
            var it = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            it.transform.SetParent(root.transform);
            it.transform.position = new Vector3(0, 0.5f, z);
            it.transform.localScale = Vector3.one * 0.6f;
            var col = it.GetComponent<SphereCollider>();
            col.isTrigger = true; col.radius = 0.9f;
            var ci = it.AddComponent<Collectible>();
            ci.manager = gm;
            gm.items.Add(ci);
        }
    }

    public override void TearDown()
    {
        Object.Destroy(root);
        base.TearDown();
    }

    IEnumerator Hold(Key key, float seconds)
    {
        Press(kb[key]);
        yield return new WaitForSeconds(seconds);
        Release(kb[key]);
        yield return null;
    }

    [UnityTest]
    public IEnumerator W_MovesPlayerAlongPlusZ()
    {
        yield return null;
        float z0 = pc.transform.position.z;
        yield return Hold(Key.W, 0.5f);
        Assert.Greater(pc.transform.position.z, z0 + 1f);
    }

    [UnityTest]
    public IEnumerator A_MovesPlayerAlongMinusX_D_PlusX()
    {
        yield return null;
        yield return Hold(Key.A, 0.4f);
        Assert.Less(pc.transform.position.x, -1f);
        yield return Hold(Key.D, 0.8f);
        Assert.Greater(pc.transform.position.x, 0f);
    }

    [UnityTest]
    public IEnumerator S_MovesPlayerAlongMinusZ()
    {
        yield return null;
        float z0 = pc.transform.position.z;
        yield return Hold(Key.S, 0.3f);
        Assert.Less(pc.transform.position.z, z0 - 0.5f);
    }

    [UnityTest]
    public IEnumerator HoldingW_CollectsAllFive_AndClears()
    {
        yield return null;
        yield return Hold(Key.W, 2.8f);
        Assert.AreEqual(5, gm.CollectedCount);
        Assert.IsTrue(gm.IsCleared);
    }

    [UnityTest]
    public IEnumerator R_ResetsMidGame()
    {
        yield return null;
        yield return Hold(Key.W, 1.0f);
        Assert.Greater(gm.CollectedCount, 0);
        yield return Hold(Key.R, 0.2f);
        yield return null;
        Assert.AreEqual(0, gm.CollectedCount);
        Assert.AreEqual(pc.startPosition.z, pc.transform.position.z, 0.05f);
    }

    [UnityTest]
    public IEnumerator TimeUp_StopsMovement_ButRStillWorks()
    {
        yield return null;
        gm.timeLimit = 1f;
        gm.ResetGame();
        yield return new WaitForSeconds(1.4f);       // 操作せず時間切れ
        Assert.IsTrue(gm.IsTimeUp);
        float z = pc.transform.position.z;
        yield return Hold(Key.W, 0.5f);              // 時間切れ後は動けない
        Assert.AreEqual(z, pc.transform.position.z, 0.05f);
        yield return Hold(Key.R, 0.2f);
        yield return null;
        Assert.IsFalse(gm.IsTimeUp);
        Assert.AreEqual(1f, gm.RemainingTime, 0.3f);
        float z1 = pc.transform.position.z;
        yield return Hold(Key.W, 0.3f);              // 再開後は動ける
        Assert.Greater(pc.transform.position.z, z1 + 0.5f);
    }

    [UnityTest]
    public IEnumerator AfterClear_InputIsIgnored_ClearStaysUntilR()
    {
        yield return null;
        yield return Hold(Key.W, 2.8f);
        Assert.IsTrue(gm.IsCleared);
        yield return new WaitForSeconds(0.3f);            // 慣性で動いていないことを確認する基準点
        var p0 = pc.transform.position;
        yield return Hold(Key.W, 1.5f);                   // 本来なら奥の端から落ちる長さ
        yield return Hold(Key.A, 1.0f);
        yield return Hold(Key.S, 1.0f);
        Assert.IsTrue(gm.IsCleared, "クリアが維持される");
        Assert.AreEqual(p0.x, pc.transform.position.x, 0.05f);
        Assert.AreEqual(p0.z, pc.transform.position.z, 0.05f);
        yield return Hold(Key.R, 0.2f);
        yield return null;
        Assert.IsFalse(gm.IsCleared);
        Assert.AreEqual(0, gm.CollectedCount);
    }

    [UnityTest]
    public IEnumerator TimerCountsDown_InRealTime()
    {
        yield return null;
        gm.ResetGame();
        yield return new WaitForSeconds(1.0f);
        Assert.That(gm.RemainingTime, Is.InRange(28.5f, 29.3f));
    }

    [UnityTest]
    public IEnumerator FallingOffTheFloor_ResetsGame()
    {
        yield return null;
        yield return Hold(Key.W, 0.8f);
        yield return Hold(Key.D, 2.5f);
        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(0, gm.CollectedCount);
        Assert.AreEqual(pc.startPosition.z, pc.transform.position.z, 0.1f);
    }
}
