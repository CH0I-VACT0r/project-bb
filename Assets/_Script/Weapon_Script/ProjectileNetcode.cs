using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider2D))]
public class ProjectileNetcode : NetworkBehaviour
{
    private Vector3 moveDirection;
    private float moveSpeed;
    private DamageInfo damageInfo;
    private bool hasHit = false;

    public void Initialize(Vector3 direction, float speed, DamageInfo info)
    {
        moveDirection = direction.normalized;
        moveSpeed = speed;
        damageInfo = info;
    }

    void Update()
    {
        // 이동 로직 (서버/클라이언트 모두 실행하여 부드럽게 렌더링)
        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer || hasHit) return;
        IDamageable target = collision.GetComponent<IDamageable>();

        if (target != null)
        {
            hasHit = true;
            damageInfo.knockbackDir = moveDirection;
            target.TakeDamage(damageInfo);

            NetworkObject netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
        }
    }
}