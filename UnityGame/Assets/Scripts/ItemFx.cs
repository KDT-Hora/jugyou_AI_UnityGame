using UnityEngine;

// アイテムの見た目だけの演出: 上下に浮く・脈打つ・虹色に光る
public class ItemFx : MonoBehaviour
{
    public float hueOffset;
    public float bobHeight = 0.18f;
    public float baseScale = 0.6f;

    Renderer rend;
    Material mat;
    Vector3 basePos;
    float phase;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        mat = rend.material;               // アイテムごとの色にするため個別インスタンス
        mat.EnableKeyword("_EMISSION");
        basePos = transform.position;
        phase = hueOffset * 6.28f;
    }

    void Update()
    {
        float t = Time.time;
        transform.position = basePos + Vector3.up * (Mathf.Sin(t * 3f + phase) * bobHeight);
        transform.localScale = Vector3.one * (baseScale * (1f + 0.12f * Mathf.Sin(t * 6f + phase)));
        var c = Color.HSVToRGB(Mathf.Repeat(hueOffset + t * 0.25f, 1f), 0.75f, 1f);
        mat.SetColor("_BaseColor", c);
        mat.SetColor("_EmissionColor", c * 0.9f);
    }

    public Color CurrentColor() => mat != null ? mat.GetColor("_BaseColor") : Color.white;
}
