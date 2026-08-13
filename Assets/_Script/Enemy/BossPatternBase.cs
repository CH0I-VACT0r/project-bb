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

    protected Action onPatternComplete;
    protected Transform currentTarget;

    // BossController가 패턴을 시작할 때 호출하는 진입점
    public virtual void ExecutePattern(Transform target, Action onComplete)
    {
        currentTarget = target;
        onPatternComplete = onComplete;

        // 공통 로직: 지정된 애니메이션 트리거가 있다면 모든 클라이언트에게 재생 명령 (서버 -> 클라이언트)
        if (!string.IsNullOrEmpty(animatorTriggerName))
        {
            PlayPatternAnimationClientRpc(animatorTriggerName);
        }

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

    // 개별 패턴 스크립트(자식 클래스)에서 반드시 구현해야 하는 실제 기믹
    protected abstract void OnPatternStart();

    // 자식 클래스에서 돌진이나 투사체 발사가 완전히 끝났을 때 반드시 호출해야 하는 함수
    protected virtual void FinishPattern()
    {
        onPatternComplete?.Invoke();
    }
}
