using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Pattern_Dash : BossPatternBase
{
    [Header("Dash Settings")]
    public float dashPrepareTime = 1.0f;
    public float dashForce = 20f;
    public float dashDuration = 0.5f;
    public float damage = 20f;

    [Header("Dash Visuals (조준선 전용)")]
    [Tooltip("플레이어를 추적하며 빙글빙글 돌 화살표 이미지 (자식 객체)")]
    public Transform dashAimIndicator;

    private Rigidbody2D rb;
    private bool isDashImpacted = false;

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
        isDashImpacted = false;
        rb.linearVelocity = Vector2.zero;

        // 머리 위 고정 느낌표
        ToggleWarningSpriteClientRpc(true);

        // 바닥 조준 화살표 켜기
        ToggleAimIndicatorClientRpc(true);

        float timer = 0f;
        Vector2 targetDir = Vector2.zero;

        while (timer < dashPrepareTime)
        {
            if (currentTarget != null)
            {
                Vector2 startPos = transform.position;
                Vector2 targetPos = currentTarget.position;
                targetDir = (targetPos - startPos).normalized;

                float angle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;
                SyncAimRotationClientRpc(angle);
            }
            timer += Time.deltaTime;
            yield return null;
        }

        ToggleWarningSpriteClientRpc(false);
        ToggleAimIndicatorClientRpc(false);

        if (!string.IsNullOrEmpty(animatorTriggerName))
        {
            PlayPatternAnimationClientRpc(animatorTriggerName);
        }

        // 락온된 방향으로 돌진
        if (targetDir != Vector2.zero)
        {
            float elapsed = 0f;
            while (elapsed < dashDuration && !isDashImpacted)
            {
                rb.linearVelocity = targetDir * dashForce;
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
        }

        rb.linearVelocity = Vector2.zero;
        FinishPattern();
    }

    // --- 화살표(조준선) 전용 ClientRpc ---
    [ClientRpc]
    private void ToggleAimIndicatorClientRpc(bool isOn)
    {
        if (dashAimIndicator != null)
        {
            dashAimIndicator.gameObject.SetActive(isOn);
        }
    }

    [ClientRpc]
    private void SyncAimRotationClientRpc(float angle)
    {
        if (dashAimIndicator != null)
        {
            dashAimIndicator.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    // --- 물리 충돌 로직 ---
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsServer) return;
        if (collision.gameObject.CompareTag("Player"))
        {
            isDashImpacted = true;
            IDamageable target = collision.gameObject.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                DamageInfo info = new DamageInfo { damageAmount = this.damage, attackType = AttackAttribute.Physical };
                target.TakeDamage(info);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;
        if (other.CompareTag("Player"))
        {
            isDashImpacted = true;
            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                DamageInfo info = new DamageInfo { damageAmount = this.damage, attackType = AttackAttribute.Physical };
                target.TakeDamage(info);
            }
        }
    }
}