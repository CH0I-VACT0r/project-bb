using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class TestSpawner : NetworkBehaviour
{
    void Update()
    {
        if (!IsServer) return;

        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            Vector3 randomOffset = (Vector3)Random.insideUnitCircle * 5f;
            Vector3 spawnPos = transform.position + randomOffset;

            if (EnemyPoolManager.Instance != null)
            {
                EnemyPoolManager.Instance.SpawnEnemy(spawnPos);
            }
        }
    }
}