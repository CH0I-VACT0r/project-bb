using UnityEngine;
using UnityEngine.SceneManagement;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("1. Lobby (Normal) Cursors")]
    public Texture2D defaultCursor;       // 로비 일반 커서
    public Vector2 defaultHotSpot = Vector2.zero;

    [Header("2. Combat Cursors")]
    public Texture2D combatDefaultCursor; // 전투 스테이지 기본 커서
    public Vector2 combatHotSpot = Vector2.zero;

    [Header("3. Interaction Cursors")]
    public Texture2D interactCursor;      // 상호작용 (힐 조각상 등)
    public Vector2 interactHotSpot = Vector2.zero;
    public Texture2D portalCursor;        // 포탈 진입
    public Vector2 portalHotSpot = Vector2.zero;

    // 현재 씬이 전투 스테이지인지 추적하는 상태 변수
    private bool isCombatMode = false;

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
            return;
        }
    }

    private void OnEnable()
    {
        // 씬 로딩 완료 이벤트 구독
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 씬이 변경될 때마다 자동 호출되는 함수
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 이름이 CombatScene이면 전투 커서 모드로 변환 (이름이 다르다면 맞게 수정해주세요)
        if (scene.name == "CombatScene")
        {
            isCombatMode = true;
        }
        else
        {
            isCombatMode = false;
        }

        // 씬 로딩 직후 알맞은 기본 커서로 즉시 변경
        SetDefaultCursor();
    }

    public void SetDefaultCursor()
    {
        if (isCombatMode && combatDefaultCursor != null)
        {
            // 전투 스테이지일 때의 기본 커서
            Cursor.SetCursor(combatDefaultCursor, combatHotSpot, CursorMode.Auto);
        }
        else if (defaultCursor != null)
        {
            // 로비 등 평상시 기본 커서
            Cursor.SetCursor(defaultCursor, defaultHotSpot, CursorMode.Auto);
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }

    public void SetInteractCursor()
    {
        if (interactCursor != null)
            Cursor.SetCursor(interactCursor, interactHotSpot, CursorMode.Auto);
    }

    public void SetPortalCursor()
    {
        if (portalCursor != null)
            Cursor.SetCursor(portalCursor, portalHotSpot, CursorMode.Auto);
    }
}