using UnityEngine;

// メダルの見た目だけの演出: 金色のコインがくるくる回って浮く。追加で出る時は上から落ちてポンと現れる。
public class ItemFx : MonoBehaviour
{
    public float hueOffset;                       // 位相ずらし用
    public float bobHeight = 0.2f;
    public Vector3 baseScale = new Vector3(0.8f, 0.06f, 0.8f);

    static readonly Color Gold = new Color(1f, 0.72f, 0.1f);

    Material mat;
    Vector3 basePos;
    float phase;
    float spawnT = 1f;                            // 0→1で出現アニメ。1なら通常

    void Awake()
    {
        mat = GetComponent<Renderer>().material;   // 個別インスタンス
        mat.EnableKeyword("_EMISSION");
        basePos = transform.position;
        phase = hueOffset * 6.28f + Random.value * 0.5f;
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);   // renderer.material で作った個別インスタンスを解放
    }

    // 追加で出現する時に呼ぶ(位置を確定して出現アニメを始める)
    public void Spawn(Vector3 pos)
    {
        basePos = pos;
        spawnT = 0f;
    }

    void Update()
    {
        float t = Time.time;
        float pop = 1f, drop = 0f;
        if (spawnT < 1f)
        {
            spawnT = Mathf.Min(1f, spawnT + Time.deltaTime / 0.45f);
            float e = 1f - Mathf.Pow(1f - spawnT, 3f);       // easeOutCubic
            pop = e; drop = (1f - e) * 5f;                   // 上から落ちつつ大きくなる
        }
        transform.position = basePos + Vector3.up * (Mathf.Sin(t * 3f + phase) * bobHeight + drop);
        // 円柱を立てて、縦軸まわりにスピン(コインがひっくり返って見える)
        transform.rotation = Quaternion.Euler(0f, t * 260f + phase * 60f, 0f) * Quaternion.Euler(90f, 0f, 0f);
        transform.localScale = baseScale * ((1f + 0.08f * Mathf.Sin(t * 7f + phase)) * pop);
        float glow = 0.3f + 0.2f * Mathf.Sin(t * 8f + phase);   // 光り過ぎると形が潰れる
        mat.SetColor("_BaseColor", Gold);
        mat.SetColor("_EmissionColor", Gold * glow);
    }

    public Color CurrentColor() => Gold;
}
