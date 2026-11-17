using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 派手な演出担当。GameManagerのイベントと状態を読むだけで、ゲームロジックには触れない。
public class Juice : MonoBehaviour
{
    public GameManager gm;
    public PlayerController player;
    public Camera cam;
    public Material particleMat;
    public Text countText, timeText, clearText, timeUpText;
    public Image flash;                     // 全画面フラッシュ
    public Transform backdrop;              // 回転する放射状の背景
    public Material backdropMat;
    public Material bulbOn, bulbOff, bulbRed;
    public PachinkoBoard board;             // 玉が穴に入った時の演出用

    static readonly Color Gold = new Color(1f, 0.78f, 0.15f);

    ParticleSystem burst, confetti, coinRain;
    TrailRenderer trail;
    readonly List<Renderer> bulbs = new List<Renderer>();
    readonly List<Material> bulbMats = new List<Material>();
    Vector3 camBase;
    float fovBase;
    Vector2 timeUpBase;
    float shakeT, shakeDur = 0.3f, shakeMag;
    float countPunch, clearAge, secPunch, flashAlpha, strobeT, redT;
    int lastSec = -1;
    Color flashColor = Gold;

    void Awake()
    {
        burst = MakeParticles("Burst", 0.8f, 0.3f, 1.2f);
        confetti = MakeParticles("Confetti", 2.2f, 0.32f, 0.5f);
        coinRain = MakeParticles("CoinRain", 2.6f, 0.42f, 0.9f);
        MakeAmbient();
        MakeTrail();
        MakeBulbs();
        if (cam == null) cam = Camera.main;
    }

    void Start()
    {
        camBase = cam.transform.position;
        fovBase = cam.fieldOfView;
        if (timeUpText != null) timeUpBase = timeUpText.rectTransform.anchoredPosition;
    }

    void OnEnable()
    {
        if (gm != null)
        {
            gm.ItemCollected += OnCollected;
            gm.GameCleared += OnCleared;
            gm.TimeUpReached += OnTimeUp;
            gm.GameReset += OnReset;
        }
        if (board != null) board.HoleEntered += OnHole;
    }

    void OnDisable()
    {
        if (gm != null)
        {
            gm.ItemCollected -= OnCollected;
            gm.GameCleared -= OnCleared;
            gm.TimeUpReached -= OnTimeUp;
            gm.GameReset -= OnReset;
        }
        if (board != null) board.HoleEntered -= OnHole;
    }

    // ---------- イベント ----------
    void OnCollected(Collectible item)
    {
        Emit(burst, item.transform.position, 30, 6f, Gold);
        countPunch = 0.7f;
        Flash(Gold, 0.35f);
        Shake(0.12f, 0.2f);
    }

    void OnCleared()
    {
        clearAge = 0f;
        strobeT = 1.8f;
        Vector3 p = player != null ? player.transform.position : Vector3.zero;
        for (int k = 0; k < 3; k++)
            Emit(confetti, p + Vector3.up * 1.5f + Random.insideUnitSphere * 2f, 70, 9f, Color.white, true);
        // コインのシャワー(盤面全体に降る)
        for (int i = 0; i < 260; i++)
        {
            var ep = new ParticleSystem.EmitParams();
            ep.position = new Vector3(Random.Range(-10f, 10f), Random.Range(9f, 26f), Random.Range(-10f, 10f));
            ep.velocity = new Vector3(0f, -Random.Range(1f, 4f), 0f);
            ep.startColor = Color.Lerp(Gold, Color.white, Random.Range(0f, 0.4f));
            ep.startSize = Random.Range(0.3f, 0.55f);
            ep.startLifetime = Random.Range(2f, 3.2f);
            coinRain.Emit(ep, 1);
        }
        Shake(0.45f, 0.7f);
    }

    void OnTimeUp()
    {
        redT = 1.2f;
        Flash(new Color(1f, 0.1f, 0.1f), 0.5f);
        Shake(0.5f, 0.6f);
    }

    // 玉がパチンコの穴に入った
    void OnHole(int index)
    {
        Flash(new Color(1f, 0.9f, 0.4f), 0.3f);
        Shake(0.1f, 0.15f);
    }

    void OnReset()
    {
        if (trail != null) trail.Clear();
        // 前のラウンドの演出を持ち越さない
        burst.Clear(); confetti.Clear(); coinRain.Clear();
        clearAge = 0f; strobeT = 0f; redT = 0f;
        flashAlpha = 0f; shakeT = 0f; countPunch = 0f; secPunch = 0f;
        lastSec = -1;
        if (flash != null) { var c = flash.color; c.a = 0f; flash.color = c; }
    }

    void Flash(Color c, float a) { flashColor = c; flashAlpha = Mathf.Max(flashAlpha, a); }

    // ---------- 毎フレーム ----------
    void Update()
    {
        float dt = Time.deltaTime;
        countPunch = Mathf.Lerp(countPunch, 0f, 10f * dt);
        if (countText != null) countText.rectTransform.localScale = Vector3.one * (1f + countPunch);

        UpdateTimeText(dt);
        UpdateClearText(dt);
        UpdateBackdrop();
        UpdateBulbs();
        UpdateFlash(dt);

        if (timeUpText != null && timeUpText.gameObject.activeSelf)
            timeUpText.rectTransform.anchoredPosition = timeUpBase + Random.insideUnitCircle * 10f;
    }

    void LateUpdate()
    {
        if (cam == null) return;
        if (shakeT > 0f)
        {
            shakeT -= Time.deltaTime;
            float k = Mathf.Clamp01(shakeT / shakeDur);
            cam.transform.position = camBase + (Vector3)(Random.insideUnitCircle * shakeMag * k);
        }
        else cam.transform.position = camBase;
        cam.fieldOfView = fovBase - 5f * countPunch;      // 回収でズームイン
    }

    void UpdateTimeText(float dt)
    {
        if (timeText == null || gm == null) return;
        int sec = Mathf.CeilToInt(gm.RemainingTime);
        bool danger = gm.IsPlaying && gm.RemainingTime <= 10f;
        if (danger && sec != lastSec) secPunch = 0.35f;
        lastSec = sec;
        secPunch = Mathf.Lerp(secPunch, 0f, 9f * dt);
        timeText.rectTransform.localScale = Vector3.one * (1f + (danger ? secPunch : 0f));
        timeText.color = danger
            ? Color.Lerp(Color.white, new Color(1f, 0.15f, 0.2f), 0.5f + 0.5f * Mathf.Sin(Time.time * 12f))
            : Color.white;
    }

    void UpdateClearText(float dt)
    {
        if (clearText == null || !clearText.gameObject.activeSelf) return;
        clearAge += dt;
        float s = EaseOutBack(Mathf.Clamp01(clearAge / 0.45f)) * (1f + 0.06f * Mathf.Sin(Time.time * 9f));
        clearText.rectTransform.localScale = Vector3.one * s;
        clearText.color = Color.HSVToRGB(Mathf.Repeat(Time.time * 1.2f, 1f), 0.7f, 1f);
    }

    void UpdateBackdrop()
    {
        if (backdrop == null || cam == null) return;
        var f = cam.transform.forward;
        backdrop.position = camBase + f * 90f;
        backdrop.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, Time.time * 9f);
        if (backdropMat != null)
        {
            // ゆっくり色が回る。クリアで金/ピンク点滅、時間切れで赤。
            Color c = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.07f, 1f), 0.85f, 0.85f);
            if (gm != null && gm.IsCleared) c = Mathf.Repeat(Time.time * 10f, 1f) < 0.5f ? Gold : new Color(1f, 0.3f, 0.6f);
            else if (redT > 0f) c = new Color(0.9f, 0.05f, 0.05f);
            backdropMat.SetColor("_BaseColor", c);
        }
    }

    void UpdateBulbs()
    {
        if (bulbs.Count == 0) return;
        bool cleared = gm != null && gm.IsCleared;
        bool timeUp = gm != null && gm.IsTimeUp && !cleared;
        if (redT > 0f) redT -= Time.deltaTime;
        int step = (int)(Time.time * 14f);
        for (int i = 0; i < bulbs.Count; i++)
        {
            Material m;
            if (cleared) m = (int)(Time.time * 10f) % 2 == 0 ? bulbOn : bulbOff;           // 全点滅
            else if (timeUp) m = (int)(Time.time * 3f) % 2 == 0 ? bulbRed : bulbOff;       // 赤くゆっくり
            else m = (i + step) % 4 == 0 ? bulbOn : bulbOff;                               // 電飾が走る
            if (bulbMats[i] != m) { bulbMats[i] = m; bulbs[i].sharedMaterial = m; }       // 変化した時だけ代入
        }
    }

    void UpdateFlash(float dt)
    {
        if (flash == null) return;
        float a = flashAlpha;
        Color c = flashColor;
        if (strobeT > 0f)
        {
            strobeT -= dt;
            bool on = Mathf.Repeat(Time.time * 9f, 1f) < 0.5f;
            c = Color.HSVToRGB(Mathf.Repeat(Time.time * 2f, 1f), 0.6f, 1f);
            a = Mathf.Max(a, on ? 0.35f : 0f);
        }
        flashAlpha = Mathf.Lerp(flashAlpha, 0f, 8f * dt);
        c.a = a;
        flash.color = c;
    }

    void Shake(float mag, float dur) { shakeMag = mag; shakeDur = dur; shakeT = dur; }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
    }

    // ---------- 生成 ----------
    void MakeBulbs()
    {
        if (bulbOn == null || bulbOff == null) return;
        var pts = new List<Vector3>();
        const float h = 10.6f;
        for (float x = -h; x < h; x += 1f) pts.Add(new Vector3(x, 0.15f, -h));
        for (float z = -h; z < h; z += 1f) pts.Add(new Vector3(h, 0.15f, z));
        for (float x = h; x > -h; x -= 1f) pts.Add(new Vector3(x, 0.15f, h));
        for (float z = h; z > -h; z -= 1f) pts.Add(new Vector3(-h, 0.15f, z));
        var root = new GameObject("Marquee").transform;
        root.SetParent(transform, false);
        foreach (var p in pts)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(root, false);
            b.transform.position = p;
            b.transform.localScale = Vector3.one * 0.38f;
            var r = b.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.sharedMaterial = bulbOff;
            bulbs.Add(r);
            bulbMats.Add(bulbOff);
        }
    }

    ParticleSystem MakeParticles(string name, float life, float size, float gravity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = life;
        main.startSize = size;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 2000;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var sol = ps.sizeOverLifetime; sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMat;
        return ps;
    }

    void Emit(ParticleSystem ps, Vector3 pos, int count, float speed, Color color, bool rainbow = false)
    {
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            ep.position = pos;
            ep.velocity = Random.onUnitSphere * speed * Random.Range(0.4f, 1f);
            if (rainbow) ep.velocity = new Vector3(ep.velocity.x, Mathf.Abs(ep.velocity.y) + 3f, ep.velocity.z);
            ep.startColor = rainbow ? (Color)Color.HSVToRGB(Random.value, 0.7f, 1f) : color;
            ep.startSize = ps.main.startSize.constant * Random.Range(0.7f, 1.4f);
            ep.startLifetime = ps.main.startLifetime.constant * Random.Range(0.7f, 1.1f);
            ps.Emit(ep, 1);
        }
    }

    void MakeAmbient()
    {
        var go = new GameObject("AmbientSparkles");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(0f, 0.3f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 5f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(GoldGradient());
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 14f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(22f, 1f, 22f);
        var vol = ps.velocityOverLifetime; vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        vol.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vol.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        vol.y = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        var sol = ps.sizeOverLifetime; sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMat;
    }

    void MakeTrail()
    {
        if (player == null) return;
        trail = player.GetComponent<TrailRenderer>();
        if (trail == null) trail = player.gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.5f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(1f, 0f));
        trail.colorGradient = GoldGradient();
        trail.sharedMaterial = particleMat;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public static Gradient GoldGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 1f, 0.8f), 0f),
                new GradientColorKey(new Color(1f, 0.75f, 0.1f), 0.4f),
                new GradientColorKey(new Color(1f, 0.25f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return g;
    }
}
