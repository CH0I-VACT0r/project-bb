using System.Collections;
using Unity.Netcode;
using UnityEngine;

public enum EnemyAttackType { Melee, Projectile }

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
    [HideInInspector]
    public bool isSummonedMinion = false;

    private Rigidbody2D rb;
    public bool isStunned = false;
    private bool isDead = false;

    private float lastDotPopupTime = 0f;
    private const float DOT_POPUP_INTERVAL = 0.4f;

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
                base.OnNetworkSpawn();
            }
        }
    }

    public void ApplyScaling(int currentFloor, int playerCount)
    {
        if (!IsServer || enemyData == null) return;
 
        float stageMultiplier = 1f + ((currentFloor - 1) * 0.05f); // 층별 스케일 배율 계산 (1층 = 1.0, 2층 = 1.05, 3층 = 1.10 ...)

        // 체력 연산: 반올림(기본 체력 * 층 배율 * 플레이어 수)
        float scaledMaxHP = Mathf.RoundToInt(enemyData.maxHP * stageMultiplier * playerCount);
        currentHP.Value = scaledMaxHP;

        // 공격력 연산: 반올림(기본 공격력 * 층 배율)
        baseDamage = Mathf.RoundToInt(enemyData.baseDamage * stageMultiplier);

        // 기타 방어/명중 스탯 캐싱
        accuracy = enemyData.accuracy;
        penetration = (enemyData.attackAttribute == AttackAttribute.Physical) ? enemyData.physicalPenetration : enemyData.magicPenetration;
        defense = enemyData.defense;
        magicDefense = enemyData.magicDefense;
        evasion = enemyData.evasion;

        // 5. StatusEffectManagerNetcode에 면역 플래그 전달
        var statusManager = GetComponent<StatusEffectManagerNetcode>();
        if (statusManager != null)
        {
            statusManager.immuneElements = enemyData.immuneElements;
            statusManager.immuneCC = enemyData.immuneCC;
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
        float targetDefense = 0f;
        if (info.attackType == AttackAttribute.Physical)
        {
            targetDefense = Mathf.Max(0f, defense - info.attackerPenetration);
        }
        else if (info.attackType == AttackAttribute.Magic)
        {
            targetDefense = Mathf.Max(0f, magicDefense - info.attackerPenetration);
        }

        float defenseMultiplier = 100f / (100f + targetDefense);
        finalDamage = finalDamage * defenseMultiplier;

        finalDamage = Mathf.Round(finalDamage);
        finalDamage = Mathf.Max(1f, finalDamage);

        currentHP.Value -= finalDamage;

        if (info.isDoTDamage)
        {
            if (Time.time - lastDotPopupTime >= DOT_POPUP_INTERVAL)
            {
                lastDotPopupTime = Time.time;
                ShowDamagePopupClientRpc(finalDamage, transform.position, false, false);
            }
        }
        else
        {
            ShowDamagePopupClientRpc(finalDamage, transform.position, info.isCritical, false);
        }

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
        
        if (IsServer)
        {
            if (!isSummonedMinion && MonsterSpawnerNetcode.Instance != null)
            {
                MonsterSpawnerNetcode.Instance.OnMonsterDead();
            }
        }


        EnemyAINetcode aiNetcode = GetComponent<EnemyAINetcode>();
        if (aiNetcode != null)
        {
            aiNetcode.HandleDeath();
        }
        else
        {
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
}