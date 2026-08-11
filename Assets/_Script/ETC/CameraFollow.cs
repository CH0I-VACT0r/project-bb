using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 5f; // 카메라가 따라가는 부드러운 정도
    public Vector3 offset = new Vector3(0, 0, -10f); // 2D 환경이므로 Z축을 뒤로 당겨줍니다.
    private bool shouldSnapNextFrame = false;

    [Header("Map Boundaries")]
    public Vector2 minBounds; // 맵 좌측 하단 끝 좌표 (예: -20, -15)
    public Vector2 maxBounds;
    private Camera cam;
    private bool isBoundsSet = false;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    // 플레이어 스크립트에서 타겟 지정 시 첫 프레임 스냅 여부를 제어하는 메서드
    public void SetTarget(Transform newTarget, bool snapImmediately = true)
    {
        target = newTarget;
        shouldSnapNextFrame = snapImmediately;
    }

    public void SetBounds(Vector2 min, Vector2 max)
    {
        minBounds = min;
        maxBounds = max;
        isBoundsSet = true;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        Vector3 targetPosition = desiredPosition;

        if (isBoundsSet && cam != null)
        {
            float camHalfHeight = cam.orthographicSize;
            float camHalfWidth = camHalfHeight * cam.aspect;

            float clampedX = Mathf.Clamp(desiredPosition.x, minBounds.x + camHalfWidth, maxBounds.x - camHalfWidth);
            float clampedY = Mathf.Clamp(desiredPosition.y, minBounds.y + camHalfHeight, maxBounds.y - camHalfHeight);

            targetPosition = new Vector3(clampedX, clampedY, offset.z);
        }

        // 로딩 직후 첫 프레임은 Lerp 없이 즉시 카메라 좌표를 일치시켜 비어보이는 공백을 제거
        if (shouldSnapNextFrame)
        {
            transform.position = targetPosition;
            shouldSnapNextFrame = false;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
        }
    }
}