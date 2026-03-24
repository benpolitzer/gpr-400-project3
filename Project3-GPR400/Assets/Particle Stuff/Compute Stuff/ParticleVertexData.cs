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
    private int threadGroups;

    //passes all needed data to the compute shader (must match struct in compute)
    private struct vertexData
    {
        public Vector3 position;
    };

    private void Awake()
    {
        //cant run with an empty buffer
        if (vertexCount == 0)
        {
            Debug.LogError("Vertex count cannot be 0. ");
            this.enabled = false;
            return;
        }

        //initialise vertex data based on size needed
        vertexBuffer = new ComputeBuffer(vertexCount * vertexCount, sizeof(float) * 3);
        threadGroups = ((int)Mathf.Sqrt(vertexCount)) / 8;

        //needed compute setup thats the same on subsequent runs
        kernelIndex = computeShader.FindKernel("MoveParticles");
        computeShader.SetBuffer(kernelIndex, "verts", vertexBuffer);
        computeShader.SetInt("vertCount", vertexCount);
        computeShader.SetInt("threadGroups", threadGroups);

        //getting the material and setting the buffer to recieve computer shader data
        material = GetComponent<MeshRenderer>().material;
        material.SetBuffer("verts", vertexBuffer);
    }

    private void Update()
    {
        SendParticles();
    }

    void SendParticles()
    {
        //setting needed per tick data and dispatching
        computeShader.SetFloats("basePosition", new float[] { transform.position.x, transform.position.y, transform.position.z });
        computeShader.SetFloat("time", Time.time);
        computeShader.Dispatch(kernelIndex, threadGroups, threadGroups, 1);

        //draw everything
        Graphics.DrawProcedural(material, new Bounds(Vector3.zero, Vector3.one * 100f),
                        MeshTopology.Points, vertexCount);
    }

    private void OnDisable()
    {
        vertexBuffer?.Release();
        vertexBuffer = null;
    }
}
