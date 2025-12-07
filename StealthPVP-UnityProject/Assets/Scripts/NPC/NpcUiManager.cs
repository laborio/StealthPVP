using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles target-related UI such as a directional radial indicator and color.
/// </summary>
[DisallowMultipleComponent]
public class NpcUiManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Player transform used for forward/direction reference.")] private Transform playerTransform;
    [SerializeField, Tooltip("Camera used to test if the target is on screen. Defaults to main camera.")] private Camera targetCamera;
    [SerializeField, Tooltip("UI Image set to radial 360 fill. Rotation indicates direction; fill indicates proximity.")] private Image targetDirectionImage;
    [SerializeField, Tooltip("Enable debug logs for target assignment/clearing.")] private bool debugLogs = false;

    [Header("Fill Settings")]
    [SerializeField, Tooltip("Distance at which the fill is considered full (excluding on-screen override).")] private float minFullDistance = 5f;
    [SerializeField, Tooltip("Distance at which the fill is at its minimum.")] private float maxEmptyDistance = 50f;
    [SerializeField, Tooltip("If the target stays on screen for this duration, the fill becomes full.")] private float onScreenFullFillDelay = 2f;
    [SerializeField, Tooltip("Viewport margin (0-0.5) used to treat targets well within the screen rectangle as full fill immediately.")] [Range(0f, 0.5f)] private float screenFullMargin = 0.05f;

    private NpcIdentity _currentTarget;
    private float _onScreenTimer;

    private void Awake()
    {
        if (!targetCamera)
        {
            targetCamera = Camera.main;
        }
    }

    private void Update()
    {
        UpdateIndicator();
    }

    public void SetTarget(NpcIdentity identity)
    {
        _currentTarget = identity;
        _onScreenTimer = 0f;
        if (targetDirectionImage)
        {
            targetDirectionImage.enabled = identity != null;
            if (identity)
            {
                targetDirectionImage.color = identity.IdentifierColor;
            }
        }
        LogDebug(identity ? $"Set target UI -> {identity.name}" : "Set target UI -> null");
    }

    public void ClearTarget()
    {
        _currentTarget = null;
        _onScreenTimer = 0f;
        if (targetDirectionImage)
        {
            targetDirectionImage.enabled = false;
        }
        LogDebug("Cleared target UI");
    }

    private void UpdateIndicator()
    {
        if (!_currentTarget || !playerTransform || !targetDirectionImage)
        {
            return;
        }

        Transform targetTransform = _currentTarget.transform;
        if (!targetTransform)
        {
            ClearTarget();
            return;
        }

        Vector3 toTarget = targetTransform.position - playerTransform.position;
        Vector3 planarToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
        if (planarToTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // Direction: rotate slice toward the target in world-space (up = world forward).
        planarToTarget.Normalize();
        float angle = Mathf.Atan2(planarToTarget.x, planarToTarget.z) * Mathf.Rad2Deg;
        targetDirectionImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);

        // Distance-based fill with screen-rectangle override.
        float distance = toTarget.magnitude;
        float baseFill = Mathf.InverseLerp(maxEmptyDistance, minFullDistance, distance);
        baseFill = Mathf.Clamp01(baseFill);

        Vector3 viewport = targetCamera ? targetCamera.WorldToViewportPoint(targetTransform.position) : new Vector3(-1f, -1f, -1f);
        bool onScreen = viewport.z > 0f && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f;
        bool wellInsideScreen = onScreen &&
                                viewport.x > screenFullMargin &&
                                viewport.x < 1f - screenFullMargin &&
                                viewport.y > screenFullMargin &&
                                viewport.y < 1f - screenFullMargin;

        if (onScreen)
        {
            _onScreenTimer += Time.deltaTime;
        }
        else
        {
            _onScreenTimer = 0f;
        }

        if (wellInsideScreen || _onScreenTimer >= onScreenFullFillDelay)
        {
            baseFill = 1f;
        }

        targetDirectionImage.fillAmount = baseFill;
    }

    private void LogDebug(string message)
    {
        if (debugLogs)
        {
            Debug.Log($"[NpcUiManager] {message}", this);
        }
    }
}
