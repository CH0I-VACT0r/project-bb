using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class StatusEffectManagerNetcode : NetworkBehaviour
{
    private IDamageable statManager; // 범용 인터페이스 사용 (Enemy, Player 모두 호환)

    [Header("Resistances (Max Gauge)")]
    public float maxFire = 100f;
    public float maxPoison = 100f;
    public float maxBleed = 100f;
    public float maxFrost = 100f;
    public float maxShock = 100f;
    private float curFire, curPoison, curBleed, curFrost, curShock;

    [Header("Immunities (보스/엘리트 전용 면역 플래그)")]
    public ElementFlags immuneElements = ElementFlags.None;
    public StatusEffectFlags immuneCC = StatusEffectFlags.None;

    [Header("Offensive Modifiers (공격 스탯: 정수 1당 1% 증가)")]
    public float bonusFireDamage = 0f;       // 화염 피해 증가치 (0.2 = 20% 증가)
    public float bonusPoisonDamage = 0f;
    public float bonusBleedDamage = 0f;
    public float bonusSlowEffect = 0f;       // 슬로우 추가 둔화량 (0.1 = 10% 더 느려짐)
    public float bonusVulnerableEffect = 0f;

    [Header("Duration Modifiers (시간 스탯: 1.0당 1초 연장)")]
    public float bonusStunDuration = 0f;
    public float bonusSlowDuration = 0f;
    public float bonusTauntDuration = 0f;
    public float bonusFearDuration = 0f;
    public float bonusVulnerableDuration = 0f;
    public float bonusFireDuration = 0f;
    public float bonusPoisonDuration = 0f;
    public float bleedDecayReduction = 0f;

    [Header("Defensive Stats (방어 스탯: 무한 성장 가능, 점감 공식 적용)")]
    public int ccResistanceStat = 0;         // 예: 100 입력 시 -> 50% 저항, 300 입력 시 -> 75% 저항
    public int dotDamageResistanceStat = 0;
    public int elementalResistanceStat = 0;

    private const float DEFENSE_CAP_CONSTANT = 300f;
    public float CCResistancePct => ccResistanceStat / (ccResistanceStat + DEFENSE_CAP_CONSTANT);
    public float DotDamageResistancePct => dotDamageResistanceStat / (dotDamageResistanceStat + DEFENSE_CAP_CONSTANT);
    public float ElementalResistancePct => elementalResistanceStat / (elementalResistanceStat + DEFENSE_CAP_CONSTANT);

    [Header("Network Synced States")]
    public NetworkVariable<float> damageTakenMultiplier = new NetworkVariable<float>(1.0f);
    public NetworkVariable<float> moveSpeedMultiplier = new NetworkVariable<float>(1.0f);
    public NetworkVariable<bool> isStunned = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isTaunted = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isFeared = new NetworkVariable<bool>(false);
    public NetworkVariable<ulong> effectSourceId = new NetworkVariable<ulong>(0);

    private Coroutine fireDotCor, poisonDotCor, bleedDotCor;

    void Awake()
    {
        statManager = GetComponent<IDamageable>();
    }

    public void ApplyStatusEffects(DamageInfo info)
    {
        if (!IsServer) return;

        if (info.directStatusEffects != StatusEffectFlags.None)
        {
            ApplyDirectCC(info.directStatusEffects, info.attackerNetworkId);
        }

        if (info.elementTypes != ElementFlags.None && info.elementBuildUp > 0f)
        {
            HandleElementBuildup(info.elementTypes, info.elementBuildUp, info.elementDotDamage);
        }
    }

    private void HandleElementBuildup(ElementFlags types, float amount, float baseDotDamage)
    {
        float finalAmount = amount * (1f - ElementalResistancePct);
        if (finalAmount <= 0f) return;

        if ((types & ElementFlags.Fire) != 0 && (immuneElements & ElementFlags.Fire) == 0)
        {
            curFire += finalAmount;
            RefreshDoT(ref fireDotCor, FireDoTRoutine(baseDotDamage));
            if (curFire >= maxFire) TriggerFireExplosion(baseDotDamage);
        }

        if ((types & ElementFlags.Poison) != 0 && (immuneElements & ElementFlags.Poison) == 0)
        {
            curPoison += finalAmount;
            RefreshDoT(ref poisonDotCor, PoisonDoTRoutine(baseDotDamage));
            if (curPoison >= maxPoison) SetVulnerable(true, 5f);
        }

        if ((types & ElementFlags.Bleed) != 0 && (immuneElements & ElementFlags.Bleed) == 0)
        {
            curBleed += finalAmount;
            RefreshDoT(ref bleedDotCor, BleedDoTRoutine(baseDotDamage));
        }

        if ((types & ElementFlags.Frost) != 0 && (immuneElements & ElementFlags.Frost) == 0)
        {
            curFrost += finalAmount;
            ApplySlow(0.25f, 3f);
            if (curFrost >= maxFrost) TriggerFrostStun();
        }

        if ((types & ElementFlags.Shock) != 0 && (immuneElements & ElementFlags.Shock) == 0)
        {
            curShock += finalAmount;
            ApplySlow(0.25f, 3f);
            if (curShock >= maxShock) TriggerShockOverload();
        }
    }

    private void RefreshDoT(ref Coroutine dotCoroutine, IEnumerator newRoutine)
    {
        if (dotCoroutine != null) StopCoroutine(dotCoroutine);
        dotCoroutine = StartCoroutine(newRoutine);
    }

    #region DoT Routines (스탯 비례 연산 및 확장 변수 적용)
    private IEnumerator FireDoTRoutine(float baseDmg)
    {
        int totalTicks = Mathf.Max(1, 3 + Mathf.FloorToInt(bonusFireDuration));

        for (int i = 0; i < totalTicks; i++)
        {
            yield return new WaitForSeconds(1f);
            float finalDmg = baseDmg * (1f + (bonusFireDamage / 100f));
            ApplyDoTDamage(finalDmg, ElementFlags.Fire);
        }
        curFire = 0f;
    }

    private IEnumerator PoisonDoTRoutine(float baseDmg)
    {
        int totalTicks = Mathf.Max(1, 3 + Mathf.FloorToInt(bonusPoisonDuration));

        for (int i = 0; i < totalTicks; i++)
        {
            yield return new WaitForSeconds(1f);
            float finalDmg = baseDmg * (1f + (bonusPoisonDamage / 100f));
            ApplyDoTDamage(finalDmg, ElementFlags.Poison);
        }
        curPoison = 0f;
    }

    private IEnumerator BleedDoTRoutine(float baseDmg)
    {
        while (curBleed > 0)
        {
            yield return new WaitForSeconds(1f);

            float bleedDmg = (baseDmg + (curBleed * 0.1f)) * (1f + (bonusBleedDamage / 100f));
            ApplyDoTDamage(bleedDmg, ElementFlags.Bleed);
            float finalDecayRate = Mathf.Max(0.01f, 0.20f - (bleedDecayReduction / 100f));
            float decayAmount = curBleed * finalDecayRate;
            curBleed -= decayAmount;

            if (curBleed < 1f)
            {
                curBleed = 0f;
            }
        }
    }

    private void ApplyDoTDamage(float dmg, ElementFlags element)
    {
        float resistedDmg = dmg * (1f - DotDamageResistancePct);
        DamageInfo dotInfo = new DamageInfo { damageAmount = resistedDmg, elementTypes = element };
        if (statManager != null) statManager.TakeDamage(dotInfo);
    }
    #endregion

    #region Element Max Stack Triggers
    private void TriggerFireExplosion(float baseDmg)
    {
        curFire = 0f; // 스택 초기화
        float explosionDmg = baseDmg * 3f * (1f + (bonusFireDamage / 100f));

        DamageInfo explosionInfo = new DamageInfo
        {
            damageAmount = explosionDmg,
            elementTypes = ElementFlags.Fire
        };

        if (statManager != null) statManager.TakeDamage(explosionInfo);
    }

    private void TriggerFrostStun()
    {
        curFrost = 0f;
        ApplyDirectCC(StatusEffectFlags.Stun, 0, 2f);
    }

    private void TriggerShockOverload()
    {
        curShock = 0f;
        SetVulnerable(true, 2f);
    }
    #endregion

    #region CC & Modifier Control
    private void ApplyDirectCC(StatusEffectFlags flags, ulong attackerId = 0, float duration = 2f)
    {
        if ((flags & StatusEffectFlags.Stun) != 0 && (immuneCC & StatusEffectFlags.Stun) == 0)
            StartCoroutine(StunRoutine(duration));

        if ((flags & StatusEffectFlags.Slow) != 0 && (immuneCC & StatusEffectFlags.Slow) == 0)
            ApplySlow(0.25f, duration);

        if ((flags & StatusEffectFlags.Vulnerable) != 0 && (immuneCC & StatusEffectFlags.Vulnerable) == 0)
            SetVulnerable(true, duration);

        if (((flags & StatusEffectFlags.Taunt) != 0 || (flags & StatusEffectFlags.Fear) != 0) && attackerId != 0)
        {
            effectSourceId.Value = attackerId;

            if ((flags & StatusEffectFlags.Taunt) != 0 && (immuneCC & StatusEffectFlags.Taunt) == 0)
                StartCoroutine(TauntRoutine(duration));

            if ((flags & StatusEffectFlags.Fear) != 0 && (immuneCC & StatusEffectFlags.Fear) == 0)
                StartCoroutine(FearRoutine(duration));
        }
    }

    private float GetResistedDuration(float baseDuration, float bonusDuration)
    {
        float duration = (baseDuration + bonusDuration) * (1f - CCResistancePct);
        return Mathf.Max(0.1f, duration);
    }

    private IEnumerator StunRoutine(float baseDuration)
    {
        float finalDuration = GetResistedDuration(baseDuration, bonusStunDuration);
        isStunned.Value = true;
        yield return new WaitForSeconds(finalDuration);
        isStunned.Value = false;
    }

    private IEnumerator TauntRoutine(float baseDuration)
    {
        float finalDuration = GetResistedDuration(baseDuration, bonusTauntDuration);
        isTaunted.Value = true;
        yield return new WaitForSeconds(finalDuration);
        isTaunted.Value = false;
        effectSourceId.Value = 0;
    }

    private IEnumerator FearRoutine(float baseDuration)
    {
        float finalDuration = GetResistedDuration(baseDuration, bonusFearDuration);
        isFeared.Value = true;
        yield return new WaitForSeconds(finalDuration);
        isFeared.Value = false;
        effectSourceId.Value = 0;
    }

    private void ApplySlow(float baseSlowPercentage, float baseDuration)
    {
        StartCoroutine(SlowRoutine(baseSlowPercentage, baseDuration));
    }

    private IEnumerator SlowRoutine(float baseSlowPercentage, float baseDuration)
    {
        float finalDuration = GetResistedDuration(baseDuration, bonusSlowDuration);
        float totalSlow = baseSlowPercentage + (bonusSlowEffect / 100f);
        moveSpeedMultiplier.Value = Mathf.Clamp(1.0f - totalSlow, 0.1f, 1.0f);

        yield return new WaitForSeconds(finalDuration);
        moveSpeedMultiplier.Value = 1.0f;
    }

    private void SetVulnerable(bool isVulnerable, float baseDuration)
    {
        StartCoroutine(VulnerableRoutine(baseDuration));
    }

    private IEnumerator VulnerableRoutine(float baseDuration)
    {
        float finalDuration = GetResistedDuration(baseDuration, bonusVulnerableDuration);
        damageTakenMultiplier.Value = 1.0f + 0.25f + (bonusVulnerableEffect / 100f);

        yield return new WaitForSeconds(finalDuration);
        damageTakenMultiplier.Value = 1.0f;
    }
    #endregion
}