using UnityEngine;

public class CursorManager : MonoBehaviour
{
    [Header("Cursor Settings")]
    public Texture2D cursorTexture; // 에디터에서 커서로 쓸 2D 이미지 할당
    public Vector2 hotSpot = Vector2.zero; // 클릭 판정이 일어나는 픽셀 위치

    void Start()
    {
        SetCustomCursor();
    }

    private void SetCustomCursor()
    {
        if (cursorTexture != null)
        {
            // 커서 텍스처를 적용 (소프트웨어 커서 모드)
            Cursor.SetCursor(cursorTexture, hotSpot, CursorMode.Auto);
        }
        else
        {
            Debug.LogWarning("커서 텍스처가 할당되지 않았습니다.");
        }
    }

    // 테스트를 위해 커서를 다시 기본으로 되돌리고 싶을 때 호출
    public void ResetCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}
