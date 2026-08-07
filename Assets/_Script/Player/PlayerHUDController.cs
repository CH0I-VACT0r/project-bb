using UnityEngine;
using UnityEngine.UIElements;

public class PlayerHUDController : MonoBehaviour
{
    public static PlayerHUDController Instance;

    private UIDocument uiDocument;

    // UI Toolkit 쿼리용 변수 변경
    private VisualElement hpOrbFluid;
    private Label hpOrbText;
    private VisualElement shieldContainer;
    private VisualElement identityContainer;

    private PlayerStatManager localPlayerStats;
    private BarbarianIdentityUI barbarianUIController;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        uiDocument = GetComponent<UIDocument>();
        uiDocument.rootVisualElement.style.display = DisplayStyle.None;
    }

    public void BindLocalPlayer(PlayerStatManager statManager)
    {
        uiDocument.rootVisualElement.style.display = DisplayStyle.Flex;
        localPlayerStats = statManager;
        var root = uiDocument.rootVisualElement;

        // UXML 요소 매핑
        hpOrbFluid = root.Q<VisualElement>("hp-orb-fluid");
        hpOrbText = root.Q<Label>("hp-orb-text");
        shieldContainer = root.Q<VisualElement>("shield-container");
        identityContainer = root.Q<VisualElement>("identity-container");

        // 이벤트 구독
        localPlayerStats.CurrentHealth.OnValueChanged += UpdateHPUI;
        localPlayerStats.MaxHealth.OnValueChanged += UpdateHPUI;
        localPlayerStats.CurrentShield.OnValueChanged += UpdateShieldUI;

        InstantiateIdentityUI(statManager);

        // 초기 수치 반영
        UpdateHPUI(0, localPlayerStats.CurrentHealth.Value);
        UpdateShieldUI(0, localPlayerStats.CurrentShield.Value);
    }

    private void InstantiateIdentityUI(PlayerStatManager statManager)
    {
        if (statManager.classData.identityUXML == null || identityContainer == null) return;

        identityContainer.Clear();

        VisualElement identityInstance = statManager.classData.identityUXML.Instantiate();
        identityContainer.Add(identityInstance);

        if (statManager.classData.className == "Barbarian")
        {
            var barbarianLogic = statManager.GetComponent<BarbarianIdentityNetcode>();
            if (barbarianLogic != null)
            {
                barbarianUIController = new BarbarianIdentityUI();
                barbarianUIController.Bind(identityInstance, barbarianLogic);
            }
        }
    }

    private void UpdateHPUI(float previousValue, float newValue)
    {
        if (localPlayerStats == null || hpOrbFluid == null || hpOrbText == null) return;

        float current = localPlayerStats.CurrentHealth.Value;
        float max = localPlayerStats.MaxHealth.Value;

        // 퍼센트 계산 (0.0 ~ 100.0)
        float percent = Mathf.Clamp01(current / max) * 100f;

        // fluid 요소의 높이를 퍼센트로 조절
        hpOrbFluid.style.height = new Length(percent, LengthUnit.Percent);

        // 텍스트 업데이트
        hpOrbText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    private void UpdateShieldUI(float previousValue, float newValue)
    {
        if (shieldContainer == null) return;

        int targetShieldCount = Mathf.CeilToInt(newValue);
        int currentIconCount = shieldContainer.childCount;

        // 쉴드가 증가한 경우: 아이콘 추가
        if (currentIconCount < targetShieldCount)
        {
            int amountToAdd = targetShieldCount - currentIconCount;
            for (int i = 0; i < amountToAdd; i++)
            {
                VisualElement shieldIcon = new VisualElement();
                shieldIcon.AddToClassList("shield-icon");
                shieldContainer.Add(shieldIcon);
            }
        }
        // 쉴드가 감소한 경우: 아이콘 제거 (왼쪽부터 깎임)
        else if (currentIconCount > targetShieldCount)
        {
            int amountToRemove = currentIconCount - targetShieldCount;
            for (int i = 0; i < amountToRemove; i++)
            {
                // 트리의 가장 마지막에 추가된 자식 노드를 제거
                // (row-reverse로 인해 시각적으로는 가장 왼쪽에 배치된 아이콘입니다)
                shieldContainer.RemoveAt(shieldContainer.childCount - 1);
            }
        }
    }

    private void OnDestroy()
    {
        if (localPlayerStats != null)
        {
            localPlayerStats.CurrentHealth.OnValueChanged -= UpdateHPUI;
            localPlayerStats.MaxHealth.OnValueChanged -= UpdateHPUI;
            localPlayerStats.CurrentShield.OnValueChanged -= UpdateShieldUI;
        }

        if (barbarianUIController != null)
        {
            barbarianUIController.Unbind();
        }
    }
}