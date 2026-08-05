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

    [Header("Effect Modifiers (확장성: 아이템/버프 획득 시 이 값들을 증가시킴)")]
    public float bonusFireDamage = 0f;       // 화염 피해 증가치 (0.2 = 20% 증가)
    public float bonusPoisonDamage = 0f;
    public float bonusBleedDamage = 0f;
    public float bonusSlowEffect = 0f;       // 슬로우 추가 둔화량 (0.1 = 10% 더 느려짐)
    public float bonusVulnerableEffect = 0f;
    
    public float bonusStunDuration = 0f;
    public float bonusSlowDuration = 0f;
    public float bonusTauntDuration = 0f;
    public float bonusFearDuration = 0f;
    public float bonusVulnerableDuration = 0f;

    public float bonusFireDuration = 0f;
    public float bonusPoisonDuration = 0f;
    public float bleedDecayReduction = 0f;

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
        if ((types & ElementFlags.Fire) != 0)
        {
            curFire += amount;
            RefreshDoT(ref fireDotCor, FireDoTRoutine(baseDotDamage));
            if (curFire >= maxFire) TriggerFireExplosion(baseDotDamage);
        }

        if ((types & ElementFlags.Poison) != 0)
        {
            curPoison += amount;
            RefreshDoT(ref poisonDotCor, PoisonDoTRoutine(baseDotDamage));
            if (curPoison >= maxPoison) SetVulnerable(true, 5f);
        }

        if ((types & ElementFlags.Bleed) != 0)
        {
            curBleed += amount;
            RefreshDoT(ref bleedDotCor, BleedDoTRoutine(baseDotDamage));
        }

        if ((types & ElementFlags.Frost) != 0)
        {
            curFrost += amount;
            ApplySlow(0.25f, 3f); // 기본 25% 슬로우
            if (curFrost >= maxFrost) TriggerFrostStun();
        }

        if ((types & ElementFlags.Shock) != 0)
        {
            curShock += amount;
            ApplySlow(0.25f, 3f); // 기본 25% 슬로우
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
            float finalDmg = baseDmg * (1f + bonusFireDamage);
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
            float finalDmg = baseDmg * (1f + bonusPoisonDamage);
            ApplyDoTDamage(finalDmg, ElementFlags.Poison);
        }
        curPoison = 0f;
    }

    private IEnumerator BleedDoTRoutine(float baseDmg)
    {
        while (curBleed > 0)
        {
            yield return new WaitForSeconds(1f);

            // 데미지 산출 (기존 동일)
            float bleedDmg = (baseDmg + (curBleed * 0.1f)) * (1f + bonusBleedDamage);
            ApplyDoTDamage(bleedDmg, ElementFlags.Bleed);

            // 비율 기반 스택 붕괴 연산
            // 기본 20%(0.2f) 감소에서 bleedDecayReduction을 뺀 최종 감소율 계산
            // 감소율이 0% 이하가 되는 것을 막기 위해 최소 1%(0.01f) 하한선 적용
            float finalDecayRate = Mathf.Max(0.01f, 0.20f - bleedDecayReduction);

            // 현재 스택의 퍼센트만큼 차감
            float decayAmount = curBleed * finalDecayRate;
            curBleed -= decayAmount;

            // 무한 루프 방지용
            if (curBleed < 1f)
            {
                curBleed = 0f;
            }
        }
    }

    private void ApplyDoTDamage(float dmg, ElementFlags element)
    {
        DamageInfo dotInfo = new DamageInfo { damageAmount = dmg, elementTypes = element };
        if (statManager != null) statManager.TakeDamage(dotInfo);
    }
    #endregion

    #region Element Max Stack Triggers
    private void TriggerFireExplosion(float baseDmg)
    {
        curFire = 0f; // 스택 초기화
        float explosionDmg = baseDmg * 3f * (1f + bonusFireDamage); // 폭발은 틱뎀의 3배

        DamageInfo explosionInfo = new DamageInfo
        {
            damageAmount = explosionDmg,
            elementTypes = ElementFlags.Fire
        };

        if (statManager != null)
        {
            statManager.TakeDamage(explosionInfo);
        }
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
        if ((flags & StatusEffectFlags.Stun) != 0) StartCoroutine(StunRoutine(duration));
        if ((flags & StatusEffectFlags.Slow) != 0) ApplySlow(0.25f, duration);
        if ((flags & StatusEffectFlags.Vulnerable) != 0) SetVulnerable(true, duration);

        if (((flags & StatusEffectFlags.Taunt) != 0 || (flags & StatusEffectFlags.Fear) != 0) && attackerId != 0)
        {
            effectSourceId.Value = attackerId; // 타겟 ID 등록
            if ((flags & StatusEffectFlags.Taunt) != 0) StartCoroutine(TauntRoutine(duration));
            if ((flags & StatusEffectFlags.Fear) != 0) StartCoroutine(FearRoutine(duration));
        }
    }

    private IEnumerator StunRoutine(float baseDuration)
    {
        float finalDuration = Mathf.Max(0.1f, baseDuration + bonusStunDuration);
        isStunned.Value = true;
        yield return new WaitForSeconds(finalDuration);
        isStunned.Value = false;
    }

    private IEnumerator TauntRoutine(float baseDuration)
    {
        float finalDuration = Mathf.Max(0.1f, baseDuration + bonusTauntDuration);
        isTaunted.Value = true;
        yield return new WaitForSeconds(finalDuration);
        isTaunted.Value = false;
        effectSourceId.Value = 0;
    }

    private IEnumerator FearRoutine(float baseDuration)
    {
        float finalDuration = Mathf.Max(0.1f, baseDuration + bonusFearDuration);
        isFeared.Value = true;
        yield return new WaitForSeconds(finalDuration);
        isFeared.Value = false;
        effectSourceId.Value = 0;
    }

    private void ApplySlow(float baseSlowPercentage, float duration)
    {
        StartCoroutine(SlowRoutine(baseSlowPercentage, duration));
    }

    private IEnumerator SlowRoutine(float baseSlowPercentage, float baseDuration)
    {
        float finalDuration = Mathf.Max(0.1f, baseDuration + bonusSlowDuration);
        float totalSlow = baseSlowPercentage + bonusSlowEffect;
        moveSpeedMultiplier.Value = Mathf.Clamp(1.0f - totalSlow, 0.1f, 1.0f);

        yield return new WaitForSeconds(finalDuration);
        moveSpeedMultiplier.Value = 1.0f;
    }

    private void SetVulnerable(bool isVulnerable, float duration)
    {
        StartCoroutine(VulnerableRoutine(duration));
    }

    private IEnumerator VulnerableRoutine(float baseDuration)
    {
        float finalDuration = Mathf.Max(0.1f, baseDuration + bonusVulnerableDuration);
        damageTakenMultiplier.Value = 1.0f + 0.25f + bonusVulnerableEffect;

        yield return new WaitForSeconds(finalDuration);
        damageTakenMultiplier.Value = 1.0f;
    }
    #endregion
}