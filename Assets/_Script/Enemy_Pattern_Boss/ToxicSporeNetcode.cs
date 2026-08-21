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

    // 장판 안에 있는 플레이어들을 추적하여, 장판 소멸 시 안전하게 복구하기 위한 리스트
    private HashSet<Collider2D> playersInZone = new HashSet<Collider2D>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            StartCoroutine(LifeTimeRoutine());
        }
    }

    private IEnumerator LifeTimeRoutine()
    {
        yield return new WaitForSeconds(lifeTime);
        if (IsServer && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }

    // 트리거(장판)에 들어왔을 때
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            playersInZone.Add(other);
            ApplySlow(other, true);
        }
    }

    // 트리거(장판)에서 나갔을 때
    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player") && playersInZone.Contains(other))
        {
            playersInZone.Remove(other);
            ApplySlow(other, false);
        }
    }

    // 장판 수명이 다해 강제 소멸될 때 (OnTriggerExit이 호출되지 않는 상황 대비)
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

    // 실제 플레이어 스크립트에 접근하여 속도를 제어하는 함수
    private void ApplySlow(Collider2D playerCol, bool isSlowed)
    {
        var statusManager = playerCol.GetComponent<StatusEffectManagerNetcode>();

        if (statusManager != null)
        {
            float currentMultiplier = isSlowed ? slowMultiplier : 1.0f;
            statusManager.moveSpeedMultiplier.Value = currentMultiplier;

            Debug.Log($"[서버] {playerCol.name}에게 둔화 장판 적용: {isSlowed} (현재 배율: {currentMultiplier})");
        }
    }
}