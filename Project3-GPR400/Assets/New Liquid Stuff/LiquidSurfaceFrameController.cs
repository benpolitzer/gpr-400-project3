// Benjamin Politzer - main interface for the whole system. It tracks the parent containers movement and rotation,
// converts that motion into liquid normal tilt, slosh direction, and disturbance strength, then sends those values to the compute shader each frame.
using System.Runtime.InteropServices;
using UnityEngine;

[ExecuteAlways]
public class LiquidSurfaceFrameController : MonoBehaviour
{
    // must match struct layout used in compute shader
    // each vertex stores:
        // its original/rest position
        // its current vertical liquid displacement
        // its current vertical velocity
    [StructLayout(LayoutKind.Sequential)]
    private struct VertexState
    {
        public Vector3 restPosOS;
        public float heightOffset;
        public float heightVelocity;
    }

    [Header("References")]
    [SerializeField] private Transform bottleTransform;
    [SerializeField] private Transform surfaceFrame;
    [SerializeField] private LiquidSurfaceGrid surfaceGrid;
    [SerializeField] private Renderer liquidSurfaceRenderer;
    [SerializeField] private Renderer liquidVolumeRenderer;
    [SerializeField] private ComputeShader rippleCompute;

    [Header("Fill / Surface Frame")]
    [SerializeField] private Vector3 surfaceLocalCenter = Vector3.zero;
    [SerializeField] private float normalSpring = 8f;
    [SerializeField, Range(0.5f, 1f)] private float normalDamping = 0.96f;
    [SerializeField] private float maxAngularSpeedDegrees = 540f;
    [SerializeField] private float apparentGravityInfluence = 0.15f;

    [Header("Motion Input")]
    [SerializeField] private float accelerationSmoothing = 8f;
    [SerializeField] private float angularSmoothing = 8f;
    [SerializeField] private float maxAcceleration = 35f;
    [SerializeField] private float maxAngularVelocity = 12f;
    [SerializeField] private float teleportDistance = 1.0f;

    [Header("Coherent Slosh")]
    [SerializeField] private float sloshAccelerationInfluence = 0.035f;
    [SerializeField] private float sloshAngularInfluence = 0.08f;
    [SerializeField] private float sloshSpring = 7f;
    [SerializeField, Range(0.5f, 1f)] private float sloshDamping = 0.96f;
    [SerializeField] private float maxSloshAmount = 0.22f;

    [Header("Ripple Simulation")]
    [SerializeField] private float rippleSpringStrength = 7f;
    [SerializeField, Range(0.8f, 1f)] private float velocityDamping = 0.994f;
    [SerializeField] private float waveStrength = 12f;
    [SerializeField] private float maxRippleHeight = 0.08f;

    [Header("Surface Footprint")]
    [SerializeField] private float containerRadiusX = 0.48f;
    [SerializeField] private float containerRadiusZ = 0.48f;
    [SerializeField] private float edgeSoftness = 0.06f;
    [SerializeField, Range(0.5f, 1f)] private float edgeDamping = 0.84f;

    [Header("Dynamic Surface Footprint")]
    [SerializeField] private bool useDynamicFootprint = true;
    [SerializeField] private Vector3 containerAxisLocal = Vector3.forward;
    [SerializeField] private float containerRadius = 0.5f;
    [SerializeField] private float containerHalfLength = 1.0f;
    [SerializeField] private float minAxisAlignment = 0.15f;
    [SerializeField] private float footprintPadding = 0.02f;

    private float currentContainerRadiusX;
    private float currentContainerRadiusZ;

    [Header("Volume Seam")]
    [SerializeField] private float volumeSurfaceOverlap = 0.025f;
    [SerializeField] private float volumeTopFadeWidth = 0.08f;

    [Header("Debug")]
    [SerializeField] private bool debugImpulseWithSpace = true;
    [SerializeField] private float debugImpulseStrength = 0.15f;
    [SerializeField] private Vector2 debugImpulseDirection = new Vector2(1f, 0f);

    [Header("Safety")]
    [SerializeField] private float maxDeltaTime = 1f / 30f;

    [Header("Small Wave Disturbance")]
    [SerializeField] private float disturbanceAccelerationGain = 0.015f;
    [SerializeField] private float disturbanceAngularGain = 0.08f;
    [SerializeField] private float disturbanceSloshGain = 1.5f;
    [SerializeField] private float disturbanceDecay = 1.25f;
    [SerializeField] private float maxDisturbanceAmount = 1.0f;

    [Header("Small Wave Shape")]
    [SerializeField] private float detailWaveAmplitude = 0.018f;
    [SerializeField] private float detailWaveFrequency = 18f;
    [SerializeField] private float detailWaveSpeed = 4.5f;

    [SerializeField] private float edgeWaveAmplitude = 0.025f;
    [SerializeField] private float edgeWaveFrequency = 26f;
    [SerializeField] private float edgeWaveSpeed = 5.5f;


    // Current amount of visual disturbance
    private float disturbanceAmount;

    // two compute buffers so the compute shader can read from one buffer and write to the other without overwriting data it still needs
    private ComputeBuffer stateA;
    private ComputeBuffer stateB;

    // True means stateA is current read buffer
    // False means stateB is current read buffer
    private bool stateAIsReadBuffer = true;

    private int kernel;
    private int vertexCount;

    // Motion tracking values
    private Vector3 lastPosition;
    private Vector3 lastVelocityWS;
    private Quaternion lastRotation;

    private Vector3 smoothedAccelerationWS;
    private Vector3 smoothedAngularVelocityWS;

    // This controls the large liquid plane orientation
    private Vector3 currentLiquidNormalWS = Vector3.up;
    private Vector3 liquidAngularVelocityWS;

    // Coherent slosh controls large wave shape
    private Vector2 sloshVector;
    private Vector2 sloshVelocity;

    // Property blocks lets per renderer values to be sent to materials without creating unique material instances every frame
    private MaterialPropertyBlock surfaceBlock;
    private MaterialPropertyBlock volumeBlock;

    // VertexState size
    private const int StateStride = 20;

    private void OnEnable()
    {
        AutoAssignReferences();
        InitializeBuffers();
        ResetMotionHistory(true);
        UpdateSurfaceFrameTransform();
        BindMaterials();
    }

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    private void LateUpdate()
    {
        AutoAssignReferences();

        // Catch for missing refs
        if (bottleTransform == null || surfaceFrame == null || surfaceGrid == null ||
            liquidSurfaceRenderer == null || rippleCompute == null)
        {
            return;
        }

        // If buffers were not created or were released, rebuild them
        if (stateA == null || stateB == null)
        {
            InitializeBuffers();
        }

        if (stateA == null || stateB == null)
        {
            return;
        }

        float dt;

        if (Application.isPlaying)
        {
            dt = Time.unscaledDeltaTime;
        }
        else
        {
            dt = 0.016f;
        }

        dt = Mathf.Clamp(dt, 0.0001f, maxDeltaTime);

        // If object moved too far in one frame, treat it like a teleport (NEED TO CHANGE PROBABLY)
        Vector3 frameDelta = bottleTransform.position - lastPosition;

        if (Application.isPlaying && frameDelta.magnitude > teleportDistance)
        {
            ResetMotionHistory(false);
            DispatchCompute(dt);
            BindMaterials();
            return;
        }

        // Measure bottle movement and rotation
        UpdateMotion(dt);

        // Apparent gravity is gravity adjusted by acceleration
        Vector3 apparentGravityWS = Physics.gravity - smoothedAccelerationWS * apparentGravityInfluence;

        if (apparentGravityWS.sqrMagnitude < 0.0001f)
        {
            apparentGravityWS = Vector3.down;
        }

        // liquid surface normal points opposite gravity
        Vector3 desiredNormalWS = -apparentGravityWS.normalized;

        // Smoothly rotate liquid plane toward desired normal
        UpdateLiquidNormal(dt, desiredNormalWS);

        // Move/rotate SurfaceFrame so grid sits on mean liquid plane
        UpdateSurfaceFrameTransform();

        // Convert motion into SurfaceFrame space
        Vector3 accelInSurfaceFrame = surfaceFrame.InverseTransformDirection(smoothedAccelerationWS);
        Vector3 angularInSurfaceFrame = surfaceFrame.InverseTransformDirection(smoothedAngularVelocityWS);

        // Update large slosh vector
        UpdateCoherentSlosh(dt, accelInSurfaceFrame, angularInSurfaceFrame);

        // Update smaller detail wave intensity
        UpdateDisturbance(dt, accelInSurfaceFrame, angularInSurfaceFrame);

        // Debug impulse for testing
        if (Application.isPlaying && debugImpulseWithSpace && Input.GetKeyDown(KeyCode.Space))
        {
            Vector2 dir;

            if (debugImpulseDirection.sqrMagnitude > 0.0001f)
            {
                dir = debugImpulseDirection.normalized;
            }
            else
            {
                dir = Vector2.right;
            }

            sloshVelocity += dir * debugImpulseStrength;
            disturbanceAmount = maxDisturbanceAmount;
        }

        DispatchCompute(dt);
        BindMaterials();
        lastPosition = bottleTransform.position;
    }

    private ComputeBuffer GetReadBuffer()
    {
        if (stateAIsReadBuffer)
        {
            return stateA;
        }

        return stateB;
    }
    private void UpdateDynamicFootprint()
    {
        if (!useDynamicFootprint)
        {
            currentContainerRadiusX = containerRadiusX;
            currentContainerRadiusZ = containerRadiusZ;
            return;
        }

        if (bottleTransform == null || surfaceFrame == null)
        {
            currentContainerRadiusX = containerRadiusX;
            currentContainerRadiusZ = containerRadiusZ;
            return;
        }

        Vector3 bottleAxisWS = bottleTransform.TransformDirection(containerAxisLocal).normalized;
        Vector3 liquidNormalWS = surfaceFrame.up.normalized;

        float axisAlignment = Mathf.Abs(Vector3.Dot(bottleAxisWS, liquidNormalWS));
        axisAlignment = Mathf.Max(axisAlignment, minAxisAlignment);

        float stretchedRadius = containerRadius / axisAlignment;
        float longRadius = Mathf.Min(stretchedRadius, containerHalfLength);

        // Local X is width
        currentContainerRadiusX = Mathf.Max(0.01f, containerRadius - footprintPadding);

        // Local Z is bottle length because LookRotation uses forward as local Z
        currentContainerRadiusZ = Mathf.Max(0.01f, longRadius - footprintPadding);
    }
    private ComputeBuffer GetWriteBuffer()
    {
        if (stateAIsReadBuffer)
        {
            return stateB;
        }

        return stateA;
    }

    private void AutoAssignReferences()
    {
        // If no bottle transform is assigned, assume parent object is bottle
        if (bottleTransform == null)
        {
            if (transform.parent != null)
            {
                bottleTransform = transform.parent;
            }
            else
            {
                bottleTransform = transform;
            }
        }

        // Find surface grid under this object if it was not assigned
        if (surfaceGrid == null)
        {
            surfaceGrid = GetComponentInChildren<LiquidSurfaceGrid>();
        }

        // Use grids renderer as surface renderer
        if (surfaceGrid != null && liquidSurfaceRenderer == null)
        {
            liquidSurfaceRenderer = surfaceGrid.GetComponent<Renderer>();
        }

        // If no surface frame was assigned, use grids parent
        if (surfaceGrid != null && surfaceFrame == null)
        {
            if (surfaceGrid.transform.parent != null)
            {
                surfaceFrame = surfaceGrid.transform.parent;
            }
            else
            {
                surfaceFrame = surfaceGrid.transform;
            }
        }
    }

    private void InitializeBuffers()
    {
        if (surfaceGrid == null || rippleCompute == null)
        {
            return;
        }

        // Generate/update grid mesh
        surfaceGrid.Generate();

        MeshFilter meshFilter = surfaceGrid.GetComponent<MeshFilter>();

        if (meshFilter == null)
        {
            return;
        }

        Mesh mesh;

        if (Application.isPlaying)
        {
            mesh = meshFilter.mesh;
        }
        else
        {
            mesh = meshFilter.sharedMesh;
        }

        if (mesh == null)
        {
            return;
        }

        Vector3[] vertices = mesh.vertices;
        vertexCount = vertices.Length;

        if (vertexCount <= 0)
        {
            return;
        }

        // Clean up old buffers before creating new ones
        ReleaseBuffers();

        VertexState[] initialState = new VertexState[vertexCount];

        // Store each vertex's original local position
            // compute shader uses this as its stable rest position
        for (int i = 0; i < vertexCount; i++)
        {
            VertexState vertexState = new VertexState();

            vertexState.restPosOS = vertices[i];
            vertexState.heightOffset = 0f;
            vertexState.heightVelocity = 0f;

            initialState[i] = vertexState;
        }

        // Create two buffers for ping pong simulation
        stateA = new ComputeBuffer(vertexCount, StateStride);
        stateB = new ComputeBuffer(vertexCount, StateStride);

        stateA.SetData(initialState);
        stateB.SetData(initialState);

        stateAIsReadBuffer = true;

        // Find compute shader kernel
        kernel = rippleCompute.FindKernel("CSMain");

        // Create material property blocks if needed
        if (surfaceBlock == null)
        {
            surfaceBlock = new MaterialPropertyBlock();
        }

        if (volumeBlock == null)
        {
            volumeBlock = new MaterialPropertyBlock();
        }
    }

    private void ResetMotionHistory(bool resetNormal)
    {
        if (bottleTransform == null)
        {
            return;
        }

        lastPosition = bottleTransform.position;
        lastRotation = bottleTransform.rotation;

        lastVelocityWS = Vector3.zero;
        smoothedAccelerationWS = Vector3.zero;
        smoothedAngularVelocityWS = Vector3.zero;

        liquidAngularVelocityWS = Vector3.zero;

        sloshVector = Vector2.zero;
        sloshVelocity = Vector2.zero;

        disturbanceAmount = 0f;

        if (resetNormal)
        {
            currentLiquidNormalWS = Vector3.up;
        }
    }

    private void UpdateMotion(float dt)
    {
        if (!Application.isPlaying)
        {
            lastPosition = bottleTransform.position;
            lastRotation = bottleTransform.rotation;
            lastVelocityWS = Vector3.zero;
            smoothedAccelerationWS = Vector3.zero;
            smoothedAngularVelocityWS = Vector3.zero;
            return;
        }

        // Linear velocity and acceleration
        Vector3 velocityWS = (bottleTransform.position - lastPosition) / dt;
        Vector3 accelerationWS = (velocityWS - lastVelocityWS) / dt;

        accelerationWS = Vector3.ClampMagnitude(accelerationWS, maxAcceleration);

        // Smooth acceleration to avoid jittery spikes
        float accelBlend = 1f - Mathf.Exp(-accelerationSmoothing * dt);
        smoothedAccelerationWS = Vector3.Lerp(smoothedAccelerationWS, accelerationWS, accelBlend);

        lastVelocityWS = velocityWS;

        // Angular velocity from rotation delta
        Quaternion deltaRotation = bottleTransform.rotation * Quaternion.Inverse(lastRotation);

        float angleDegrees;
        Vector3 axisWS;

        deltaRotation.ToAngleAxis(out angleDegrees, out axisWS);

        if (angleDegrees > 180f)
        {
            angleDegrees -= 360f;
        }

        Vector3 angularVelocityWS = Vector3.zero;

        if (axisWS.sqrMagnitude > 0.0001f)
        {
            angularVelocityWS = axisWS.normalized * (angleDegrees * Mathf.Deg2Rad / dt);
        }

        angularVelocityWS = Vector3.ClampMagnitude(angularVelocityWS, maxAngularVelocity);

        // Smooth angular velocity to reduce jitter
        float angularBlend = 1f - Mathf.Exp(-angularSmoothing * dt);
        smoothedAngularVelocityWS = Vector3.Lerp(smoothedAngularVelocityWS, angularVelocityWS, angularBlend);

        lastRotation = bottleTransform.rotation;
    }

    private void UpdateLiquidNormal(float dt, Vector3 desiredNormalWS)
    {
        currentLiquidNormalWS.Normalize();
        desiredNormalWS.Normalize();

        // Find rotation from current liquid normal to desired normal
        Quaternion deltaRotation = Quaternion.FromToRotation(currentLiquidNormalWS, desiredNormalWS);

        float angleDegrees;
        Vector3 axisWS;

        deltaRotation.ToAngleAxis(out angleDegrees, out axisWS);

        if (angleDegrees > 180f)
        {
            angleDegrees -= 360f;
        }

        // Add angular velocity toward target normal
        if (axisWS.sqrMagnitude > 0.0001f)
        {
            float angleRadians = angleDegrees * Mathf.Deg2Rad;
            liquidAngularVelocityWS += axisWS.normalized * (angleRadians * normalSpring * dt);
        }

        // Dampen liquid normal movement
        liquidAngularVelocityWS *= Mathf.Pow(normalDamping, dt * 60f);

        float maxAngularSpeed = maxAngularSpeedDegrees * Mathf.Deg2Rad;
        liquidAngularVelocityWS = Vector3.ClampMagnitude(liquidAngularVelocityWS, maxAngularSpeed);

        // Rotate current normal by angular velocity
        float stepAngle = liquidAngularVelocityWS.magnitude * dt;

        if (stepAngle > 0.000001f)
        {
            Quaternion stepRotation = Quaternion.AngleAxis(
                stepAngle * Mathf.Rad2Deg,
                liquidAngularVelocityWS.normalized
            );

            currentLiquidNormalWS = (stepRotation * currentLiquidNormalWS).normalized;
        }
    }

    private void UpdateSurfaceFrameTransform()
    {
        if (bottleTransform == null || surfaceFrame == null)
        {
            return;
        }

        surfaceFrame.position = bottleTransform.TransformPoint(surfaceLocalCenter);

        Vector3 bottleAxisWS = bottleTransform.TransformDirection(containerAxisLocal).normalized;

        // Project bottle length direction onto liquid plane
        Vector3 forwardWS = Vector3.ProjectOnPlane(bottleAxisWS, currentLiquidNormalWS);

        if (forwardWS.sqrMagnitude < 0.0001f)
        {
            forwardWS = Vector3.ProjectOnPlane(bottleTransform.right, currentLiquidNormalWS);
        }

        if (forwardWS.sqrMagnitude < 0.0001f)
        {
            forwardWS = Vector3.forward;
        }

        forwardWS.Normalize();

        surfaceFrame.rotation = Quaternion.LookRotation(forwardWS, currentLiquidNormalWS);

        UpdateDynamicFootprint();
    }

    private void UpdateCoherentSlosh(float dt, Vector3 accelSF, Vector3 angularSF)
    {
        // Acceleration makes liquid pile up opposite the direction of motion
        Vector2 accelerationTarget = new Vector2(-accelSF.x, -accelSF.z);
        accelerationTarget *= sloshAccelerationInfluence;

        // Rotation also contributes to slosh
        Vector2 angularTarget = new Vector2(-angularSF.z, angularSF.x);
        angularTarget *= sloshAngularInfluence;

        Vector2 target = accelerationTarget + angularTarget;
        target = Vector2.ClampMagnitude(target, maxSloshAmount);

        // Spring slosh vector toward target
        Vector2 error = target - sloshVector;

        sloshVelocity += error * sloshSpring * dt;
        sloshVelocity *= Mathf.Pow(sloshDamping, dt * 60f);

        sloshVector += sloshVelocity * dt;

        if (sloshVector.magnitude > maxSloshAmount)
        {
            sloshVector = sloshVector.normalized * maxSloshAmount;
            sloshVelocity *= 0.5f;
        }
    }

    private void UpdateDisturbance(float dt, Vector3 accelSF, Vector3 angularSF)
    {
        // Disturbance controls small waves/noise
        // It increases when motion is strong and fades over time

        Vector2 accelXZ = new Vector2(accelSF.x, accelSF.z);
        Vector2 angularXZ = new Vector2(angularSF.x, angularSF.z);

        float accelerationInput = accelXZ.magnitude * disturbanceAccelerationGain;
        float angularInput = angularXZ.magnitude * disturbanceAngularGain;
        float sloshInput = sloshVelocity.magnitude * disturbanceSloshGain;

        float input = accelerationInput + angularInput + sloshInput;

        disturbanceAmount = Mathf.Max(
            disturbanceAmount,
            Mathf.Clamp(input, 0f, maxDisturbanceAmount)
        );

        disturbanceAmount *= Mathf.Exp(-disturbanceDecay * dt);
    }

    private void DispatchCompute(float dt)
    {
        if (rippleCompute == null || stateA == null || stateB == null)
        {
            return;
        }

        // Assign current read/write buffers
        rippleCompute.SetBuffer(kernel, "_StateRead", GetReadBuffer());
        rippleCompute.SetBuffer(kernel, "_StateWrite", GetWriteBuffer());

        // Send grid info
        rippleCompute.SetInt("_VertexCount", vertexCount);
        rippleCompute.SetInt("_GridWidth", surfaceGrid.GridWidth);
        rippleCompute.SetInt("_GridHeight", surfaceGrid.GridHeight);

        // Send simulation values
        rippleCompute.SetFloat("_DeltaTime", dt);

        rippleCompute.SetFloat("_SpringStrength", rippleSpringStrength);
        rippleCompute.SetFloat("_VelocityDamping", velocityDamping);
        rippleCompute.SetFloat("_WaveStrength", waveStrength);
        rippleCompute.SetFloat("_MaxRippleHeight", maxRippleHeight);

        // Large coherent slosh
        rippleCompute.SetVector("_SloshVector", new Vector4(sloshVector.x, sloshVector.y, 0f, 0f));

        // Shape mask for surface
        rippleCompute.SetFloat("_ContainerRadiusX", currentContainerRadiusX);
        rippleCompute.SetFloat("_ContainerRadiusZ", currentContainerRadiusZ);
        rippleCompute.SetFloat("_EdgeSoftness", edgeSoftness);
        rippleCompute.SetFloat("_EdgeDamping", edgeDamping);

        // Small wave/detail layer
        rippleCompute.SetFloat("_TimeValue", Time.time);
        rippleCompute.SetFloat("_DisturbanceAmount", disturbanceAmount);

        rippleCompute.SetFloat("_DetailWaveAmplitude", detailWaveAmplitude);
        rippleCompute.SetFloat("_DetailWaveFrequency", detailWaveFrequency);
        rippleCompute.SetFloat("_DetailWaveSpeed", detailWaveSpeed);

        rippleCompute.SetFloat("_EdgeWaveAmplitude", edgeWaveAmplitude);
        rippleCompute.SetFloat("_EdgeWaveFrequency", edgeWaveFrequency);
        rippleCompute.SetFloat("_EdgeWaveSpeed", edgeWaveSpeed);

        // Dispatch enough thread groups for all vertices
        int threadGroups = Mathf.CeilToInt(vertexCount / 64f);
        rippleCompute.Dispatch(kernel, threadGroups, 1, 1);

        // Swap read/write buffers for next frame
        stateAIsReadBuffer = !stateAIsReadBuffer;
    }

    private void BindMaterials()
    {
        BindSurfaceMaterial();
        BindVolumeMaterial();
    }

    private void BindSurfaceMaterial()
    {
        if (liquidSurfaceRenderer == null || surfaceGrid == null || GetReadBuffer() == null)
        {
            return;
        }

        if (surfaceBlock == null)
        {
            surfaceBlock = new MaterialPropertyBlock();
        }

        liquidSurfaceRenderer.GetPropertyBlock(surfaceBlock);

        // surface shader reads this compute buffer to displace vertices
        surfaceBlock.SetBuffer("_LiquidState", GetReadBuffer());

        // Grid data used by shader to compute normals
        surfaceBlock.SetInt("_GridWidth", surfaceGrid.GridWidth);
        surfaceBlock.SetInt("_GridHeight", surfaceGrid.GridHeight);

        surfaceBlock.SetFloat("_GridSizeX", surfaceGrid.SizeX);
        surfaceBlock.SetFloat("_GridSizeZ", surfaceGrid.SizeZ);

        // Shape mask used by shader
        surfaceBlock.SetFloat("_ContainerRadiusX", currentContainerRadiusX);
        surfaceBlock.SetFloat("_ContainerRadiusZ", currentContainerRadiusZ);
        surfaceBlock.SetFloat("_EdgeSoftness", edgeSoftness);

        liquidSurfaceRenderer.SetPropertyBlock(surfaceBlock);
    }

    private void BindVolumeMaterial()
    {
        if (liquidVolumeRenderer == null || surfaceFrame == null)
        {
            return;
        }

        if (volumeBlock == null)
        {
            volumeBlock = new MaterialPropertyBlock();
        }

        liquidVolumeRenderer.GetPropertyBlock(volumeBlock);

        // volume shader clips body of liquid against this plane
        Vector3 planeNormalWS = surfaceFrame.up.normalized;
        Vector3 planePointWS = surfaceFrame.position;

        float planeDistance = Vector3.Dot(planeNormalWS, planePointWS);

        volumeBlock.SetVector(
            "_LiquidPlaneWS",
            new Vector4(
                planeNormalWS.x,
                planeNormalWS.y,
                planeNormalWS.z,
                planeDistance
            )
        );

        // soften seam where volume meets surface
        volumeBlock.SetFloat("_SurfaceOverlap", volumeSurfaceOverlap);
        volumeBlock.SetFloat("_TopFadeWidth", volumeTopFadeWidth);

        liquidVolumeRenderer.SetPropertyBlock(volumeBlock);
    }

    private void ReleaseBuffers()
    {
        if (stateA != null)
        {
            stateA.Release();
            stateA = null;
        }

        if (stateB != null)
        {
            stateB.Release();
            stateB = null;
        }
    }
}