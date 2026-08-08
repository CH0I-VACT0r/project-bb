using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(Animator), typeof(SpriteRenderer), typeof(NetworkAnimator))]
public class EnemyVisualNetcode : NetworkBehaviour
{
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;

    // 서버가 결정한 좌우 반전 상태를 모든 클라이언트로 동기화
    private NetworkVariable<bool> isFlipped = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        NetworkAnimator netAnimator = GetComponent<NetworkAnimator>();
        if (netAnimator != null && animator != null)
        {
            netAnimator.Animator = animator; // 인스펙터 설정과 무관하게 코드로 묶어버림
        }
    }

    public override void OnNetworkSpawn()
    {
        isFlipped.OnValueChanged += OnFlipStateChanged;
        spriteRenderer.flipX = isFlipped.Value;
    }

    public override void OnNetworkDespawn()
    {
        isFlipped.OnValueChanged -= OnFlipStateChanged;
    }

    private void OnFlipStateChanged(bool previousValue, bool newValue)
    {
        spriteRenderer.flipX = newValue;
    }

    private void Update()
    {
        // 적의 AI 이동 및 판단은 서버에서만 이루어지므로 시각 연산도 서버만 수행
        if (!IsServer) return;

        // 걷기 애니메이션 동기화: 속도의 크기(sqrMagnitude)가 0보다 크면 걷는 것으로 판정
        bool isWalking = rb.linearVelocity.sqrMagnitude > 0.01f;
        animator.SetBool("isWalking", isWalking);

        // 좌우 반전 동기화: X축 속도 방향에 따라 결정
        if (rb.linearVelocity.x > 0.01f && isFlipped.Value)
        {
            isFlipped.Value = false;
        }
        else if (rb.linearVelocity.x < -0.01f && !isFlipped.Value)
        {
            isFlipped.Value = true;
        }
    }

    public void TriggerAttackAnimation()
    {
        if (IsServer)
        {
            animator.SetTrigger("Attack");
        }
    }

    // 외부(EnemyStatManager)에서 사망 시 호출할 함수
    public void TriggerDeathAnimation()
    {
        if (IsServer)
        {
            animator.SetTrigger("Die");
        }
    }
}
