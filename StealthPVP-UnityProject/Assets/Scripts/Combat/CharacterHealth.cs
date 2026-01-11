using System;
using UnityEngine;

/// <summary>
/// Tracks health for any character/unit and exposes damage + death events.
/// </summary>
[DisallowMultipleComponent]
public class CharacterHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField, Tooltip("If true, this object is destroyed automatically on death.")] private bool destroyOnDeath = false;
    [SerializeField, Tooltip("If true, ignore all incoming damage.")] private bool invulnerable = false;
    [SerializeField, Tooltip("Enable to print debug logs for damage/death.")] private bool debugLogs = false;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    private DamagePayload _lastDamage;
    private bool _hasLastDamage;

    public event Action<DamagePayload> Damaged;
    public event Action<CharacterHealth> Died;
    public static event Action<CharacterHealth, DamagePayload> AnyDied;

    private void Awake()
    {
        CurrentHealth = Mathf.Max(CurrentHealth > 0f ? CurrentHealth : maxHealth, 0f);
        LogDebug($"Awake health={CurrentHealth}/{maxHealth}");
    }

    /// <summary>
    /// Revives this health component, resetting death state and restoring health.
    /// </summary>
    /// <param name="healthAmount">If > 0, sets the current health to this value; otherwise uses maxHealth.</param>
    public void Revive(float healthAmount = -1f)
    {
        IsDead = false;
        _hasLastDamage = false;
        CurrentHealth = healthAmount > 0f ? Mathf.Min(healthAmount, maxHealth) : maxHealth;
    }

    public void ApplyDamage(DamagePayload payload)
    {
        if (IsDead || invulnerable)
        {
            LogDebug("ApplyDamage ignored (dead or invulnerable)");
            return;
        }

        float amount = Mathf.Max(payload.Amount, 0f);
        if (amount <= 0f)
        {
            LogDebug("ApplyDamage ignored (amount <= 0)");
            return;
        }

        _lastDamage = payload;
        _hasLastDamage = true;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        LogDebug($"ApplyDamage amount={amount} newHealth={CurrentHealth}");
        Damaged?.Invoke(payload);

        if (CurrentHealth <= 0f)
        {
            IsDead = true;
            LogDebug("Health reached 0 -> Died event");
            Died?.Invoke(this);
            AnyDied?.Invoke(this, _lastDamage);
            if (destroyOnDeath)
            {
                Destroy(gameObject);
            }
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
    }

    public void SetMaxHealth(float value, bool healToFull)
    {
        maxHealth = Mathf.Max(1f, value);
        if (healToFull && !IsDead)
        {
            CurrentHealth = maxHealth;
        }
        else
        {
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, maxHealth);
        }
    }

    public bool TryGetLastDamage(out DamagePayload payload)
    {
        payload = _lastDamage;
        return _hasLastDamage;
    }

    private void LogDebug(string message)
    {
        if (debugLogs)
        {
            Debug.Log($"[CharacterHealth:{name}] {message}", this);
        }
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, maxHealth);
    }
}
