using System;
using Unity.Netcode;
using UnityEngine;

// 무기가 타겟에게 전달할 모든 전투 정보를 담는 구조체
public struct DamageInfo : INetworkSerializable
{
    public float damageAmount;
    public AttackAttribute attackType;
    public float attackerAccuracy;
    public float attackerPenetration;

    // 물리 넉백 관련
    public Vector3 knockbackDir;
    public float knockbackForce;
    public bool isCritical;

    [Header("Status & Elements")]
    public ElementFlags elementTypes; // 다중 속성 부여 가능
    public float elementBuildUp;      // 속성 축적치 (스택 부여량)
    public float elementDotDamage;    // 시전자의 스탯에 비례해 계산된 도트 틱당 피해량
    public StatusEffectFlags directStatusEffects;
    public ulong attackerNetworkId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref elementTypes);
        serializer.SerializeValue(ref elementBuildUp);
        serializer.SerializeValue(ref elementDotDamage);
        serializer.SerializeValue(ref directStatusEffects);
        serializer.SerializeValue(ref attackerNetworkId);
    }
}

public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}

[Flags]
public enum ElementFlags
{
    None = 0,
    Fire = 1 << 0,
    Poison = 1 << 1,
    Bleed = 1 << 2,
    Frost = 1 << 3,
    Shock = 1 << 4
}

[Flags]
public enum StatusEffectFlags
{
    None = 0,
    Stun = 1 << 0,
    Slow = 1 << 1,
    Taunt = 1 << 2,
    Fear = 1 << 3,
    Vulnerable = 1 << 4 // 받는 피해 증가
}