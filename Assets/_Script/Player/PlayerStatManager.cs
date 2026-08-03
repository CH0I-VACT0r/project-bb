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

    // 데미지 입었을 때 호출 (서버 전용)
    public void TakeDamage(float damageAmount, AttackAttribute attackType, float attackerAccuracy, float attackerPenetration)
    {
        if (!IsServer) return;

        // 1. 회피 연산 (Evasion vs Accuracy)
        // 플레이어의 회피율에서 공격자의 명중 수치를 뺍니다. (최소 0%)
        float effectiveEvasion = Mathf.Max(0f, Evasion.Value - attackerAccuracy);

        // 0 ~ 100 사이의 난수를 뽑아 실효 회피율보다 낮으면 회피(Miss) 성공
        if (Random.Range(0f, 100f) < effectiveEvasion)
        {
            Debug.Log("회피 성공! (Miss) 데미지를 무효화합니다.");
            return;
        }

        // 회피에 실패하여 피격이 확정되었으므로 쉴드 재생 대기시간을 초기화합니다.
        lastDamageTime = Time.time;

        // 2. 쉴드 연산 (천상의 보호막 - 1회 피해 무효화)
        if (CurrentShield.Value >= 1f)
        {
            CurrentShield.Value -= 1f; // 쉴드 1 스택 차감
            Debug.Log($"쉴드 방어 성공! 데미지가 0이 됩니다. 남은 쉴드 스택: {CurrentShield.Value}");
            return; // 쉴드가 방어했으므로 체력 연산 생략
        }

        // 3. 방어력 및 관통 연산 (고정 감소 방식)
        float finalDamage = damageAmount;

        if (attackType == AttackAttribute.Physical)
        {
            // 실효 방어력 = 플레이어 물리 방어 - 공격자 물리 관통
            float effectiveDefense = Mathf.Max(0f, Defense.Value - attackerPenetration);

            // 데미지 = 기본 데미지 - 실효 방어력 (단, 최소 데미지는 1로 고정하여 완전 면역 방지)
            finalDamage = Mathf.Max(1f, finalDamage - effectiveDefense);
        }
        else if (attackType == AttackAttribute.Magic)
        {
            // 실효 마법 저항 = 플레이어 마법 저항 - 공격자 마법 관통
            float effectiveMagicDefense = Mathf.Max(0f, MagicDefense.Value - attackerPenetration);

            finalDamage = Mathf.Max(1f, finalDamage - effectiveMagicDefense);
        }

        // 4. 최종 체력 차감
        CurrentHealth.Value -= finalDamage;
        Debug.Log($"플레이어 피격! 최종 받은 데미지: {finalDamage}, 남은 체력: {CurrentHealth.Value}");

        // 사망 판정
        if (CurrentHealth.Value <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("플레이어 사망!");
        // TODO: 사망 연출, 게임 오버 처리 등
    }
}
