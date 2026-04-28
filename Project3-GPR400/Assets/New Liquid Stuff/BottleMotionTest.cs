using UnityEngine;

public class BottleMotionTest : MonoBehaviour
{
    [SerializeField] private float moveAmount = 0.25f;
    [SerializeField] private float moveSpeed = 1.4f;

    [SerializeField] private float rotateAmount = 10f;
    [SerializeField] private float rotateSpeed = 1.1f;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    private void Update()
    {
        float t = Time.time;

        transform.position = startPosition + new Vector3(
            Mathf.Sin(t * moveSpeed) * moveAmount,
            0f,
            Mathf.Cos(t * moveSpeed * 0.7f) * moveAmount * 0.5f
        );

        transform.rotation = startRotation * Quaternion.Euler(
            Mathf.Sin(t * rotateSpeed) * rotateAmount,
            0f,
            Mathf.Sin(t * rotateSpeed * 0.8f) * rotateAmount
        );
    }
}