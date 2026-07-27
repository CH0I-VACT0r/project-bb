using UnityEngine;

public enum WeaponType { Strike, Slash, Ranged }

[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Game/Weapon Data")]
public class WeaponDataSO : ScriptableObject
{
    [Header("Basic Info")]
    public string weaponName;
    public WeaponType weaponType;

    [Header("Combat Stats")]
    public float baseDamage = 10f;
    public float baseCooldown = 2f;
    public float attackRange = 1f; // Strike: 반경, Slash: 부채꼴 반지름, Ranged: 투사체 사거리/수명
    public float travelDistance = 2f;
    public float travelSpeed = 15f;

    [Header("Slash Specific")]
    [Range(0f, 360f)]
    public float slashAngle = 90f; // 부채꼴 사잇각

    [Header("Ranged Specific")]
    public GameObject projectilePrefab; // 생성할 투사체 프리팹
    public float projectileSpeed = 15f;

    [Header("Orbit Settings")]
    public float orbitRadius = 0.5f;
    public float orbitSpeed = 180f;
}
