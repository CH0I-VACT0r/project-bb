using System.Collections;
using Unity.Netcode;
using UnityEngine;

public enum EnemyAttackType { ConeSlash, Dash, Projectile }

public class EnemyStatManager : NetworkBehaviour, IDamageable
{
    [Header("Enemy Stats")]
    public float maxHP = 50f;
    public NetworkVariable<float> currentHP = new NetworkVariable<float>();

    [Header("Defense Stats")]
    public float defense = 1f; 
    public float magicDefense = 1f;
    public float evasion = 0f;      
    
    [Header("Offense Stats")]
    public AttackAttribute attackAttribute = AttackAttribute.Physical;
    public float accuracy = 100f;
    public float physicalPenetration = 0f;
    public float magicPenetration = 0f;

    [Header("Attack Pattern Config")]
    public EnemyAttackType attackType;

    private Rigidbody2D rb;
    public bool isStunned = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHP.Value = maxHP;
        }
    }

    public void TakeDamage(DamageInfo info)
    {
        if (!IsServer) return;

        float effectiveEvasion = Mathf.Max(0f, evasion - info.attackerAccuracy);
        if (Random.Range(0f, 100f) < effectiveEvasion)
        {
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

        currentHP.Value -= finalDamage;

        if (rb != null && info.knockbackForce > 0f)
        {
            StartCoroutine(ApplyKnockbackRoutine(info.knockbackDir, info.knockbackForce));
        }

        if (currentHP.Value <= 0)
        {
            Die();
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

    private void Die()
    {
        NetworkObject.Despawn(false);
        EnemyPoolManager.Instance.ReturnEnemy(this.gameObject);
    }
}