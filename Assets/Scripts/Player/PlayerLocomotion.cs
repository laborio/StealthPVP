using UnityEngine;

/// <summary>
/// Handles horizontal locomotion, ground detection, and rotation for the player character.
/// </summary>
[DisallowMultipleComponent]
public class PlayerLocomotion : MonoBehaviour
{
    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float runMultiplier = 1.5f;

    [Header("Acceleration")]
    [SerializeField] private float walkAcceleration = 20f;
    [SerializeField] private float runAcceleration = 30f;

    [Header("Rotation")]
    [SerializeField] private float walkRotationSpeed = 720f;
    [SerializeField] private float runRotationSpeed = 900f;

    [Header("Click To Move")]
    [SerializeField] private float stopDistance = 0.2f;

    [Header("Falling")]
    [SerializeField] private float fallSpeed = 5f;
    [SerializeField] private float fallForwardSpeed = 0f;
    [SerializeField] private float fallGroundSnapOffset = 0.05f;
    [SerializeField] private CapsuleCollider capsuleCollider;

    [Header("Grounding")]
    [SerializeField] private LayerMask walkableLayerMask;
    [SerializeField] private float groundedCheckStartHeight = 0.1f;
    [SerializeField] private float groundedDistanceThreshold = 0.2f;
    [SerializeField] private float groundedDistanceDetectionFromPlayer = 1.5f;

    private const float GroundedRaycastPadding = 0.05f;

    private Vector3 _moveTarget;
    private bool _hasMoveTarget;
    private Vector3 _currentVelocity;
    private bool _isRunningThisFrame;
    private bool _isGrounded;
    private bool _isFalling;

    public bool HasMoveTarget => _hasMoveTarget;
    public bool IsGrounded => _isGrounded;
    public bool IsFalling => _isFalling;
    public bool IsRunningThisFrame => _isRunningThisFrame;
    public Vector3 CurrentVelocity => _currentVelocity;
    public float WalkSpeed => walkSpeed;
    public float RunSpeed => walkSpeed * runMultiplier;

    private void Awake()
    {
        EnsureGroundLayerMask();
        if (!capsuleCollider)
        {
            capsuleCollider = GetComponent<CapsuleCollider>();
        }
    }

    public void SetMoveTarget(Vector3 worldPosition)
    {
        _moveTarget = worldPosition;
        _hasMoveTarget = true;
    }

    public void ClearMoveTarget()
    {
        _hasMoveTarget = false;
    }

    public void ApplyImmediateStop()
    {
        _hasMoveTarget = false;
        _currentVelocity = Vector3.zero;
        _isRunningThisFrame = false;
    }

    /// <summary>
    /// Processes ground-based locomotion for this frame.
    /// </summary>
    public LocomotionStep TickGrounded(float deltaTime, bool runInputActive)
    {
        Vector3 desiredVelocity = Vector3.zero;

        bool wantsToRun = runInputActive && _hasMoveTarget;
        float targetSpeed = walkSpeed * (wantsToRun ? runMultiplier : 1f);
        float currentAcceleration = wantsToRun ? runAcceleration : walkAcceleration;

        if (_hasMoveTarget)
        {
            Vector3 toTarget = _moveTarget - transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude <= stopDistance)
            {
                _hasMoveTarget = false;
            }
            else
            {
                desiredVelocity = toTarget.normalized * targetSpeed;
            }
        }

        _currentVelocity = Vector3.MoveTowards(_currentVelocity, desiredVelocity, currentAcceleration * deltaTime);
        transform.position += _currentVelocity * deltaTime;

        if (_hasMoveTarget)
        {
            Vector3 remaining = _moveTarget - transform.position;
            remaining.y = 0f;
            if (remaining.magnitude <= stopDistance)
            {
                _hasMoveTarget = false;
                _currentVelocity = Vector3.zero;
            }
        }

        Vector3 planarVelocity = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);
        float speed = planarVelocity.magnitude;
        bool isMoving = speed > 0.01f;

        _isRunningThisFrame = wantsToRun && isMoving;

        return new LocomotionStep
        {
            Velocity = _currentVelocity,
            PlanarVelocity = planarVelocity,
            HasMoveTarget = _hasMoveTarget,
            IsRunning = _isRunningThisFrame,
            IsMoving = isMoving
        };
    }

    /// <summary>
    /// Applies falling physics when the character leaves the ground.
    /// </summary>
    public void AttachCapsuleCollider(CapsuleCollider collider)
    {
        capsuleCollider = collider;
    }

    public Vector3 TickFalling(float deltaTime)
    {
        Vector3 fallForward = transform.forward;
        fallForward.y = 0f;
        if (fallForward.sqrMagnitude > 0.0001f)
        {
            fallForward.Normalize();
        }
        else
        {
            fallForward = Vector3.zero;
        }

        Vector3 forwardContribution = fallForward * Mathf.Max(0f, fallForwardSpeed);
        Vector3 velocity = (Vector3.down * Mathf.Max(0f, fallSpeed)) + forwardContribution;
        Vector3 displacement = velocity * deltaTime;

        bool snappedToGround = TrySnapToGround(displacement, out Vector3 adjustedDisplacement);

        if (snappedToGround)
        {
            transform.position += adjustedDisplacement;
            _currentVelocity = Vector3.zero;
            _isGrounded = true;
            _isFalling = false;
            _isRunningThisFrame = false;
            return _currentVelocity;
        }

        transform.position += displacement;
        _currentVelocity = velocity;
        _isFalling = true;
        _isRunningThisFrame = false;
        _hasMoveTarget = false;
        return _currentVelocity;
    }

    public void ApplyRotation(float deltaTime)
    {
        Vector3 planarVelocity = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);
        if (planarVelocity.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float currentRotationSpeed = _isRunningThisFrame ? runRotationSpeed : walkRotationSpeed;
        Quaternion targetRotation = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
        Quaternion newRotation = Quaternion.RotateTowards(transform.rotation, targetRotation, currentRotationSpeed * deltaTime);
        transform.rotation = newRotation;
    }

    public void EvaluateGroundedState(bool forceGrounded)
    {
        if (forceGrounded)
        {
            _isGrounded = true;
            _isFalling = false;
            return;
        }

        if (walkableLayerMask == 0)
        {
            return;
        }

        Vector3 origin = transform.position + (Vector3.up * groundedCheckStartHeight) + (Vector3.forward * groundedDistanceDetectionFromPlayer);
        float rayLength = groundedCheckStartHeight + groundedDistanceThreshold + GroundedRaycastPadding;
        bool hitWalkable = Physics.Raycast(origin, Vector3.down, out RaycastHit hitInfo, rayLength, walkableLayerMask, QueryTriggerInteraction.Ignore);

        float distanceFromFeet = hitWalkable ? Mathf.Max(0f, hitInfo.distance - groundedCheckStartHeight) : float.PositiveInfinity;
        bool groundedNow = hitWalkable && distanceFromFeet <= groundedDistanceThreshold;

        if (groundedNow != _isGrounded)
        {
            _isGrounded = groundedNow;
            string state = groundedNow ? "Grounded" : "Airborne";
            // Debug.Log($"PlayerLocomotion: {state} (distance {distanceFromFeet:F3})", this);
        }

        if (_isGrounded)
        {
            _isFalling = false;
        }
        else
        {
            _isFalling = true;
            Debug.Log("NOT GROUNDED");
        }
    }

    public void ResetState()
    {
        _moveTarget = Vector3.zero;
        _hasMoveTarget = false;
        _currentVelocity = Vector3.zero;
        _isRunningThisFrame = false;
        _isGrounded = false;
        _isFalling = false;
    }

    private void EnsureGroundLayerMask()
    {
        walkableLayerMask = AddLayerToMask(walkableLayerMask, "Walkable");
        walkableLayerMask = AddLayerToMask(walkableLayerMask, "Climbable");
        walkableLayerMask = AddLayerToMask(walkableLayerMask, "Ground");
    }

    private static int AddLayerToMask(int mask, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer != -1)
        {
            mask |= 1 << layer;
        }

        return mask;
    }

    private void OnDisable()
    {
        ResetState();
    }

    private void OnValidate()
    {
        walkSpeed = Mathf.Max(0f, walkSpeed);
        runMultiplier = Mathf.Max(1f, runMultiplier);
        walkAcceleration = Mathf.Max(0f, walkAcceleration);
        runAcceleration = Mathf.Max(0f, runAcceleration);
        walkRotationSpeed = Mathf.Max(0f, walkRotationSpeed);
        runRotationSpeed = Mathf.Max(0f, runRotationSpeed);
        stopDistance = Mathf.Max(0f, stopDistance);
        fallSpeed = Mathf.Max(0f, fallSpeed);
        fallForwardSpeed = Mathf.Max(0f, fallForwardSpeed);
        fallGroundSnapOffset = Mathf.Max(0f, fallGroundSnapOffset);
        groundedCheckStartHeight = Mathf.Max(0f, groundedCheckStartHeight);
        groundedDistanceThreshold = Mathf.Max(0f, groundedDistanceThreshold);
        groundedDistanceDetectionFromPlayer = Mathf.Max(0f, groundedDistanceDetectionFromPlayer);
        EnsureGroundLayerMask();
        if (!capsuleCollider)
        {
            capsuleCollider = GetComponent<CapsuleCollider>();
        }
    }

    private bool TrySnapToGround(Vector3 displacement, out Vector3 adjustedDisplacement)
    {
        adjustedDisplacement = displacement;

        if (walkableLayerMask == 0)
        {
            return false;
        }

        float distance = displacement.magnitude;
        if (distance <= Mathf.Epsilon)
        {
            return false;
        }

        Vector3 direction = displacement.normalized;
        if (direction.y >= -0.0001f)
        {
            return false;
        }

        if (capsuleCollider && CapsuleCast(direction, distance, out RaycastHit hitInfo) && hitInfo.collider != capsuleCollider)
        {
            float travel = Mathf.Max(0f, hitInfo.distance - fallGroundSnapOffset);
            adjustedDisplacement = direction * travel;
            return true;
        }

        Vector3 rayOrigin = transform.position + (Vector3.up * groundedCheckStartHeight);
        float dropMagnitude = Mathf.Abs(displacement.y);
        float rayLength = groundedCheckStartHeight + dropMagnitude + fallGroundSnapOffset;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit rayHit, rayLength, walkableLayerMask, QueryTriggerInteraction.Ignore))
        {
            if (rayHit.distance <= groundedCheckStartHeight + dropMagnitude + fallGroundSnapOffset)
            {
                float allowedDrop = Mathf.Max(0f, rayHit.distance - groundedCheckStartHeight - fallGroundSnapOffset);
                float drop = Mathf.Min(dropMagnitude, allowedDrop);
                adjustedDisplacement = new Vector3(displacement.x, -drop, displacement.z);
                return true;
            }
        }

        return false;
    }

    private bool CapsuleCast(Vector3 direction, float distance, out RaycastHit hitInfo)
    {
        hitInfo = default;

        float scaleX = Mathf.Abs(transform.lossyScale.x);
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        float scaleZ = Mathf.Abs(transform.lossyScale.z);

        float radius = capsuleCollider.radius * Mathf.Max(scaleX, scaleZ);
        float height = Mathf.Max(radius * 2f, capsuleCollider.height * scaleY);

        Vector3 center = transform.TransformPoint(capsuleCollider.center);
        Vector3 up = transform.up;
        float halfHeight = Mathf.Max(0f, (height * 0.5f) - radius);

        Vector3 point1 = center + up * halfHeight;
        Vector3 point2 = center - up * halfHeight;

        return Physics.CapsuleCast(point1, point2, radius, direction, out hitInfo, distance + fallGroundSnapOffset, walkableLayerMask, QueryTriggerInteraction.Ignore);
    }
}

public struct LocomotionStep
{
    public Vector3 Velocity;
    public Vector3 PlanarVelocity;
    public bool HasMoveTarget;
    public bool IsRunning;
    public bool IsMoving;
}
