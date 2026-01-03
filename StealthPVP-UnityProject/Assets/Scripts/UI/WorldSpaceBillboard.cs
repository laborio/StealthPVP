using UnityEngine;

/// <summary>
/// Keeps a world-space canvas or UI element facing the active camera.
/// </summary>
public class WorldSpaceBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool lockYAxis = true;
    [SerializeField, Tooltip("Face the camera currently rendering this object for multi-camera setups.")]
    private bool useRenderingCamera;

    private void LateUpdate()
    {
        if (useRenderingCamera)
        {
            return;
        }

        if (!ValidateCamera())
        {
            return;
        }

        FaceCamera(targetCamera);
    }

    private void OnWillRenderObject()
    {
        if (!useRenderingCamera)
        {
            return;
        }

        Camera renderingCamera = Camera.current;
        if (!renderingCamera)
        {
            return;
        }

        FaceCamera(renderingCamera);
    }

    private bool ValidateCamera()
    {
        if (targetCamera)
        {
            return true;
        }

        targetCamera = ResolveOwnerCamera();
        if (targetCamera)
        {
            return true;
        }

        targetCamera = Camera.main;
        return targetCamera;
    }

    private Camera ResolveOwnerCamera()
    {
        PlayerInputRouter inputRouter = GetComponentInParent<PlayerInputRouter>();
        if (inputRouter)
        {
            return inputRouter.ResolveCamera();
        }

        return null;
    }

    private void FaceCamera(Camera cameraToFace)
    {
        Vector3 lookDirection = cameraToFace.transform.position - transform.position;
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

    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
    }

    public void SetUseRenderingCamera(bool value)
    {
        useRenderingCamera = value;
    }

    public Camera TargetCamera => targetCamera;
    public bool UseRenderingCamera => useRenderingCamera;
}
