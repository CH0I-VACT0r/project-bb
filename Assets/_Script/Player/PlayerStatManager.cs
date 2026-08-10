using UnityEngine;
using Unity.Netcode;
using System;
using UnityEngine.SceneManagement;

public class PlayerStatManager : NetworkBehaviour, IDamageable
{
    [Header("Class Setup")]
    public PlayerClassDataSO classData;

    [Header("Weapon System")]
    public WeaponControllerNetcode weaponController;

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

    [Header("4. Elemental Offense")]
    public NetworkVariable<float> BonusFireDamage = new NetworkVariable<float>(0f);
    public NetworkVariable<float> BonusPoisonDamage = new NetworkVariable<float>(0f);
    public NetworkVariable<float> BonusBleedDamage = new NetworkVariable<float>(0f);
    public NetworkVariable<float> BonusSlowEffect = new NetworkVariable<float>(0f);
    public NetworkVariable<float> BonusVulnerableEffect = new NetworkVariable<float>(0f);

    [Header("5. Duration Modifiers")]
    public NetworkVariable<float> BonusStunDuration = new NetworkVariable<float>(0f);
    public NetworkVariable<float> BonusSlowDuration = new NetworkVariable<float>(0f);
    public NetworkVariable<float> BonusTauntDuration = new NetworkVariable<float>(0f);
    public NetworkVariable<float> BonusFearDuration = new NetworkVariable<float>(0f);
    public NetworkVariable<float> BonusVulnerableDuration = new NetworkVariable<float>(0f);
    public NetworkVariable<float> BonusFireDuration = new NetworkVariable<float>(0f);
    public NetworkVariable<float> BonusPoisonDuration = new NetworkVariable<float>(0f);
    public NetworkVariable<float> BleedDecayReduction = new NetworkVariable<float>(0f);

    [Header("6. Advanced Defenses")]
    public NetworkVariable<int> CcResistanceStat = new NetworkVariable<int>(0);
    public NetworkVariable<int> DotDamageResistanceStat = new NetworkVariable<int>(0);
    public NetworkVariable<int> ElementalResistanceStat = new NetworkVariable<int>(0);

    public event Action OnShieldBroken;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (classData != null && classData.classAnimator != null)
        {
            Animator anim = GetComponent<Animator>();
            if (anim != null)
            {
                anim.runtimeAnimatorController = classData.classAnimator;
            }

        }
        if (IsOwner)
        {
            if (PlayerHUDController.Instance != null)
            {
                PlayerHUDController.Instance.BindLocalPlayer(this);
            }
            else
            {
                Debug.LogError("PlayerHUDController 인스턴스를 찾을 수 없습니다.");
            }

            InitializeClassWeapon();
            AssignCameraTarget();
            AssignPlayerUI();
        }

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

                BonusFireDamage.Value = classData.bonusFireDamage;
                BonusPoisonDamage.Value = classData.bonusPoisonDamage;
                BonusBleedDamage.Value = classData.bonusBleedDamage;
                BonusSlowEffect.Value = classData.bonusSlowEffect;
                BonusVulnerableEffect.Value = classData.bonusVulnerableEffect;

                BonusStunDuration.Value = classData.bonusStunDuration;
                BonusSlowDuration.Value = classData.bonusSlowDuration;
                BonusTauntDuration.Value = classData.bonusTauntDuration;
                BonusFearDuration.Value = classData.bonusFearDuration;
                BonusVulnerableDuration.Value = classData.bonusVulnerableDuration;
                BonusFireDuration.Value = classData.bonusFireDuration;
                BonusPoisonDuration.Value = classData.bonusPoisonDuration;
                BleedDecayReduction.Value = classData.bleedDecayReduction;

                CcResistanceStat.Value = classData.ccResistanceStat;
                DotDamageResistanceStat.Value = classData.dotDamageResistanceStat;
                ElementalResistanceStat.Value = classData.elementalResistanceStat;
            }

            // 런타임 현재 체력/쉴드를 최대치로 초기화
            CurrentHealth.Value = MaxHealth.Value;
            CurrentShield.Value = MaxShield.Value;

            StatusEffectManagerNetcode statusManager = GetComponent<StatusEffectManagerNetcode>();
            if (statusManager != null)
            {
                statusManager.bonusFireDamage = BonusFireDamage.Value;
                statusManager.bonusPoisonDamage = BonusPoisonDamage.Value;
                statusManager.bonusBleedDamage = BonusBleedDamage.Value;
                statusManager.bonusSlowEffect = BonusSlowEffect.Value;
                statusManager.bonusVulnerableEffect = BonusVulnerableEffect.Value;

                statusManager.bonusStunDuration = BonusStunDuration.Value;
                statusManager.bonusSlowDuration = BonusSlowDuration.Value;
                statusManager.bonusTauntDuration = BonusTauntDuration.Value;
                statusManager.bonusFearDuration = BonusFearDuration.Value;
                statusManager.bonusVulnerableDuration = BonusVulnerableDuration.Value;
                statusManager.bonusFireDuration = BonusFireDuration.Value;
                statusManager.bonusPoisonDuration = BonusPoisonDuration.Value;
                statusManager.bleedDecayReduction = BleedDecayReduction.Value;

                statusManager.ccResistanceStat = CcResistanceStat.Value;
                statusManager.dotDamageResistanceStat = DotDamageResistanceStat.Value;
                statusManager.elementalResistanceStat = ElementalResistanceStat.Value;
            }
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsOwner && weaponController != null)
        {
            AssignCameraTarget();
            AssignPlayerUI();

            if (scene.name == "LobbyScene")
            {
                weaponController.SetLobbyMode(true);
            }
            else
            {
                weaponController.SetLobbyMode(false);
            }
        }
    }

    private void AssignCameraTarget()
    {
        if (Camera.main != null)
        {
            CameraFollow camFollow = Camera.main.GetComponent<CameraFollow>();
            if (camFollow != null)
            {
                camFollow.target = this.transform;
            }
        }
    }

    private void AssignPlayerUI()
    {
        PlayerHUDController hud = FindFirstObjectByType<PlayerHUDController>();

        if (hud != null)
        {
            // 수정: BindPlayer -> BindLocalPlayer (사용자님의 실제 함수명과 일치시킴)
            hud.BindLocalPlayer(this);
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

    private void InitializeClassWeapon()
    {
        if (classData != null && classData.defaultWeapon != null)
        {
            if (weaponController != null)
            {
                weaponController.EquipWeapon(classData.defaultWeapon);

                if (SceneManager.GetActiveScene().name == "LobbyScene")
                {
                    weaponController.SetLobbyMode(true);
                }
                else
                {
                    weaponController.SetLobbyMode(false);
                }
            }
            else
            {
                Debug.LogError("PlayerStatManager에 weaponController 참조가 빠져있습니다!");
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

        float effectiveEvasion = Mathf.Max(0f, Evasion.Value - info.attackerAccuracy);
        if (UnityEngine.Random.Range(0f, 100f) < effectiveEvasion)
        {
            return;
        }

        lastDamageTime = Time.time;

        // 2. 쉴드 연산 확인
        if (CurrentShield.Value >= 1f)
        {
            CurrentShield.Value -= 1f;
            OnShieldBroken?.Invoke();
            GrantInvincibility(0.2f);
            return; // 쉴드가 있으면 여기서 끝남 (체력 안 깎임)
        }

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

        // 3. 실제 체력 차감 확인
        finalDamage = Mathf.Round(finalDamage);
        CurrentHealth.Value -= finalDamage;

        GrantInvincibility(0.2f);

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
        GetComponent<Collider2D>().enabled = false;            // 캐릭터 조작 및 충돌체 비활성화 (서버/클라이언트 공통)
        GetComponent<PlayerMovementNetcode>().enabled = false; // 플레이어의 이동 비활성화

        // 내 화면(로컬)의 카메라 타겟 변경 (관전 모드)
        if (IsOwner)
        {
            SwitchCameraToSpectatorMode();
        }
    }

    private void SwitchCameraToSpectatorMode()
    {
        if (Camera.main == null) return;
        CameraFollow camFollow = Camera.main.GetComponent<CameraFollow>();
        if (camFollow == null) return;

        PlayerStatManager[] allPlayers = FindObjectsByType<PlayerStatManager>(FindObjectsSortMode.None);

        foreach (var player in allPlayers)
        {
            if (player != this && player.CurrentHealth.Value > 0)
            {
                camFollow.target = player.transform;
                Debug.Log($"{player.gameObject.name} 플레이어 관전 시작");
                return;
            }
        }

        Debug.Log("살아있는 다른 파티원이 없어 관전할 수 없습니다.");
        // TODO: 파티 전멸 시 '실패(Game Over)' 로비 귀환 로직을 이 부분에 추가할 수 있습니다.
    }
}
