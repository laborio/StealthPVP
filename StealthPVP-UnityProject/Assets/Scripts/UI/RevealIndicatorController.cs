using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space reveal compass circle driven by reveal ability + target visibility.
/// </summary>
[DisallowMultipleComponent]
public class RevealIndicatorController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private VisionSource playerVisionSource;
    [SerializeField] private Transform compassRoot;
    [SerializeField] private Image compassCircle;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private AbilityRunner revealAbility;
    [SerializeField, Tooltip("Fog manager used to determine visibility (target must be out of fog). Optional.")] private FogOfWarManager fogManager;
    [SerializeField, Tooltip("Optional fog manager name to auto-find (e.g., Fog_P1 or Fog_P2).")] private string fogManagerNameHint;

    [Header("Targets")]
    [SerializeField] private NpcIdentity currentTarget;

    [Header("Fill Amount")]
    [SerializeField, Range(0.15f, 0.3f)] private float minFillAmount = 0.15f;
    [SerializeField, Tooltip("Distance where the fill is clamped to minFillAmount.")] private float farDistanceForMinFill = 60f;
    [SerializeField, Tooltip("Distance where the fill reaches 0.3 (max hidden fill) right before popping to 1.0 when visible.")] private float nearDistanceForMaxFill = 10f;
    [SerializeField, Tooltip("Extra Z rotation applied to the circle so the fill origin aligns with player forward.")] private float compassRotationOffset = 90f;
    [Header("Color")]
    [SerializeField, Tooltip("Color when target is far (min fill).")] private Color farFillColor = new Color32(255, 218, 0, 255); // #FFDA00
    [SerializeField, Tooltip("Color when target is near (max fill before visible).")] private Color nearFillColor = new Color32(255, 64, 0, 255); // #FF4000
    [SerializeField, Tooltip("Extra distance added to nearDistanceForMaxFill before reaching near color.")] private float nearColorDistanceOffset = 2f;

    [Header("Visibility (driven by GameplayTuning)")] 
    [HideInInspector] [SerializeField] private float verticalFadeDuration = 0.4f;
    [HideInInspector] [SerializeField] private float circleFadeDuration = 0.2f;
    [SerializeField, Tooltip("If true, compass is visible whenever a target is set (ignores reveal ability state).")] private bool alwaysShowWhenTargetSet = true;

    private float _verticalAlpha;
    private float _currentCircleAlpha;
    private float _currentFillAmount;
    private Color _circleBaseColor = Color.white;
    private NpcIdentity _selfIdentity;
    private float _nextAutoFindTime;
    private float _nextFogFindTime;

    private const float MaxFillBeforeVisible = 0.3f;
    private const float AutoFindInterval = 0.5f;
    private const float FogFindInterval = 0.5f;

    public void SetTarget(NpcIdentity identity)
    {
        currentTarget = identity;
        Debug.Log($"[RevealIndicatorController] SetTarget -> {(identity ? identity.name : "null")}", this);
    }

    public void ClearTarget()
    {
        currentTarget = null;
        Debug.Log("[RevealIndicatorController] ClearTarget", this);
    }

    public void ConfigurePlayer(Transform player, VisionSource vision, AbilityRunner ability, Camera camera = null)
    {
        playerTransform = player;
        playerVisionSource = vision;
        revealAbility = ability;
        if (camera)
        {
            worldCamera = camera;
        }

        CacheSelfIdentity();
        TryAutoAssignFogManager(force: true);
    }

    public void SetWorldCamera(Camera camera)
    {
        worldCamera = camera;
        TryAutoAssignFogManager(force: true);
    }

    public void SetFogManager(FogOfWarManager manager)
    {
        fogManager = manager;
    }

    public void ApplyFadeConfig(float verticalFade, float circleFade)
    {
        verticalFadeDuration = Mathf.Max(0f, verticalFade);
        circleFadeDuration = Mathf.Max(0f, circleFade);
    }

    public void SetAlwaysShowWhenTargetSet(bool value)
    {
        alwaysShowWhenTargetSet = value;
    }

    private void Awake()
    {
        if (compassCircle)
        {
            _circleBaseColor = compassCircle.color;
        }

        if (!worldCamera)
        {
            worldCamera = Camera.main;
        }

        if (!fogManager)
        {
            TryAutoAssignFogManager(force: true);
        }

        CacheSelfIdentity();
    }

    private void OnEnable()
    {
        TryAutoAssignTarget(force: true);
    }

    private void Update()
    {
        UpdateIndicator();
    }

    private void UpdateIndicator()
    {
        TryAutoAssignTarget();
        TryAutoAssignFogManager();

        if (!playerTransform || !compassRoot || !compassCircle)
        {
            Debug.LogWarning("[RevealIndicatorController] Missing refs (player/compass/circle).", this);
            return;
        }

        Transform targetTransform = currentTarget ? currentTarget.transform : null;
        if (!targetTransform)
        {
            Debug.Log("[RevealIndicatorController] No target set; hiding visuals.", this);
            UpdateVisibility(targetVisible: false, hasTarget: false, distanceToTarget: 0f);
            return;
        }

        Vector3 toTarget = targetTransform.position - playerTransform.position;
        Vector3 planarToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
        if (planarToTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        planarToTarget.Normalize();
        float yawWorld = Mathf.Atan2(planarToTarget.x, planarToTarget.z) * Mathf.Rad2Deg;
        float parentYaw = playerTransform ? playerTransform.eulerAngles.y : 0f;
        float localYaw = Mathf.DeltaAngle(parentYaw, yawWorld);
        ApplyCompassRotation(planarToTarget, yawWorld, parentYaw, localYaw);
        // Uncomment for debugging
        // Debug.Log($"[RevealIndicatorController] Rotating to target {currentTarget.name} yawWorld={yawWorld} localYaw={localYaw}", this);

        bool targetVisible = IsTargetVisible(targetTransform);
        UpdateVisibility(targetVisible, hasTarget: true, distanceToTarget: toTarget.magnitude);
    }

    private void TryAutoAssignFogManager(bool force = false)
    {
        if (fogManager)
        {
            return;
        }

        if (!force && Time.time < _nextFogFindTime)
        {
            return;
        }

        _nextFogFindTime = Time.time + FogFindInterval;

        if (!worldCamera)
        {
            worldCamera = Camera.main;
        }

        if (TryResolveFogName(out string fogName))
        {
            FogOfWarManager namedFog = FindFogByName(fogName);
            if (namedFog)
            {
                fogManager = namedFog;
                return;
            }
        }

        FogOfWarCameraBinder binder = worldCamera ? worldCamera.GetComponent<FogOfWarCameraBinder>() : null;
        if (binder && binder.FogManager)
        {
            fogManager = binder.FogManager;
            return;
        }

        FogOfWarManager[] fogs = UnityEngine.Object.FindObjectsByType<FogOfWarManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (playerVisionSource && fogs != null)
        {
            for (int i = 0; i < fogs.Length; i++)
            {
                FogOfWarManager fog = fogs[i];
                if (!fog || fog.visionSources == null)
                {
                    continue;
                }

                for (int j = 0; j < fog.visionSources.Count; j++)
                {
                    if (fog.visionSources[j] == playerVisionSource)
                    {
                        fogManager = fog;
                        return;
                    }
                }
            }
        }

        if (playerTransform)
        {
            FogOfWarManager fogFromPlayer = playerTransform.GetComponentInChildren<FogOfWarManager>(true)
                ?? playerTransform.GetComponentInParent<FogOfWarManager>();
            if (fogFromPlayer)
            {
                fogManager = fogFromPlayer;
                return;
            }
        }

        if (fogs != null && fogs.Length == 1)
        {
            fogManager = fogs[0];
        }
    }

    private bool TryResolveFogName(out string fogName)
    {
        fogName = string.IsNullOrWhiteSpace(fogManagerNameHint) ? null : fogManagerNameHint.Trim();
        if (!string.IsNullOrEmpty(fogName))
        {
            return true;
        }

        string sourceName = null;
        if (playerTransform)
        {
            sourceName = playerTransform.name;
        }
        else if (transform.root)
        {
            sourceName = transform.root.name;
        }
        else if (worldCamera)
        {
            sourceName = worldCamera.name;
        }
        else
        {
            sourceName = name;
        }

        if (string.IsNullOrEmpty(sourceName))
        {
            return false;
        }

        string upper = sourceName.ToUpperInvariant();
        if (upper.Contains("PLAYER1") || upper.Contains("P1"))
        {
            fogName = "Fog_P1";
            return true;
        }

        if (upper.Contains("PLAYER2") || upper.Contains("P2"))
        {
            fogName = "Fog_P2";
            return true;
        }

        return false;
    }

    private static FogOfWarManager FindFogByName(string fogName)
    {
        if (string.IsNullOrEmpty(fogName))
        {
            return null;
        }

        FogOfWarManager[] fogs = UnityEngine.Object.FindObjectsByType<FogOfWarManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (fogs == null || fogs.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < fogs.Length; i++)
        {
            FogOfWarManager fog = fogs[i];
            if (!fog)
            {
                continue;
            }

            if (string.Equals(fog.name, fogName, StringComparison.OrdinalIgnoreCase))
            {
                return fog;
            }
        }

        return null;
    }

    private void TryAutoAssignTarget(bool force = false)
    {
        if (currentTarget && (!_selfIdentity || currentTarget != _selfIdentity))
        {
            return;
        }

        if (!force && Time.time < _nextAutoFindTime)
        {
            return;
        }

        _nextAutoFindTime = Time.time + AutoFindInterval;

        if (!_selfIdentity)
        {
            CacheSelfIdentity();
        }

        RevealIndicatorController[] indicators = UnityEngine.Object.FindObjectsByType<RevealIndicatorController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (indicators == null || indicators.Length == 0)
        {
            return;
        }

        for (int i = 0; i < indicators.Length; i++)
        {
            RevealIndicatorController indicator = indicators[i];
            if (!indicator || indicator == this)
            {
                continue;
            }

            NpcIdentity identity = ResolveIdentity(indicator.transform);
            if (!identity)
            {
                continue;
            }

            if (_selfIdentity && identity == _selfIdentity)
            {
                continue;
            }

            SetTarget(identity);
            return;
        }
    }

    private void CacheSelfIdentity()
    {
        Transform root = playerTransform ? playerTransform : transform;
        _selfIdentity = ResolveIdentity(root);
    }

    private static NpcIdentity ResolveIdentity(Transform root)
    {
        if (!root)
        {
            return null;
        }

        return root.GetComponent<NpcIdentity>()
            ?? root.GetComponentInChildren<NpcIdentity>(true)
            ?? root.GetComponentInParent<NpcIdentity>();
    }

    private void UpdateVisibility(bool targetVisible, bool hasTarget, float distanceToTarget)
    {
        UpdateVerticalAlpha();

        float abilityAlpha = revealAbility ? revealAbility.ActiveNormalized : 0f;
        if (alwaysShowWhenTargetSet && hasTarget)
        {
            abilityAlpha = Mathf.Max(abilityAlpha, 1f);
        }

        float desiredCircleAlpha = 0f;
        float desiredFillAmount = 0f;

        if (hasTarget && abilityAlpha > 0f)
        {
            desiredCircleAlpha = abilityAlpha;
            desiredFillAmount = targetVisible ? 1f : ComputeFillAmount(distanceToTarget);
        }

        _currentCircleAlpha = SmoothTowards(_currentCircleAlpha, desiredCircleAlpha, circleFadeDuration);
        _currentFillAmount = SmoothTowards(_currentFillAmount, desiredFillAmount, circleFadeDuration);

        if (compassCircle)
        {
            compassCircle.fillAmount = _currentFillAmount;
            Color c = ComputeCircleColor(distanceToTarget, targetVisible);
            c.a = _currentCircleAlpha;
            compassCircle.color = c;
            bool circleVisible = _currentCircleAlpha > 0.001f;
            compassCircle.enabled = circleVisible;
            if (compassCircle.gameObject.activeSelf != circleVisible)
            {
                compassCircle.gameObject.SetActive(circleVisible);
            }
        }
    }

    private void ApplyCompassRotation(Vector3 planarToTargetWorld, float targetYawWorld, float parentYaw, float localYaw)
    {
        if (compassRoot)
        {
            // Preserve parent-authored X/Z (e.g., canvas tilt) and keep yaw neutral so player spin doesn't accumulate.
            Vector3 rootEuler = compassRoot.localEulerAngles;
            rootEuler.y = 0f;
            compassRoot.localEulerAngles = rootEuler;
        }

        if (compassCircle)
        {
            // Rotate only around Z in the canvas' local space so the filled slice points to the target.
            RectTransform circleRect = compassCircle.rectTransform;
            Transform canvasSpace = circleRect.parent ? circleRect.parent : circleRect;
            Vector3 localDir = canvasSpace.InverseTransformDirection(planarToTargetWorld);
            localDir.z = 0f;
            if (localDir.sqrMagnitude > 0.0001f)
            {
                localDir.Normalize();
                float angle = Mathf.Atan2(localDir.x, localDir.y) * Mathf.Rad2Deg + compassRotationOffset;
                circleRect.localEulerAngles = new Vector3(0f, 0f, -angle);
            }
        }
    }

    private void UpdateVerticalAlpha()
    {
        float verticalTarget = 0f;
        if (playerVisionSource)
        {
            float threshold = playerVisionSource.level2MinHeight;
            float playerY = playerTransform ? playerTransform.position.y : 0f;
            verticalTarget = playerY >= threshold ? 1f : 0f;
        }

        _verticalAlpha = SmoothTowards(_verticalAlpha, verticalTarget, verticalFadeDuration);
    }

    private float SmoothTowards(float current, float target, float duration)
    {
        if (duration <= 0f)
        {
            return target;
        }

        float t = Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.0001f, duration));
        return Mathf.Lerp(current, target, t);
    }

    private float ComputeFillAmount(float distanceToTarget)
    {
        float far = Mathf.Max(farDistanceForMinFill, nearDistanceForMaxFill);
        float near = Mathf.Min(nearDistanceForMaxFill, farDistanceForMinFill);
        float t = Mathf.InverseLerp(far, near, distanceToTarget);
        float clampedMin = Mathf.Clamp(minFillAmount, 0.15f, MaxFillBeforeVisible);
        float clampedMax = MaxFillBeforeVisible;
        float fill = Mathf.Lerp(clampedMin, clampedMax, t);
        return Mathf.Clamp(fill, clampedMin, clampedMax);
    }

    private Color ComputeCircleColor(float distanceToTarget, bool targetVisible)
    {
        float far = Mathf.Max(farDistanceForMinFill, nearDistanceForMaxFill);
        float near = Mathf.Max(0.0001f, nearDistanceForMaxFill + nearColorDistanceOffset);
        if (Mathf.Approximately(far, near))
        {
            far += 0.001f;
        }

        float t = targetVisible ? 1f : Mathf.InverseLerp(far, near, distanceToTarget);
        return Color.Lerp(farFillColor, nearFillColor, t);
    }

    private bool IsTargetVisible(Transform target)
    {
        if (!target || !worldCamera)
        {
            return false;
        }

        if (fogManager)
        {
            if (fogManager.SampleFog01(target.position) <= 0.5f)
            {
                return false;
            }
        }

        Vector3 viewport = worldCamera.WorldToViewportPoint(target.position);
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }
}
