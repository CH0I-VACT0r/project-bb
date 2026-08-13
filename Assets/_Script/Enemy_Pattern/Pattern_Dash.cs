using System;
using System.Collections;
using UnityEngine;

public class Pattern_Dash : BossPatternBase // 범용 Base 클래스로 상속
{
    [Header("Dash Settings")]
    public float dashPrepareTime = 1.0f; // 멈춰서 기 모으는 시간
    public float dashForce = 20f;        // 돌진 속도/힘
    public float dashDuration = 0.5f;    // 돌진 지속 시간
    public float damage = 20f;

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
        // 1. 기존 이동 멈춤 및 방향 고정
        rb.linearVelocity = Vector2.zero;
        Vector2 targetDir = (currentTarget.position - transform.position).normalized;

        // TODO: 클라이언트 쪽에 붉은색 직선 경로(Telegraph)를 그리는 ClientRpc 호출 가능

        // 2. 준비 시간 대기
        yield return new WaitForSeconds(dashPrepareTime);

        // 3. 돌진 실행
        rb.AddForce(targetDir * dashForce, ForceMode2D.Impulse);

        // 4. 돌진 시간 대기 (이 동안 충돌(OnCollisionEnter2D) 시 대미지 판정)
        yield return new WaitForSeconds(dashDuration);

        // 5. 정지 후 패턴 종료
        rb.linearVelocity = Vector2.zero;
        FinishPattern();
    }

    // 돌진 중 플레이어와 부딪히면 대미지 적용 (서버 전용)
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsServer) return;
        if (collision.gameObject.CompareTag("Player"))
        {
            // DamageInfo 구조체에 맞춰 대미지 전달
            DamageInfo info = new DamageInfo { damageAmount = this.damage, attackType = AttackAttribute.Physical };
            collision.gameObject.GetComponent<IDamageable>()?.TakeDamage(info);
        }
    }
}