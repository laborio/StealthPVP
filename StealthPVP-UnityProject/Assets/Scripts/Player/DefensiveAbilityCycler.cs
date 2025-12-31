using UnityEngine;

/// <summary>
/// Cycles between defensive abilities on the same input.
/// </summary>
[DisallowMultipleComponent]
public class DefensiveAbilityCycler : MonoBehaviour
{
    public enum DefensiveSlot
    {
        Smoke,
        Defensive02
    }

    public event System.Action<DefensiveSlot> SlotChanged;

    [SerializeField, Tooltip("Smoke ability to trigger as Defensive 01.")] private SmokeAbility smokeAbility;
    [SerializeField, Tooltip("Morph ability to trigger as Defensive 02.")] private MorphAbility morphAbility;
    [SerializeField, Tooltip("Cooldown for the placeholder Defensive 02 ability.")] private float defensive02CooldownSeconds = 90f;
    [SerializeField, Tooltip("If true, listens for input to trigger defensive abilities.")] private bool useInput = true;
    [SerializeField, Tooltip("Override key for defensive ability activation.")] private KeyCode overrideKey = KeyCode.C;

    private float _defensive02CooldownTimer;
    private bool _inputEnabled = true;
    private bool _inputSuppressed;
    private KeyCode _resolvedKey;
    private DefensiveSlot _nextSlot = DefensiveSlot.Smoke;

    public DefensiveSlot NextSlot => _nextSlot;
    public float Defensive02CooldownRemaining => _defensive02CooldownTimer;
    public MorphAbility MorphAbility => morphAbility;

    private void Awake()
    {
        ResolveSmokeAbility();
        ResolveKey();
        DisableSmokeInput();
    }

    private void Update()
    {
        TickCooldown(Time.deltaTime);

        if (useInput && _inputEnabled && !_inputSuppressed && Input.GetKeyDown(_resolvedKey))
        {
            if (morphAbility && morphAbility.IsMorphed)
            {
                morphAbility.BreakMorph();
                return;
            }

            TryTrigger();
        }
    }

    public void SetSmokeAbility(SmokeAbility ability)
    {
        smokeAbility = ability;
        DisableSmokeInput();
    }

    public void SetMorphAbility(MorphAbility ability)
    {
        morphAbility = ability;
    }

    public void SetDefensive02Cooldown(float seconds)
    {
        defensive02CooldownSeconds = Mathf.Max(0f, seconds);
    }

    public void SetOverrideKey(KeyCode key)
    {
        overrideKey = key;
        ResolveKey();
    }

    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
    }

    public void SetInputSuppressed(bool suppressed)
    {
        _inputSuppressed = suppressed;
    }

    private void TickCooldown(float deltaTime)
    {
        if (_defensive02CooldownTimer > 0f)
        {
            _defensive02CooldownTimer = Mathf.Max(0f, _defensive02CooldownTimer - deltaTime);
        }
    }

    private void TryTrigger()
    {
        if (_nextSlot == DefensiveSlot.Smoke)
        {
            if (_defensive02CooldownTimer > 0f)
            {
                return;
            }

            if (!smokeAbility || smokeAbility.IsCoolingDown)
            {
                return;
            }

            smokeAbility.Trigger();
            SetNextSlot(DefensiveSlot.Defensive02);
            return;
        }

        if (smokeAbility && smokeAbility.IsCoolingDown)
        {
            return;
        }

        if (_defensive02CooldownTimer > 0f)
        {
            return;
        }

        if (!morphAbility || !morphAbility.TryTrigger())
        {
            return;
        }

        _defensive02CooldownTimer = Mathf.Max(0f, defensive02CooldownSeconds);
        SetNextSlot(DefensiveSlot.Smoke);
    }

    private void SetNextSlot(DefensiveSlot slot)
    {
        if (_nextSlot == slot)
        {
            return;
        }

        _nextSlot = slot;
        SlotChanged?.Invoke(_nextSlot);
    }

    private void ResolveSmokeAbility()
    {
        if (!smokeAbility)
        {
            smokeAbility = GetComponent<SmokeAbility>() ?? GetComponentInChildren<SmokeAbility>(true);
        }
    }

    private void DisableSmokeInput()
    {
        if (!smokeAbility)
        {
            return;
        }

        smokeAbility.SetUseInput(false);
    }

    private void ResolveKey()
    {
        _resolvedKey = overrideKey != KeyCode.None ? overrideKey : KeyCode.C;
    }
}
