using UnityEngine;
using Mirror;

public class MonsterAnimationGeneric : NetworkBehaviour
{
    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int TakeDamageHash = Animator.StringToHash("TakeDamage");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int TakeDamageStateHash = Animator.StringToHash("Base Layer.TakeDamage");
    private static readonly int DeathStateHash = Animator.StringToHash("Base Layer.Death");

    private Animator animator;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private MonsterAI monsterAI;

    [SyncVar(hook = nameof(OnFlipChanged))]
    private float moveDirectionX = 1f; // Mặc định quay phải

    [SyncVar(hook = nameof(OnIsMovingChanged))]
    private bool isMovingNetwork;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        monsterAI = GetComponent<MonsterAI>();
        ApplyFlip(moveDirectionX);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyFlip(moveDirectionX);
        ApplyIsMoving(isMovingNetwork);
    }

    [Server]
    public void SetMoveDirection(float x)
    {
        // Chỉ cập nhật SyncVar nếu hướng X đủ lớn (tránh nhiễu)
        if (Mathf.Abs(x) > 0.1f)
        {
            moveDirectionX = x;
        }
    }

    private void Update()
    {
        if (animator == null || rb == null) return;

        if (isServer)
        {
            // Đồng bộ trạng thái di chuyển để client nào cũng thấy cùng animation.
            bool isMoving = rb.linearVelocity.magnitude > 0.8f;
            if (isMovingNetwork != isMoving)
            {
                isMovingNetwork = isMoving;
            }

            ApplyIsMoving(isMoving);
        }
    }

    void OnFlipChanged(float oldVal, float newVal)
    {
        ApplyFlip(newVal);
    }

    void OnIsMovingChanged(bool oldVal, bool newVal)
    {
        ApplyIsMoving(newVal);
    }

    private void ApplyFlip(float directionX)
    {
        if (spriteRenderer == null) return;

        // Nếu hướng dương (>0): Không lật (quay phải)
        // Nếu hướng âm (<0): Lật (quay trái)
        if (directionX > 0.1f)
            spriteRenderer.flipX = false;
        else if (directionX < -0.1f)
            spriteRenderer.flipX = true;
    }

    private void ApplyIsMoving(bool isMoving)
    {
        if (animator == null) return;
        animator.SetBool(IsMovingHash, isMoving);
    }
    [ClientRpc]
    public void TriggerAttackAnimation()
    {
        if (animator != null)
        {
            animator.ResetTrigger(TakeDamageHash);
            animator.SetTrigger(AttackHash);
        }
    }

    [ClientRpc]
    public void TriggerTakeDamageAnimation()
    {
        if (animator != null)
        {
            animator.ResetTrigger(AttackHash);
            animator.SetTrigger(TakeDamageHash);
            animator.CrossFadeInFixedTime(TakeDamageStateHash, 0f, 0, 0f);
        }
    }

    [ClientRpc]
    public void TriggerDeathAnimation()
    {
        if (animator != null)
        {
            isMovingNetwork = false;
            animator.ResetTrigger(AttackHash);
            animator.ResetTrigger(TakeDamageHash);
            animator.SetBool(IsMovingHash, false);
            animator.SetTrigger(DeathHash);
            animator.CrossFadeInFixedTime(DeathStateHash, 0f, 0, 0f);
        }
    }

    // Gọi từ Animation Event ở clip Attack (thường đặt gần cuối clip).
    public void OnAttackAnimationHitEvent()
    {
        if (!isServer) return;
        if (monsterAI == null) return;

        monsterAI.ResolveAttackFromAnimationEvent();
    }
}