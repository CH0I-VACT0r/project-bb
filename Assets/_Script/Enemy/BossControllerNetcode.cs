using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class BossControllerNetcode : NetworkBehaviour
{
    [Header("AI & Stat References")]
    public EnemyAINetcode baseAI;
    public EnemyStatManager statManager;

    [Header("Phase Settings")]
    [Tooltip("페이즈 2 진입 체력 비율 (0.5 = 50%)")]
    public float phase2Threshold = 0.5f;
    private int currentPhase = 1;
    private float initialMaxHP;

    [Header("Phase 1 Patterns")]
    public BossPatternBase[] phase1Patterns;

    [Header("Phase 2 Patterns")]
    public BossPatternBase[] phase2Patterns;

    private bool isExecutingPattern = false;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (baseAI == null) baseAI = GetComponent<EnemyAINetcode>();
        if (statManager == null) statManager = GetComponent<EnemyStatManager>();

        if (baseAI != null)
        {
            baseAI.canAutoAttack = false;
        }

        StartCoroutine(InitializeBossRoutine());
    }

    private IEnumerator InitializeBossRoutine()
    {
        // 스탯 스케일링이 적용될 때까지 잠시 대기
        yield return new WaitForSeconds(0.5f);
        initialMaxHP = statManager.currentHP.Value;

        // 패턴 루프 시작
        StartCoroutine(BossCombatRoutine());
    }

    private IEnumerator BossCombatRoutine()
    {
        yield return new WaitForSeconds(2f);

        while (true)
        {
            if (statManager.currentHP.Value <= 0) yield break;

            CheckPhaseTransition();

            if (!isExecutingPattern && baseAI.TargetPlayer != null)
            {
                BossPatternBase selectedPattern = SelectRandomPattern();
                if (selectedPattern != null)
                {
                    isExecutingPattern = true;

                    selectedPattern.ExecutePattern(baseAI.TargetPlayer, () =>
                    {
                        StartCoroutine(PatternCooldownRoutine(selectedPattern.patternCooldown));
                    });
                }
            }
            yield return null;
        }
    }

    private void CheckPhaseTransition()
    {
        if (currentPhase == 1 && initialMaxHP > 0)
        {
            float hpPercent = statManager.currentHP.Value / initialMaxHP;
            if (hpPercent <= phase2Threshold)
            {
                currentPhase = 2;
                Debug.Log("[서버] 보스 체력 50% 이하! 2페이즈 패턴 개방.");
                // 필요 시 페이즈 전환 애니메이션 트리거 호출
            }
        }
    }

    private BossPatternBase SelectRandomPattern()
    {
        BossPatternBase[] available = (currentPhase == 1) ? phase1Patterns : phase2Patterns;
        if (available == null || available.Length == 0) return null;

        // 가중치(Weight) 기반 랜덤 룰렛 로직
        float totalWeight = 0f;
        foreach (var p in available) totalWeight += p.patternWeight;

        float randomVal = Random.Range(0f, totalWeight);
        float cursor = 0f;

        foreach (var p in available)
        {
            cursor += p.patternWeight;
            if (randomVal <= cursor) return p;
        }

        return available[0];
    }

    private IEnumerator PatternCooldownRoutine(float cooldown)
    {
        yield return new WaitForSeconds(cooldown);
        isExecutingPattern = false;
    }
}
