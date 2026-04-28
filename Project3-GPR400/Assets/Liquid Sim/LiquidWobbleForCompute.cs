using UnityEngine;

public class LiquidWobbleForCompute : MonoBehaviour
{
    int vertexCount;
    [SerializeField] ComputeShader computeShader;

    private int kernelIndex;
    private Material material;
    private int threadGroups;

    [Header("Wobble")]
    [SerializeField] private float maxWobble = 0.03f;
    [SerializeField] private float wobbleSpeed = 2.0f;
    [SerializeField] private float recovery = 2.0f;
    [SerializeField] private float movementInfluence = 1.0f;
    [SerializeField] private float rotationInfluence = 0.2f;

    [Header("Surface Clip")]
    [SerializeField] private Vector2 clipCenter = Vector2.zero;
    [SerializeField] private Vector2 clipRadius = new Vector2(0.5f, 1.0f);

    private Vector3 lastPosition;
    private Vector3 lastEulerAngles;
    private float wobbleAddX;
    private float wobbleAddZ;
    private ComputeBuffer baseVertexBuffer;
    private ComputeBuffer displacedVertexBuffer;

    //passes all needed data to the compute shader (must match struct in compute)
    private struct vertexData
    {
        public Vector3 position;
    };

    private void Start()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;

        vertexCount = mesh.vertexCount;

        Debug.Log("Liquid verts: " + mesh.vertexCount);
        Debug.Log("Liquid triangles: " + mesh.triangles.Length / 3);

        baseVertexBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        displacedVertexBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);

        baseVertexBuffer.SetData(mesh.vertices);
        displacedVertexBuffer.SetData(mesh.vertices);

        threadGroups = Mathf.CeilToInt(vertexCount / 64.0f);

        kernelIndex = computeShader.FindKernel("MoveVertices");

        computeShader.SetBuffer(kernelIndex, "baseVerts", baseVertexBuffer);
        computeShader.SetBuffer(kernelIndex, "verts", displacedVertexBuffer);
        computeShader.SetInt("vertCount", vertexCount);

        material = GetComponent<MeshRenderer>().material;
        material.SetBuffer("verts", displacedVertexBuffer);

        material.SetFloat("_ClipCenterX", clipCenter.x);
        material.SetFloat("_ClipCenterZ", clipCenter.y);
        material.SetFloat("_ClipRadiusX", clipRadius.x);
        material.SetFloat("_ClipRadiusZ", clipRadius.y);

        lastPosition = transform.position;
        lastEulerAngles = transform.eulerAngles;
    }

    private void Update()
    {
        float dt = Mathf.Max(Application.isPlaying ? Time.deltaTime : 0.016f, 0.0001f);

        // How much the object moved since last frame
        Vector3 velocity = (transform.position - lastPosition) / dt;

        // How much the object rotated since last frame (using Euler angles)
        Vector3 currentEuler = transform.eulerAngles;

        Vector3 angularDelta = new Vector3(
            Mathf.DeltaAngle(lastEulerAngles.x, currentEuler.x),
            Mathf.DeltaAngle(lastEulerAngles.y, currentEuler.y),
            Mathf.DeltaAngle(lastEulerAngles.z, currentEuler.z)
        );

        // Add wobble along Axis(X,Z) based on movement and relevant rotation
        wobbleAddX += Mathf.Clamp(
            (velocity.x * movementInfluence + angularDelta.z * rotationInfluence) * maxWobble,
            -maxWobble,
            maxWobble
        );
        wobbleAddZ += Mathf.Clamp(
            (velocity.z * movementInfluence + angularDelta.x * rotationInfluence) * maxWobble,
            -maxWobble,
            maxWobble
        );

        // Gradually damp the wobble back toward zero so the liquid settles over time
        wobbleAddX = Mathf.Lerp(wobbleAddX, 0f, recovery * dt);
        wobbleAddZ = Mathf.Lerp(wobbleAddZ, 0f, recovery * dt);

        // Sine wave so the wobble oscillates instead of instantly snapping
        float pulse = Time.realtimeSinceStartup * wobbleSpeed * Mathf.PI * 2f;
        float wobbleX = wobbleAddX * Mathf.Sin(pulse);
        float wobbleZ = wobbleAddZ * Mathf.Sin(pulse);

        // Control fill level
        Vector3 surfaceOriginWS = transform.position;

        // Send the surface origin into the shader
        computeShader.SetFloats("_SurfaceOriginWS", new float[] { 
            surfaceOriginWS.x,
            surfaceOriginWS.y,
            surfaceOriginWS.z,
        });

        computeShader.SetFloat("time", Time.realtimeSinceStartup);

        computeShader.SetFloat("_WobbleX", wobbleX);
        computeShader.SetFloat("_WobbleZ", wobbleZ);

        computeShader.SetFloat("_WaveAmp", 0.02f);
        computeShader.SetFloat("_WaveFreq", 8.0f);
        computeShader.SetFloat("_WaveSpeed", 2.0f);

        computeShader.SetFloat("_FillY", 0.0f);

        // If your mesh is centered around 0, use 0 and 0.
        // If your grid goes from 0 to SizeX / 0 to SizeZ, use SizeX * 0.5 and SizeZ * 0.5.
        computeShader.SetFloat("_CenterX", 0.0f);
        computeShader.SetFloat("_CenterZ", 0.0f);

        computeShader.Dispatch(kernelIndex, threadGroups, 1, 1);

        //vertexBuffer.GetData(GetComponent<MeshFilter>().mesh.vertices);

        // Store current frame's transform so the next frame can measure movement against it
        lastPosition = transform.position;
        lastEulerAngles = transform.eulerAngles;

    }

    private void OnDisable()
    {
        if (baseVertexBuffer != null)
        {
            baseVertexBuffer.Release();
            baseVertexBuffer = null;
        }

        if (displacedVertexBuffer != null)
        {
            displacedVertexBuffer.Release();
            displacedVertexBuffer = null;
        }
    }
}
