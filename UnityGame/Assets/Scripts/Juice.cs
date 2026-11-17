using UnityEngine;
using UnityEngine.UI;

// 派手な演出担当。GameManagerのイベントを聞くだけで、ゲームロジックには触れない。
public class Juice : MonoBehaviour
{
    public GameManager gm;
    public PlayerController player;
    public Camera cam;
    public Material particleMat;
    public Text countText, timeText, clearText, timeUpText;

    ParticleSystem burst, confetti;
    TrailRenderer trail;
    Vector3 camBase;
    Vector2 timeUpBase;
    float shakeT, shakeDur = 0.3f, shakeMag;
    float countPunch, clearAge;
    int lastSec = -1;
    float secPunch;

    void Awake()
    {
        burst = MakeParticles("Burst", 0.8f, 0.28f, 1.2f);
        confetti = MakeParticles("Confetti", 2.2f, 0.32f, 0.5f);
        MakeAmbient();
        MakeTrail();
        if (cam == null) cam = Camera.main;
    }

    void Start()
    {
        camBase = cam.transform.position;
        if (timeUpText != null) timeUpBase = timeUpText.rectTransform.anchoredPosition;
    }

    void OnEnable()
    {
        if (gm == null) return;
        gm.ItemCollected += OnCollected;
        gm.GameCleared += OnCleared;
        gm.TimeUpReached += OnTimeUp;
        gm.GameReset += OnReset;
    }

    void OnDisable()
    {
        if (gm == null) return;
        gm.ItemCollected -= OnCollected;
        gm.GameCleared -= OnCleared;
        gm.TimeUpReached -= OnTimeUp;
        gm.GameReset -= OnReset;
    }

    // ---------- イベント ----------
    void OnCollected(Collectible item)
    {
        var fx = item.GetComponent<ItemFx>();
        Color c = fx != null ? fx.CurrentColor() : Color.yellow;
        Emit(burst, item.transform.position, 28, 6f, c);
        countPunch = 0.7f;
        Shake(0.12f, 0.2f);
    }

    void OnCleared()
    {
        clearAge = 0f;
        Vector3 p = player != null ? player.transform.position : Vector3.zero;
        for (int k = 0; k < 3; k++)
            Emit(confetti, p + Vector3.up * 1.5f + Random.insideUnitSphere * 2f, 70, 9f, Color.white, true);
        Shake(0.45f, 0.6f);
    }

    void OnTimeUp() => Shake(0.5f, 0.6f);

    void OnReset()
    {
        if (trail != null) trail.Clear();
        clearAge = 0f;
        lastSec = -1;
    }

    // ---------- 毎フレーム ----------
    void Update()
    {
        countPunch = Mathf.Lerp(countPunch, 0f, 10f * Time.deltaTime);
        if (countText != null) countText.rectTransform.localScale = Vector3.one * (1f + countPunch);

        if (timeText != null && gm != null)
        {
            int sec = Mathf.CeilToInt(gm.RemainingTime);
            bool danger = gm.IsPlaying && gm.RemainingTime <= 10f;
            if (danger && sec != lastSec) secPunch = 0.35f;   // 1秒ごとにドクン
            lastSec = sec;
            secPunch = Mathf.Lerp(secPunch, 0f, 9f * Time.deltaTime);
            timeText.rectTransform.localScale = Vector3.one * (1f + (danger ? secPunch : 0f));
            timeText.color = danger
                ? Color.Lerp(Color.white, new Color(1f, 0.15f, 0.2f), 0.5f + 0.5f * Mathf.Sin(Time.time * 12f))
                : Color.white;
        }

        if (clearText != null && clearText.gameObject.activeSelf)
        {
            clearAge += Time.deltaTime;
            float s = EaseOutBack(Mathf.Clamp01(clearAge / 0.45f)) * (1f + 0.06f * Mathf.Sin(Time.time * 9f));
            clearText.rectTransform.localScale = Vector3.one * s;
            clearText.color = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.8f, 1f), 0.65f, 1f);
        }

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
    }

    void Shake(float mag, float dur) { shakeMag = mag; shakeDur = dur; shakeT = dur; }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
    }

    // ---------- パーティクル ----------
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
        main.maxParticles = 1000;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var sol = ps.sizeOverLifetime; sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = particleMat;
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
        main.startColor = new ParticleSystem.MinMaxGradient(RainbowGradient());
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 14f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(22f, 1f, 22f);
        var vol = ps.velocityOverLifetime; vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        // x/y/z は同じモード(TwoConstants)に揃える必要がある
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
        trail.time = 0.45f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(1f, 0f));
        trail.colorGradient = RainbowGradient();
        trail.sharedMaterial = particleMat;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Gradient RainbowGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 0.3f, 0.5f), 0f),
                new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0.33f),
                new GradientColorKey(new Color(0.3f, 1f, 0.6f), 0.66f),
                new GradientColorKey(new Color(0.3f, 0.7f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return g;
    }
}
