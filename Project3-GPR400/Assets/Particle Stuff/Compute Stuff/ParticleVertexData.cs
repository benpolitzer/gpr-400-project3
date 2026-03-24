using Unity.Mathematics;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public class ParticleVertexData : MonoBehaviour
{
    [SerializeField] int vertexCount;
    [SerializeField] ComputeShader computeShader;

    private int kernelIndex;
    private Material material;
    private ComputeBuffer vertexBuffer;

    private struct vertexData
    {
        public Vector3 position;
    };

    private void Awake()
    {
        if (vertexCount == 0)
        {
            Debug.LogError("Vertex count cannot be 0. ");
            this.enabled = false;
            return;
        }

        vertexBuffer = new ComputeBuffer(vertexCount * vertexCount, sizeof(float) * 3);

        kernelIndex = computeShader.FindKernel("MoveParticles");
        computeShader.SetBuffer(kernelIndex, "verts", vertexBuffer);
        computeShader.SetInt("vertCount", vertexCount);

        material = GetComponent<MeshRenderer>().material;
        material.SetBuffer("verts", vertexBuffer);
    }

    private void Update()
    {
        SendParticles();
    }

    void SendParticles()
    {
        computeShader.SetFloats("basePosition", new float[] { transform.position.x, transform.position.y, transform.position.z });
        computeShader.SetFloat("time", Time.time);
        int threadGroups = (((int)Mathf.Sqrt(vertexCount)) + 63) / 64;
        computeShader.Dispatch(kernelIndex, threadGroups, threadGroups, 1);

        Graphics.DrawProcedural(material, new Bounds(Vector3.zero, Vector3.one * 100f),
                        MeshTopology.Points, vertexCount);
    }

    private void OnDisable()
    {
        vertexBuffer?.Release();
        vertexBuffer = null;
    }
}
