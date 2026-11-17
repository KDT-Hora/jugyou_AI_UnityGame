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
        Object.DestroyImmediate(root);   // 次のテストのSetupと重ならないよう即時破棄
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
        gm.timeLimit = 30f;                          // 再開後の移動確認はタイムアップに左右されないよう長くする
        yield return Hold(Key.R, 0.2f);
        yield return null;
        Assert.IsFalse(gm.IsTimeUp);
        Assert.Greater(gm.RemainingTime, 20f);
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
    public IEnumerator Clear_StopsTheClock_InRealTime()
    {
        yield return null;
        yield return Hold(Key.W, 2.8f);
        Assert.IsTrue(gm.IsCleared);
        float t = gm.RemainingTime;
        yield return new WaitForSeconds(0.6f);
        Assert.AreEqual(t, gm.RemainingTime, 0.001f);
        Assert.IsFalse(gm.IsTimeUp);
    }

    // ---- パチンコ台 ----

    // gmを先に渡してから有効化する(OnEnableでイベント購読するため)
    PachinkoBoard MakeBoard()
    {
        var go = new GameObject("Board");
        go.SetActive(false);
        go.transform.SetParent(root.transform);
        var b = go.AddComponent<PachinkoBoard>();
        b.gm = gm;
        go.SetActive(true);
        return b;
    }

    [UnityTest]
    public IEnumerator Pachinko_CollectLaunchesBall_ThatFallsThroughPinsAndLeaves()
    {
        var board = MakeBoard();
        yield return null;
        gm.Collect(gm.items[0]);
        Assert.AreEqual(1, board.ActiveBallCount, "拾うと玉が1つ発射される");
        yield return new WaitForSeconds(1.2f);
        var ball = board.GetComponentInChildren<PachinkoBall>();
        Assert.IsNotNull(ball);
        Assert.IsFalse(ball.body.isKinematic, "盤に着いたら物理で動く");
        Assert.AreEqual(PachinkoBoard.Z, ball.transform.position.z, 0.01f, "盤の上(z固定)で動く");
        float timeout = Time.time + 35f;
        while (board.ActiveBallCount > 0 && Time.time < timeout) yield return null;
        Assert.AreEqual(0, board.ActiveBallCount, "ピンに引っかかり続けず、最後はアウト口へ落ちる");
    }

    [UnityTest]
    public IEnumerator Pachinko_ResetClearsAllBalls()
    {
        var board = MakeBoard();
        yield return null;
        gm.Collect(gm.items[0]);
        gm.Collect(gm.items[1]);
        Assert.AreEqual(2, board.ActiveBallCount);
        gm.ResetGame();
        Assert.AreEqual(0, board.ActiveBallCount);
        yield return new WaitForSeconds(1.0f);          // 飛行中のコルーチンも止まっている(玉が復活しない)
        Assert.AreEqual(0, board.ActiveBallCount);
    }

    [UnityTest]
    public IEnumerator Pachinko_BoardDoesNotInterfereWithStagePhysics()
    {
        var board = MakeBoard();
        yield return null;
        Assert.IsTrue(Physics.GetIgnoreLayerCollision(PachinkoBoard.Layer, 0), "盤はステージ(Default)と衝突しない");
        float z0 = pc.transform.position.z;
        yield return Hold(Key.W, 0.5f);
        Assert.Greater(pc.transform.position.z, z0 + 1f, "盤があってもプレイヤーは普通に動ける");
    }

    [UnityTest]
    public IEnumerator Pachinko_BallDroppedAboveHole_TriggersHoleEvent()
    {
        var board = MakeBoard();
        yield return null;
        int hit = -1;
        board.HoleEntered += i => hit = i;
        board.DropBallAt(new Vector3(0f, 1.95f, PachinkoBoard.Z));      // 中央の穴の真上
        float timeout = Time.time + 3f;
        while (hit < 0 && Time.time < timeout) yield return null;
        Assert.AreEqual(1, hit, "中央の穴に入ったらイベントが出る");
        Assert.AreEqual(1, board.HoleHits);
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
