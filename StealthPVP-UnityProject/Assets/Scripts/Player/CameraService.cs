using UnityEngine;

/// <summary>
/// Centralizes camera follow control. Uses Cinemachine when available, falls back to the legacy CameraController.
/// </summary>
[DisallowMultipleComponent]
public class CameraService : MonoBehaviour
{
#if CINEMACHINE
    [SerializeField] private Cinemachine.CinemachineVirtualCamera virtualCamera;
#endif
    [SerializeField] private CameraController fallbackController;
    [SerializeField] private Transform defaultTarget;

    private Transform _currentTarget;

    public Transform CurrentTarget
    {
        get
        {
#if CINEMACHINE
            if (virtualCamera && virtualCamera.Follow)
            {
                return virtualCamera.Follow;
            }
#endif
            if (_currentTarget)
            {
                return _currentTarget;
            }
            return fallbackController ? fallbackController.CurrentTarget : null;
        }
    }

    private void Awake()
    {
#if CINEMACHINE
        if (!virtualCamera)
        {
            virtualCamera = Object.FindFirstObjectByType<Cinemachine.CinemachineVirtualCamera>();
        }
#endif

        if (!fallbackController)
        {
            fallbackController = Object.FindFirstObjectByType<CameraController>();
        }

        if (!defaultTarget)
        {
            defaultTarget = CurrentTarget;
        }
    }

    public void SetTarget(Transform target, bool snap = false)
    {
        _currentTarget = target;
        ApplyFollowTarget(target, snap);
    }

    public void RestoreDefault()
    {
        SetTarget(defaultTarget, true);
    }

    private void ApplyFollowTarget(Transform target, bool snap)
    {
#if CINEMACHINE
        if (virtualCamera)
        {
            virtualCamera.Follow = target;

            if (snap && target)
            {
                Vector3 cameraPosition = virtualCamera.transform.position;
                Vector3 targetPosition = target.position;
                Vector3 delta = targetPosition - cameraPosition;
                virtualCamera.OnTargetObjectWarped(target, delta);
            }
        }
#endif

        if (fallbackController)
        {
            fallbackController.SetTarget(target);
        }
    }
}
