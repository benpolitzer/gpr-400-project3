using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LiquidSurfaceGrid : MonoBehaviour
{
    [SerializeField, Min(2)] private int gridWidth = 21;
    [SerializeField, Min(2)] private int gridHeight = 21;
    [SerializeField, Min(0.01f)] private float sizeX = 1.0f;
    [SerializeField, Min(0.01f)] private float sizeZ = 1.0f;

    private MeshFilter meshFilter;

    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public float SizeX => sizeX;
    public float SizeZ => sizeZ;
    private void OnEnable()
    {
        Generate();
    }

    private void OnValidate()
    {
        Generate();
    }

    [ContextMenu("Generate Grid")]
    public void Generate()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        Mesh mesh = new Mesh();
        mesh.name = "LiquidSurfaceGrid";

        int vertCount = gridWidth * gridHeight;
        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];

        int[] triangles = new int[(gridWidth - 1) * (gridHeight - 1) * 6];

        float halfX = sizeX * 0.5f;
        float halfZ = sizeZ * 0.5f;

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int i = y * gridWidth + x;

                float tx = gridWidth > 1 ? (float)x / (gridWidth - 1) : 0f;
                float ty = gridHeight > 1 ? (float)y / (gridHeight - 1) : 0f;

                float px = Mathf.Lerp(-halfX, halfX, tx);
                float pz = Mathf.Lerp(-halfZ, halfZ, ty);

                vertices[i] = new Vector3(px, 0f, pz);
                normals[i] = Vector3.up;
                uvs[i] = new Vector2(tx, ty);
            }
        }

        int tri = 0;
        for (int y = 0; y < gridHeight - 1; y++)
        {
            for (int x = 0; x < gridWidth - 1; x++)
            {
                int i0 = y * gridWidth + x;
                int i1 = i0 + 1;
                int i2 = i0 + gridWidth;
                int i3 = i2 + 1;

                triangles[tri++] = i0;
                triangles[tri++] = i2;
                triangles[tri++] = i1;

                triangles[tri++] = i1;
                triangles[tri++] = i2;
                triangles[tri++] = i3;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        if (Application.isPlaying)
        {
            meshFilter.mesh = mesh;
        }
        else
        {
            meshFilter.sharedMesh = mesh;
        }
    }
}