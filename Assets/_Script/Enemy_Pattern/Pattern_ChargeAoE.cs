using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Pattern_ChargeAoE : BossPatternBase
{
    [Header("AoE Settings")]
    public float chargeTime = 1.5f;
    public float attackRadius = 3f;
    public float damage = 30f;
    public LayerMask playerLayer;

    protected override void OnPatternStart()
    {
        if (!IsServer) return;
        StartCoroutine(ChargeAoERoutine());
    }

    private IEnumerator ChargeAoERoutine()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        ToggleWarningSpriteClientRpc(true);

        // Todo : 점점 채워지는 공격 범위 표시

        yield return new WaitForSeconds(chargeTime);

        ToggleWarningSpriteClientRpc(false);

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRadius, playerLayer);
        foreach (var hit in hits)
        {
            DamageInfo info = new DamageInfo { damageAmount = this.damage, attackType = AttackAttribute.Magic };
            hit.GetComponent<IDamageable>()?.TakeDamage(info);
        }

        FinishPattern();
    }

    [ClientRpc]
    private void ShowAoEWarningClientRpc(Vector3 pos, float radius, float duration)
    {
        // TODO: 미리 만들어둔 AoEIndicatorMesh를 여기서 활성화하여 radius 크기로 점진적 확대(Lerp) 표시
    }
}