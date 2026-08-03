using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AoEIndicatorMesh : MonoBehaviour
{
    private Mesh mesh;
    private MeshFilter meshFilter;

    void Awake()
    {
        mesh = new Mesh();
        meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;
    }

    public void DrawShape(float radius, float angle, int segments = 10)
    {
        int vertexCount = segments + 2;
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;

        float currentAngle = -angle / 2f;
        float angleStep = angle / segments;

        for (int i = 0; i <= segments; i++)
        {
            float rad = Mathf.Deg2Rad * currentAngle;
            vertices[i + 1] = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius;
            currentAngle += angleStep;
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    public void DrawRectangle(float length, float width)
    {
        Vector3[] vertices = new Vector3[4];
        int[] triangles = new int[6];

        float halfWidth = width / 2f;

        // 원점(0)에서 +X 방향으로 뻗어나가는 네모 생성
        vertices[0] = new Vector3(0, -halfWidth, 0);
        vertices[1] = new Vector3(0, halfWidth, 0);
        vertices[2] = new Vector3(length, halfWidth, 0);
        vertices[3] = new Vector3(length, -halfWidth, 0);

        triangles[0] = 0; triangles[1] = 1; triangles[2] = 2;
        triangles[3] = 0; triangles[4] = 2; triangles[5] = 3;

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }
    public void ClearMesh()
    {
        if (mesh != null) mesh.Clear();
    }
}