using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 LastMoveDirection { get; private set; } = Vector2.down;
    public bool IsRunning { get; private set; }

    private Rigidbody2D rb;
    private PlayerNetworkState networkState;
    private PlayerStats stats;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        networkState = GetComponent<PlayerNetworkState>();
        stats = GetComponent<PlayerStats>();
        rb.simulated = true;
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        ReadInput();
        UpdateLastMoveDirection();

        // Chỉ gửi Command khi network đã ready
        if (networkState != null && NetworkClient.active && NetworkClient.ready)
        {
            networkState.CmdUpdateAnimationState(
                MoveInput != Vector2.zero,
                LastMoveDirection,
                IsRunning
            );
        }
        if ( networkState != null && !networkState.IsAlive)
        {
            MoveInput = Vector2.zero;
            IsRunning = false;
            return;
        }
    }

    private void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        float walkSpeed = stats != null ? stats.GetWalkSpeed() : 5f;
        float runSpeed = stats != null ? stats.GetRunSpeed() : 8f;
        float currentSpeed = IsRunning ? runSpeed : walkSpeed;
        rb.linearVelocity = MoveInput.normalized * currentSpeed;

        if (networkState != null && !networkState.IsAlive)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
    }

    private void ReadInput()
    {
        Vector2 input = Vector2.zero;
        bool wantsRun = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) input.y += 1;
            if (Keyboard.current.sKey.isPressed) input.y -= 1;
            if (Keyboard.current.aKey.isPressed) input.x -= 1;
            if (Keyboard.current.dKey.isPressed) input.x += 1;

            wantsRun = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        }

        MoveInput = input;
        IsRunning = wantsRun && input != Vector2.zero;
    }

    private void UpdateLastMoveDirection()
    {
        if (MoveInput == Vector2.zero) return;

        float x = MoveInput.x;
        float y = MoveInput.y;

        // Ưu tiên trục dọc cho top-down 4 hướng
        if (Mathf.Abs(y) >= Mathf.Abs(x))
        {
            LastMoveDirection = y > 0 ? Vector2.up : Vector2.down;
        }
        else
        {
            LastMoveDirection = x > 0 ? Vector2.right : Vector2.left;
        }
    }
}