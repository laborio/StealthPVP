using System.Collections;
using UnityEngine;

/// <summary>
/// Simple camera shake utility. Attach to the camera transform you want to shake.
/// </summary>
[DisallowMultipleComponent]
public class CameraShake : MonoBehaviour
{
    [SerializeField, Tooltip("Default shake duration in seconds.")] private float defaultDuration = 0.2f;
    [SerializeField, Tooltip("Default shake magnitude in units.")] private float defaultMagnitude = 0.2f;
    [SerializeField, Tooltip("Enable debug logging for camera shake events.")] private bool debugLogs = true;
    [SerializeField, Tooltip("Transform to apply shake to; defaults to this transform. For Cinemachine, set this to a parent/pivot not driven by the brain.")] private Transform shakeTarget;

    public static CameraShake Instance { get; private set; }
    public Vector3 CurrentOffset => _currentOffset;
    public static Vector3 CurrentOffsetGlobal => Instance ? Instance._currentOffset : Vector3.zero;

    private Coroutine _shakeRoutine;
    private Vector3 _currentOffset;

    public void Shake(float magnitude = -1f, float duration = -1f)
    {
        if (!shakeTarget)
        {
            shakeTarget = transform;
            LogDebug($"ShakeTarget not set, defaulting to {shakeTarget.name}");
        }

        float useDuration = duration > 0f ? duration : defaultDuration;
        float useMagnitude = magnitude > 0f ? magnitude : defaultMagnitude;

        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
        }

        LogDebug($"Shake start mag={useMagnitude} dur={useDuration}");
        _shakeRoutine = StartCoroutine(ShakeRoutine(useMagnitude, useDuration));
    }

    private IEnumerator ShakeRoutine(float magnitude, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Wait until end of frame so other camera controllers finish positioning.
            yield return new WaitForEndOfFrame();

            // Remove previous offset to track the latest base position.
            shakeTarget.localPosition -= _currentOffset;

            elapsed += Time.deltaTime;
            float damper = 1f - Mathf.Clamp01(elapsed / duration);
            _currentOffset = Random.insideUnitSphere * magnitude * damper;
            shakeTarget.localPosition += _currentOffset;
        }

        shakeTarget.localPosition -= _currentOffset;
        _currentOffset = Vector3.zero;
        _shakeRoutine = null;
        LogDebug("Shake finished");
    }

    private void OnDisable()
    {
        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
        }

        if (shakeTarget)
        {
            shakeTarget.localPosition -= _currentOffset;
        }
        _currentOffset = Vector3.zero;
        LogDebug("Shake cancelled/OnDisable");
    }

    private void Awake()
    {
        if (!shakeTarget)
        {
            shakeTarget = transform;
        }

        if (!Instance || IsMainCameraComponent(Instance))
        {
            Instance = this;
        }
        else if (IsMainCameraComponent(this) && Instance != this)
        {
            Instance = this;
        }
    }

    private bool IsMainCameraComponent(CameraShake shake)
    {
        if (!shake)
        {
            return false;
        }

        Camera cam = shake.GetComponentInParent<Camera>();
        return cam && cam == Camera.main;
    }

    /// <summary>
    /// Static helper to shake whichever CameraShake is available (prefers one on the main camera).
    /// </summary>
    public static void ShakeGlobal(float magnitude = -1f, float duration = -1f)
    {
        CameraShake target = Instance;
        if (!target)
        {
            target = FindFirstObjectByType<CameraShake>();
            if (target)
            {
                Instance = target;
            }
        }

        target?.Shake(magnitude, duration);
    }

    private void OnValidate()
    {
        defaultDuration = Mathf.Max(0f, defaultDuration);
        defaultMagnitude = Mathf.Max(0f, defaultMagnitude);
    }

    private void LogDebug(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log($"[CameraShake:{name}] {message}", this);
    }
}
