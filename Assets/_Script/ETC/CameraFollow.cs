using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 5f; // 카메라가 따라가는 부드러운 정도
    public Vector3 offset = new Vector3(0, 0, -10f); // 2D 환경이므로 Z축을 뒤로 당겨줍니다.

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }
}
