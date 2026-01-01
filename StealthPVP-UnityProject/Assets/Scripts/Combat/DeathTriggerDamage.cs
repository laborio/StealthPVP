using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trigger volume that applies a lethal damage payload with optional ragdoll impulse overrides.
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class DeathTriggerDamage : MonoBehaviour
{
    public enum ImpulseDirectionMode
    {
        None,
        AwayFromCenter,
        TowardCenter,
        UseTransformForward,
        UseTransformUp,
        CustomWorldDirection
    }

    [Header("Damage")]
    [SerializeField] private float damageAmount = 100f;
    [SerializeField, Tooltip("Only apply damage to players when a game manager is present.")] private bool playersOnly = true;
    [SerializeField, Tooltip("Optional layer filter; empty = everything.")] private LayerMask targetLayers = ~0;

    [Header("Ragdoll Impulse")]
    [SerializeField, Tooltip("Override ragdoll impulse force. 0 uses the ragdoll's default.")] private float ragdollImpulseStrength = 0f;
    [SerializeField] private ImpulseDirectionMode impulseDirectionMode = ImpulseDirectionMode.AwayFromCenter;
    [SerializeField] private Vector3 customImpulseDirection = Vector3.up;
    [SerializeField, Tooltip("Bias applied away from the trigger center when calculating hit point.")] private float hitPointBias = 0f;

    private readonly HashSet<CharacterHealth> _targetsInside = new HashSet<CharacterHealth>();
    private Collider _trigger;

    private void Awake()
    {
        _trigger = GetComponent<Collider>();
        if (_trigger && !_trigger.isTrigger)
        {
            _trigger.isTrigger = true;
        }
    }

    private void OnEnable()
    {
        if (_trigger)
        {
            _trigger.isTrigger = true;
        }
    }

    private void OnDisable()
    {
        _targetsInside.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyDamage(other);
    }

    private void OnTriggerExit(Collider other)
    {
        CharacterHealth target = ResolveTarget(other);
        if (target)
        {
            _targetsInside.Remove(target);
        }
    }

    private void TryApplyDamage(Collider other)
    {
        CharacterHealth target = ResolveTarget(other);
        if (!target || !_targetsInside.Add(target))
        {
            return;
        }

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        if (hitPointBias > 0f)
        {
            Vector3 dir = (hitPoint - transform.position).normalized;
            hitPoint += dir * hitPointBias;
        }

        Vector3 impulseDirection = ResolveImpulseDirection(hitPoint);
        Vector3 hitNormal = (transform.position - hitPoint).normalized;

        DamagePayload payload = new DamagePayload
        {
            Amount = damageAmount,
            HitPoint = hitPoint,
            HitNormal = hitNormal,
            Source = gameObject,
            Instigator = null,
            HitCollider = other,
            ImpulseDirection = impulseDirection,
            ImpulseStrength = ragdollImpulseStrength
        };

        target.ApplyDamage(payload);
    }

    private CharacterHealth ResolveTarget(Collider other)
    {
        if (!other)
        {
            return null;
        }

        if (((1 << other.gameObject.layer) & targetLayers.value) == 0)
        {
            return null;
        }

        CharacterHealth target = other.GetComponentInParent<CharacterHealth>()
            ?? other.GetComponentInChildren<CharacterHealth>(true);
        if (!target || target.IsDead)
        {
            return null;
        }

        if (playersOnly)
        {
            LocalVersusGameManager manager = LocalVersusGameManager.Instance;
            if (manager && !manager.IsPlayerHealth(target))
            {
                return null;
            }
        }

        return target;
    }

    private Vector3 ResolveImpulseDirection(Vector3 hitPoint)
    {
        switch (impulseDirectionMode)
        {
            case ImpulseDirectionMode.AwayFromCenter:
                return (hitPoint - transform.position).normalized;
            case ImpulseDirectionMode.TowardCenter:
                return (transform.position - hitPoint).normalized;
            case ImpulseDirectionMode.UseTransformForward:
                return transform.forward.normalized;
            case ImpulseDirectionMode.UseTransformUp:
                return transform.up.normalized;
            case ImpulseDirectionMode.CustomWorldDirection:
                return customImpulseDirection.sqrMagnitude > 0.0001f ? customImpulseDirection.normalized : Vector3.zero;
            default:
                return Vector3.zero;
        }
    }

    private void OnValidate()
    {
        damageAmount = Mathf.Max(0f, damageAmount);
        ragdollImpulseStrength = Mathf.Max(0f, ragdollImpulseStrength);
        hitPointBias = Mathf.Max(0f, hitPointBias);
    }
}
