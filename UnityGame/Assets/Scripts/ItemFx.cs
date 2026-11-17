using UnityEngine;

// アイテムの見た目だけの演出: 金色のコインがくるくる回って浮く
public class ItemFx : MonoBehaviour
{
    public float hueOffset;                       // 位相ずらし用
    public float bobHeight = 0.2f;
    public Vector3 baseScale = new Vector3(0.8f, 0.06f, 0.8f);

    static readonly Color Gold = new Color(1f, 0.72f, 0.1f);

    Material mat;
    Vector3 basePos;
    float phase;

    void Awake()
    {
        mat = GetComponent<Renderer>().material;   // 個別インスタンス
        mat.EnableKeyword("_EMISSION");
        basePos = transform.position;
        phase = hueOffset * 6.28f;
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);   // renderer.material で作った個別インスタンスを解放
    }

    void Update()
    {
        float t = Time.time;
        transform.position = basePos + Vector3.up * (Mathf.Sin(t * 3f + phase) * bobHeight);
        // 円柱を立てて、縦軸まわりにスピン(コインがひっくり返って見える)
        transform.rotation = Quaternion.Euler(0f, t * 260f + phase * 60f, 0f) * Quaternion.Euler(90f, 0f, 0f);
        transform.localScale = baseScale * (1f + 0.08f * Mathf.Sin(t * 7f + phase));
        float glow = 0.3f + 0.2f * Mathf.Sin(t * 8f + phase);   // 光り過ぎると形が潰れる
        mat.SetColor("_BaseColor", Gold);
        mat.SetColor("_EmissionColor", Gold * glow);
    }

    public Color CurrentColor() => Gold;
}
