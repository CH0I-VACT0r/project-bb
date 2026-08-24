using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ToxicSporeNetcode : NetworkBehaviour
{
    [Header("Spore Settings")]
    public float lifeTime = 5f;
    [Tooltip("적용할 이동 속도 배율 (예: 0.5 = 50% 느려짐)")]
    public float slowMultiplier = 0.5f;

    [Header("Animation Settings")]
    [Tooltip("소멸 애니메이션이 재생되는 시간 (이 시간만큼 먼저 애니메이션이 시작됨)")]
    public float fadeOutDuration = 0.5f;
    [Tooltip("애니메이터에 설정할 소멸 트리거 이름")]
    public string fadeOutTriggerName = "FadeOut";

    private HashSet<Collider2D> playersInZone = new HashSet<Collider2D>();
    private Animator anim;

    private void Awake()
    {
        anim = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            StartCoroutine(LifeTimeRoutine());
        }
    }

    private IEnumerator LifeTimeRoutine()
    {
        // 1. 소멸 애니메이션이 시작되기 전까지 대기
        float waitTime = Mathf.Max(0f, lifeTime - fadeOutDuration);
        yield return new WaitForSeconds(waitTime);

        // 2. 소멸 애니메이션 재생 시점 도달: 모든 클라이언트에 트리거 발동 지시
        if (!string.IsNullOrEmpty(fadeOutTriggerName))
        {
            TriggerFadeOutClientRpc(fadeOutTriggerName);
        }

        // 3. 애니메이션이 재생되는 시간만큼 추가 대기
        yield return new WaitForSeconds(fadeOutDuration);

        // 4. 수명이 완전히 끝났으므로 서버 권위 하에 실제 오브젝트 파괴
        if (IsServer && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }

    [ClientRpc]
    private void TriggerFadeOutClientRpc(string triggerName)
    {
        if (anim != null)
        {
            anim.SetTrigger(triggerName);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            playersInZone.Add(other);
            ApplySlow(other, true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player") && playersInZone.Contains(other))
        {
            playersInZone.Remove(other);
            ApplySlow(other, false);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            foreach (var playerCol in playersInZone)
            {
                if (playerCol != null)
                {
                    ApplySlow(playerCol, false);
                }
            }
            playersInZone.Clear();
        }
    }

    private void ApplySlow(Collider2D playerCol, bool isSlowed)
    {
        var statusManager = playerCol.GetComponent<StatusEffectManagerNetcode>();

        if (statusManager != null)
        {
            float currentMultiplier = isSlowed ? slowMultiplier : 1.0f;
            statusManager.moveSpeedMultiplier.Value = currentMultiplier;
        }
    }
}