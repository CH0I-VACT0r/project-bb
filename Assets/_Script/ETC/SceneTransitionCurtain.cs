using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SceneTransitionCurtain : MonoBehaviour
{
    public static SceneTransitionCurtain Instance { get; private set; }

    [Header("UI Reference")]
    public CanvasGroup curtainCanvasGroup; // 인스펙터 또는 런타임에 자동 생성된 캔버스 그룹
    public float fadeDuration = 0.25f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            SetupDefaultCurtain();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 씬에 캔버스가 없어도 자동 생성하여 세팅
    private void SetupDefaultCurtain()
    {
        if (curtainCanvasGroup != null) return;

        GameObject canvasObj = new GameObject("TransitionCanvas");
        canvasObj.transform.SetParent(this.transform);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // 모든 인게임 요소보다 최상단에 렌더링

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject imageObj = new GameObject("BlackCurtain");
        imageObj.transform.SetParent(canvasObj.transform, false);

        Image img = imageObj.AddComponent<Image>();
        img.color = Color.black;
        img.rectTransform.anchorMin = Vector2.zero;
        img.rectTransform.anchorMax = Vector2.one;
        img.rectTransform.offsetMin = Vector2.zero;
        img.rectTransform.offsetMax = Vector2.zero;

        curtainCanvasGroup = imageObj.AddComponent<CanvasGroup>();
        curtainCanvasGroup.alpha = 0f;
        curtainCanvasGroup.blocksRaycasts = false;
    }

    public void FadeOutAndCall(System.Action onComplete)
    {
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(0f, 1f, onComplete));
    }

    public void FadeIn()
    {
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(1f, 0f, null));
    }

    private IEnumerator FadeRoutine(float startAlpha, float endAlpha, System.Action onComplete)
    {
        curtainCanvasGroup.blocksRaycasts = true;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            curtainCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeDuration);
            yield return null;
        }

        curtainCanvasGroup.alpha = endAlpha;
        curtainCanvasGroup.blocksRaycasts = (endAlpha > 0.5f);
        onComplete?.Invoke();
    }
}