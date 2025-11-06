using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 45f, -21f);
    [SerializeField] private Vector3 rotationEuler = new Vector3(65f, 0f, 0f);
    [SerializeField] private float followSmoothTime = 0.2f;

    private Vector3 _velocity;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("CameraController: No target assigned, camera will stay in place.");
            return;
        }

        // Snap into place on start to avoid a noticeable first-frame jump.
        transform.position = target.position + offset;
        transform.rotation = Quaternion.Euler(rotationEuler);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _velocity, followSmoothTime);
        transform.rotation = Quaternion.Euler(rotationEuler);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
