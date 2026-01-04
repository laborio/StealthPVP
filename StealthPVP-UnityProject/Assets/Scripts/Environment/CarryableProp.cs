using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Allows a prop (also used for morphing) to be picked up via the contextual action system.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(MorphTarget))]
public class CarryableProp : MonoBehaviour, IContextualAction, IContextualActionHint
{
    [SerializeField, Tooltip("Higher number wins when multiple actions overlap.")] private int actionPriority = 150;
    [SerializeField, Tooltip("Max distance from player to allow pickup. 0 disables distance check.")] private float maxPickupDistance = 2f;
    [SerializeField, Range(-1f, 1f), Tooltip("Dot threshold for facing check; -1 disables facing requirement.")] private float facingDotThreshold = 0.35f;
    [SerializeField, Tooltip("Optional trigger collider used for pickup detection (must be on this GameObject).")] private Collider triggerCollider;

    private readonly HashSet<SimpleCharacterController> _playersInRange = new HashSet<SimpleCharacterController>();
    private Collider _collider;
    private MorphTarget _morphTarget;
    private bool _isCarried;

    public int Priority => actionPriority;
    public bool IsBusy => _isCarried;
    public ContextActionHintType HintType => ContextActionHintType.Pickup;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _morphTarget = GetComponent<MorphTarget>();
        EnsureTriggerCollider();
    }

    private void OnDisable()
    {
        _playersInRange.Clear();
    }

    public void OnEnterRange(SimpleCharacterController player)
    {
        if (player)
        {
            _playersInRange.Add(player);
        }
    }

    public void OnExitRange(SimpleCharacterController player)
    {
        if (player)
        {
            _playersInRange.Remove(player);
        }
    }

    public bool CanExecute(SimpleCharacterController player, bool isGrounded)
    {
        if (!_playersInRange.Contains(player))
        {
            return false;
        }

        if (_isCarried || !gameObject.activeInHierarchy || !player)
        {
            return false;
        }

        if (!TryResolveCarryController(player, out PlayerCarryController carryController))
        {
            return false;
        }

        if (carryController.IsCarrying)
        {
            return false;
        }

        if (!IsFacingTarget(player))
        {
            return false;
        }

        return true;
    }

    public bool ShouldShowHint(SimpleCharacterController player, bool isGrounded)
    {
        return CanExecute(player, isGrounded);
    }

    public bool TryExecute(SimpleCharacterController player, bool isGrounded)
    {
        if (!CanExecute(player, isGrounded))
        {
            return false;
        }

        if (!TryResolveCarryController(player, out PlayerCarryController carryController))
        {
            return false;
        }

        if (!carryController.TryPickup(this))
        {
            return false;
        }

        _playersInRange.Remove(player);
        return true;
    }

    public bool TryBeginCarry(PlayerCarryController carrier)
    {
        if (_isCarried || !carrier)
        {
            return false;
        }

        _isCarried = true;
        _playersInRange.Clear();
        gameObject.SetActive(false);
        return true;
    }

    public void DropAt(Vector3 position, Quaternion rotation)
    {
        _isCarried = false;
        transform.SetPositionAndRotation(position, rotation);
        gameObject.SetActive(true);
    }

    private bool IsFacingTarget(SimpleCharacterController player)
    {
        if (!player)
        {
            return false;
        }

        if (!TryResolveTargetPoint(out Vector3 targetPoint))
        {
            return true;
        }

        Vector3 toTarget = targetPoint - player.transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        if (maxPickupDistance > 0f && toTarget.sqrMagnitude > maxPickupDistance * maxPickupDistance)
        {
            return false;
        }

        if (facingDotThreshold <= -1f)
        {
            return true;
        }

        Vector3 forward = player.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        forward.Normalize();
        Vector3 dir = toTarget.normalized;
        return Vector3.Dot(forward, dir) >= facingDotThreshold;
    }

    private bool TryResolveTargetPoint(out Vector3 point)
    {
        if (_morphTarget && _morphTarget.TryGetWorldBounds(out Bounds bounds))
        {
            point = bounds.center;
            return true;
        }

        if (_collider)
        {
            point = _collider.bounds.center;
            return true;
        }

        point = transform.position;
        return true;
    }

    private bool TryResolveCarryController(SimpleCharacterController player, out PlayerCarryController controller)
    {
        if (!player)
        {
            controller = null;
            return false;
        }

        controller = player.GetComponent<PlayerCarryController>()
            ?? player.GetComponentInChildren<PlayerCarryController>(true);
        return controller != null;
    }

    private void OnValidate()
    {
        maxPickupDistance = Mathf.Max(0f, maxPickupDistance);
        actionPriority = Mathf.Max(0, actionPriority);
        facingDotThreshold = Mathf.Clamp(facingDotThreshold, -1f, 1f);
    }

    private void EnsureTriggerCollider()
    {
        if (triggerCollider && triggerCollider.gameObject != gameObject)
        {
            triggerCollider = null;
        }

        if (!triggerCollider)
        {
            Collider[] colliders = GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] && colliders[i].isTrigger)
                {
                    triggerCollider = colliders[i];
                    break;
                }
            }
        }

        if (!triggerCollider)
        {
            triggerCollider = CreateTriggerCollider();
        }

        if (triggerCollider && !triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private Collider CreateTriggerCollider()
    {
        if (_collider is BoxCollider box)
        {
            BoxCollider trigger = gameObject.AddComponent<BoxCollider>();
            trigger.center = box.center;
            trigger.size = box.size;
            return trigger;
        }

        if (_collider is SphereCollider sphere)
        {
            SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
            trigger.center = sphere.center;
            trigger.radius = sphere.radius;
            return trigger;
        }

        if (_collider is CapsuleCollider capsule)
        {
            CapsuleCollider trigger = gameObject.AddComponent<CapsuleCollider>();
            trigger.center = capsule.center;
            trigger.radius = capsule.radius;
            trigger.height = capsule.height;
            trigger.direction = capsule.direction;
            return trigger;
        }

        BoxCollider fallback = gameObject.AddComponent<BoxCollider>();
        if (TryResolveTargetBounds(out Bounds bounds))
        {
            fallback.center = transform.InverseTransformPoint(bounds.center);
            Vector3 lossy = transform.lossyScale;
            fallback.size = new Vector3(
                lossy.x != 0f ? bounds.size.x / lossy.x : bounds.size.x,
                lossy.y != 0f ? bounds.size.y / lossy.y : bounds.size.y,
                lossy.z != 0f ? bounds.size.z / lossy.z : bounds.size.z);
        }
        return fallback;
    }

    private bool TryResolveTargetBounds(out Bounds bounds)
    {
        if (_morphTarget && _morphTarget.TryGetWorldBounds(out bounds))
        {
            return true;
        }

        if (_collider)
        {
            bounds = _collider.bounds;
            return true;
        }

        Renderer renderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
        if (renderer)
        {
            bounds = renderer.bounds;
            return true;
        }

        bounds = default;
        return false;
    }
}
