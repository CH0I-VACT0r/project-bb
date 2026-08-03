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
    public float shieldResetTime = 5.0f;

    [Header("2. Combat & Offense")]
    public float attackDamage = 20f;
    public float abilityPower = 0f;
    public float cooldownReduction = 0f;
    public float critChance = 5.0f;
    public float critDamage = 150f;

    // 신규 추가: 관통 및 명중
    public float physicalPenetration = 0f; // 물리 관통 (상대 방어력 무시)
    public float magicPenetration = 0f;    // 마법 관통 (상대 저항력 무시)
    public float accuracy = 100f;          // 명중 (상대 회피율 상쇄)

    [Header("3. Utility Stats")]
    public float moveSpeed = 5.0f;
    public float luck = 1.0f;
    public float charisma = 1.0f;

    [Header("4. Resistance")]
    public float statusResistance = 0f;
}