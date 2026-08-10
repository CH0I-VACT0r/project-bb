using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 5f; // 카메라가 따라가는 부드러운 정도
    public Vector3 offset = new Vector3(0, 0, -10f); // 2D 환경이므로 Z축을 뒤로 당겨줍니다.

    [Header("Map Boundaries")]
    public Vector2 minBounds; // 맵 좌측 하단 끝 좌표 (예: -20, -15)
    public Vector2 maxBounds;
    private Camera cam;
    private bool isBoundsSet = false;

    void Start()
    {
        cam = GetComponent<Camera>();
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

        if (isBoundsSet)
        {
            float camHalfHeight = cam.orthographicSize;
            float camHalfWidth = camHalfHeight * cam.aspect;

            float clampedX = Mathf.Clamp(desiredPosition.x, minBounds.x + camHalfWidth, maxBounds.x - camHalfWidth);
            float clampedY = Mathf.Clamp(desiredPosition.y, minBounds.y + camHalfHeight, maxBounds.y - camHalfHeight);

            Vector3 clampedPosition = new Vector3(clampedX, clampedY, offset.z);
            transform.position = Vector3.Lerp(transform.position, clampedPosition, smoothSpeed * Time.deltaTime);
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        }
    }
}
