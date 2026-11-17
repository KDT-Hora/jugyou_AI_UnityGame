using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 玉が出入りする場所の目印(ボールが触れたら反応する)
public class PachinkoZone : MonoBehaviour
{
    public bool isOut;       // true: アウト口(玉を消す) / false: 穴
    public int holeIndex;
}

// パチンコの玉。盤の上だけで動く(z固定)。穴・アウト口に触れたら盤に知らせる。
public class PachinkoBall : MonoBehaviour
{
    public PachinkoBoard board;
    public Rigidbody body;
    public Collider col;
    public bool scored;      // 1球につき1回だけ穴に反応
    public float age;

    float stillT;

    void Update()
    {
        age += Time.deltaTime;
        if (age > PachinkoBoard.MaxBallLife || transform.position.y < -3f) { board.RemoveBall(this); return; }

        // ピンの間に挟まって止まったら、ちょんと揺らして落とす(実機の台でいう「釘調整」の代わり)
        if (col != null && col.enabled && !body.isKinematic && body.linearVelocity.sqrMagnitude < 0.01f)
        {
            stillT += Time.deltaTime;
            if (stillT > 1.2f)
            {
                stillT = 0f;
                body.AddForce(new Vector3(Random.Range(-2f, 2f), 2.5f, 0f), ForceMode.Impulse);
            }
        }
        else stillT = 0f;
    }

    void OnTriggerEnter(Collider other)
    {
        var zone = other.GetComponent<PachinkoZone>();
        if (zone == null) return;
        if (zone.isOut) board.RemoveBall(this);
        else if (!scored) { scored = true; board.OnHole(zone.holeIndex, transform.position); }
    }
}

// ステージの後ろに立つパチンコ台。
// コインを拾うと玉がコインの位置から盤の上へ発射され、ピンの間を物理演算で落ちる。
// ゲームロジックには触れず、GameManagerのイベントを聞くだけ。
public class PachinkoBoard : MonoBehaviour
{
    public const int Layer = 8;                   // 盤専用。プレイヤー等(Default)とは衝突しない
    public const float Z = 11.5f;                 // 盤の奥行き位置(ステージ奥端のさらに後ろ)
    public const float MaxBallLife = 40f;
    public static readonly float[] HoleX = { -3.6f, 0f, 3.6f };
    const float HoleY = 1.35f;

    public GameManager gm;
    public Material pinMat, ballMat, bladeMat, panelMat, trailMat, lampOn, lampOff, lampRed;

    public event System.Action<int> HoleEntered;
    public int HoleHits { get; private set; }
    public int ActiveBallCount => balls.Count;

    readonly List<PachinkoBall> balls = new List<PachinkoBall>();
    readonly List<Rigidbody> blades = new List<Rigidbody>();
    readonly List<float> bladeSpeed = new List<float>();
    readonly List<float> bladeAngle = new List<float>();
    readonly List<Renderer> holeLamps = new List<Renderer>();
    readonly float[] holeLampT = new float[3];
    readonly List<Renderer> frameBulbs = new List<Renderer>();
    readonly List<Material> frameMats = new List<Material>();
    PhysicsMaterial bouncy;
    ParticleSystem sparks;
    float launchPulseT;

    void Awake()
    {
        Physics.IgnoreLayerCollision(Layer, 0);   // 盤の玉はステージのものに当たらない
        bouncy = new PhysicsMaterial("PachinkoBouncy")
        {
            bounciness = 0.45f, dynamicFriction = 0.08f, staticFriction = 0.08f,
            bounceCombine = PhysicsMaterialCombine.Maximum, frictionCombine = PhysicsMaterialCombine.Minimum
        };
        BuildPanel();
        BuildPins();
        BuildWindmills();
        BuildHoles();
        BuildWalls();
        BuildFrame();
        MakeSparks();
    }

    void OnEnable()
    {
        if (gm == null) return;
        gm.ItemCollected += OnCollected;
        gm.GameReset += ClearBalls;
    }

    void OnDisable()
    {
        if (gm == null) return;
        gm.ItemCollected -= OnCollected;
        gm.GameReset -= ClearBalls;
    }

    // ---------- ゲームとの接点 ----------
    void OnCollected(Collectible item) => LaunchBall(item.transform.position);

    public void ClearBalls()
    {
        StopAllCoroutines();
        foreach (var b in balls) if (b != null) Destroy(b.gameObject);
        balls.Clear();
        for (int i = 0; i < holeLampT.Length; i++) holeLampT[i] = 0f;
        launchPulseT = 0f;
    }

    public void RemoveBall(PachinkoBall b)
    {
        if (b == null || !balls.Remove(b)) return;
        Destroy(b.gameObject);
    }

    public void OnHole(int index, Vector3 pos)
    {
        HoleHits++;
        holeLampT[index] = 0.9f;
        for (int i = 0; i < 24; i++)
        {
            var ep = new ParticleSystem.EmitParams();
            ep.position = pos;
            ep.velocity = Random.onUnitSphere * Random.Range(2f, 6f);
            ep.startColor = Color.Lerp(new Color(1f, 0.8f, 0.2f), Color.white, Random.value * 0.5f);
            ep.startSize = Random.Range(0.15f, 0.3f);
            ep.startLifetime = Random.Range(0.4f, 0.8f);
            sparks.Emit(ep, 1);
        }
        HoleEntered?.Invoke(index);
    }

    // コインの位置から玉を発射する(放物線で盤の上部へ飛び、そこから物理で落下)
    public PachinkoBall LaunchBall(Vector3 from)
    {
        var ball = CreateBall(from);
        StartCoroutine(Fly(ball, from));
        return ball;
    }

    // 盤の上に直接玉を置いて落とす(テスト用/拡張用)
    public PachinkoBall DropBallAt(Vector3 pos)
    {
        var ball = CreateBall(pos);
        Arm(ball, Vector3.zero);
        return ball;
    }

    IEnumerator Fly(PachinkoBall ball, Vector3 p0)
    {
        var p2 = new Vector3(Random.Range(-4f, 4f), 6.7f, Z);
        var p1 = (p0 + p2) * 0.5f + Vector3.up * 7f;
        float t = 0f;
        const float dur = 0.75f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            if (ball == null) yield break;
            float u = Mathf.Clamp01(t);
            ball.transform.position = (1 - u) * (1 - u) * p0 + 2 * (1 - u) * u * p1 + u * u * p2;
            yield return null;
        }
        if (ball == null) yield break;
        ball.transform.position = p2;
        Arm(ball, new Vector3(Random.Range(-1.5f, 1.5f), 0f, 0f));
        launchPulseT = 0.6f;
    }

    // 飛行が終わったら、当たり判定と物理を有効にする
    void Arm(PachinkoBall ball, Vector3 v)
    {
        var p = ball.transform.position; p.z = Z; ball.transform.position = p;
        ball.col.enabled = true;
        ball.body.isKinematic = false;
        ball.body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        ball.body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        ball.body.linearVelocity = v;
    }

    PachinkoBall CreateBall(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "PachinkoBall";
        go.layer = Layer;
        go.transform.SetParent(transform, true);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.5f;
        var r = go.GetComponent<Renderer>();
        if (ballMat != null) r.sharedMaterial = ballMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var col = go.GetComponent<SphereCollider>();
        col.sharedMaterial = bouncy;
        col.enabled = false;                         // 飛行中は無効
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        var tr = go.AddComponent<TrailRenderer>();
        tr.time = 0.35f;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(1f, 0f));
        tr.colorGradient = Juice.GoldGradient();
        if (trailMat != null) tr.sharedMaterial = trailMat;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var ball = go.AddComponent<PachinkoBall>();
        ball.board = this; ball.body = rb; ball.col = col;
        balls.Add(ball);
        return ball;
    }

    // ---------- 毎フレーム ----------
    void FixedUpdate()
    {
        for (int i = 0; i < blades.Count; i++)
        {
            bladeAngle[i] += bladeSpeed[i] * Time.fixedDeltaTime;
            blades[i].MoveRotation(Quaternion.Euler(0f, 0f, bladeAngle[i]));   // 風車を回す
        }
    }

    void Update()
    {
        for (int i = 0; i < holeLamps.Count; i++)
        {
            holeLampT[i] = Mathf.Max(0f, holeLampT[i] - Time.deltaTime);
            var m = holeLampT[i] > 0f ? (((int)(Time.time * 14f) % 2 == 0) ? lampOn : lampRed) : lampOff;
            if (m != null && holeLamps[i].sharedMaterial != m) holeLamps[i].sharedMaterial = m;
        }
        UpdateFrameLights();
        if (launchPulseT > 0f) launchPulseT -= Time.deltaTime;
    }

    void UpdateFrameLights()
    {
        if (frameBulbs.Count == 0) return;
        bool cleared = gm != null && gm.IsCleared;
        bool active = launchPulseT > 0f || balls.Count > 0;
        int step = (int)(Time.time * (active ? 20f : 7f));    // 玉が動いている間は電飾が速く走る
        for (int i = 0; i < frameBulbs.Count; i++)
        {
            Material m;
            if (cleared) m = (int)(Time.time * 10f) % 2 == 0 ? lampOn : lampOff;
            else m = (i + step) % 3 == 0 ? lampOn : lampOff;
            if (m != null && frameMats[i] != m) { frameMats[i] = m; frameBulbs[i].sharedMaterial = m; }
        }
    }

    // ---------- 盤の組み立て ----------
    GameObject Prim(PrimitiveType t, string name, Vector3 pos, Vector3 scale, Material mat, bool keepCollider, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        go.layer = Layer;
        go.transform.SetParent(parent != null ? parent : transform, true);
        go.transform.position = pos;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        if (mat != null) r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var c = go.GetComponent<Collider>();
        if (!keepCollider) Destroy(c);
        else c.sharedMaterial = bouncy;
        return go;
    }

    void BuildPanel()
    {
        var q = Prim(PrimitiveType.Quad, "BoardPanel", new Vector3(0f, 3.9f, Z + 0.45f), new Vector3(13.4f, 7.7f, 1f), panelMat, false);
        q.transform.rotation = Quaternion.identity;   // 手前(-Z側=カメラ側)を向く
    }

    void BuildPins()
    {
        var windCenters = new[] { new Vector2(-3.4f, 4.2f), new Vector2(3.4f, 5.4f) };
        int row = 0;
        for (float y = 2.2f; y < 6.8f; y += 0.72f, row++)
        {
            for (float x = -5.5f + (row % 2) * 0.5f; x <= 5.6f; x += 1f)
            {
                bool nearWind = false;
                foreach (var w in windCenters) if (Vector2.Distance(w, new Vector2(x, y)) < 1.7f) nearWind = true;
                if (nearWind) continue;
                // 穴の真上は漏斗状に空けておく(ここにくぎがあると玉が案内くぎとの間に挟まって止まる)
                bool nearHole = false;
                foreach (var hx in HoleX) if (y < 3.0f && Mathf.Abs(x - hx) < 1.6f) nearHole = true;
                if (nearHole) continue;
                Prim(PrimitiveType.Sphere, "Pin", new Vector3(x, y, Z), Vector3.one * 0.26f, pinMat, true);
            }
        }
        // 穴の両脇のくぎ(玉を穴へ導く)
        foreach (var hx in HoleX)
        {
            Prim(PrimitiveType.Sphere, "GuidePin", new Vector3(hx - 0.7f, 1.9f, Z), Vector3.one * 0.28f, pinMat, true);
            Prim(PrimitiveType.Sphere, "GuidePin", new Vector3(hx + 0.7f, 1.9f, Z), Vector3.one * 0.28f, pinMat, true);
        }
    }

    void BuildWindmills()
    {
        AddWindmill(new Vector3(-3.4f, 4.2f, Z), 90f);
        AddWindmill(new Vector3(3.4f, 5.4f, Z), -120f);
    }

    void AddWindmill(Vector3 pos, float degPerSec)
    {
        var pivot = new GameObject("Windmill");
        pivot.layer = Layer;
        pivot.transform.SetParent(transform, true);
        pivot.transform.position = pos;
        var rb = pivot.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        for (int k = 0; k < 3; k++)
        {
            var b = Prim(PrimitiveType.Cube, "Blade", pos, new Vector3(2.2f, 0.16f, 0.4f), bladeMat, true, pivot.transform);
            b.transform.localRotation = Quaternion.Euler(0f, 0f, k * 60f);
        }
        blades.Add(rb); bladeSpeed.Add(degPerSec); bladeAngle.Add(0f);
    }

    void BuildHoles()
    {
        for (int i = 0; i < HoleX.Length; i++)
        {
            var lamp = Prim(PrimitiveType.Sphere, "HoleLamp", new Vector3(HoleX[i], HoleY, Z + 0.15f), new Vector3(1.0f, 1.0f, 0.15f), lampOff, false);
            holeLamps.Add(lamp.GetComponent<Renderer>());
            var z = new GameObject("Hole" + i);
            z.layer = Layer;
            z.transform.SetParent(transform, true);
            z.transform.position = new Vector3(HoleX[i], HoleY, Z);
            var sc = z.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 0.55f;
            var zone = z.AddComponent<PachinkoZone>(); zone.holeIndex = i;
        }
        // アウト口(盤の底。ここに触れた玉は消える)
        var o = new GameObject("OutZone");
        o.layer = Layer;
        o.transform.SetParent(transform, true);
        o.transform.position = new Vector3(0f, 0.5f, Z);
        var bc = o.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(14f, 0.6f, 1.4f);
        o.AddComponent<PachinkoZone>().isOut = true;
    }

    void BuildWalls()
    {
        foreach (var sx in new[] { -6.5f, 6.5f })
        {
            var w = new GameObject("SideWall");
            w.layer = Layer;
            w.transform.SetParent(transform, true);
            w.transform.position = new Vector3(sx, 3.9f, Z);
            var bc = w.AddComponent<BoxCollider>(); bc.size = new Vector3(0.4f, 8f, 1.2f); bc.sharedMaterial = bouncy;
        }
        // 上の蓋(玉が飛び出さない)
        var top = new GameObject("TopWall");
        top.layer = Layer;
        top.transform.SetParent(transform, true);
        top.transform.position = new Vector3(0f, 7.7f, Z);
        var tc = top.AddComponent<BoxCollider>(); tc.size = new Vector3(13f, 0.4f, 1.2f); tc.sharedMaterial = bouncy;
    }

    void BuildFrame()
    {
        if (lampOn == null) return;
        // 縁の電飾
        var pts = new List<Vector3>();
        const float hx = 6.9f, y0 = 0.2f, y1 = 7.9f;
        for (float x = -hx; x < hx; x += 0.7f) pts.Add(new Vector3(x, y0, Z - 0.2f));
        for (float y = y0; y < y1; y += 0.7f) pts.Add(new Vector3(hx, y, Z - 0.2f));
        for (float x = hx; x > -hx; x -= 0.7f) pts.Add(new Vector3(x, y1, Z - 0.2f));
        for (float y = y1; y > y0; y -= 0.7f) pts.Add(new Vector3(-hx, y, Z - 0.2f));
        foreach (var p in pts)
        {
            var b = Prim(PrimitiveType.Sphere, "FrameBulb", p, Vector3.one * 0.3f, lampOff, false);
            frameBulbs.Add(b.GetComponent<Renderer>());
            frameMats.Add(lampOff);
        }
    }

    void MakeSparks()
    {
        var go = new GameObject("HoleSparks");
        go.transform.SetParent(transform, false);
        sparks = go.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sparks.main;
        main.playOnAwake = false; main.loop = false; main.gravityModifier = 0.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = sparks.emission; em.enabled = false;
        var sh = sparks.shape; sh.enabled = false;
        var sol = sparks.sizeOverLifetime; sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        if (trailMat != null) go.GetComponent<ParticleSystemRenderer>().sharedMaterial = trailMat;
    }
}
