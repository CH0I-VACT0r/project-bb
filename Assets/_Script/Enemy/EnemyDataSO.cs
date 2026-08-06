using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "ScriptableObjects/EnemyData")]
public class EnemyDataSO : ScriptableObject
{
    public string enemyName = "Enemy";

    [Header("Base Stats")]
    public float maxHP = 50f;
    public float moveSpeed = 3f;

    [Header("Defense & Immunities")]
    public float defense = 1f;
    public float magicDefense = 1f;
    public float evasion = 0f;

    //면역 플래그 (보스 및 엘리트용)
    public ElementFlags immuneElements = ElementFlags.None; // 예: Fire 체크 시 화염 면역
    public StatusEffectFlags immuneCC = StatusEffectFlags.None; // 예: Stun 체크 시 기절 면역

    [Header("Offense (Damage)")]
    public AttackAttribute attackAttribute = AttackAttribute.Physical;
    public float baseDamage = 10f;
    public float knockbackForce = 5f;
    public float accuracy = 100f;
    public float physicalPenetration = 0f;
    public float magicPenetration = 0f;

    [Header("Offense (Status Infliction - 플레이어에게 부여)")]
    public ElementFlags inflictElement = ElementFlags.None;
    public float elementBuildupAmount = 0f;
    public float elementDotDamage = 0f;
    public StatusEffectFlags inflictCC = StatusEffectFlags.None;

    [Header("Attack Type")]
    public EnemyAttackType attackType;

    [Header("Ranged Attack Config (원거리 전용)")]
    public float attackRange = 7f;        // 공격 사거리 (이 거리 안으로 들어오면 멈춤)
    public float attackCooldown = 2f;     // 발사 간격 (초)
    public float projectileSpeed = 10f;   // 투사체 날아가는 속도
    public int projectileCount = 1;       // 한 번에 발사할 투사체 개수 (샷건 모드)
    public float spreadAngle = 15f;       // 다중 발사 시 투사체 간의 벌어지는 각도
    public GameObject projectilePrefab;
}
