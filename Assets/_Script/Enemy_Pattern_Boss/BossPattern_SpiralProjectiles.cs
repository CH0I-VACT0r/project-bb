using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System;

public class BossPattern_SpiralProjectiles : BossPatternBase
{
    [Header("Spiral Projectile Settings")]
    public EnemyDataSO patternData;

    [Header("Pattern Shape")]
    [Min(1)] public int totalArms = 1;
    public int projectilesPerArm = 30;
    public float spawnInterval = 0.1f;
    public float angleIncrement = 15f;
    public bool isClockwise = false;

    public float postPatternDelay = 1.0f;

    protected override void OnPatternStart()
    {
        if (!IsServer) return;

        if (!string.IsNullOrEmpty(animatorTriggerName))
        {
            PlayPatternAnimationClientRpc(animatorTriggerName);
        }

        StartCoroutine(SpiralAttackRoutine());
    }

    private IEnumerator SpiralAttackRoutine()
    {
        int randomMultiplier = UnityEngine.Random.Range(0, 72);
        float currentAngle = randomMultiplier * 5f;

        float directionMultiplier = isClockwise ? -1f : 1f;
        float finalAngleIncrement = angleIncrement * directionMultiplier;

        float armSpacing = 360f / Mathf.Max(1, totalArms);

        for (int i = 0; i < projectilesPerArm; i++)
        {
            for (int a = 0; a < totalArms; a++)
            {
                float armAngle = currentAngle + (armSpacing * a);
                FireSingleProjectile(armAngle);
            }

            currentAngle += finalAngleIncrement;
            yield return new WaitForSeconds(spawnInterval);
        }

        yield return new WaitForSeconds(postPatternDelay);

        FinishPattern();
    }

    private void FireSingleProjectile(float angle)
    {
        if (patternData == null || patternData.projectilePrefab == null)
        {
            Debug.LogWarning("패턴 데이터(EnemyDataSO)가 등록되지 않았습니다.");
            return;
        }

        Quaternion rotation = Quaternion.Euler(0, 0, angle);
        NetworkObject netObj = NetworkProjectilePool.Instance.GetProjectile(patternData.projectilePrefab, transform.position, rotation);

        if (netObj != null)
        {
            EnemyProjectile projectileScript = netObj.GetComponent<EnemyProjectile>();
            if (projectileScript != null)
            {
                projectileScript.Initialize(patternData, angle);
            }
        }
    }
}