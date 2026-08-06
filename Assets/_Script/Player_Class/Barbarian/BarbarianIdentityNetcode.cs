using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(PlayerStatManager), typeof(StatusEffectManagerNetcode))]
public class BarbarianIdentityNetcode : NetworkBehaviour
{
    private PlayerStatManager statManager;
    private StatusEffectManagerNetcode statusManager;

    [Header("Brain Cells (뇌 세포)")]
    public float baseMaxBrainCells = 100f;
    public float apToBrainCellMultiplier = 5f; // AP 1당 증가하는 뇌 세포 최대치
    public float brainCellDecayRate = 10f;     // 초당 감소량

    public NetworkVariable<float> MaxBrainCells = new NetworkVariable<float>(100f);
    public NetworkVariable<float> CurrentBrainCells = new NetworkVariable<float>(0f);

    [Header("Awakening (각성)")]
    public float baseAwakeningTime = 45f;      // 각성에 필요한 기본 대기 시간
    public float adToTimeReduction = 0.1f;     // AD 1당 감소하는 시간 (초)
    public float awakeningDuration = 5f;       // 각성 유지 시간

    public NetworkVariable<float> RequiredAwakeningTime = new NetworkVariable<float>(10f);
    public NetworkVariable<float> CurrentAwakeningProgress = new NetworkVariable<float>(0f);
    public NetworkVariable<bool> IsAwakened = new NetworkVariable<bool>(false);

    private float noBrainCellUseTimer = 0f;
    private float awakeningTimer = 0f;

    private void Awake()
    {
        statManager = GetComponent<PlayerStatManager>();
        statusManager = GetComponent<StatusEffectManagerNetcode>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // AP와 AD 기반으로 최대치 및 필요 시간 초기화
            MaxBrainCells.Value = baseMaxBrainCells + (statManager.AbilityPower.Value * apToBrainCellMultiplier);
            RequiredAwakeningTime.Value = Mathf.Max(2f, baseAwakeningTime - (statManager.AttackDamage.Value * adToTimeReduction));
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        HandleBrainCells();
        HandleAwakening();
    }

    // 외부(스킬 매니저)에서 스킬 사용 시 호출할 함수
    public void UseBrainCells(float amount)
    {
        if (!IsServer || IsAwakened.Value) return;

        CurrentBrainCells.Value += amount;
        noBrainCellUseTimer = 0f; // 뇌 세포를 사용했으므로 각성 타이머 초기화

        // 뇌 과부하 (Stun) 검사
        if (CurrentBrainCells.Value >= MaxBrainCells.Value)
        {
            TriggerBrainOverload();
        }
    }

    private void HandleBrainCells()
    {
        // 일정 시간 후 뇌 세포 감소 로직
        if (CurrentBrainCells.Value > 0)
        {
            CurrentBrainCells.Value = Mathf.Max(0, CurrentBrainCells.Value - (brainCellDecayRate * Time.deltaTime));
        }

        // TODO: 사용한 뇌 세포 절대치에 비례하여 스킬 가속 및 크리티컬 확률 증가 로직 추가
    }

    private void TriggerBrainOverload()
    {
        CurrentBrainCells.Value = 0f;
        // StatusEffectManagerNetcode를 통해 2초 기절 부여 (CC 저항 적용됨)
        DamageInfo overloadInfo = new DamageInfo { directStatusEffects = StatusEffectFlags.Stun };
        statusManager.ApplyStatusEffects(overloadInfo);
    }

    private void HandleAwakening()
    {
        if (IsAwakened.Value)
        {
            // 각성 유지
            awakeningTimer -= Time.deltaTime;
            if (awakeningTimer <= 0f)
            {
                EndAwakening();
            }
        }
        else
        {
            // 뇌 세포를 사용하지 않는 동안 각성 게이지 차오름
            if (CurrentBrainCells.Value <= 0)
            {
                noBrainCellUseTimer += Time.deltaTime;
                CurrentAwakeningProgress.Value = Mathf.Clamp(noBrainCellUseTimer, 0, RequiredAwakeningTime.Value);

                if (noBrainCellUseTimer >= RequiredAwakeningTime.Value)
                {
                    StartAwakening();
                }
            }
        }
    }

    private void StartAwakening()
    {
        IsAwakened.Value = true;
        awakeningTimer = awakeningDuration;
        noBrainCellUseTimer = 0f;
        CurrentAwakeningProgress.Value = 0f;

        // TODO: AD 비례 회피율 증가 및 쿨타임 대폭 감소 버프 적용
        // statManager.Evasion.Value += 50f;
    }

    private void EndAwakening()
    {
        IsAwakened.Value = false;
        // TODO: 버프 해제
    }
}