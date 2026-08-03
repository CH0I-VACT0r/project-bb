using UnityEngine;
using Unity.Netcode;
using System;

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
    private float invincibilityEndTime = 0f;

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

    public event Action OnShieldBroken;

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
        if (CurrentShield.Value < MaxShield.Value)
        {
            if (Time.time - lastDamageTime >= ShieldResetTime.Value)
            {
                CurrentShield.Value += 1f; // 쉴드 1회 복구
                lastDamageTime = Time.time; // 다중 스택일 경우 다음 1스택 충전을 위해 시간 갱신
            }
        }
    }

    public void GrantInvincibility(float duration)
    {
        if (!IsServer) return;
        invincibilityEndTime = Mathf.Max(invincibilityEndTime, Time.time + duration);
    }

    // 데미지 입었을 때 호출 (서버 전용)
    public void TakeDamage(DamageInfo info)
    {
        if (!IsServer) return;

        if (Time.time < invincibilityEndTime)
        {
            return;
        }

        // 회피 연산
        float effectiveEvasion = Mathf.Max(0f, Evasion.Value - info.attackerAccuracy);
        if (UnityEngine.Random.Range(0f, 100f) < effectiveEvasion)
        {
            return;
        }

        // 회피 실패 시 쉴드 재생 대기시간 초기화
        lastDamageTime = Time.time;

        // 쉴드 연산
        if (CurrentShield.Value >= 1f)
        {
            CurrentShield.Value -= 1f; // 쉴드 1 스택 차감
            OnShieldBroken?.Invoke(); // 쉴드 파괴 이벤트 발생
            GrantInvincibility(0.2f);  // 쉴드 파괴 후 무적 0.2초 부여

            return;
        }

        // 방어력 및 관통 연산
        float finalDamage = info.damageAmount;

        if (info.attackType == AttackAttribute.Physical)
        {
            float effectiveDefense = Mathf.Max(0f, Defense.Value - info.attackerPenetration);
            finalDamage = Mathf.Max(1f, finalDamage - effectiveDefense);
        }
        else if (info.attackType == AttackAttribute.Magic)
        {
            float effectiveMagicDefense = Mathf.Max(0f, MagicDefense.Value - info.attackerPenetration);
            finalDamage = Mathf.Max(1f, finalDamage - effectiveMagicDefense);
        }

        // 최종 체력 차감
        CurrentHealth.Value -= finalDamage;

        GrantInvincibility(0.2f); // 피격 무적

        if (CurrentHealth.Value <= 0)
        {
            Die();
        }
    }

    #region Healing System (서버 전용)
    // 1. 고정 수치 회복
    public void HealFixed(float amount)
    {
        if (!IsServer || CurrentHealth.Value <= 0) return;

        CurrentHealth.Value = Mathf.Min(CurrentHealth.Value + amount, MaxHealth.Value);
        Debug.Log($"고정 회복: {amount}. 현재 체력: {CurrentHealth.Value}");
    }

    // 2. 최대 체력 비례(%) 회복
    public void HealPercentage(float percent)
    {
        if (!IsServer || CurrentHealth.Value <= 0) return;

        float amount = MaxHealth.Value * (percent / 100f);
        CurrentHealth.Value = Mathf.Min(CurrentHealth.Value + amount, MaxHealth.Value);
        Debug.Log($"비율 회복: {percent}%. 회복량: {amount}. 현재 체력: {CurrentHealth.Value}");
    }
    #endregion

    private void Die()
    {
        Debug.Log("플레이어 사망!");
        // TODO: 사망 연출, 게임 오버 처리 등
    }
}
