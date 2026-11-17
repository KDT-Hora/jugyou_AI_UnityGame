using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    public GameManager manager;
    public float moveSpeed = 6f;
    public float fallY = -5f;
    public Vector3 startPosition = new Vector3(0f, 0.5f, -7f);

    Rigidbody rb;
    Vector2 move;
    bool rPrev;

    void Awake() => rb = GetComponent<Rigidbody>();

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) { move = Vector2.zero; return; }

        move = Vector2.zero;
        if (kb.wKey.isPressed) move.y += 1f;
        if (kb.sKey.isPressed) move.y -= 1f;
        if (kb.dKey.isPressed) move.x += 1f;
        if (kb.aKey.isPressed) move.x -= 1f;

        // isPressed の立ち上がりで検出(擬似入力でも実キーでも動く)
        bool rNow = kb.rKey.isPressed;
        bool rDown = rNow && !rPrev;
        rPrev = rNow;

        if (rDown) manager.ResetGame();
        else if (transform.position.y < fallY) manager.ResetGame();
    }

    void FixedUpdate()
    {
        var v = new Vector3(move.x, 0f, move.y).normalized * moveSpeed;
        rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
    }

    public void ResetPlayer()
    {
        move = Vector2.zero;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = startPosition;
        transform.position = startPosition;
        transform.rotation = Quaternion.identity;
    }
}
