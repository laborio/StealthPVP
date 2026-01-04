using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles melee hit detection driven by animation events that open/close a damage window.
/// Attach to a weapon with a trigger collider; call StartDamageWindow/EndDamageWindow via animation events.
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class WeaponDamage : MonoBehaviour
{
    public static event Action<CharacterHealth, CharacterHealth> AnyStunned;

    [SerializeField, Tooltip("Owning unit so we don't damage ourselves. If left empty, will search in parents.")] private CharacterHealth owner;
    [SerializeField, Tooltip("Damage dealt per successful hit.")] private float damageAmount = 25f;
    [SerializeField, Tooltip("Optional layer filter; empty = everything.")] private LayerMask targetLayers = ~0;
    [SerializeField, Tooltip("Optional extra reach added when picking hit point.")] private float hitPointBias = 0f;
    [SerializeField, Tooltip("Tag used for weapons that can kill assigned targets.")] private string weaponKillTag = "WeaponKill";
    [SerializeField, Tooltip("Tag used for weapons that apply stun to players.")] private string weaponStunTag = "WeaponStun";
    [SerializeField, Tooltip("If true, ignore SphereCollider hits (useful for NPC detection spheres).")] private bool ignoreSphereColliders = true;

    private readonly HashSet<CharacterHealth> _hitThisWindow = new HashSet<CharacterHealth>();
    private bool _windowActive;
    private Collider _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        if (_collider && !_collider.isTrigger)
        {
            _collider.isTrigger = true;
        }

        if (!owner)
        {
            owner = GetComponentInParent<CharacterHealth>();
        }
    }

    public void StartDamageWindow()
    {
        _windowActive = true;
        _hitThisWindow.Clear();
    }

    public void EndDamageWindow()
    {
        _windowActive = false;
        _hitThisWindow.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDealDamage(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryDealDamage(other);
    }

    private void TryDealDamage(Collider other)
    {
        if (!_windowActive || !other)
        {
            return;
        }

        if (ignoreSphereColliders && other is SphereCollider)
        {
            return;
        }

        if (((1 << other.gameObject.layer) & targetLayers.value) == 0)
        {
            return;
        }

        CharacterHealth targetHealth = other.GetComponentInParent<CharacterHealth>();
        if (!targetHealth || targetHealth == owner || targetHealth.IsDead || _hitThisWindow.Contains(targetHealth))
        {
            return;
        }

        if (ShouldApplyStun(targetHealth))
        {
            _hitThisWindow.Add(targetHealth);
            return;
        }

        if (ShouldIgnoreKillHit(targetHealth))
        {
            return;
        }

        _hitThisWindow.Add(targetHealth);
        Vector3 hitPoint = other.ClosestPoint(transform.position);
        if (hitPointBias > 0f)
        {
            Vector3 dir = (hitPoint - transform.position).normalized;
            hitPoint += dir * hitPointBias;
        }

        DamagePayload payload = new DamagePayload
        {
            Amount = damageAmount,
            HitPoint = hitPoint,
            HitNormal = (transform.position - hitPoint).normalized,
            Source = gameObject,
            Instigator = owner ? owner.gameObject : gameObject,
            HitCollider = other
        };

        targetHealth.ApplyDamage(payload);
    }

    private bool ShouldApplyStun(CharacterHealth targetHealth)
    {
        if (string.IsNullOrEmpty(weaponStunTag) || !CompareTag(weaponStunTag))
        {
            return false;
        }

        PlayerStunController stun = targetHealth.GetComponentInParent<PlayerStunController>()
            ?? targetHealth.GetComponentInChildren<PlayerStunController>(true);
        if (!stun)
        {
            return false;
        }

        stun.ApplyStun();
        LocalVersusGameManager manager = LocalVersusGameManager.Instance;
        if (manager && owner)
        {
            manager.TryHandleHumiliation(owner, targetHealth);
        }

        AnyStunned?.Invoke(owner, targetHealth);
        return true;
    }

    private bool ShouldIgnoreKillHit(CharacterHealth targetHealth)
    {
        if (string.IsNullOrEmpty(weaponKillTag) || !CompareTag(weaponKillTag))
        {
            return false;
        }

        LocalVersusGameManager manager = LocalVersusGameManager.Instance;
        if (!manager)
        {
            return false;
        }

        if (!manager.IsPlayerHealth(owner) || !manager.IsPlayerHealth(targetHealth))
        {
            return false;
        }

        return !manager.CanKillPlayer(owner, targetHealth);
    }

    private void OnValidate()
    {
        damageAmount = Mathf.Max(0f, damageAmount);
    }
}
