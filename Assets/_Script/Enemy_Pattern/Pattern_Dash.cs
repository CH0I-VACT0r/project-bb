using System;
using System.Collections;
using UnityEngine;

public class Pattern_Dash : BossPatternBase // 범용 Base 클래스로 상속
{
    [Header("Dash Settings")]
    public float dashPrepareTime = 0.25f; // 멈춰서 기 모으는 시간
    public float dashForce = 10f;        // 돌진 속도/힘
    public float dashDuration = 0.4f;    // 돌진 지속 시간
    public float damage = 10f;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    protected override void OnPatternStart()
    {
        if (!IsServer) return;
        StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        rb.linearVelocity = Vector2.zero;
        ToggleWarningSpriteClientRpc(true);

        // 기 모으는 시간 대기
        yield return new WaitForSeconds(dashPrepareTime);

        // 공격 직전 플레이어 위치로 방향 계산 (360도 정밀 조준)
        Vector2 targetDir = Vector2.zero;
        if (currentTarget != null)
        {
            targetDir = (currentTarget.position - transform.position).normalized;
        }

        ToggleWarningSpriteClientRpc(false);

        // 애니메이션 발동
        if (!string.IsNullOrEmpty(animatorTriggerName))
        {
            PlayPatternAnimationClientRpc(animatorTriggerName);
        }

        // 물리 강제 돌진
        if (targetDir != Vector2.zero)
        {
            float elapsed = 0f;
            while (elapsed < dashDuration)
            {
                rb.linearVelocity = targetDir * dashForce;
                elapsed += Time.fixedDeltaTime;

                yield return new WaitForFixedUpdate(); // 물리 연산 주기와 동기화
            }
        }

        // 패턴 종료
        rb.linearVelocity = Vector2.zero;
        FinishPattern();
    }

    // 대시 중 플레이어와 부딪힐 때 피해 (트리거 콜라이더)
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;
        if (other.CompareTag("Player"))
        {
            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                DamageInfo info = new DamageInfo { damageAmount = this.damage, attackType = AttackAttribute.Physical };
                target.TakeDamage(info);
            }
        }
    }
}