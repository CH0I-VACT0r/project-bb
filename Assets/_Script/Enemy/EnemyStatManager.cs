using System.Collections;
using Unity.Netcode;
using UnityEngine;

public enum EnemyAttackType { ConeSlash, Dash, Projectile }

public class EnemyStatManager : NetworkBehaviour, IDamageable
{
    public EnemyDataSO enemyData;
    public NetworkVariable<float> currentHP = new NetworkVariable<float>();
    
    // 런타임 캐싱 변수들
    [HideInInspector] public float baseDamage;
    [HideInInspector] public float accuracy;
    [HideInInspector] public float penetration; // 물리/마법 공용
    [HideInInspector] public float defense;
    [HideInInspector] public float magicDefense;
    [HideInInspector] public float evasion;

    private Rigidbody2D rb;
    public bool isStunned = false;
    private bool isDead = false;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (enemyData != null)
            {
                currentHP.Value = enemyData.maxHP;

                // 공격 스탯 캐싱
                baseDamage = enemyData.baseDamage;
                accuracy = enemyData.accuracy;
                penetration = (enemyData.attackAttribute == AttackAttribute.Physical) ? enemyData.physicalPenetration : enemyData.magicPenetration;

                // 방어 스탯 캐싱 (오류 해결)
                defense = enemyData.defense;
                magicDefense = enemyData.magicDefense;
                evasion = enemyData.evasion;

                // StatusEffectManagerNetcode에 면역 플래그 전달
                var statusManager = GetComponent<StatusEffectManagerNetcode>();
                if (statusManager != null)
                {
                    // 아래 추가할 StatusEffectManagerNetcode의 변수에 접근
                    statusManager.immuneElements = enemyData.immuneElements;
                    statusManager.immuneCC = enemyData.immuneCC;
                }
            }
        }
    }

    public void TakeDamage(DamageInfo info)
    {
        if (!IsServer) return;

        float effectiveEvasion = Mathf.Max(0f, evasion - info.attackerAccuracy);
        if (Random.Range(0f, 100f) < effectiveEvasion)
        {
            ShowDamagePopupClientRpc(0f, transform.position, false, true);
            return;
        }

        float finalDamage = info.damageAmount;
        if (info.attackType == AttackAttribute.Physical)
        {
            float effectiveDefense = Mathf.Max(0f, defense - info.attackerPenetration);
            finalDamage = Mathf.Max(1f, finalDamage - effectiveDefense);
        }
        else if (info.attackType == AttackAttribute.Magic)
        {
            float effectiveMagicDefense = Mathf.Max(0f, magicDefense - info.attackerPenetration);
            finalDamage = Mathf.Max(1f, finalDamage - effectiveMagicDefense);
        }
        finalDamage = Mathf.Round(finalDamage);
        finalDamage = Mathf.Max(1f, finalDamage);
        currentHP.Value -= finalDamage;
        ShowDamagePopupClientRpc(finalDamage, transform.position, info.isCritical, false);

        if (rb != null && info.knockbackForce > 0f)
        {
            StartCoroutine(ApplyKnockbackRoutine(info.knockbackDir, info.knockbackForce));
        }

        if (currentHP.Value <= 0)
        {
            Die();
        }
    }

    [ClientRpc]
    private void ShowDamagePopupClientRpc(float damage, Vector3 position, bool isCritical, bool isMiss)
    {
        if (DamagePopupManager.Instance != null)
        {
            Vector3 randomOffset = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0f, 0.5f), 0);
            DamagePopupManager.Instance.CreatePopup(position + randomOffset, damage, isCritical, isMiss);
        }
    }

    private IEnumerator ApplyKnockbackRoutine(Vector3 dir, float force)
    {
        isStunned = true;
        rb.linearVelocity = Vector2.zero;

        rb.AddForce(dir * force, ForceMode2D.Impulse);

        yield return new WaitForSeconds(0.3f);

        rb.linearVelocity = Vector2.zero;
        isStunned = false;
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;

        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}