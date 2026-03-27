using UnityEngine;

[ExecuteAlways]
public class LiquidWobble : MonoBehaviour
{
    [SerializeField] private Renderer liquidRenderer;

    [Header("Fill")]
    [SerializeField, Range(-1f, 1f)] private float fillOffset = 0f;

    [Header("Wobble")]
    [SerializeField] private float maxWobble = 0.03f;
    [SerializeField] private float wobbleSpeed = 2.0f;
    [SerializeField] private float recovery = 2.0f;
    [SerializeField] private float movementInfluence = 1.0f;
    [SerializeField] private float rotationInfluence = 0.2f;

    private Material liquidMaterial;
    private Vector3 lastPosition;
    private Vector3 lastEulerAngles;
    private float wobbleAddX;
    private float wobbleAddZ;

    private void OnEnable()
    {
        if (liquidRenderer != null)
            liquidMaterial = liquidRenderer.sharedMaterial;

        lastPosition = transform.position;
        lastEulerAngles = transform.eulerAngles;
    }

    private void Update()
    {
        if (liquidRenderer == null)
            return;

        if (liquidMaterial == null)
            liquidMaterial = liquidRenderer.material;

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
        Bounds b = liquidRenderer.bounds;
        Vector3 surfaceOriginWS = b.center + Vector3.up * fillOffset;

        // Send the surface origin into the shader
        liquidMaterial.SetVector("_SurfaceOriginWS", new Vector4(
            surfaceOriginWS.x,
            surfaceOriginWS.y,
            surfaceOriginWS.z,
            0f
        ));

        // Send the wobble values into the shader for tilt
        liquidMaterial.SetFloat("_WobbleX", wobbleX);
        liquidMaterial.SetFloat("_WobbleZ", wobbleZ);

        // Store current frame's transform so the next frame can measure movement against it
        lastPosition = transform.position;
        lastEulerAngles = transform.eulerAngles;
    }
}