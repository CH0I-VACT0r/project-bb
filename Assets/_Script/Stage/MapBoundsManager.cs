using UnityEngine;
using Unity.Netcode;

public class MapBoundsManager : NetworkBehaviour
{
    [Header("Boundary Setup")]
    public Collider2D mapBoundaryCollider; // 인스펙터에서 Boundary 오브젝트(EdgeCollider)를 끌어다 넣습니다.

    public override void OnNetworkSpawn()
    {
        if (mapBoundaryCollider == null)
        {
            Debug.LogError($"{gameObject.name}에 외곽선 콜라이더가 연결되지 않았습니다!");
            return;
        }

        // 콜라이더가 그리는 사각형 영역의 가장 작은(좌하단) 값과 큰(우상단) 값을 추출
        Vector2 min = mapBoundaryCollider.bounds.min;
        Vector2 max = mapBoundaryCollider.bounds.max;

        // 1. 내 화면(로컬)의 카메라 제한 구역 자동 업데이트
        if (Camera.main != null)
        {
            CameraFollow camFollow = Camera.main.GetComponent<CameraFollow>();
            if (camFollow != null)
            {
                camFollow.SetBounds(min, max);
            }
        }

        // 2. 서버(방장)일 경우, 몬스터 스포너의 스폰 구역 자동 업데이트
        if (IsServer && MonsterSpawnerNetcode.Instance != null)
        {
            MonsterSpawnerNetcode.Instance.SetSpawnBounds(min, max);
        }
    }
}