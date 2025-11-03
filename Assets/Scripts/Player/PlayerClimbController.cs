using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages climb states, movement, and climbable contact tracking.
/// </summary>
[DisallowMultipleComponent]
public class PlayerClimbController : MonoBehaviour
{
    [Header("Climb Movement")]
    [SerializeField] private float climbRotationSpeed = 540f;
    [SerializeField] private float climbSpeed = 3f;
    [SerializeField] private float climbAnimationBaseSpeed = 1f;
    [SerializeField] private float finishClimbSpeed = 2f;
    [SerializeField] private float finishClimbAnimationBaseSpeed = 1f;
    [SerializeField] private float climbAnimationForwardOffset = 8f;

    [Header("Climb Stop Detection")]
    [SerializeField] private float climbStopRayOriginHeight = 1.8f;
    [SerializeField] private float climbStopRaycastRange = 1f;
    [SerializeField] private float climbStopDistanceThreshold = 0.35f;

    [Header("Timeouts")]
    [SerializeField] private float finishClimbTimeout = 2f;

    private readonly HashSet<Collider> _activeClimbContacts = new HashSet<Collider>();

    private bool _isClimbing;
    private bool _isFinishingClimb;
    private bool _hasClimbFacingDirection;
    private Vector3 _climbFacingDirection;
    private float _finishClimbTimer;
    private int _climbableLayer;
    private int _climbableLayerMask;
    private CapsuleCollider _capsuleCollider;

    public bool IsClimbing => _isClimbing;
    public bool IsFinishingClimb => _isFinishingClimb;
    public bool CanBeginClimb => !_isClimbing && !_isFinishingClimb && _activeClimbContacts.Count > 0;
    public bool HasFacingDirection => _hasClimbFacingDirection;
    public Vector3 FacingDirection => _climbFacingDirection;
    public float ClimbAnimationSpeed => climbAnimationBaseSpeed;
    public float FinishClimbAnimationSpeed => finishClimbAnimationBaseSpeed;

    private void Awake()
    {
        RefreshClimbableLayerData();
    }

    public void AttachCollider(CapsuleCollider capsuleCollider)
    {
        _capsuleCollider = capsuleCollider;
    }

    public void BeginClimb()
    {
        if (!CanBeginClimb)
        {
            return;
        }

        _isClimbing = true;
        _isFinishingClimb = false;
        _finishClimbTimer = 0f;
        if (_capsuleCollider)
        {
            _capsuleCollider.isTrigger = false;
        }
    }

    public void ForceDropFromClimb()
    {
        _isClimbing = false;
        _isFinishingClimb = false;
        _hasClimbFacingDirection = false;
        _climbFacingDirection = Vector3.zero;
        _finishClimbTimer = 0f;
        _activeClimbContacts.Clear();
        if (_capsuleCollider)
        {
            _capsuleCollider.isTrigger = false;
        }
    }

    public Vector3 TickClimb(float deltaTime, Transform characterTransform)
    {
        if (!_isClimbing)
        {
            return Vector3.zero;
        }

        Vector3 climbVelocity = Vector3.up * climbSpeed;
        characterTransform.position += climbVelocity * deltaTime;
        return climbVelocity;
    }

    public Vector3 TickFinishClimb(float deltaTime, Transform characterTransform)
    {
        if (!_isFinishingClimb)
        {
            return Vector3.zero;
        }

        Vector3 climbForward = characterTransform.forward;
        climbForward.y = 0f;
        if (climbForward.sqrMagnitude < 0.0001f)
        {
            climbForward = Vector3.forward;
        }
        climbForward.Normalize();

        Vector3 velocity = (Vector3.up * finishClimbSpeed) + (climbForward * climbAnimationForwardOffset);
        characterTransform.position += velocity * deltaTime;
        return velocity;
    }

    public void UpdateClimbRotation(float deltaTime, Transform characterTransform)
    {
        if (!_hasClimbFacingDirection)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(_climbFacingDirection, Vector3.up);
        characterTransform.rotation = Quaternion.RotateTowards(characterTransform.rotation, targetRotation, climbRotationSpeed * deltaTime);
    }

    public void CheckForClimbStop(Transform characterTransform)
    {
        if (!_isClimbing || _isFinishingClimb)
        {
            return;
        }

        if (_climbableLayerMask == 0)
        {
            Debug.LogWarning("PlayerClimbController: Climb stop raycast skipped, Climbable layer not found.", this);
            return;
        }

        float rayLength = Mathf.Max(climbStopRaycastRange, climbStopDistanceThreshold);
        if (rayLength <= 0f)
        {
            return;
        }

        Vector3 origin = characterTransform.position + (Vector3.up * climbStopRayOriginHeight);
        Vector3 direction = characterTransform.forward;

        if (Physics.Raycast(origin, direction, out RaycastHit hitInfo, rayLength, _climbableLayerMask, QueryTriggerInteraction.Ignore))
        {
            if (hitInfo.distance <= climbStopDistanceThreshold)
            {
                return;
            }

            StartFinishClimb();
        }
        else
        {
            StartFinishClimb();
        }
    }

    private void StartFinishClimb()
    {
        if (_isFinishingClimb || !_isClimbing)
        {
            return;
        }

        _isFinishingClimb = true;
        _isClimbing = false;
        _finishClimbTimer = 0f;
        _activeClimbContacts.Clear();
        if (_capsuleCollider)
        {
            _capsuleCollider.isTrigger = true;
        }
    }

    public void UpdateFinishClimbTimer(float deltaTime)
    {
        if (!_isFinishingClimb)
        {
            _finishClimbTimer = 0f;
            return;
        }

        _finishClimbTimer += deltaTime;
        if (_finishClimbTimer >= finishClimbTimeout)
        {
            Debug.LogWarning("PlayerClimbController: Finish climb timeout reached, forcing reset.", this);
            ResetFinishClimbState();
        }
    }

    public void ResetFinishClimbState()
    {
        _isFinishingClimb = false;
        _finishClimbTimer = 0f;
        _hasClimbFacingDirection = false;
        _climbFacingDirection = Vector3.zero;
        if (_capsuleCollider)
        {
            _capsuleCollider.isTrigger = false;
        }
    }

    public void HandleCollisionEnter(Collision collision)
    {
        TryRegisterClimbContact(collision);
    }

    public void HandleCollisionStay(Collision collision)
    {
        TryRegisterClimbContact(collision);
    }

    public void HandleCollisionExit(Collision collision)
    {
        Collider collider = collision.collider;
        if (!IsClimbableCollider(collider))
        {
            return;
        }

        _activeClimbContacts.Remove(collider);
        if (_activeClimbContacts.Count == 0 && !_isFinishingClimb)
        {
            _isClimbing = false;
            _hasClimbFacingDirection = false;
            _climbFacingDirection = Vector3.zero;
        }
    }

    private void TryRegisterClimbContact(Collision collision)
    {
        if (_isFinishingClimb)
        {
            return;
        }

        Collider collider = collision.collider;
        if (!IsClimbableCollider(collider))
        {
            return;
        }

        if (_activeClimbContacts.Add(collider))
        {
            UpdateClimbFacingDirection(collision);
        }
        else
        {
            UpdateClimbFacingDirection(collision);
        }
    }

    private void UpdateClimbFacingDirection(Collision collision)
    {
        _hasClimbFacingDirection = false;

        if (collision.contactCount > 0)
        {
            Vector3 averagedNormal = Vector3.zero;
            ContactPoint[] contacts = collision.contacts;
            for (int i = 0; i < contacts.Length; i++)
            {
                averagedNormal += contacts[i].normal;
            }

            if (averagedNormal.sqrMagnitude > 0.0001f)
            {
                averagedNormal.Normalize();
                Vector3 facing = -averagedNormal;
                facing.y = 0f;
                if (facing.sqrMagnitude > 0.0001f)
                {
                    _climbFacingDirection = facing.normalized;
                    _hasClimbFacingDirection = true;
                    return;
                }
            }
        }

        Vector3 directionToCollider = collision.collider.transform.position - transform.position;
        directionToCollider.y = 0f;
        if (directionToCollider.sqrMagnitude > 0.0001f)
        {
            _climbFacingDirection = directionToCollider.normalized;
            _hasClimbFacingDirection = true;
        }
    }

    private bool IsClimbableCollider(Collider collider)
    {
        if (!collider || !collider.CompareTag("Wall"))
        {
            return false;
        }

        if (_climbableLayer != -1 && collider.gameObject.layer != _climbableLayer)
        {
            return false;
        }

        return true;
    }

    private void RefreshClimbableLayerData()
    {
        _climbableLayer = LayerMask.NameToLayer("Climbable");
        _climbableLayerMask = _climbableLayer != -1 ? 1 << _climbableLayer : 0;
    }

    public void ResetAll()
    {
        _isClimbing = false;
        _isFinishingClimb = false;
        _hasClimbFacingDirection = false;
        _climbFacingDirection = Vector3.zero;
        _finishClimbTimer = 0f;
        _activeClimbContacts.Clear();
        if (_capsuleCollider)
        {
            _capsuleCollider.isTrigger = false;
        }
    }

    private void OnDisable()
    {
        ResetAll();
    }

    private void OnValidate()
    {
        climbRotationSpeed = Mathf.Max(0f, climbRotationSpeed);
        climbSpeed = Mathf.Max(0f, climbSpeed);
        finishClimbSpeed = Mathf.Max(0f, finishClimbSpeed);
        climbAnimationForwardOffset = Mathf.Max(0f, climbAnimationForwardOffset);
        finishClimbTimeout = Mathf.Max(0.01f, finishClimbTimeout);
        climbStopRayOriginHeight = Mathf.Max(0f, climbStopRayOriginHeight);
        climbStopRaycastRange = Mathf.Max(0f, climbStopRaycastRange);
        if (climbStopRaycastRange <= 0f)
        {
            climbStopDistanceThreshold = 0f;
        }
        else
        {
            climbStopDistanceThreshold = Mathf.Clamp(climbStopDistanceThreshold, 0f, climbStopRaycastRange);
        }

        climbAnimationBaseSpeed = Mathf.Max(0.01f, climbAnimationBaseSpeed);
        finishClimbAnimationBaseSpeed = Mathf.Max(0.01f, finishClimbAnimationBaseSpeed);

        RefreshClimbableLayerData();
    }
}
