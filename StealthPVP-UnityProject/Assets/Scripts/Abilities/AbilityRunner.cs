using UnityEngine;

/// <summary>
/// Handles a single ability's cooldown/hold/fade cycle. Can be triggered via input (player) or manually (NPC).
/// </summary>
[DisallowMultipleComponent]
public class AbilityRunner : MonoBehaviour
{
    [SerializeField, Tooltip("Ability id for reference only (no external config).")] private string abilityId = "Reveal";
    [SerializeField, Tooltip("If true, listens for the ability key defined below (or overrideKey).")]
    private bool useInput = true;
    [SerializeField, Tooltip("Treat this runner as the player when resolving input.")]
    private bool isPlayer = true;
    [SerializeField, Tooltip("Override key. If None, defaults to F.")] private KeyCode overrideKey = KeyCode.None;

    private float _cooldownSeconds = 30f;
    private float _holdSeconds = 2f;
    private float _fadeSeconds = 1f;

    private float _cooldownTimer;
    private float _holdTimer;
    private float _fadeTimer;
    private bool _inputEnabled = true;

    public float CooldownRemaining => _cooldownTimer;
    public bool IsCoolingDown => _cooldownTimer > 0f;
    public bool IsActive => _holdTimer > 0f || _fadeTimer > 0f;
    public float ActiveNormalized
    {
        get
        {
            if (_holdTimer > 0f)
            {
                return 1f;
            }
            if (_fadeTimer > 0f && _fadeSeconds > 0.0001f)
            {
                return Mathf.Clamp01(_fadeTimer / _fadeSeconds);
            }
            return 0f;
        }
    }

    private KeyCode _resolvedKey;

    private void Awake()
    {
        ResolveConfig();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        TickTimers(dt);

        if (useInput && _inputEnabled && Input.GetKeyDown(_resolvedKey) && !IsCoolingDown)
        {
            Trigger();
        }
    }

    public void Trigger()
    {
        if (IsCoolingDown)
        {
            return;
        }

        _holdTimer = _holdSeconds;
        _fadeTimer = _fadeSeconds;
        _cooldownTimer = _cooldownSeconds;
    }

    public void TriggerWithDurations(float hold, float fade)
    {
        if (IsCoolingDown)
        {
            return;
        }

        _holdTimer = Mathf.Max(0f, hold);
        _fadeTimer = Mathf.Max(0f, fade);
        _cooldownTimer = _cooldownSeconds;
    }

    public void ApplyOverrides(float cooldown, float hold, float fade)
    {
        _cooldownSeconds = Mathf.Max(0f, cooldown);
        _holdSeconds = Mathf.Max(0f, hold);
        _fadeSeconds = Mathf.Max(0f, fade);
    }

    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
    }

    /// <summary>
    /// Accelerates cooldown ticking by applying an additional multiplier (> 1 = faster). Only affects active cooldown.
    /// </summary>
    public void AccelerateCooldown(float multiplier, float deltaTime)
    {
        if (_cooldownTimer <= 0f)
        {
            return;
        }

        float extra = Mathf.Max(0f, multiplier - 1f);
        if (extra <= 0f)
        {
            return;
        }

        _cooldownTimer = Mathf.Max(0f, _cooldownTimer - deltaTime * extra);
    }

    private void TickTimers(float dt)
    {
        if (_cooldownTimer > 0f)
        {
            _cooldownTimer = Mathf.Max(0f, _cooldownTimer - dt);
        }

        if (_holdTimer > 0f)
        {
            _holdTimer = Mathf.Max(0f, _holdTimer - dt);
        }
        else if (_fadeTimer > 0f)
        {
            _fadeTimer = Mathf.Max(0f, _fadeTimer - dt);
        }
    }

    private void ResolveConfig()
    {
        _resolvedKey = overrideKey != KeyCode.None ? overrideKey : KeyCode.F;
    }

    public void SetOverrideKey(KeyCode key)
    {
        overrideKey = key;
        ResolveConfig();
    }

    public void SetUseInput(bool enabled)
    {
        useInput = enabled;
    }
}
