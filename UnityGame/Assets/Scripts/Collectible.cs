using UnityEngine;

public class Collectible : MonoBehaviour
{
    public GameManager manager;
    public bool Collected { get; private set; }

    void OnTriggerEnter(Collider other)
    {
        if (Collected || manager == null) return;
        if (other.GetComponentInParent<PlayerController>() != null) manager.Collect(this);
    }

    public void MarkCollected()
    {
        Collected = true;
        gameObject.SetActive(false);
    }

    public void ResetItem()
    {
        Collected = false;
        gameObject.SetActive(true);
    }
}
