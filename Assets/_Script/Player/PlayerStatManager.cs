using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class PlayerStatManager : NetworkBehaviour
{
    [Header("1. Survival Stats")]
    public NetworkVariable<float> MaxHealth = new NetworkVariable<float>(100f);
    public NetworkVariable<float> CurrentHealth = new NetworkVariable<float>(100f);
    public NetworkVariable<float> HealthRegen = new NetworkVariable<float>(1.0f);
    public NetworkVariable<float> Defense = new NetworkVariable<float>(10f);
    public NetworkVariable<float> MagicDefense = new NetworkVariable<float>(10f);
    public NetworkVariable<float> Evasion = new NetworkVariable<float>(5f);

    [Header("Shield System")]
    public NetworkVariable<float> MaxShield = new NetworkVariable<float>(50f);
    public NetworkVariable<float> CurrentShield = new NetworkVariable<float>(0f);
    public NetworkVariable<float> ShieldRegenRate = new NetworkVariable<float>(5.0f); // 초당 재생량
    public NetworkVariable<float> ShieldResetTime = new NetworkVariable<float>(5.0f); // 피격 후 재생 대기시간
    private float lastDamageTime;

    [Header("2. Resource Stats")]
    public NetworkVariable<float> MaxMana = new NetworkVariable<float>(100f);
    public NetworkVariable<float> CurrentMana = new NetworkVariable<float>(100f);
    public NetworkVariable<float> ManaRegen = new NetworkVariable<float>(2.0f);

    [Header("3. Combat Stats")]
    public NetworkVariable<float> AttackDamage = new NetworkVariable<float>(20f);
    public NetworkVariable<float> AbilityPower = new NetworkVariable<float>(0f);
    public NetworkVariable<float> AttackSpeed = new NetworkVariable<float>(0.625f); // 초당 공격 횟수
    public NetworkVariable<float> CritChance = new NetworkVariable<float>(5.0f); // %
    public NetworkVariable<float> CritDamage = new NetworkVariable<float>(150f); // %
    public NetworkVariable<float> Accuracy = new NetworkVariable<float>(100f);
    public NetworkVariable<float> PhysicalPenetration = new NetworkVariable<float>(0f);
    public NetworkVariable<float> MagicPenetration = new NetworkVariable<float>(0f);

    [Header("4. Utility Stats")]
    public NetworkVariable<float> MoveSpeed = new NetworkVariable<float>(5.0f);
    public NetworkVariable<float> Luck = new NetworkVariable<float>(1.0f);
    public NetworkVariable<float> Charisma = new NetworkVariable<float>(1.0f);

    [Header("5. Resistance (CC Duration Reduction %)")]
    public NetworkVariable<float> StatusResistance = new NetworkVariable<float>(0f);

    // 상태 이상 리스트
    public enum StatusType { Stun, Burn, Freeze, Shock, Poison, Bleed }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CurrentHealth.Value = MaxHealth.Value;
            CurrentMana.Value = MaxMana.Value;
        }
    }

    private void Update()
    {
        if (!IsServer) return;
        // 초당 재생 로직 (서버에서만 계산)
        RegenerateStats();
    }

    private void RegenerateStats()
    {
        // 체력/마나 재생
        if (CurrentHealth.Value < MaxHealth.Value)
            CurrentHealth.Value = Mathf.Min(CurrentHealth.Value + HealthRegen.Value * Time.deltaTime, MaxHealth.Value);

        if (CurrentMana.Value < MaxMana.Value)
            CurrentMana.Value = Mathf.Min(CurrentMana.Value + ManaRegen.Value * Time.deltaTime, MaxMana.Value);

        // 쉴드 재생 (마지막 피격 후 일정 시간 경과 시)
        if (Time.time - lastDamageTime >= ShieldResetTime.Value)
        {
            if (CurrentShield.Value < MaxShield.Value)
                CurrentShield.Value = Mathf.Min(CurrentShield.Value + ShieldRegenRate.Value * Time.deltaTime, MaxShield.Value);
        }
    }

    // 데미지 입었을 때 호출 (서버 전용)
    public void TakeDamage(float damage)
    {
        if (!IsServer) return;

        lastDamageTime = Time.time;
        // 쉴드 먼저 깎고 체력 깎는 로직 추가...
    }
}
