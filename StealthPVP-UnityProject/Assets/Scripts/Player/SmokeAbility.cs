using UnityEngine;

/// <summary>
/// Simple smoke ability that activates a smoke GameObject for a short duration with a cooldown.
/// </summary>
[DisallowMultipleComponent]
public class SmokeAbility : MonoBehaviour
{
    [SerializeField, Tooltip("Smoke VFX root to enable/disable.")] private GameObject smokeObject;
    [SerializeField, Tooltip("Seconds the smoke stays active.")] private float smokeDuration = 3f;
    [SerializeField, Tooltip("Seconds before the smoke can be used again.")] private float cooldownSeconds = 90f;
    [SerializeField, Tooltip("If true, listens for input to trigger smoke.")] private bool useInput = true;
    [SerializeField, Tooltip("Override key for smoke activation.")] private KeyCode overrideKey = KeyCode.C;

    private float _cooldownTimer;
    private float _activeTimer;
    private bool _inputEnabled = true;
    private bool _inputSuppressed;
    private KeyCode _resolvedKey;
    private SmokeZone _smokeZone;

    public float CooldownRemaining => _cooldownTimer;
    public bool IsCoolingDown => _cooldownTimer > 0f;
    public bool IsActive => _activeTimer > 0f;

    private void Awake()
    {
        ResolveSmokeObject();
        ResolveKey();
        EnsureSmokeZoneOwner();
        if (smokeObject)
        {
            smokeObject.SetActive(false);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        TickTimers(dt);

        if (useInput && _inputEnabled && !_inputSuppressed && Input.GetKeyDown(_resolvedKey))
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

        _activeTimer = Mathf.Max(0f, smokeDuration);
        _cooldownTimer = Mathf.Max(0f, cooldownSeconds);
        if (smokeObject)
        {
            smokeObject.SetActive(true);
            EnsureSmokeZoneOwner();
        }
    }

    public void SetCooldown(float seconds)
    {
        cooldownSeconds = Mathf.Max(0f, seconds);
    }

    public void SetDuration(float seconds)
    {
        smokeDuration = Mathf.Max(0f, seconds);
    }

    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
    }

    public void SetInputSuppressed(bool suppressed)
    {
        _inputSuppressed = suppressed;
    }

    public void SetOverrideKey(KeyCode key)
    {
        overrideKey = key;
        ResolveKey();
    }

    private void TickTimers(float dt)
    {
        if (_cooldownTimer > 0f)
        {
            _cooldownTimer = Mathf.Max(0f, _cooldownTimer - dt);
        }

        if (_activeTimer > 0f)
        {
            _activeTimer = Mathf.Max(0f, _activeTimer - dt);
            if (_activeTimer <= 0f && smokeObject)
            {
                smokeObject.SetActive(false);
            }
        }
    }

    private void ResolveSmokeObject()
    {
        if (smokeObject)
        {
            return;
        }

        Transform child = transform.Find("Smoke");
        if (child)
        {
            smokeObject = child.gameObject;
        }
    }

    private void ResolveKey()
    {
        _resolvedKey = overrideKey != KeyCode.None ? overrideKey : KeyCode.C;
    }

    private void EnsureSmokeZoneOwner()
    {
        if (!smokeObject)
        {
            return;
        }

        if (!_smokeZone)
        {
            _smokeZone = smokeObject.GetComponent<SmokeZone>() ?? smokeObject.GetComponentInChildren<SmokeZone>(true);
        }

        if (_smokeZone)
        {
            CharacterHealth owner = GetComponent<CharacterHealth>() ?? GetComponentInChildren<CharacterHealth>(true);
            _smokeZone.SetOwner(owner);
            return;
        }

        _smokeZone = smokeObject.AddComponent<SmokeZone>();
        CharacterHealth fallbackOwner = GetComponent<CharacterHealth>() ?? GetComponentInChildren<CharacterHealth>(true);
        _smokeZone.SetOwner(fallbackOwner);
    }
}
