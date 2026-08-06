using UnityEngine;
using UnityEngine.UIElements;

public class PlayerHUDController : MonoBehaviour
{
    public static PlayerHUDController Instance;

    private UIDocument uiDocument;

    // UI Toolkit 요소들
    private ProgressBar hpBar;
    private ProgressBar shieldBar;
    private VisualElement identityContainer;

    private PlayerStatManager localPlayerStats;
    private BarbarianIdentityUI barbarianUIController; // 바바리안 UI 로직 인스턴스

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        uiDocument = GetComponent<UIDocument>();
    }

    public void BindLocalPlayer(PlayerStatManager statManager)
    {
        localPlayerStats = statManager;
        var root = uiDocument.rootVisualElement;

        // UXML에 정의한 각 요소의 Name(ID)으로 쿼리합니다.
        hpBar = root.Q<ProgressBar>("hp-bar");
        shieldBar = root.Q<ProgressBar>("shield-bar");
        identityContainer = root.Q<VisualElement>("identity-container");

        // 이벤트 구독
        localPlayerStats.CurrentHealth.OnValueChanged += UpdateHPUI;
        localPlayerStats.MaxHealth.OnValueChanged += UpdateHPUI;
        localPlayerStats.CurrentShield.OnValueChanged += UpdateShieldUI;
        localPlayerStats.MaxShield.OnValueChanged += UpdateShieldUI;

        // 직업 고유 UI(UXML) 동적 생성 및 바인딩
        InstantiateIdentityUI(statManager);

        // 초기 UI 업데이트
        UpdateHPUI(0, localPlayerStats.CurrentHealth.Value);
        UpdateShieldUI(0, localPlayerStats.CurrentShield.Value);
    }

    private void InstantiateIdentityUI(PlayerStatManager statManager)
    {
        if (statManager.classData.identityUXML == null || identityContainer == null) return;

        identityContainer.Clear(); // 기존 UI 노드 제거

        // SO에 등록된 UXML을 인스턴스화하여 하단 컨테이너에 자식으로 추가
        VisualElement identityInstance = statManager.classData.identityUXML.Instantiate();
        identityContainer.Add(identityInstance);

        // 직업명에 따라 알맞은 로직 클래스를 연결
        if (statManager.classData.className == "Barbarian")
        {
            var barbarianLogic = statManager.GetComponent<BarbarianIdentityNetcode>();
            if (barbarianLogic != null)
            {
                // UI Toolkit은 MonoBehaviour가 불필요하므로 순수 C# 클래스로 인스턴스 생성
                barbarianUIController = new BarbarianIdentityUI();
                barbarianUIController.Bind(identityInstance, barbarianLogic);
            }
        }
    }

    private void UpdateHPUI(float previousValue, float newValue)
    {
        if (localPlayerStats == null || hpBar == null) return;

        float current = localPlayerStats.CurrentHealth.Value;
        float max = localPlayerStats.MaxHealth.Value;

        hpBar.value = current;
        hpBar.highValue = max;
        hpBar.title = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    private void UpdateShieldUI(float previousValue, float newValue)
    {
        if (localPlayerStats == null || shieldBar == null) return;

        float current = localPlayerStats.CurrentShield.Value;
        float max = localPlayerStats.MaxShield.Value;

        if (max > 0)
        {
            shieldBar.style.display = DisplayStyle.Flex; // 쉴드가 있을 때만 표시
            shieldBar.value = current;
            shieldBar.highValue = max;
            shieldBar.title = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
        else
        {
            shieldBar.style.display = DisplayStyle.None; // 쉴드 0이면 숨김
        }
    }

    private void OnDestroy()
    {
        if (localPlayerStats != null)
        {
            localPlayerStats.CurrentHealth.OnValueChanged -= UpdateHPUI;
            localPlayerStats.MaxHealth.OnValueChanged -= UpdateHPUI;
            localPlayerStats.CurrentShield.OnValueChanged -= UpdateShieldUI;
            localPlayerStats.MaxShield.OnValueChanged -= UpdateShieldUI;
        }

        if (barbarianUIController != null)
        {
            barbarianUIController.Unbind();
        }
    }
}