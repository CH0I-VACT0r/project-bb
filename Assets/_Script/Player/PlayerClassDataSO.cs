using UnityEngine;

[CreateAssetMenu(fileName = "New Player Class", menuName = "ScriptableObjects/PlayerClassData")]
public class PlayerClassDataSO : ScriptableObject
{
    public string className = "Warrior";

    [Header("1. Survival & Defense")]
    public float maxHealth = 100f;
    public float healthRegen = 1.0f;
    public float defense = 10f;          // 물리 방어 (고정 감소)
    public float magicDefense = 10f;     // 마법 저항 (고정 감소)
    public float evasion = 0f;           // 회피율 (%)

    [Header("Shield System")]
    public float maxShield = 0f;         // 정수형 쉴드
    public float shieldResetTime = 10.0f;

    [Header("2. Combat & Offense")]
    public float attackDamage = 20f;
    public float abilityPower = 0f;
    public float cooldownReduction = 0f;
    public float critChance = 5.0f;
    public float critDamage = 150f;
    public float physicalPenetration = 0f; // 물리 관통 (상대 방어력 무시)
    public float magicPenetration = 0f;    // 마법 관통 (상대 저항력 무시)
    public float accuracy = 100f;          // 명중 (상대 회피율 상쇄)

    [Header("3. Utility Stats")]
    public float moveSpeed = 3.0f;
    public float luck = 1.0f;
    public float charisma = 1.0f;

    [Header("4. Elemental Offense (정수 1 = 1%)")]
    public float bonusFireDamage = 0f;
    public float bonusPoisonDamage = 0f;
    public float bonusBleedDamage = 0f;
    public float bonusSlowEffect = 0f;
    public float bonusVulnerableEffect = 0f;

    [Header("5. Duration Modifiers (1.0 = 1초/1틱)")]
    public float bonusStunDuration = 0f;
    public float bonusSlowDuration = 0f;
    public float bonusTauntDuration = 0f;
    public float bonusFearDuration = 0f;
    public float bonusVulnerableDuration = 0f;
    public float bonusFireDuration = 0f;
    public float bonusPoisonDuration = 0f;
    public float bleedDecayReduction = 0f;

    [Header("6. Advanced Defenses (점감 공식)")]
    public int ccResistanceStat = 0;         // 둔화, 기절, 도발, 공포, 취약 저항
    public int dotDamageResistanceStat = 0;  // 도트 데미지 감소
    public int elementalResistanceStat = 0;  // 속성 축적치 감소
}