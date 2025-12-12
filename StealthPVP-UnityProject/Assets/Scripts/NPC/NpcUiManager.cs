using UnityEngine;
using TMPro;

/// <summary>
/// Handles target-related UI such as a directional radial indicator and color.
/// </summary>
[DisallowMultipleComponent]
public class NpcUiManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Player transform used for forward/direction reference.")] private Transform playerTransform;
    [SerializeField, Tooltip("Player vision source used to read level 2 height threshold.")] private VisionSource playerVisionSource;
    [SerializeField, Tooltip("Root transform for the compass circle (SpriteRenderer). Rotates around Z toward the target.")] private Transform compassRoot;
    [SerializeField, Tooltip("Arrow sprite placed on the compass edge that points toward the target.")] private SpriteRenderer arrowRenderer;
    [SerializeField, Tooltip("Full circle sprite shown while reveal is active and target is in view.")] private SpriteRenderer revealCircleRenderer;
    [SerializeField, Tooltip("Camera used to test if the target is on screen. Defaults to main camera.")] private Camera worldCamera;
    [SerializeField, Tooltip("TMP text to display remaining cooldown (seconds) for the reveal ability.")] private TMP_Text abilityCooldownText;
    [SerializeField, Tooltip("Enable debug logs for target assignment/clearing.")] private bool debugLogs = false;

    [Header("Ability Config")]
    [SerializeField, Tooltip("Optional shared ability configuration. If set, Reveal uses this entry for player settings.")] private AbilityConfig abilityConfig;
    [SerializeField, Tooltip("Ability id to use for reveal ability.")] private string revealAbilityId = "Reveal";

    [Header("Arrow Visibility")]
    [SerializeField, Tooltip("Seconds to fade arrow in/out when entering/leaving high ground level 2.")] private float verticalFadeDuration = 0.4f;
    [SerializeField, Tooltip("Seconds the reveal ability holds full arrow visibility before fading.")] private float abilityFullVisibilitySeconds = 2f;
    [SerializeField, Tooltip("Seconds to fade arrow visibility for the reveal skill.")] private float abilityFadeDuration = 1f;
    [SerializeField, Tooltip("Cooldown seconds for the reveal skill.")] private float abilityCooldownSeconds = 30f;
    [SerializeField, Tooltip("Key to trigger the reveal skill.")] private KeyCode abilityKey = KeyCode.F;
    [SerializeField, Tooltip("Seconds to fade the reveal circle in/out when toggling.")] private float circleFadeDuration = 0.2f;

    private NpcIdentity _currentTarget;
    private float _currentArrowAlpha;
    private float _currentCircleAlpha;
    private float _verticalAlpha;
    private Color _arrowBaseColor = Color.white;
    private float _abilityCooldownTimer;
    private float _abilityHoldTimer;
    private float _abilityFadeTimer;

    private void Awake()
    {
        ApplyAbilityConfig();
        if (arrowRenderer)
        {
            _arrowBaseColor = arrowRenderer.color;
        }

        if (!worldCamera)
        {
            worldCamera = Camera.main;
        }
    }

    private void Update()
    {
        HandleAbilityInput();
        UpdateIndicator();
        UpdateAbilityCooldownUi();
    }

    public void SetTarget(NpcIdentity identity)
    {
        _currentTarget = identity;
        if (arrowRenderer)
        {
            arrowRenderer.enabled = identity != null;
            if (identity)
            {
                Color c = identity.IdentifierColor;
                c.a = arrowRenderer.color.a;
                arrowRenderer.color = c;
                _arrowBaseColor = c;
            }
        }
        LogDebug(identity ? $"Set target UI -> {identity.name}" : "Set target UI -> null");
    }

    public void ClearTarget()
    {
        _currentTarget = null;
        if (arrowRenderer)
        {
            arrowRenderer.enabled = false;
        }
        LogDebug("Cleared target UI");
    }

    private void HandleAbilityInput()
    {
        if (_abilityCooldownTimer > 0f)
        {
            _abilityCooldownTimer = Mathf.Max(0f, _abilityCooldownTimer - Time.deltaTime);
        }

        if (Input.GetKeyDown(abilityKey) && _abilityCooldownTimer <= 0f)
        {
            _abilityHoldTimer = abilityFullVisibilitySeconds;
            _abilityFadeTimer = abilityFadeDuration;
            _abilityCooldownTimer = abilityCooldownSeconds;
        }

        if (_abilityHoldTimer > 0f)
        {
            _abilityHoldTimer = Mathf.Max(0f, _abilityHoldTimer - Time.deltaTime);
        }
        else if (_abilityFadeTimer > 0f)
        {
            _abilityFadeTimer = Mathf.Max(0f, _abilityFadeTimer - Time.deltaTime);
        }
    }

    private void ApplyAbilityConfig()
    {
        if (!abilityConfig)
        {
            return;
        }

        AbilityDefinition def = abilityConfig.GetAbility(revealAbilityId, isPlayer: true);
        if (def != null)
        {
            abilityKey = def.Key;
            abilityCooldownSeconds = def.CooldownSeconds;
            abilityFullVisibilitySeconds = def.FullDurationSeconds;
            abilityFadeDuration = def.FadeDurationSeconds;
        }
    }

    private void UpdateArrowVisibility(bool targetInView)
    {
        // Height-based visibility (faded with verticalFadeDuration).
        float verticalTarget = 0f;
        if (playerVisionSource)
        {
            float threshold = playerVisionSource.level2MinHeight;
            float playerY = playerTransform ? playerTransform.position.y : 0f;
            verticalTarget = playerY >= threshold ? 1f : 0f;
        }

        if (verticalFadeDuration <= 0f)
        {
            _verticalAlpha = verticalTarget;
        }
        else
        {
            float step = Time.deltaTime / Mathf.Max(0.0001f, verticalFadeDuration);
            _verticalAlpha = Mathf.MoveTowards(_verticalAlpha, verticalTarget, step);
        }

        // Ability-based visibility: full for hold duration, then fades 1 -> 0 over abilityFadeDuration.
        float abilityAlpha = 0f;
        if (_abilityHoldTimer > 0f)
        {
            abilityAlpha = 1f;
        }
        else if (_abilityFadeTimer > 0f && abilityFadeDuration > 0.0001f)
        {
            abilityAlpha = Mathf.Clamp01(_abilityFadeTimer / abilityFadeDuration);
        }

        float baseAlpha = Mathf.Max(_verticalAlpha, abilityAlpha);

        float desiredArrowAlpha = (targetInView && abilityAlpha > 0f) ? 0f : baseAlpha;
        float desiredCircleAlpha = (targetInView && abilityAlpha > 0f) ? abilityAlpha : 0f;

        if (verticalFadeDuration <= 0f)
        {
            _currentArrowAlpha = desiredArrowAlpha;
        }
        else
        {
            float t = Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.0001f, verticalFadeDuration));
            _currentArrowAlpha = Mathf.Lerp(_currentArrowAlpha, desiredArrowAlpha, t);
        }

        if (circleFadeDuration <= 0f)
        {
            _currentCircleAlpha = desiredCircleAlpha;
        }
        else
        {
            float t = Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.0001f, circleFadeDuration));
            _currentCircleAlpha = Mathf.Lerp(_currentCircleAlpha, desiredCircleAlpha, t);
        }

        if (arrowRenderer)
        {
            Color c = _arrowBaseColor;
            c.a = _currentArrowAlpha;
            arrowRenderer.color = c;
        }

        if (revealCircleRenderer)
        {
            Color c = revealCircleRenderer.color;
            c.a = _currentCircleAlpha;
            revealCircleRenderer.color = c;
            revealCircleRenderer.enabled = _currentCircleAlpha > 0.001f;
        }
    }

    private void UpdateAbilityCooldownUi()
    {
        if (!abilityCooldownText)
        {
            return;
        }

        if (_abilityCooldownTimer > 0f)
        {
            abilityCooldownText.text = Mathf.CeilToInt(_abilityCooldownTimer).ToString();
        }
        else
        {
            abilityCooldownText.text = string.Empty;
        }
    }

    private bool IsTargetInView(Transform target)
    {
        if (!worldCamera || !target)
        {
            return false;
        }

        Vector3 viewport = worldCamera.WorldToViewportPoint(target.position);
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    private void UpdateIndicator()
    {
        if (!_currentTarget || !playerTransform || !compassRoot || !arrowRenderer)
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

        // Direction: rotate compass (and its child arrow) toward the target in world-space (up = world forward).
        planarToTarget.Normalize();
        float yawWorld = Mathf.Atan2(planarToTarget.x, planarToTarget.z) * Mathf.Rad2Deg;
        float parentYaw = compassRoot.parent ? compassRoot.parent.eulerAngles.y : 0f;
        float localYaw = Mathf.DeltaAngle(parentYaw, yawWorld);
        // Root has zeroed rotation; rotate only around Y so the arrow's local X offset (e.g., 90,0,0) still points outward.
        compassRoot.localRotation = Quaternion.Euler(0f, localYaw, 0f);

        bool targetInView = IsTargetInView(targetTransform);
        UpdateArrowVisibility(targetInView);
    }

    private void LogDebug(string message)
    {
        if (debugLogs)
        {
            Debug.Log($"[NpcUiManager] {message}", this);
        }
    }
}
