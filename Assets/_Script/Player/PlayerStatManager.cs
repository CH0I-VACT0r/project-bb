using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class PlayerStatManager : NetworkBehaviour
{
    [Header("Class Setup")]
    public PlayerClassDataSO classData;

    [Header("1. Survival Stats")]
    public NetworkVariable<float> MaxHealth = new NetworkVariable<float>(100f);
    public NetworkVariable<float> CurrentHealth = new NetworkVariable<float>(100f);
    public NetworkVariable<float> HealthRegen = new NetworkVariable<float>(1.0f);
    public NetworkVariable<float> Defense = new NetworkVariable<float>(10f);
    public NetworkVariable<float> MagicDefense = new NetworkVariable<float>(10f);
    public NetworkVariable<float> Evasion = new NetworkVariable<float>(0f);

    [Header("Shield System")]
    public NetworkVariable<float> MaxShield = new NetworkVariable<float>(50f);
    public NetworkVariable<float> CurrentShield = new NetworkVariable<float>(0f);
    public NetworkVariable<float> ShieldRegenRate = new NetworkVariable<float>(5.0f); // 초당 재생량
    public NetworkVariable<float> ShieldResetTime = new NetworkVariable<float>(5.0f); // 피격 후 재생 대기시간
    private float lastDamageTime;

    [Header("2. Combat Stats")]
    public NetworkVariable<float> AttackDamage = new NetworkVariable<float>(20f);
    public NetworkVariable<float> AbilityPower = new NetworkVariable<float>(0f);
    public NetworkVariable<float> CooldownReduction = new NetworkVariable<float>(0f); // 공격 쿨타임 감소
    public NetworkVariable<float> CritChance = new NetworkVariable<float>(5.0f); // %
    public NetworkVariable<float> CritDamage = new NetworkVariable<float>(150f); // %
    public NetworkVariable<float> PhysicalPenetration = new NetworkVariable<float>(0f);
    public NetworkVariable<float> MagicPenetration = new NetworkVariable<float>(0f);

    [Header("3. Utility Stats")]
    public NetworkVariable<float> MoveSpeed = new NetworkVariable<float>(5.0f);
    public NetworkVariable<float> Luck = new NetworkVariable<float>(1.0f);
    public NetworkVariable<float> Charisma = new NetworkVariable<float>(1.0f);

    [Header("4. Resistance (CC Duration Reduction %)")]
    public NetworkVariable<float> StatusResistance = new NetworkVariable<float>(0f);

    // 상태 이상 리스트
    public enum StatusType { Stun, Burn, Freeze, Shock, Poison, Bleed }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // SO 데이터가 연결되어 있다면 기본 스탯을 덮어씌움
            if (classData != null)
            {
                MaxHealth.Value = classData.maxHealth;
                HealthRegen.Value = classData.healthRegen;
                Defense.Value = classData.defense;
                MagicDefense.Value = classData.magicDefense;
                Evasion.Value = classData.evasion;

                MaxShield.Value = classData.maxShield;
                ShieldRegenRate.Value = classData.shieldRegenRate;
                ShieldResetTime.Value = classData.shieldResetTime;

                AttackDamage.Value = classData.attackDamage;
                AbilityPower.Value = classData.abilityPower;
                CooldownReduction.Value = classData.cooldownReduction;
                CritChance.Value = classData.critChance;
                CritDamage.Value = classData.critDamage;
                PhysicalPenetration.Value = classData.physicalPenetration;
                MagicPenetration.Value = classData.magicPenetration;

                MoveSpeed.Value = classData.moveSpeed;
                Luck.Value = classData.luck;
                Charisma.Value = classData.charisma;

                StatusResistance.Value = classData.statusResistance;
            }

            // 런타임 현재 체력/쉴드를 최대치로 초기화
            CurrentHealth.Value = MaxHealth.Value;
            CurrentShield.Value = MaxShield.Value;
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
        // 체력 재생
        if (CurrentHealth.Value < MaxHealth.Value)
            CurrentHealth.Value = Mathf.Min(CurrentHealth.Value + HealthRegen.Value * Time.deltaTime, MaxHealth.Value);


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
