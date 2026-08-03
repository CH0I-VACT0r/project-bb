using UnityEngine;

// 무기가 타겟에게 전달할 모든 전투 정보를 담는 구조체
public struct DamageInfo
{
    public float damageAmount;
    public AttackAttribute attackType;
    public float attackerAccuracy;
    public float attackerPenetration;

    // 물리 넉백 관련
    public Vector3 knockbackDir;
    public float knockbackForce;
}

public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}