using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider2D))]
public class ProjectileNetcode : NetworkBehaviour
{
    private Vector3 moveDirection;
    private float moveSpeed;
    private float damage;
    private float lifeTime = 3f; // 3초 뒤 자동 소멸

    // 서버에서 투사체 생성 시 호출
    public void Initialize(Vector3 direction, float speed, float damageAmount)
    {
        moveDirection = direction.normalized;
        moveSpeed = speed;
        damage = damageAmount;
    }

    void Update()
    {
        // 위치 이동은 모든 클라이언트에서 개별적으로 시각적 시뮬레이션
        transform.position += moveDirection * moveSpeed * Time.deltaTime;

        if (IsServer)
        {
            lifeTime -= Time.deltaTime;
            if (lifeTime <= 0)
            {
                GetComponent<NetworkObject>().Despawn();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer) return; // 피해 판정은 무조건 서버에서만

        if (collision.CompareTag("Enemy"))
        {
            IDamageable target = collision.GetComponent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(damage);
            }

            // 관통 속성이 없다면 타격 후 즉시 파괴
            GetComponent<NetworkObject>().Despawn();
        }
    }
}
