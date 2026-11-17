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
    MedalField field;
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
            var it = MakeMedal(new Vector3(0, 0.5f, z));
            gm.items.Add(it);
        }

        // 追加メダルの複製元(非アクティブ)
        var tmpl = MakeMedal(new Vector3(0, 0.5f, 0));
        tmpl.gameObject.SetActive(false);
        field = new GameObject("Field").AddComponent<MedalField>();
        field.transform.SetParent(root.transform);
        field.gm = gm;
        field.template = tmpl;
        gm.field = field;
    }

    Collectible MakeMedal(Vector3 pos)
    {
        var it = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        it.transform.SetParent(root.transform);
        it.transform.position = pos;
        it.transform.localScale = Vector3.one * 0.6f;
        var col = it.GetComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 0.9f;
        var ci = it.AddComponent<Collectible>();
        ci.manager = gm;
        return ci;
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

    void ScoreTimes(int n) { for (int i = 0; i < n; i++) gm.ReportBall(BallResult.Score); }

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

    // ---- 移動 ----

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

    // ---- メダルを拾う: 玉が出るだけでカウントは増えない ----

    [UnityTest]
    public IEnumerator WalkingIntoMedals_PicksThemUp_ButDoesNotCountOrClear()
    {
        yield return null;
        int launched = 0;
        gm.ItemCollected += _ => launched++;
        yield return Hold(Key.W, 2.8f);        // 5枚すべてを通り抜ける
        Assert.AreEqual(5, launched, "触れたメダルごとに玉が1つ出る");
        Assert.IsTrue(gm.items.TrueForAll(i => i.Collected));
        Assert.AreEqual(0, gm.CollectedCount, "拾っただけではカウントされない");
        Assert.IsFalse(gm.IsCleared, "5枚拾ってもクリアにならない");
    }

    // ---- 再開・落下 ----

    [UnityTest]
    public IEnumerator R_ResetsMidGame()
    {
        yield return null;
        ScoreTimes(2);
        yield return Hold(Key.W, 1.0f);
        Assert.AreEqual(2, gm.CollectedCount);
        yield return Hold(Key.R, 0.2f);
        yield return null;
        Assert.AreEqual(0, gm.CollectedCount);
        Assert.IsTrue(gm.items.TrueForAll(i => !i.Collected), "拾ったメダルも元に戻る");
        Assert.AreEqual(pc.startPosition.z, pc.transform.position.z, 0.05f);
    }

    [UnityTest]
    public IEnumerator FallingOffTheFloor_ResetsGame()
    {
        yield return null;
        ScoreTimes(1);
        yield return Hold(Key.W, 0.8f);
        yield return Hold(Key.D, 2.5f);
        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(0, gm.CollectedCount);
        Assert.AreEqual(pc.startPosition.z, pc.transform.position.z, 0.1f);
    }

    // ---- クリア後の入力停止・時計停止 ----

    [UnityTest]
    public IEnumerator AfterClear_InputIsIgnored_ClearStaysUntilR()
    {
        yield return null;
        ScoreTimes(5);
        Assert.IsTrue(gm.IsCleared);
        yield return new WaitForSeconds(0.3f);
        var p0 = pc.transform.position;
        yield return Hold(Key.W, 1.5f);
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
        ScoreTimes(5);
        float t = gm.RemainingTime;
        yield return new WaitForSeconds(0.6f);
        Assert.AreEqual(t, gm.RemainingTime, 0.001f);
        Assert.IsFalse(gm.IsTimeUp);
    }

    // ---- 時間制限 ----

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
    public IEnumerator TimerCountsDown_InRealTime()
    {
        yield return null;
        gm.ResetGame();
        yield return new WaitForSeconds(1.0f);
        Assert.That(gm.RemainingTime, Is.InRange(28.5f, 29.3f));
    }

    // ---- パチンコ台 ----

    [UnityTest]
    public IEnumerator Pachinko_PickingAMedalLaunchesABall_ThatFallsAndLeaves()
    {
        var board = MakeBoard();
        yield return null;
        gm.Collect(gm.items[0]);
        Assert.AreEqual(1, board.ActiveBallCount, "拾うと玉が1つ発射される");
        Assert.AreEqual(0, gm.CollectedCount, "この時点ではまだ獲得していない");
        yield return new WaitForSeconds(1.2f);
        var ball = board.GetComponentInChildren<PachinkoBall>();
        Assert.IsNotNull(ball);
        Assert.IsFalse(ball.body.isKinematic, "盤に着いたら物理で動く");
        Assert.AreEqual(PachinkoBoard.Z, ball.transform.position.z, 0.01f, "盤の上(z固定)で動く");
        float timeout = Time.time + 35f;
        while (board.ActiveBallCount > 0 && Time.time < timeout) yield return null;
        Assert.AreEqual(0, board.ActiveBallCount, "ピンに引っかかり続けず、最後は盤の外へ落ちる");
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

    // 穴の真上に玉を落として、結果がゲームに反映されることを確認する(穴の位置は PachinkoBoard.HoleX)
    [UnityTest]
    public IEnumerator Pachinko_BallInScoreHole_AwardsAnItem()
    {
        var board = MakeBoard();
        yield return null;
        board.DropBallAt(new Vector3(0f, 1.95f, PachinkoBoard.Z));      // 中央=得点の穴
        float timeout = Time.time + 4f;
        while (gm.CollectedCount < 1 && Time.time < timeout) yield return null;
        Assert.AreEqual(1, gm.CollectedCount, "得点の穴に入るとアイテム獲得(+1)");
        Assert.AreEqual(1, board.ScoreBalls);
        Assert.AreEqual(0, field.SpawnedCount, "メダルは増えない");
    }

    [UnityTest]
    public IEnumerator Pachinko_BallInBonusHole_SpawnsManyMedals()
    {
        var board = MakeBoard();
        yield return null;
        board.DropBallAt(new Vector3(PachinkoBoard.HoleX[0], 1.95f, PachinkoBoard.Z));   // 左=ボーナスの穴
        float timeout = Time.time + 4f;
        while (field.SpawnedCount == 0 && Time.time < timeout) yield return null;
        Assert.AreEqual(gm.bonusMedals, field.SpawnedCount, "ボーナスの穴でメダルが大量に出る");
        Assert.AreEqual(0, gm.CollectedCount, "カウントは増えない");
        Assert.AreEqual(1, board.BonusBalls);
    }

    [UnityTest]
    public IEnumerator Pachinko_BallThatMissesEveryHole_SpawnsExactlyOneMedal()
    {
        var board = MakeBoard();
        yield return null;
        board.DropBallAt(new Vector3(-3.5f, 1.6f, PachinkoBoard.Z));    // どの穴(x=-5.5 / 0 / 5.5)からも離れた位置
        float timeout = Time.time + 4f;
        while (board.MissBalls == 0 && Time.time < timeout) yield return null;
        Assert.AreEqual(1, board.MissBalls, "どの穴にも入らずアウト口へ");
        Assert.AreEqual(1, field.SpawnedCount, "メダルが1枚だけ出る");
        Assert.AreEqual(0, gm.CollectedCount);
    }

    [UnityTest]
    public IEnumerator Pachinko_BallThatEnteredAHole_IsNotAlsoCountedAsAMiss()
    {
        var board = MakeBoard();
        yield return null;
        var ball = board.DropBallAt(new Vector3(0f, 1.95f, PachinkoBoard.Z));   // 得点の穴 → そのままアウト口へ落ちる
        float timeout = Time.time + 5f;
        while (board.ActiveBallCount > 0 && Time.time < timeout) yield return null;
        Assert.AreEqual(0, board.ActiveBallCount);
        Assert.AreEqual(1, board.ScoreBalls);
        Assert.AreEqual(0, board.MissBalls, "穴に入った玉は、アウト口でもはずれ扱いにならない");
        Assert.AreEqual(0, field.SpawnedCount);
    }

    [UnityTest]
    public IEnumerator Pachinko_BallResultsAfterClear_DoNothing()
    {
        var board = MakeBoard();
        yield return null;
        ScoreTimes(5);
        board.DropBallAt(new Vector3(PachinkoBoard.HoleX[0], 1.95f, PachinkoBoard.Z));   // ボーナスの穴
        yield return new WaitForSeconds(2.0f);
        Assert.AreEqual(0, field.SpawnedCount, "クリア後はメダルが増えない");
        Assert.AreEqual(5, gm.CollectedCount);
    }
}
