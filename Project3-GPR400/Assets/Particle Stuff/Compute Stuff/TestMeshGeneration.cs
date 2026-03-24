using UnityEngine;

[ExecuteInEditMode]
public class TestMeshGeneration : MonoBehaviour
{
    MeshFilter mf;

    private void Awake()
    {
        mf = GetComponent<MeshFilter>();

        Mesh mesh = new Mesh();

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0)
        };

        mesh.vertices = vertices;

        int[] indices = new int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
            indices[i] = i;

        mesh.SetIndices(indices, MeshTopology.Points, 0);

        mf.mesh = mesh;
    }
}
