using UnityEngine;

public class LiquidWobbleForCompute : MonoBehaviour
{
    int vertexCount;
    [SerializeField] ComputeShader computeShader;

    private int kernelIndex;
    private Material material;
    private ComputeBuffer vertexBuffer;
    private int threadGroups;

    [Header("Wobble")]
    [SerializeField] private float maxWobble = 0.03f;
    [SerializeField] private float wobbleSpeed = 2.0f;
    [SerializeField] private float recovery = 2.0f;
    [SerializeField] private float movementInfluence = 1.0f;
    [SerializeField] private float rotationInfluence = 0.2f;

    private Vector3 lastPosition;
    private Vector3 lastEulerAngles;
    private float wobbleAddX;
    private float wobbleAddZ;

    //passes all needed data to the compute shader (must match struct in compute)
    private struct vertexData
    {
        public Vector3 position;
    };

    private void Start()
    {
        vertexCount = GetComponent<MeshFilter>().mesh.vertexCount;

        //initialise vertex data based on size needed
        vertexBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        vertexBuffer.SetData(GetComponent<MeshFilter>().mesh.vertices);
        threadGroups = ((int)Mathf.Sqrt(vertexCount)) / 8;

        //needed compute setup thats the same on subsequent runs
        kernelIndex = computeShader.FindKernel("MoveVertices");
        computeShader.SetBuffer(kernelIndex, "verts", vertexBuffer);
        computeShader.SetInt("vertCount", vertexCount);
        computeShader.SetInt("threadGroups", threadGroups);

        //getting the material and setting the buffer to recieve computer shader data
        material = GetComponent<MeshRenderer>().material;
        material.SetBuffer("verts", vertexBuffer);

        lastPosition = transform.position;
        lastEulerAngles = transform.eulerAngles;
    }

    private void Update()
    {
        float dt = Mathf.Max(Application.isPlaying ? Time.deltaTime : 0.016f, 0.0001f);

        // How much the object moved since last frame
        Vector3 velocity = (transform.position - lastPosition) / dt;

        // How much the object rotated since last frame (using Euler angles)
        Vector3 angularDelta = transform.eulerAngles - lastEulerAngles;

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

        // Send the wobble values into the shader for tilt
        computeShader.SetFloat("_WobbleX", wobbleX);
        computeShader.SetFloat("_WobbleZ", wobbleZ);

        computeShader.Dispatch(kernelIndex, threadGroups, threadGroups, 1);

        //vertexBuffer.GetData(GetComponent<MeshFilter>().mesh.vertices);

        // Store current frame's transform so the next frame can measure movement against it
        lastPosition = transform.position;
        lastEulerAngles = transform.eulerAngles;
    }

    private void OnDisable()
    {
        vertexBuffer?.Release();
        vertexBuffer = null;
    }
}
