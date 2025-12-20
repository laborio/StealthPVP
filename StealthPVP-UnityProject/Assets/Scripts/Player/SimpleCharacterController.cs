using UnityEngine;

/// <summary>
/// Lightweight movement controller that drives a CharacterController with walking, running, jumping, and click-to-move support.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public partial class SimpleCharacterController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float runMultiplier = 1.5f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private PlayerInputRouter inputRouter;

    [Header("Click To Move")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float stopDistance = 0.2f;
    [SerializeField] private float maximumRayDistance = 250f;
    [SerializeField] private ClickMoveMarkerPool markerPool;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGravity = -2f;
    [SerializeField] private float jumpVelocity = 7.5f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField, Range(0f, 5f)] private float airControl = 0.3f;
    [Header("Attack")]
    [SerializeField, Tooltip("Name of the animator trigger used for the attack animation.")] private string attackTriggerName = "Attack";
    [SerializeField, Tooltip("Optional range indicator shown while holding the attack button.")] private GameObject rangeIndicator;
    [SerializeField, Tooltip("Rotation speed (deg/sec) when aligning to the attack aim.")] private float attackAimRotationSpeed = 1080f;
    [SerializeField, Tooltip("Layers considered for attack aiming.")] private LayerMask attackGroundMask = Physics.DefaultRaycastLayers;
    [SerializeField, Tooltip("Minimum time movement stays locked after triggering an attack.")] private float attackLockMinDuration = 0.05f;
    [SerializeField, Tooltip("Impulse applied to rigidbodies when bumped by the CharacterController.")] private float rigidbodyPushForce = 3f;
    [SerializeField] private float dashSpeedMultiplier = 3f;
    [SerializeField] private float dashDuration = 0.25f;
    [SerializeField] private float dashCooldown = 1f;
    [SerializeField] private float groundProbeRadius = 0.2f;
    [SerializeField] private float groundProbeDistance = 0.25f;
    [Header("Wall Jump")]
    [SerializeField] private float wallJumpUpVelocity = 7.5f;
    [SerializeField] private float wallJumpHorizontalSpeed = 6f;
    [SerializeField, Range(0f, 0.5f)] private float wallContactLinger = 0.15f;
    [SerializeField, Range(0f, 1f)] private float wallJumpCooldown = 0.2f;
    [Header("Camera Override")]
    [SerializeField, Tooltip("Optional camera set per player; falls back to MainCamera.")] private Camera overrideCamera;

    [Header("State Smoothing")]
    [SerializeField, Range(0f, 0.5f)] private float groundedStateLinger = 0.12f;
    [Header("Water Interaction")]
    [SerializeField] private LayerMask waterLayerMask = 1 << 4;
    [SerializeField, Range(0.1f, 1f)] private float waterMoveSpeedMultiplier = 0.6f;
    [SerializeField, Range(0.1f, 1f)] private float waterJumpVelocityMultiplier = 0.6f;

    partial void OnBenchAwake();

    private CharacterController _characterController;
    private Camera _camera;
    private Transform _cameraTransform;
    private float _verticalVelocity;
    private bool _hasMoveTarget;
    private Vector3 _moveTarget;
    private bool _isRunning;
    private bool _isJumping;
    private bool _isFalling;
    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private Vector3 _currentPlanarVelocity;
    private float _dashTimer;
    private float _dashCooldownTimer;
    private bool _isDashing;
    private float _groundedStateTimer;
    private float _wallContactTimer;
    private Vector3 _wallNormal;
    private float _wallJumpCooldownTimer;
    private bool _isInWater;
    [SerializeField] private CharacterAnimations characterAnimations;
    private bool _attackChargeActive;
    private bool _attackLockActive;
    private bool _attackAimInProgress;
    private Quaternion _attackTargetRotation;
    private Vector3 _lastAimDirection = Vector3.forward;
    private Transform _rangeIndicatorTransform;
    private float _attackLockTimer;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();

        _camera = overrideCamera ? overrideCamera : Camera.main;
        _cameraTransform = _camera ? _camera.transform : null;
        if (!characterAnimations)
        {
            characterAnimations = GetComponentInChildren<CharacterAnimations>();
        }
        if (!inputRouter)
        {
            inputRouter = GetComponent<PlayerInputRouter>();
        }
        if (inputRouter)
        {
            inputRouter.SetInputCamera(_camera);
        }
        CacheRangeIndicator();
        SetRangeIndicatorActive(false);
        OnBenchAwake();
    }

    private void Update()
    {
        PlayerInputSnapshot inputSnapshot = inputRouter ? inputRouter.PollInput() : PollLegacyInput();
        bool isGrounded = IsGrounded();
        if (_teleportLocked)
        {
            HandleTeleportLockUpdate(isGrounded);
            return;
        }

        HandleClickToMove(inputSnapshot);
        Vector2 movementInputRaw = inputSnapshot.MoveAxis;
        bool requestedMovement = movementInputRaw.sqrMagnitude > 0.0001f;
        bool interactPressed = inputSnapshot.InteractPressed;
        float deltaTime = Time.deltaTime;
        HandleBenchInput(requestedMovement, interactPressed);
        HandleAttackInput(inputSnapshot, isGrounded);
        UpdateAttackLockState(deltaTime);
        bool actionKeyAllowed = _seatingState == SeatingState.Standing;
        HandleActionInput(interactPressed && actionKeyAllowed, isGrounded);

        bool seatingLocked = _seatingState != SeatingState.Standing;
        bool attackMovementLocked = _attackLockActive;
        bool movingToSeat = _seatingState == SeatingState.MovingToSeat;
        bool walkOverrideActive = movingToSeat;
        Vector2 movementInput = (seatingLocked || attackMovementLocked) ? Vector2.zero : movementInputRaw;
        Vector3 moveDirection = ResolveMoveDirection(movementInput);
        bool hasMovementInput = moveDirection.sqrMagnitude > 0.0001f;
        bool wantsToWalk = inputSnapshot.RunHeld;
        _wallContactTimer = Mathf.Max(_wallContactTimer - deltaTime, 0f);
        if (_wallJumpCooldownTimer > 0f)
        {
            _wallJumpCooldownTimer = Mathf.Max(_wallJumpCooldownTimer - deltaTime, 0f);
        }
        _isInWater = DetectWater();
        float waterSpeedMultiplier = _isInWater ? this.waterMoveSpeedMultiplier : 1f;
        float waterJumpMultiplier = _isInWater ? this.waterJumpVelocityMultiplier : 1f;

        Vector3 desiredPlanarVelocity = Vector3.zero;

        if (movingToSeat)
        {
            Vector3 toSeat = _sitTargetPosition - transform.position;
            toSeat.y = 0f;
            float sqrDistance = toSeat.sqrMagnitude;

            if (sqrDistance <= seatSnapDistance * seatSnapDistance)
            {
                SnapToSeatPoint();
                hasMovementInput = false;
            }
            else
            {
                Vector3 approachDirection = toSeat.normalized;
                float speed = Mathf.Max(benchApproachSpeed, 0.01f);
                desiredPlanarVelocity = approachDirection * speed;
                hasMovementInput = true;
                _hasMoveTarget = false;
            }
        }
        else
        {
            if (seatingLocked)
            {
                _hasMoveTarget = false;
                hasMovementInput = false;
            }
            else if (attackMovementLocked)
            {
                _hasMoveTarget = false;
                hasMovementInput = false;
                requestedMovement = false;
            }

            if (hasMovementInput)
            {
                moveDirection.Normalize();
                float speed = moveSpeed * ((wantsToWalk || walkOverrideActive) ? 1f : runMultiplier);
                speed *= waterSpeedMultiplier;
                desiredPlanarVelocity = moveDirection * speed;
                _hasMoveTarget = false;
            }
            else if (_hasMoveTarget)
            {
                Vector3 toTarget = _moveTarget - transform.position;
                toTarget.y = 0f;
                float remainingDistance = toTarget.magnitude;

                if (remainingDistance <= stopDistance)
                {
                    _hasMoveTarget = false;
                }
                else
                {
                    float speed = moveSpeed * ((wantsToWalk || walkOverrideActive) ? 1f : runMultiplier);
                    speed *= waterSpeedMultiplier;
                    desiredPlanarVelocity = toTarget.normalized * speed;
                    hasMovementInput = true;
                }
            }
        }

        if (isGrounded)
        {
            _coyoteTimer = coyoteTime;
            _groundedStateTimer = groundedStateLinger;
            if (_verticalVelocity < 0f)
            {
                _verticalVelocity = groundedGravity;
                if (!_isJumping)
                {
                    _isFalling = false;
                }
            }
        }
        else
        {
            _coyoteTimer = Mathf.Max(_coyoteTimer - deltaTime, 0f);
            _groundedStateTimer = Mathf.Max(_groundedStateTimer - deltaTime, 0f);
        }
        bool groundedForAnimation = isGrounded || _groundedStateTimer > 0f;

        if (!seatingLocked)
        {
            if (inputSnapshot.JumpPressed)
            {
                _jumpBufferTimer = jumpBufferTime;
            }
            else
            {
                _jumpBufferTimer = Mathf.Max(_jumpBufferTimer - deltaTime, 0f);
            }
        }
        else
        {
            _jumpBufferTimer = 0f;
        }

        if (seatingLocked)
        {
            _dashCooldownTimer = 0f;
            _dashTimer = 0f;
            _isDashing = false;
        }
        else
        {
            if (_dashCooldownTimer > 0f)
            {
                _dashCooldownTimer = Mathf.Max(_dashCooldownTimer - deltaTime, 0f);
            }

            if (!_isDashing && _dashCooldownTimer <= 0f && inputSnapshot.DashPressed)
            {
                Vector3 forward = transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f)
                {
                    forward = Vector3.forward;
                }
                forward.Normalize();

                float speed = moveSpeed * runMultiplier * dashSpeedMultiplier * waterSpeedMultiplier;
                _currentPlanarVelocity = forward * speed;
                _dashTimer = dashDuration;
                _dashCooldownTimer = dashCooldown;
                _isDashing = true;
                _hasMoveTarget = false;
            }
        }

        if (_isDashing)
        {
            _dashTimer -= deltaTime;
            if (_dashTimer <= 0f)
            {
                _isDashing = false;
            }
        }
        else if (attackMovementLocked)
        {
            _dashTimer = 0f;
            _dashCooldownTimer = 0f;
            _isDashing = false;
        }

        Vector3 targetPlanarVelocity = _isDashing ? _currentPlanarVelocity : desiredPlanarVelocity;
        if (_isDashing)
        {
            _currentPlanarVelocity = targetPlanarVelocity;
        }
        else if (isGrounded)
        {
            _currentPlanarVelocity = attackMovementLocked ? Vector3.zero : targetPlanarVelocity;
        }
        else if (targetPlanarVelocity.sqrMagnitude > 0.0001f)
        {
            float lerpFactor = Mathf.Clamp01(airControl * deltaTime);
            _currentPlanarVelocity = attackMovementLocked
                ? Vector3.Lerp(_currentPlanarVelocity, Vector3.zero, lerpFactor)
                : Vector3.Lerp(_currentPlanarVelocity, targetPlanarVelocity, lerpFactor);
        }

        bool bufferedJumpRequested = _jumpBufferTimer > 0f;
        if (bufferedJumpRequested && _coyoteTimer > 0f)
        {
            _verticalVelocity = jumpVelocity * waterJumpMultiplier;
            Vector3 launchVelocity = _currentPlanarVelocity;
            if (launchVelocity.sqrMagnitude < 0.0001f)
            {
                launchVelocity = new Vector3(_characterController.velocity.x, 0f, _characterController.velocity.z);
            }
            if (launchVelocity.sqrMagnitude > 0.0001f)
            {
                _currentPlanarVelocity = launchVelocity;
            }
            _isJumping = true;
            _isFalling = false;
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
        }
        else
        {
            bool canWallJump = !isGrounded && _wallContactTimer > 0f && _wallJumpCooldownTimer <= 0f;
            if (bufferedJumpRequested && canWallJump)
            {
                Vector3 planarIncoming = _currentPlanarVelocity;
                planarIncoming.y = 0f;
                if (planarIncoming.sqrMagnitude < 0.0001f)
                {
                    planarIncoming = transform.forward;
                }

                Vector3 bounceDirection = planarIncoming.sqrMagnitude > 0.0001f && _wallNormal.sqrMagnitude > 0.0001f
                    ? Vector3.Reflect(planarIncoming.normalized, _wallNormal)
                    : (_wallNormal.sqrMagnitude > 0.0001f ? -_wallNormal : -transform.forward);

                bounceDirection.y = 0f;
                if (bounceDirection.sqrMagnitude > 0.0001f)
                {
                    Vector3 normalizedBounce = bounceDirection.normalized;
                    _currentPlanarVelocity = normalizedBounce * wallJumpHorizontalSpeed * waterSpeedMultiplier;
                    transform.rotation = Quaternion.LookRotation(normalizedBounce, Vector3.up);
                }
                _verticalVelocity = wallJumpUpVelocity * waterJumpMultiplier;
                _isJumping = true;
                _isFalling = false;
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;
                _wallContactTimer = 0f;
                _wallJumpCooldownTimer = wallJumpCooldown;
                _isDashing = false;
                _hasMoveTarget = false;
            }
        }

        if (seatingLocked)
        {
            _verticalVelocity = 0f;
        }
        else
        {
            _verticalVelocity += gravity * deltaTime;
        }

        Vector3 motion = _currentPlanarVelocity;
        motion.y = _verticalVelocity;
        _characterController.Move(motion * Time.deltaTime);

        Vector3 planarMove = new Vector3(_currentPlanarVelocity.x, 0f, _currentPlanarVelocity.z);
        float planarSpeed = planarMove.magnitude;

        bool attackRotationApplied = ProcessAttackAimRotation(deltaTime);

        if (!attackRotationApplied && planarMove.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(planarMove.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * deltaTime);
        }

        bool allowRunningAnimation = !walkOverrideActive;
        _isRunning = allowRunningAnimation && !wantsToWalk && planarSpeed > 0.1f;

        bool consideredAirborne = !groundedForAnimation;
        if (consideredAirborne)
        {
            bool nowFalling = _verticalVelocity < -0.1f;
            _isFalling = nowFalling;
            if (nowFalling)
            {
                _isJumping = false;
            }
        }
        else if (isGrounded)
        {
            _isJumping = false;
            _isFalling = false;
        }

        bool isAirborne = _isJumping || _isFalling;
        bool isWalking = !isAirborne && planarSpeed > 0.1f && !_isRunning && !_isDashing;
        bool isRunningEffective = !isAirborne && (_isRunning || _isDashing);

        characterAnimations?.ApplyLocomotion(new CharacterLocomotionAnimationData
        {
            IsWalking = isWalking,
            IsRunning = isRunningEffective,
            IsJumping = _isJumping,
            IsFalling = _isFalling,
            PlanarSpeed = planarSpeed,
            WalkSpeed = moveSpeed,
            RunSpeed = moveSpeed * runMultiplier,
            JumpAnimationSpeed = 1f
        });
        UpdateSeatingState(deltaTime);
        ProcessBenchCollisionRestore(deltaTime);
        UpdateActionHintDisplay(_seatingState == SeatingState.Standing, isGrounded);
    }

    private void HandleAttackInput(PlayerInputSnapshot input, bool isGrounded)
    {
        if (_seatingState != SeatingState.Standing)
        {
            CancelAttackCharge();
            return;
        }

        if (_attackLockActive || (characterAnimations != null && characterAnimations.IsInAttackState()))
        {
            return;
        }

        if (input.PrimaryPressed)
        {
            StartAttackCharge();
        }

        if (_attackChargeActive && (input.PrimaryHeld || input.PrimaryPressed))
        {
            UpdateAttackAim(input);
        }

        if (_attackChargeActive && input.PrimaryReleased)
        {
            if (isGrounded)
            {
                Debug.Log("ATCK");
                TriggerAttack(input);
            }
            else
            {
                CancelAttackCharge();
            }
        }
    }

    private void StartAttackCharge()
    {
        _attackChargeActive = true;
        _lastAimDirection = transform.forward;
        SetRangeIndicatorActive(true);
        ApplyRangeIndicatorRotation(_lastAimDirection);
    }

    private void CancelAttackCharge()
    {
        _attackChargeActive = false;
        _attackAimInProgress = false;
        SetRangeIndicatorActive(false);
    }

    private void UpdateAttackAim(PlayerInputSnapshot input)
    {
        if (!TryGetAimPoint(input, out Vector3 aimPoint))
        {
            return;
        }

        Vector3 aimDirection = aimPoint - transform.position;
        aimDirection.y = 0f;
        if (aimDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        _lastAimDirection = aimDirection.normalized;
        ApplyRangeIndicatorRotation(_lastAimDirection);
    }

    private void TriggerAttack(PlayerInputSnapshot input)
    {
        SetRangeIndicatorActive(false);
        _attackChargeActive = false;

        Vector3 aimDirection = _lastAimDirection;
        if (TryGetAimPoint(input, out Vector3 aimPoint))
        {
            Vector3 dir = aimPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                aimDirection = dir.normalized;
            }
        }

        BeginAttackRotation(aimDirection);
        _attackLockActive = true;
        _attackLockTimer = Mathf.Max(attackLockMinDuration, 0f);
        _isDashing = false;
        _dashTimer = 0f;
        _currentPlanarVelocity = Vector3.zero;
        _hasMoveTarget = false;
        characterAnimations?.TriggerAttack(attackTriggerName);
    }

    private void BeginAttackRotation(Vector3 aimDirection)
    {
        if (aimDirection.sqrMagnitude < 0.0001f)
        {
            _attackAimInProgress = false;
            return;
        }

        _attackTargetRotation = Quaternion.LookRotation(aimDirection.normalized, Vector3.up);
        _attackAimInProgress = true;
    }

    private bool ProcessAttackAimRotation(float deltaTime)
    {
        if (!_attackAimInProgress)
        {
            return false;
        }

        if (attackAimRotationSpeed <= 0f)
        {
            transform.rotation = _attackTargetRotation;
            _attackAimInProgress = false;
            return true;
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, _attackTargetRotation, attackAimRotationSpeed * deltaTime);
        float remainingAngle = Quaternion.Angle(transform.rotation, _attackTargetRotation);
        if (remainingAngle <= 0.5f)
        {
            _attackAimInProgress = false;
        }

        return true;
    }

    private void UpdateAttackLockState(float deltaTime)
    {
        if (!_attackLockActive)
        {
            return;
        }

        _attackLockTimer = Mathf.Max(0f, _attackLockTimer - deltaTime);

        if (_attackLockTimer > 0f)
        {
            return;
        }

        if (characterAnimations != null && characterAnimations.IsInAttackState())
        {
            return;
        }

        _attackLockActive = false;
    }

    private bool TryGetAimPoint(PlayerInputSnapshot input, out Vector3 point)
    {
        if (input.HasAimPoint)
        {
            point = input.AimPoint;
            return true;
        }

        point = default;
        Camera targetCamera = _camera ? _camera : Camera.main;
        if (!targetCamera)
        {
            return false;
        }

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        LayerMask mask = attackGroundMask.value != 0 ? attackGroundMask : groundMask;
        if (Physics.Raycast(ray, out RaycastHit hitInfo, maximumRayDistance, mask, QueryTriggerInteraction.Ignore))
        {
            point = hitInfo.point;
            return true;
        }

        return false;
    }

    private void SetRangeIndicatorActive(bool active)
    {
        if (!_rangeIndicatorTransform)
        {
            return;
        }

        GameObject indicatorGO = _rangeIndicatorTransform.gameObject;
        if (indicatorGO.activeSelf != active)
        {
            indicatorGO.SetActive(active);
        }
    }

    private void CacheRangeIndicator()
    {
        if (rangeIndicator)
        {
            _rangeIndicatorTransform = rangeIndicator.transform;
            return;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] && children[i].name == "RangeIndicator")
            {
                _rangeIndicatorTransform = children[i];
                rangeIndicator = children[i].gameObject;
                break;
            }
        }
    }

    private void ApplyRangeIndicatorRotation(Vector3 direction)
    {
        if (!_rangeIndicatorTransform)
        {
            return;
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.forward;
        }

        direction.Normalize();
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(90f, yaw, 0f);
        _rangeIndicatorTransform.rotation = rotation;
    }

    private bool IsGrounded()
    {
        if (_characterController == null)
        {
            return false;
        }

        if (_characterController.isGrounded)
        {
            return true;
        }

        if (groundProbeRadius <= 0f || groundProbeDistance <= 0f)
        {
            return false;
        }

        Bounds bounds = _characterController.bounds;
        Vector3 origin = bounds.center;
        origin.y = bounds.min.y + groundProbeRadius + 0.01f;

        int mask = groundMask.value != 0 ? groundMask.value : Physics.DefaultRaycastLayers;
        float distance = groundProbeDistance + 0.02f;

        return Physics.SphereCast(origin, groundProbeRadius, Vector3.down, out _, distance, mask, QueryTriggerInteraction.Ignore);
    }





    private void HandleClickToMove(PlayerInputSnapshot input)
    {
        if (_seatingState != SeatingState.Standing || !input.MoveIssued)
        {
            return;
        }

        _moveTarget = input.MoveTarget;
        _moveTarget.y = transform.position.y;
        _hasMoveTarget = true;

        if (markerPool)
        {
            markerPool.SpawnMarker(_moveTarget);
        }
    }

    private Vector3 ResolveMoveDirection(Vector2 input)
    {
        if (_cameraTransform)
        {
            Vector3 forward = _cameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = _cameraTransform.right;
            right.y = 0f;
            right.Normalize();

            return (forward * input.y) + (right * input.x);
        }

        return new Vector3(input.x, 0f, input.y);
    }


    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!_characterController || !_characterController.enabled)
        {
            return;
        }

        if (rigidbodyPushForce > 0f && hit.rigidbody && !hit.rigidbody.isKinematic)
        {
            Vector3 pushDir = hit.moveDirection;
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude > 0.0001f)
            {
                hit.rigidbody.AddForce(pushDir.normalized * rigidbodyPushForce, ForceMode.Impulse);
            }
        }

        if (wallContactLinger <= 0f)
        {
            return;
        }

        if (_characterController.isGrounded)
        {
            return;
        }

        if (!hit.collider || !hit.collider.CompareTag("Wall"))
        {
            return;
        }

        Vector3 normal = hit.normal;
        normal.y = 0f;
        if (normal.sqrMagnitude < 0.0001f)
        {
            return;
        }

        _wallNormal = normal.normalized;
        _wallContactTimer = wallContactLinger;
    }

    private void OnDisable()
    {
        _verticalVelocity = 0f;
        _isRunning = false;
        _isJumping = false;
        _isFalling = false;
        _coyoteTimer = 0f;
        _jumpBufferTimer = 0f;
        _currentPlanarVelocity = Vector3.zero;
        _groundedStateTimer = 0f;
        _wallContactTimer = 0f;
        _wallJumpCooldownTimer = 0f;
        _wallNormal = Vector3.zero;
        CancelSeatingSequence();
        _activeBench = null;
        ClearBenchTracking();
        ClearContextActions();
        characterAnimations?.ResetStates();
        _hasMoveTarget = false;
        _teleportLocked = false;
        CancelAttackCharge();
        _attackLockActive = false;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        runMultiplier = Mathf.Max(1f, runMultiplier);
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        stopDistance = Mathf.Max(0f, stopDistance);
        maximumRayDistance = Mathf.Max(0f, maximumRayDistance);
        gravity = Mathf.Min(-0.01f, gravity);
        groundedGravity = Mathf.Min(0f, groundedGravity);
        jumpVelocity = Mathf.Max(0f, jumpVelocity);
        coyoteTime = Mathf.Max(0f, coyoteTime);
        jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
        airControl = Mathf.Clamp(airControl, 0f, 5f);
        groundProbeRadius = Mathf.Max(0f, groundProbeRadius);
        groundProbeDistance = Mathf.Max(0f, groundProbeDistance);
        groundedStateLinger = Mathf.Clamp(groundedStateLinger, 0f, 0.5f);
        wallJumpUpVelocity = Mathf.Max(0f, wallJumpUpVelocity);
        wallJumpHorizontalSpeed = Mathf.Max(0f, wallJumpHorizontalSpeed);
        wallContactLinger = Mathf.Clamp(wallContactLinger, 0f, 0.5f);
        wallJumpCooldown = Mathf.Clamp(wallJumpCooldown, 0f, 1f);
        benchApproachSpeed = Mathf.Max(0.1f, benchApproachSpeed);
        seatSnapDistance = Mathf.Clamp(seatSnapDistance, 0.01f, 1f);
        benchAlignmentSpeed = Mathf.Max(0f, benchAlignmentSpeed);
        standToSitAnimSpeed = Mathf.Max(0.1f, standToSitAnimSpeed);
        waterMoveSpeedMultiplier = Mathf.Clamp(waterMoveSpeedMultiplier, 0.1f, 1f);
        waterJumpVelocityMultiplier = Mathf.Clamp(waterJumpVelocityMultiplier, 0.1f, 1f);
        attackAimRotationSpeed = Mathf.Max(0f, attackAimRotationSpeed);
        attackLockMinDuration = Mathf.Max(0f, attackLockMinDuration);
        rigidbodyPushForce = Mathf.Max(0f, rigidbodyPushForce);
        CacheRangeIndicator();
    }

    private PlayerInputSnapshot PollLegacyInput()
    {
        PlayerInputSnapshot snapshot = new PlayerInputSnapshot
        {
            RunHeld = Input.GetKey(KeyCode.LeftShift),
            StopPressed = Input.GetKeyDown(KeyCode.S),
            JumpPressed = Input.GetKeyDown(KeyCode.Space),
            DashPressed = Input.GetKeyDown(KeyCode.R),
            InteractPressed = Input.GetKeyDown(KeyCode.E),
            PrimaryPressed = Input.GetMouseButtonDown(0),
            PrimaryHeld = Input.GetMouseButton(0),
            PrimaryReleased = Input.GetMouseButtonUp(0),
            MoveAxis = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"))
        };

        if (Input.GetMouseButtonDown(1) && TryResolveClickToMove(out Vector3 targetPosition))
        {
            snapshot.MoveIssued = true;
            snapshot.MoveTarget = targetPosition;
        }

        if ((snapshot.PrimaryHeld || snapshot.PrimaryPressed || snapshot.PrimaryReleased) && TryResolveAimPoint(out Vector3 aimPoint))
        {
            snapshot.HasAimPoint = true;
            snapshot.AimPoint = aimPoint;
        }

        return snapshot;
    }

    private bool TryResolveClickToMove(out Vector3 targetPosition)
    {
        targetPosition = default;
        Camera targetCamera = _camera ? _camera : Camera.main;
        if (!targetCamera)
        {
            return false;
        }

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, maximumRayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            targetPosition = hitInfo.point;
            return true;
        }

        return false;
    }

    private bool TryResolveAimPoint(out Vector3 point)
    {
        point = default;
        Camera targetCamera = _camera ? _camera : Camera.main;
        if (!targetCamera)
        {
            return false;
        }

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        LayerMask mask = attackGroundMask.value != 0 ? attackGroundMask : groundMask;
        if (Physics.Raycast(ray, out RaycastHit hitInfo, maximumRayDistance, mask, QueryTriggerInteraction.Ignore))
        {
            point = hitInfo.point;
            return true;
        }

        return false;
    }

    private bool DetectWater()
    {
        if (_characterController == null || waterLayerMask.value == 0)
        {
            return false;
        }

        Bounds bounds = _characterController.bounds;
        float radius = Mathf.Max(0.01f, _characterController.radius * 0.95f);
        Vector3 bottom = bounds.center;
        bottom.y = bounds.min.y + radius;
        Vector3 top = bounds.center;
        top.y = bounds.max.y - radius;

        return Physics.CheckCapsule(bottom, top, radius, waterLayerMask, QueryTriggerInteraction.Collide);
    }

    public void SetCamera(Camera camera)
    {
        overrideCamera = camera;
        _camera = camera ? camera : Camera.main;
        _cameraTransform = _camera ? _camera.transform : null;

        if (inputRouter)
        {
            inputRouter.SetInputCamera(_camera);
        }
    }

    public void SetInputRouter(PlayerInputRouter router)
    {
        inputRouter = router;
        if (inputRouter)
        {
            inputRouter.SetInputCamera(_camera);
        }
    }
}
