// EnemyStatManager.cs
using Unity.Netcode;
using UnityEngine;

public class EnemyStatManager : NetworkBehaviour, IDamageable
{
    [Header("Enemy Stats")]
    public float maxHP = 50f;

    // 체력은 서버와 클라이언트 모두 동기화되어야 함
    public NetworkVariable<float> currentHP = new NetworkVariable<float>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHP.Value = maxHP;
        }
    }

    // IDamageable 인터페이스 구현
    public void TakeDamage(float damageAmount)
    {
        // 피격 연산은 서버에서만 처리
        if (!IsServer) return;

        currentHP.Value -= damageAmount;

        if (currentHP.Value <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // 1. 상태이상 등 초기화 로직이 들어갈 자리

        // 2. 네트워크 상에서 객체를 숨김 (파괴하지 않음)
        NetworkObject.Despawn(false);

        // 3. 서버의 풀 매니저로 객체 반환
        EnemyPoolManager.Instance.ReturnEnemy(this.gameObject);
    }
}
