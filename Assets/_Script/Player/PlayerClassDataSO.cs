using UnityEngine;

[CreateAssetMenu(fileName = "New Player Class", menuName = "ScriptableObjects/PlayerClassData")]
public class PlayerClassDataSO : ScriptableObject
{
    public string className = "Warrior"; // 직업 이름

    [Header("1. Survival Stats")]
    public float maxHealth = 100f;
    public float healthRegen = 1.0f;
    public float defense = 10f;
    public float magicDefense = 10f;
    public float evasion = 0f;

    [Header("Shield System")]
    public float maxShield = 50f;
    public float shieldRegenRate = 5.0f;
    public float shieldResetTime = 5.0f;

    [Header("2. Combat Stats")]
    public float attackDamage = 20f;
    public float abilityPower = 0f;
    public float cooldownReduction = 0f;
    public float critChance = 5.0f;
    public float critDamage = 150f;
    public float physicalPenetration = 0f;
    public float magicPenetration = 0f;

    [Header("3. Utility Stats")]
    public float moveSpeed = 5.0f;
    public float luck = 1.0f;
    public float charisma = 1.0f;

    [Header("4. Resistance")]
    public float statusResistance = 0f;
}