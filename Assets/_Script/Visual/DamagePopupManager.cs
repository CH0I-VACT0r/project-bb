using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamagePopupManager : MonoBehaviour
{
    public static DamagePopupManager Instance { get; private set; }

    [Header("Settings")]
    public GameObject popupPrefab; // TextMeshPro가 달린 팝업 프리팹 할당
    private Queue<GameObject> popupPool = new Queue<GameObject>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 외부(서버 RPC 등)에서 데미지 발생 시 호출
    public void CreatePopup(Vector3 position, float damageAmount, bool isCritical, bool isMiss)
    {
        if (popupPrefab == null)
        {
            Debug.LogError("[에러] DamagePopupManager에 팝업 프리팹이 할당되지 않았습니다!");
            return;
        }

        GameObject popup;

        if (popupPool.Count > 0)
        {
            popup = popupPool.Dequeue();
            popup.SetActive(true);
        }
        else
        {
            popup = Instantiate(popupPrefab);
        }

        popup.transform.position = position;
        popup.GetComponent<DamagePopup>().Setup(damageAmount, isCritical, isMiss);
    }

    // 사용이 끝난 팝업을 풀로 반환
    public void ReturnPopup(GameObject popup)
    {
        popup.SetActive(false);
        popupPool.Enqueue(popup);
    }
}