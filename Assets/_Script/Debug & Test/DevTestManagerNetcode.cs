using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class DevTestManagerNetcode : NetworkBehaviour
{
    public static DevTestManagerNetcode Instance { get; private set; }

    [Header("Test Settings")]
    [Tooltip("이동할 층 (예: 20 입력 시 보스방)")]
    public int targetFloor = 20;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame)
        {
            RequestFloorChange(targetFloor);
        }
    }

    public void RequestFloorChange(int floorNumber)
    {
        if (IsServer)
        {
            ChangeFloorLogic(floorNumber);
        }
        else
        {
            RequestFloorChangeServerRpc(floorNumber);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestFloorChangeServerRpc(int floorNumber)
    {
        ChangeFloorLogic(floorNumber);
    }

    private void ChangeFloorLogic(int floorNumber)
    {
        if (!IsServer) return;

        Debug.Log($"[서버] 개발자 테스트: {floorNumber}층으로 씬을 재로드합니다.");

        if (GameManager.Instance != null)
        {
            if (floorNumber % 20 == 0) GameManager.Instance.nextRoomType = StageRoomType.Boss;
            else if (floorNumber % 5 == 0) GameManager.Instance.nextRoomType = StageRoomType.Elite;
            else GameManager.Instance.nextRoomType = StageRoomType.Combat;
            GameManager.Instance.currentFloor = floorNumber;
        }

        NetworkManager.Singleton.SceneManager.LoadScene("CombatScene", LoadSceneMode.Single);
    }
}