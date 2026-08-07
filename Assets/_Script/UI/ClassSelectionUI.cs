using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class ClassSelectionUI : MonoBehaviour
{
    private UIDocument uiDocument;
    private VisualElement selectionPanel;
    private VisualElement classButtonContainer;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void Start()
    {
        var root = uiDocument.rootVisualElement;

        // UXML 노드 쿼리
        selectionPanel = root.Q<VisualElement>("selection-panel");
        classButtonContainer = root.Q<VisualElement>("class-button-container");

        // 게임 시작 시 패널 숨김 (클라이언트 접속 전까지 대기)
        if (selectionPanel != null)
        {
            selectionPanel.style.display = DisplayStyle.None;
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        // 로컬 클라이언트가 서버에 접속 완료되었을 때만 직업 선택 창 활성화
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            if (selectionPanel != null)
            {
                selectionPanel.style.display = DisplayStyle.Flex;
            }
            GenerateClassButtons();
        }
    }

    private void GenerateClassButtons()
    {
        if (classButtonContainer == null) return;

        classButtonContainer.Clear();

        // SpawnManager에 등록된 SO 배열을 읽어와 동적으로 버튼 생성
        var availableClasses = CharacterSpawnManager.Instance.availableClasses;
        foreach (var classData in availableClasses)
        {
            // UI Toolkit 버튼 인스턴스화
            Button btn = new Button();
            btn.text = classData.className;
            btn.AddToClassList("class-button"); // USS의 스타일 클래스 부여

            // 클로저 캡처 문제 방지
            int capturedClassId = classData.classId;
            btn.clicked += () => OnClassButtonClicked(capturedClassId);

            classButtonContainer.Add(btn);
        }
    }

    private void OnClassButtonClicked(int classId)
    {
        // 스폰 요청
        CharacterSpawnManager.Instance.SelectClassAndSpawn(classId);

        // 직업 선택 후 패널을 화면에서 제거
        if (selectionPanel != null)
        {
            selectionPanel.style.display = DisplayStyle.None;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }
    }
}