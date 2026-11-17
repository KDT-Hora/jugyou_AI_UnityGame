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

    Rigidbody Body => rb != null ? rb : (rb = GetComponent<Rigidbody>());

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
        // 1フレーム未満の短いタップは wasPressedThisFrame で拾う
        bool rDown = (rNow && !rPrev) || kb.rKey.wasPressedThisFrame;
        rPrev = rNow;

        if (rDown) manager.ResetGame();
        else if (transform.position.y < fallY) manager.ResetGame();
    }

    void FixedUpdate()
    {
        // クリア後・時間切れ後は移動できない(RはUpdateで生きている)
        bool canMove = manager == null || manager.IsPlaying;
        var v = canMove ? new Vector3(move.x, 0f, move.y).normalized * moveSpeed : Vector3.zero;
        Body.linearVelocity = new Vector3(v.x, Body.linearVelocity.y, v.z);
    }

    public void ResetPlayer()
    {
        move = Vector2.zero;
        Body.linearVelocity = Vector3.zero;
        Body.angularVelocity = Vector3.zero;
        Body.position = startPosition;
        transform.position = startPosition;
        transform.rotation = Quaternion.identity;
    }
}
