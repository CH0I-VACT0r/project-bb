using UnityEngine;

[System.Flags]
public enum WeaponTypeFlags
{
    None = 0,
    Melee = 1 << 0,      // 근접
    Ranged = 1 << 1,     // 투사체 발사
    Slash = 1 << 2,      // 부채꼴 슬래쉬
    Laser = 1 << 3,      // 레이저
    Single = 1 << 4,     // 단일
}

public enum AttackAttribute { Physical, Magic }
public enum ProjectileBehavior { Straight, Homing }

[System.Serializable]
public struct WeaponActionStep
{
    public string stepName; // 예: "1타: 원형 근접 타격"
    public WeaponTypeFlags actionTypes; // 중복 선택 가능 (예: Melee + Ranged 동시 가능)
    public float stepDelay; // 이전 타격과의 간격 (초 단위)

    [Header("Area Settings")]
    public float attackRange;
    public float slashAngle;

    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public float projectileSpeed;
    public ProjectileBehavior projectileBehavior;

    [Header("Projectile Advanced Settings")]
    public float projectileRange; // 투사체 사정거리 (예: 10f)
    public int projectileCount;   // 분사 개수 (기본값 1)
    public float spreadAngle;     // 분사 각도 (예: 120f)
    public int burstCount;        // 연사 개수 (기본값 1)
    public float burstInterval;

    [Header("Impact Settings")]
    public float knockbackForce;
}

[CreateAssetMenu(fileName = "New Weapon", menuName = "ScriptableObjects/WeaponData")]
public class WeaponDataSO : ScriptableObject
{
    public string weaponName;
    public AttackAttribute attackAttribute;

    [Header("Targeting Settings")]
    public bool isBossPriority = true; // 보스 우선 타격 여부
    public float autoTargetRange = 10f; // 오토 타겟 탐색 반경

    [Header("Damage & Scaling")]
    public float baseDamage = 10f;
    public float adScaling = 1.0f;
    public float apScaling = 0.0f;
    public float baseCooldown = 1.0f;

    [Header("Combo / Multi-Step Actions")]
    public WeaponActionStep[] actionSteps;
    public float comboWindow = 1.0f;

    [Header("Orbit (Passive Settings)")]
    public float orbitRadius = 2f;
    public float orbitSpeed = 120f;

    [Header("Weapon Movement (Attack/Return)")]
    public float travelDistance = 1f;
    public float travelSpeed = 10f;
}