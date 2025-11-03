using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
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
    [SerializeField] private float climbRotationSpeed = 540f;
    [SerializeField] private float climbSpeed = 3f;
    [SerializeField] private float climbAnimationBaseSpeed = 1f;
    [SerializeField] private float finishClimbSpeed = 2f;
    [SerializeField] private float finishClimbAnimationBaseSpeed = 1f;
    [SerializeField] private float climbAnimationForwardOffset = 8f;
    [Header("Climb Timing")]
    [SerializeField] private float finishClimbTimeout = 2f;
    [Header("Climb Stop Detection")]
    [SerializeField] private float climbStopRayOriginHeight = 1.8f;
    [SerializeField] private float climbStopRaycastRange = 1f;
    [SerializeField] private float climbStopDistanceThreshold = 0.35f;
    [Header("Falling")]
    [SerializeField] private float fallSpeed = 5f;
    [SerializeField] private float fallForwardSpeed = 0f;

    [Header("Input")]
    [SerializeField] private KeyCode runKey = KeyCode.Space;
    [SerializeField] private KeyCode stopKey = KeyCode.S;
    [SerializeField] private KeyCode climbKey = KeyCode.T;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private float walkAnimationBaseSpeed = 1f;
    [SerializeField] private float runAnimationBaseSpeed = 1f;

    [Header("Grounding")]
    [SerializeField] private LayerMask walkableLayerMask;
    [SerializeField] private float groundedCheckStartHeight = 0.1f;
    [SerializeField] private float groundedDistanceThreshold = 0.2f;
    [SerializeField] private float groundedDistanceDetectionFromPlayer = 1.5f;

    [Header("Click To Move")]
    [SerializeField] private float stopDistance = 0.2f;
    [SerializeField] private float maximumRayDistance = 250f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private ClickMoveMarkerPool moveMarkerPool;
    [SerializeField] private float markerHeight = 0.1f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem runParticleSystem;

    private static readonly int IsRunningHash = Animator.StringToHash("isRunning");
    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    private static readonly int IsIdleHash = Animator.StringToHash("isIdle");
    private static readonly int IsClimbingHash = Animator.StringToHash("isClimbing");
    private static readonly int FinishClimbingHash = Animator.StringToHash("FinishClimbing");
    private static readonly int IsFallingHash = Animator.StringToHash("isFalling");
    private const float GroundedRaycastPadding = 0.05f;

    private Vector3 _moveTarget;
    private bool _hasMoveTarget;
    private Vector3 _currentVelocity;
    private bool _runInputActive;
    private bool _isRunningThisFrame;
    private bool _isClimbing;
    private Vector3 _climbFacingDirection;
    private bool _hasClimbFacingDirection;
    private HashSet<Collider> _activeClimbContacts;
    private int _climbableLayer;
    private int _climbableLayerMask;
    private bool _finishClimbing;
    private bool _isGrounded;
    private bool _isFalling;
    private CapsuleCollider _capsuleCollider;
    private float _finishClimbTimer;

    private void Awake()
    {
        _activeClimbContacts = new HashSet<Collider>();
        RefreshClimbableLayerData();
        EnsureGroundLayerMask();

        if (!TryGetComponent(out _capsuleCollider))
        {
            Debug.LogWarning("PlayerController: CapsuleCollider component not found.", this);
        }
    }

    private void Update()
    {
        HandleStopCommand();
        HandleClickToMoveInput();
        HandleClimbInput();

        if (_isClimbing)
        {
            _hasMoveTarget = false;
        }

        bool runKeyPressed = Input.GetKey(runKey);
        _runInputActive = runKeyPressed && !_isClimbing && !_finishClimbing;

        MovePlayer();

        CheckForClimbStop();

        UpdateAnimationState();

        if (_isClimbing || _finishClimbing)
        {
            HandleClimbRotation();
        }
        else
        {
            HandleRotation();
        }

        UpdateGroundedState();
        UpdateFinishClimbTimer();
    }

    private void HandleStopCommand()
    {
        if (Input.GetKeyDown(stopKey))
        {
            _hasMoveTarget = false;
            _currentVelocity = Vector3.zero;

            if (_finishClimbing)
            {
                return;
            }

            if (_isClimbing)
            {
                _isClimbing = false;
                _isFalling = true;
                _isGrounded = false;
                _hasClimbFacingDirection = false;
                _climbFacingDirection = Vector3.zero;
                _activeClimbContacts.Clear();

                if (animator)
                {
                    animator.SetBool(IsClimbingHash, false);
                    animator.SetBool(IsFallingHash, true);
                    animator.SetBool(FinishClimbingHash, false);
                    animator.SetBool(IsRunningHash, false);
                    animator.SetBool(IsWalkingHash, false);
                    animator.SetBool(IsIdleHash, false);
                }
            }
        }
    }

    private void HandleClimbInput()
    {
        if (_finishClimbing || _isClimbing)
        {
            return;
        }

        if (_activeClimbContacts.Count == 0)
        {
            return;
        }

        if (Input.GetKeyDown(climbKey))
        {
            BeginClimb();
        }
    }

    private void HandleClickToMoveInput()
    {
        if (!Input.GetMouseButtonDown(1))
        {
            return;
        }

        if (_isFalling || !_isGrounded)
        {
            return;
        }

        Camera currentCamera = Camera.main;
        if (currentCamera == null)
        {
            Debug.LogWarning("PlayerController: No camera tagged as MainCamera found for click-to-move.");
            return;
        }

        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, maximumRayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            _moveTarget = hitInfo.point;
            _hasMoveTarget = true;
            SpawnMoveMarker(hitInfo.point);
        }
    }

    private void MovePlayer()
    {
        if (_isClimbing)
        {
            _currentVelocity = Vector3.up * climbSpeed;
            transform.position += _currentVelocity * Time.deltaTime;
            _isRunningThisFrame = false;
            return;
        }
        if (_finishClimbing)
        {
            Vector3 climbForward = transform.forward;
            climbForward.y = 0f;
            if (climbForward.sqrMagnitude < 0.0001f)
            {
                climbForward = Vector3.forward;
            }
            climbForward.Normalize();

            _currentVelocity = (Vector3.up * finishClimbSpeed) + (climbForward * climbAnimationForwardOffset);
            transform.position += _currentVelocity * Time.deltaTime;
            _isRunningThisFrame = false;
            return;
        }

        if (!_isGrounded)
        {
            _isFalling = true;
            _hasMoveTarget = false;
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
            _currentVelocity = (Vector3.down * Mathf.Max(0f, fallSpeed)) + forwardContribution;
            transform.position += _currentVelocity * Time.deltaTime;
            _isRunningThisFrame = false;
            return;
        }

        _isFalling = false;

        Vector3 desiredVelocity = Vector3.zero;

        bool wantsToRun = _runInputActive && _hasMoveTarget;
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

        _currentVelocity = Vector3.MoveTowards(_currentVelocity, desiredVelocity, currentAcceleration * Time.deltaTime);
        transform.position += _currentVelocity * Time.deltaTime;

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

        _isRunningThisFrame = wantsToRun && _currentVelocity.sqrMagnitude > 0.0001f;
    }

    private void CheckForClimbStop()
    {
        if (!_isClimbing || _finishClimbing)
        {
            return;
        }

        if (_climbableLayerMask == 0)
        {
            Debug.LogWarning("PlayerController: Climb stop raycast skipped, Climbable layer not found.", this);
            return;
        }

        float rayLength = Mathf.Max(climbStopRaycastRange, climbStopDistanceThreshold);
        if (rayLength <= 0f)
        {
            return;
        }

        Vector3 origin = transform.position + (Vector3.up * climbStopRayOriginHeight);
        Vector3 direction = transform.forward;

        if (Physics.Raycast(origin, direction, out RaycastHit hitInfo, rayLength, _climbableLayerMask, QueryTriggerInteraction.Ignore))
        {
            if (hitInfo.distance <= climbStopDistanceThreshold)
            {
                return;
            }

            FinishClimb();
        }
        else
        {
            FinishClimb();
        }
    }

    private void BeginClimb()
    {
        if (_isClimbing || _finishClimbing)
        {
            return;
        }

        _isClimbing = true;
        _currentVelocity = Vector3.zero;
        _isRunningThisFrame = false;
        _hasMoveTarget = false;
        _isFalling = false;
    }

    private void UpdateAnimationState()
    {
        if (_isClimbing)
        {
            UpdateRunParticles(false);

            if (!animator)
            {
                return;
            }

            animator.SetBool(IsClimbingHash, true);
            animator.SetBool(FinishClimbingHash, false);
            animator.SetBool(IsFallingHash, false);
            animator.SetBool(IsRunningHash, false);
            animator.SetBool(IsWalkingHash, false);
            animator.SetBool(IsIdleHash, false);
            animator.speed = climbAnimationBaseSpeed;
            return;
        }
        if (_finishClimbing)
        {
            UpdateRunParticles(false);

            if (!animator)
            {
                return;
            }

            animator.SetBool(IsClimbingHash, false);
            animator.SetBool(FinishClimbingHash, true);
            animator.SetBool(IsFallingHash, false);
            animator.SetBool(IsRunningHash, false);
            animator.SetBool(IsWalkingHash, false);
            animator.SetBool(IsIdleHash, false);
            animator.speed = finishClimbAnimationBaseSpeed;
            return;
        }
        if (_isFalling)
        {
            UpdateRunParticles(false);

            if (!animator)
            {
                return;
            }

            animator.SetBool(IsClimbingHash, false);
            animator.SetBool(FinishClimbingHash, false);
            animator.SetBool(IsFallingHash, true);
            animator.SetBool(IsRunningHash, false);
            animator.SetBool(IsWalkingHash, false);
            animator.SetBool(IsIdleHash, false);
            animator.speed = 1f;
            return;
        }

        Vector3 planarVelocity = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);
        float speed = planarVelocity.magnitude;
        bool isMoving = speed > 0.01f;

        bool isRunning = _isRunningThisFrame && isMoving;
        bool isWalking = !isRunning && isMoving;
        bool isIdle = !isRunning && !isWalking;

        UpdateRunParticles(isRunning);

        if (!animator)
        {
            return;
        }

        animator.SetBool(IsClimbingHash, false);
        animator.SetBool(FinishClimbingHash, false);
        animator.SetBool(IsFallingHash, false);
        animator.SetBool(IsRunningHash, isRunning);
        animator.SetBool(IsWalkingHash, isWalking);
        animator.SetBool(IsIdleHash, isIdle);

        if (isRunning)
        {
            float runSpeed = Mathf.Max(0.001f, walkSpeed * runMultiplier);
            float normalized = Mathf.Clamp(speed / runSpeed, 0f, 2f);
            animator.speed = runAnimationBaseSpeed * normalized;
        }
        else if (isWalking)
        {
            float normalized = Mathf.Clamp(speed / Mathf.Max(0.001f, walkSpeed), 0f, 2f);
            animator.speed = walkAnimationBaseSpeed * normalized;
        }
        else
        {
            animator.speed = 1f;
        }
    }

    private void UpdateRunParticles(bool shouldBeActive)
    {
        if (!runParticleSystem)
        {
            return;
        }

        if (shouldBeActive)
        {
            ParticleSystem.EmissionModule emission = runParticleSystem.emission;
            if (!emission.enabled)
            {
                emission.enabled = true;
            }
        }
        else
        {
            ParticleSystem.EmissionModule emission = runParticleSystem.emission;
            if (emission.enabled)
            {
                emission.enabled = false;
            }
        }
    }

    private void HandleClimbRotation()
    {
        if (!_hasClimbFacingDirection)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(_climbFacingDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, climbRotationSpeed * Time.deltaTime);
    }

    private void HandleRotation()
    {
        Vector3 planarVelocity = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);
        if (planarVelocity.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float currentRotationSpeed = _isRunningThisFrame ? runRotationSpeed : walkRotationSpeed;
        Quaternion targetRotation = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
        Quaternion newRotation = Quaternion.RotateTowards(transform.rotation, targetRotation, currentRotationSpeed * Time.deltaTime);
        transform.rotation = newRotation;
    }

    private void UpdateGroundedState()
    {
        if (_isClimbing || _finishClimbing)
        {
            _isGrounded = true;
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
            //Debug.Log($"PlayerController: {state} (distance {distanceFromFeet:F3})", this);
        }

        if (_isGrounded)
        {
            _isFalling = false;
        }
        else if (!_isClimbing && !_finishClimbing)
        {
            _isFalling = true;
            Debug.Log("NOT GROUNDED");
        }
    }

    private void UpdateFinishClimbTimer()
    {
        if (!_finishClimbing)
        {
            _finishClimbTimer = 0f;
            return;
        }

        _finishClimbTimer += Time.deltaTime;
        if (_finishClimbTimer >= finishClimbTimeout)
        {
            Debug.LogWarning("PlayerController: Finish climb timeout reached, forcing reset.", this);
            ResetFinishClimbFlag();
        }
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

    private void RefreshClimbableLayerData()
    {
        _climbableLayer = LayerMask.NameToLayer("Climbable");
        _climbableLayerMask = _climbableLayer != -1 ? 1 << _climbableLayer : 0;
    }

    private void TryBeginClimb(Collision collision)
    {
        if (_finishClimbing)
        {
            return;
        }

        Collider collider = collision.collider;
        if (!IsClimbableCollider(collider))
        {
            return;
        }

        _activeClimbContacts.Add(collider);
        UpdateClimbFacingDirection(collision);
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

    private void OnCollisionEnter(Collision collision)
    {
        TryBeginClimb(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryBeginClimb(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!IsClimbableCollider(collision.collider))
        {
            return;
        }

        _activeClimbContacts.Remove(collision.collider);
        if (_activeClimbContacts.Count == 0 && !_finishClimbing)
        {
            _isClimbing = false;
            _hasClimbFacingDirection = false;
            _climbFacingDirection = Vector3.zero;
        }
    }

    private void FinishClimb()
    {
        if (_finishClimbing || !_isClimbing)
        {
            return;
        }

        _finishClimbing = true;
        _isClimbing = false;
        _currentVelocity = Vector3.zero;
        _hasMoveTarget = false;
        _activeClimbContacts.Clear();
        _isFalling = false;
        if (_capsuleCollider)
        {
            _capsuleCollider.isTrigger = true;
        }
        _finishClimbTimer = 0f;

        if (animator)
        {
            animator.SetBool(IsClimbingHash, false);
            animator.SetBool(FinishClimbingHash, true);
            animator.SetBool(IsFallingHash, false);
            animator.SetBool(IsRunningHash, false);
            animator.SetBool(IsWalkingHash, false);
            animator.SetBool(IsIdleHash, false);
        }
    }

    private void SpawnMoveMarker(Vector3 position)
    {
        if (moveMarkerPool == null)
        {
            return;
        }

        Vector3 markerPosition = position;
        markerPosition.y = markerHeight;
        moveMarkerPool.SpawnMarker(markerPosition);
    }

    private void OnDisable()
    {
        UpdateRunParticles(false);
        _isClimbing = false;
        _hasClimbFacingDirection = false;
        _climbFacingDirection = Vector3.zero;
        _finishClimbing = false;
        _isFalling = false;
        _activeClimbContacts?.Clear();
        _isGrounded = false;
        _finishClimbTimer = 0f;

        if (animator)
        {
            animator.SetBool(IsClimbingHash, false);
            animator.SetBool(FinishClimbingHash, false);
            animator.SetBool(IsFallingHash, false);
        }
    }

    private void OnValidate()
    {
        walkSpeed = Mathf.Max(0f, walkSpeed);
        runMultiplier = Mathf.Max(1f, runMultiplier);
        walkAcceleration = Mathf.Max(0f, walkAcceleration);
        runAcceleration = Mathf.Max(0f, runAcceleration);
        walkRotationSpeed = Mathf.Max(0f, walkRotationSpeed);
        runRotationSpeed = Mathf.Max(0f, runRotationSpeed);
        climbRotationSpeed = Mathf.Max(0f, climbRotationSpeed);
        climbSpeed = Mathf.Max(0f, climbSpeed);
        finishClimbSpeed = Mathf.Max(0f, finishClimbSpeed);
        climbAnimationForwardOffset = Mathf.Max(0f, climbAnimationForwardOffset);
        finishClimbTimeout = Mathf.Max(0.01f, finishClimbTimeout);
        stopDistance = Mathf.Max(0f, stopDistance);
        maximumRayDistance = Mathf.Max(0f, maximumRayDistance);
        markerHeight = Mathf.Max(0f, markerHeight);
        walkAnimationBaseSpeed = Mathf.Max(0.01f, walkAnimationBaseSpeed);
        runAnimationBaseSpeed = Mathf.Max(0.01f, runAnimationBaseSpeed);
        climbAnimationBaseSpeed = Mathf.Max(0.01f, climbAnimationBaseSpeed);
        finishClimbAnimationBaseSpeed = Mathf.Max(0.01f, finishClimbAnimationBaseSpeed);
        groundedCheckStartHeight = Mathf.Max(0f, groundedCheckStartHeight);
        groundedDistanceThreshold = Mathf.Max(0f, groundedDistanceThreshold);
        climbStopRayOriginHeight = Mathf.Max(0f, climbStopRayOriginHeight);
        climbStopRaycastRange = Mathf.Max(0f, climbStopRaycastRange);
        fallSpeed = Mathf.Max(0f, fallSpeed);
        fallForwardSpeed = Mathf.Max(0f, fallForwardSpeed);
        if (climbStopRaycastRange <= 0f)
        {
            climbStopDistanceThreshold = 0f;
        }
        else
        {
            climbStopDistanceThreshold = Mathf.Clamp(climbStopDistanceThreshold, 0f, climbStopRaycastRange);
        }
        RefreshClimbableLayerData();
        EnsureGroundLayerMask();
    }

    public void ResetFinishClimbFlag()
    {
        _finishClimbing = false;
        _currentVelocity = Vector3.zero;
        _hasClimbFacingDirection = false;
        _climbFacingDirection = Vector3.zero;
        _isFalling = false;
        _finishClimbTimer = 0f;
        if (_capsuleCollider)
        {
            _capsuleCollider.isTrigger = false;
        }

        if (animator)
        {
            animator.SetBool(FinishClimbingHash, false);
            animator.SetBool(IsFallingHash, false);
            animator.SetBool(IsRunningHash, false);
            animator.SetBool(IsWalkingHash, false);
            animator.SetBool(IsClimbingHash, false);
            animator.SetBool(IsIdleHash, true);
        }
    }

}
