using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkProjectilePool : NetworkBehaviour
{
    public static NetworkProjectilePool Instance;

    // 프리팹별로 독립적인 풀(Queue)을 관리하는 딕셔너리
    private Dictionary<GameObject, Queue<NetworkObject>> poolDictionary = new Dictionary<GameObject, Queue<NetworkObject>>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 투사체 요청
    public NetworkObject GetProjectile(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.ContainsKey(prefab))
        {
            poolDictionary[prefab] = new Queue<NetworkObject>();
        }

        if (poolDictionary[prefab].Count > 0)
        {
            NetworkObject netObj = poolDictionary[prefab].Dequeue();
            netObj.transform.position = position;
            netObj.transform.rotation = rotation;
            netObj.gameObject.SetActive(true);

            // 클라이언트 화면에 다시 동기화
            if (!netObj.IsSpawned) netObj.Spawn();

            return netObj;
        }
        else
        {
            // 큐가 비어있으면 새로 생성
            GameObject newObj = Instantiate(prefab, position, rotation);
            NetworkObject netObj = newObj.GetComponent<NetworkObject>();
            netObj.Spawn();
            return netObj;
        }
    }

    // 투사체 반환
    public void ReturnProjectile(GameObject prefab, NetworkObject netObj)
    {
        if (netObj.IsSpawned)
        {
            netObj.Despawn(false);
        }
        netObj.gameObject.SetActive(false);

        if (!poolDictionary.ContainsKey(prefab))
        {
            poolDictionary[prefab] = new Queue<NetworkObject>();
        }
        poolDictionary[prefab].Enqueue(netObj);
    }
}