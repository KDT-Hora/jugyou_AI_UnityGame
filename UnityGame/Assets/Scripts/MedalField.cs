using System.Collections.Generic;
using UnityEngine;

// フィールドにメダルを追加で出す。最初から置いてあるメダルは GameManager.items が管理する。
public class MedalField : MonoBehaviour
{
    public GameManager gm;
    public Collectible template;      // 複製元(非アクティブのメダル)
    public float half = 8f;           // 出現範囲(床の中心から±half)
    public float minPlayerDistance = 2f;
    public float minMedalDistance = 1.1f;

    readonly List<Collectible> spawned = new List<Collectible>();

    // 今フィールドにあるメダルの数(最初のメダル+追加分のうち、まだ拾われていないもの)
    public int AliveCount
    {
        get
        {
            int n = 0;
            if (gm != null) foreach (var i in gm.items) if (i != null && !i.Collected) n++;
            foreach (var s in spawned) if (s != null && !s.Collected) n++;
            return n;
        }
    }

    public int SpawnedCount => spawned.Count;

    public void Spawn(int count)
    {
        if (template == null) return;
        for (int k = 0; k < count; k++)
        {
            var pos = PickPosition();
            var c = Instantiate(template, pos, template.transform.rotation, transform);
            c.manager = gm;
            c.gameObject.SetActive(true);
            c.ResetItem();
            var fx = c.GetComponent<ItemFx>();
            if (fx != null) fx.Spawn(pos);
            spawned.Add(c);
        }
    }

    public void ClearSpawned()
    {
        foreach (var s in spawned)
        {
            if (s == null) continue;
            if (Application.isPlaying) Destroy(s.gameObject); else DestroyImmediate(s.gameObject);
        }
        spawned.Clear();
    }

    // 他のメダル・プレイヤーから離れた場所を探す(見つからなければ最後の候補)
    Vector3 PickPosition()
    {
        Vector3 p = Vector3.zero;
        for (int tries = 0; tries < 30; tries++)
        {
            p = new Vector3(Random.Range(-half, half), 0.5f, Random.Range(-half, half));
            if (IsFarEnough(p)) break;
        }
        return p;
    }

    bool IsFarEnough(Vector3 p)
    {
        if (gm != null && gm.player != null && Flat(gm.player.transform.position, p) < minPlayerDistance) return false;
        if (gm != null) foreach (var i in gm.items) if (i != null && !i.Collected && Flat(i.transform.position, p) < minMedalDistance) return false;
        foreach (var s in spawned) if (s != null && !s.Collected && Flat(s.transform.position, p) < minMedalDistance) return false;
        return true;
    }

    static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
}
