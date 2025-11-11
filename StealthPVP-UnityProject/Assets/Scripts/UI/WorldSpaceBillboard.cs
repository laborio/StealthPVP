using UnityEngine;

/// <summary>
/// Keeps a world-space canvas or UI element facing the active camera.
/// </summary>
public class WorldSpaceBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool lockYAxis = true;

    private void LateUpdate()
    {
        if (!ValidateCamera())
        {
            return;
        }

        Vector3 lookDirection = targetCamera.transform.position - transform.position;
        if (lockYAxis)
        {
            lookDirection.y = 0f;
        }

        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(-lookDirection.normalized, Vector3.up);
    }

    private bool ValidateCamera()
    {
        if (targetCamera)
        {
            return true;
        }

        targetCamera = Camera.main;
        return targetCamera;
    }
}
