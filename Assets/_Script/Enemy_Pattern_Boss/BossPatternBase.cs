using Unity.Netcode;
using UnityEngine;
using System;

public abstract class BossPatternBase : NetworkBehaviour
{
    [Header("Pattern Base Settings")]
    [Tooltip("이 패턴이 끝난 후 보스가 대기할 쿨타임")]
    public float patternCooldown = 2f;
    [Tooltip("패턴 선택 가중치 (높을수록 자주 선택됨)")]
    public float patternWeight = 1f;
    [Tooltip("실행 시킬 애니메이터 트리거 이름")]
    public string animatorTriggerName;
    [Header("Warning Visuals")]
    public SpriteRenderer warningSprite;
    [Tooltip("공용 VFX 프리팹 (Generic_Hit_VFX 할당)")]
    public GameObject genericVfxPrefab;
    [Tooltip("재생할 VFX 애니메이터 컨트롤러")]
    public RuntimeAnimatorController vfxAnimatorController;
    public float vfxScaleMultiplier = 1.0f;

    protected Action onPatternComplete;
    protected Transform currentTarget;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 스폰(또는 풀에서 꺼내짐)될 때 무조건 경고 이미지를 끔
        if (IsServer)
        {
            ToggleWarningSpriteClientRpc(false);
        }
    }

    // BossController가 패턴을 시작할 때 호출하는 진입점
    public virtual void ExecutePattern(Transform target, Action onComplete)
    {
        currentTarget = target;
        onPatternComplete = onComplete;

        // 개별 로직(돌진, 장판 등) 실행
        OnPatternStart();
    }

    [ClientRpc]
    protected void PlayPatternAnimationClientRpc(string triggerName)
    {
        Animator anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetTrigger(triggerName);
        }
    }

    [ClientRpc]
    protected void ToggleWarningSpriteClientRpc(bool isOn)
    {
        if (warningSprite != null)
        {
            warningSprite.enabled = isOn;
        }
    }

    [ClientRpc]
    protected void SpawnHitVfxClientRpc(Vector2 position, float angle = 0f)
    {
        if (genericVfxPrefab != null && vfxAnimatorController != null)
        {
            // 1. 공용 껍데기 스폰
            GameObject vfx = Instantiate(genericVfxPrefab, position, Quaternion.Euler(0, 0, angle));
            vfx.transform.localScale = new Vector3(vfxScaleMultiplier, vfxScaleMultiplier, 1f);

            // 2. 내부 애니메이터 컨트롤러 덮어쓰기
            Animator anim = vfx.GetComponent<Animator>();
            if (anim != null)
            {
                anim.runtimeAnimatorController = vfxAnimatorController;
            }

            // 삭제는 VfxAutoDestroyer가 애니메이션 클립 길이에 맞춰 알아서 처리합니다.
        }
    }

    // 개별 패턴 스크립트(자식 클래스)에서 반드시 구현해야 하는 실제 기믹
    protected abstract void OnPatternStart();

    // 자식 클래스에서 돌진이나 투사체 발사가 완전히 끝났을 때 반드시 호출해야 하는 함수
    protected virtual void FinishPattern()
    {
        onPatternComplete?.Invoke();
    }
}
