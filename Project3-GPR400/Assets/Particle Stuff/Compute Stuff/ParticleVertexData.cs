using Unity.Mathematics;
using UnityEngine;

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

        vertexBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);

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
        computeShader.SetFloat("time", Time.time);
        int threadGroups = Mathf.CeilToInt(vertexCount / 64f);
        computeShader.Dispatch(kernelIndex, threadGroups, 1, 1);

        Graphics.DrawProcedural(material, new Bounds(Vector3.zero, Vector3.one * 100f),
                        MeshTopology.Points, vertexCount);


        vertexData[] vertOutput = new vertexData[vertexCount];
        vertexBuffer.GetData(vertOutput);
        for (int i = 0; i < vertexCount; i++)
        {
            Debug.Log(vertOutput[i].position.y);
        }
    }

    private void OnDestroy()
    {
        vertexBuffer?.Release();
        vertexBuffer = null;
    }
}
