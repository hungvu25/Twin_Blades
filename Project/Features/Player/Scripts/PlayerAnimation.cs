using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int LastMoveXHash = Animator.StringToHash("LastMoveX");
    private static readonly int LastMoveYHash = Animator.StringToHash("LastMoveY");
    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    private static readonly int IsRunningHash = Animator.StringToHash("isRunning");
    private static readonly int IsAliveHash = Animator.StringToHash("isAlive");

    private Animator animator;
    private PlayerNetworkState networkState;

    private Vector2 lastFacing = Vector2.down;
    private bool wasAlive = true;

    [Header("Respawn")]
    [SerializeField] private string respawnStateName = "Idle";
    [SerializeField] private float respawnCrossFadeSeconds = 0f;

    private int respawnStateHash;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        networkState = GetComponent<PlayerNetworkState>();
        respawnStateHash = Animator.StringToHash(respawnStateName);
        wasAlive = networkState == null || networkState.IsAlive;
    }

    private void Update()
    {
        if (animator == null || networkState == null) return;

        bool isMoving = networkState.IsMoving;
        bool isRunning = networkState.IsRunning;
        bool isAlive = networkState.IsAlive;

        if (!wasAlive && isAlive)
        {
            // Force thoat state dead khi player duoc hoi sinh.
            if (respawnStateHash != 0)
                animator.CrossFadeInFixedTime(respawnStateHash, Mathf.Max(0f, respawnCrossFadeSeconds), 0, 0f);
            animator.Update(0f);
        }
        wasAlive = isAlive;

        Vector2 rawDirection = networkState.LastMoveDirection;
        Vector2 facingDirection = ConvertToFourDirection(rawDirection);

        if (facingDirection == Vector2.zero)
        {
            facingDirection = lastFacing;
        }
        else
        {
            lastFacing = facingDirection;
        }

        animator.SetBool(IsMovingHash, isMoving);
        animator.SetBool(IsRunningHash, isRunning);
        animator.SetBool(IsAliveHash, isAlive);
        // Idle Blend Tree dùng hướng cuối cùng
        animator.SetFloat(LastMoveXHash, facingDirection.x);
        animator.SetFloat(LastMoveYHash, facingDirection.y);

        // Run Blend Tree dùng hướng di chuyển hiện tại
        if (isMoving)
        {
            animator.SetFloat(MoveXHash, facingDirection.x);
            animator.SetFloat(MoveYHash, facingDirection.y);
        }
        else
        {
            animator.SetFloat(MoveXHash, 0f);
            animator.SetFloat(MoveYHash, 0f);
        }
        if (!isAlive)
        {
            animator.SetBool(IsMovingHash, false);
            animator.SetBool(IsRunningHash, false);
            animator.SetFloat(MoveXHash, 0f);
            animator.SetFloat(MoveYHash, 0f);
            return;
        }
    }

    private Vector2 ConvertToFourDirection(Vector2 dir)
    {
        if (dir == Vector2.zero)
            return Vector2.zero;

        // Ưu tiên trục dọc cho game top-down 4 hướng
        if (Mathf.Abs(dir.y) >= Mathf.Abs(dir.x))
        {
            return dir.y > 0 ? Vector2.up : Vector2.down;
        }

        return dir.x > 0 ? Vector2.right : Vector2.left;
    }
}