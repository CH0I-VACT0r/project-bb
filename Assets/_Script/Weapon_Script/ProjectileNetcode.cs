using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider2D))]
public class ProjectileNetcode : NetworkBehaviour
{
    private Vector3 moveDirection;
    private float moveSpeed;

    // 1. float damage 대신 DamageInfo 구조체를 저장하도록 변경
    private DamageInfo damageInfo;

    private float lifeTime = 3f;

    // 2. 세 번째 매개변수를 float에서 DamageInfo로 변경
    public void Initialize(Vector3 direction, float speed, DamageInfo info)
    {
        moveDirection = direction.normalized;
        moveSpeed = speed;
        damageInfo = info;
    }

    void Update()
    {
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
        if (!IsServer) return;

        if (collision.CompareTag("Enemy"))
        {
            IDamageable target = collision.GetComponent<IDamageable>();
            if (target != null)
            {
                // 3. 넉백 방향을 투사체의 현재 진행 방향으로 덮어씌움
                damageInfo.knockbackDir = moveDirection;

                // 4. 구조체 전체를 전달
                target.TakeDamage(damageInfo);
            }

            // TODO: 추후 WeaponDataSO의 isPiercing(관통) 옵션 적용 시 분기 처리 가능
            GetComponent<NetworkObject>().Despawn();
        }
    }
}