using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem; // InputManager를 읽기 위해 추가

[RequireComponent(typeof(Animator), typeof(SpriteRenderer), typeof(NetworkAnimator))]
[RequireComponent(typeof(Rigidbody2D), typeof(StatusEffectManagerNetcode))]
public class PlayerVisualNetcode : NetworkBehaviour
{
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private StatusEffectManagerNetcode statusManager;

    private NetworkVariable<bool> isFlipped = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool localFlipState = false;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        statusManager = GetComponent<StatusEffectManagerNetcode>();
    }

    public override void OnNetworkSpawn()
    {
        isFlipped.OnValueChanged += OnFlipStateChanged;

        localFlipState = isFlipped.Value;
        spriteRenderer.flipX = localFlipState;
    }

    public override void OnNetworkDespawn()
    {
        isFlipped.OnValueChanged -= OnFlipStateChanged;
    }

    private void OnFlipStateChanged(bool previousValue, bool newValue)
    {
        if (!IsOwner)
        {
            spriteRenderer.flipX = newValue;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // 순수 입력값
        Vector2 input = InputManager.Instance.Controls.Gameplay.Move.ReadValue<Vector2>();

        // CC기 확인
        bool isForcedMoving = statusManager != null && (statusManager.isTaunted.Value || statusManager.isFeared.Value);
        bool isStunned = statusManager != null && statusManager.isStunned.Value;

        // 입력값으로 걷기 판정
        bool isWalking = (!isStunned && input.magnitude > 0.1f) || isForcedMoving;
        animator.SetBool("isWalking", isWalking);
        float checkDirX = isForcedMoving ? rb.linearVelocity.x : input.x;

        if (checkDirX > 0.1f && localFlipState)
        {
            localFlipState = false;
            spriteRenderer.flipX = false;
            UpdateFlipServerRpc(false);
        }
        else if (checkDirX < -0.1f && !localFlipState)
        {
            localFlipState = true;
            spriteRenderer.flipX = true;
            UpdateFlipServerRpc(true);
        }
    }

    [ServerRpc]
    private void UpdateFlipServerRpc(bool flipState)
    {
        isFlipped.Value = flipState;
    }
}