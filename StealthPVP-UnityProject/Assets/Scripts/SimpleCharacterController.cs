using UnityEngine;

/// <summary>
/// Lightweight movement controller that drives a CharacterController with walking, running, jumping, and click-to-move support.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class SimpleCharacterController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float runMultiplier = 1.5f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode dashKey = KeyCode.R;

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

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string walkingBoolName = "isWalking";
    [SerializeField] private string idleBoolName = "isIdle";
    [SerializeField] private string runningBoolName = "isRunning";
    [SerializeField] private string jumpingBoolName = "isJumping";
    [SerializeField] private string fallingBoolName = "isFalling";
    [Header("State Smoothing")]
    [SerializeField, Range(0f, 0.5f)] private float groundedStateLinger = 0.12f;

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

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();

        if (!animator)
        {
            animator = GetComponentInChildren<Animator>();
        }

        _camera = Camera.main;
        _cameraTransform = _camera ? _camera.transform : null;
    }

    private void Update()
    {
        HandleClickToMove();

        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector3 moveDirection = ResolveMoveDirection(input);
        bool hasMovementInput = moveDirection.sqrMagnitude > 0.0001f;
        bool wantsToRun = Input.GetKey(runKey);
        float deltaTime = Time.deltaTime;
        _wallContactTimer = Mathf.Max(_wallContactTimer - deltaTime, 0f);
        if (_wallJumpCooldownTimer > 0f)
        {
            _wallJumpCooldownTimer = Mathf.Max(_wallJumpCooldownTimer - deltaTime, 0f);
        }

        Vector3 desiredPlanarVelocity = Vector3.zero;

        if (hasMovementInput)
        {
            moveDirection.Normalize();
            float speed = moveSpeed * (wantsToRun ? runMultiplier : 1f);
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
                float speed = moveSpeed * (wantsToRun ? runMultiplier : 1f);
                desiredPlanarVelocity = toTarget.normalized * speed;
                hasMovementInput = true;
            }
        }

        bool isGrounded = IsGrounded();
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

        if (Input.GetKeyDown(jumpKey))
        {
            _jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            _jumpBufferTimer = Mathf.Max(_jumpBufferTimer - deltaTime, 0f);
        }

        if (_dashCooldownTimer > 0f)
        {
            _dashCooldownTimer = Mathf.Max(_dashCooldownTimer - deltaTime, 0f);
        }

        if (!_isDashing && _dashCooldownTimer <= 0f && Input.GetKeyDown(dashKey))
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            float speed = moveSpeed * runMultiplier * dashSpeedMultiplier;
            _currentPlanarVelocity = forward * speed;
            _dashTimer = dashDuration;
            _dashCooldownTimer = dashCooldown;
            _isDashing = true;
            _hasMoveTarget = false;
        }

        if (_isDashing)
        {
            _dashTimer -= deltaTime;
            if (_dashTimer <= 0f)
            {
                _isDashing = false;
            }
        }

        Vector3 targetPlanarVelocity = _isDashing ? _currentPlanarVelocity : desiredPlanarVelocity;
        if (_isDashing)
        {
            _currentPlanarVelocity = targetPlanarVelocity;
        }
        else if (isGrounded)
        {
            _currentPlanarVelocity = targetPlanarVelocity;
        }
        else if (targetPlanarVelocity.sqrMagnitude > 0.0001f)
        {
            float lerpFactor = Mathf.Clamp01(airControl * deltaTime);
            _currentPlanarVelocity = Vector3.Lerp(_currentPlanarVelocity, targetPlanarVelocity, lerpFactor);
        }

        bool bufferedJumpRequested = _jumpBufferTimer > 0f;
        if (bufferedJumpRequested && _coyoteTimer > 0f)
        {
            _verticalVelocity = jumpVelocity;
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
                    _currentPlanarVelocity = normalizedBounce * wallJumpHorizontalSpeed;
                    transform.rotation = Quaternion.LookRotation(normalizedBounce, Vector3.up);
                }
                _verticalVelocity = wallJumpUpVelocity;
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

        _verticalVelocity += gravity * deltaTime;

        Vector3 motion = _currentPlanarVelocity;
        motion.y = _verticalVelocity;
        _characterController.Move(motion * Time.deltaTime);

        Vector3 planarMove = new Vector3(_currentPlanarVelocity.x, 0f, _currentPlanarVelocity.z);
        float planarSpeed = planarMove.magnitude;

        if (planarMove.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(planarMove.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        _isRunning = wantsToRun && planarSpeed > 0.1f;

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
        UpdateAnimator(isWalking, isRunningEffective, _isJumping, _isFalling);
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

    private void HandleClickToMove()
    {
        if (!Input.GetMouseButtonDown(1))
        {
            return;
        }

        Camera targetCamera = _camera ? _camera : Camera.main;
        if (!targetCamera)
        {
            Debug.LogWarning("SimpleCharacterController: No main camera found for click-to-move.");
            return;
        }

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, maximumRayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            _moveTarget = hitInfo.point;
            _moveTarget.y = transform.position.y;
            _hasMoveTarget = true;

            if (markerPool)
            {
                markerPool.SpawnMarker(_moveTarget);
            }
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

    private void UpdateAnimator(bool isWalking, bool isRunning, bool isJumping, bool isFalling)
    {
        if (!animator)
        {
            return;
        }

        animator.SetBool(walkingBoolName, isWalking);
        animator.SetBool(idleBoolName, !isWalking && !isRunning && !isJumping && !isFalling);
        if (!string.IsNullOrEmpty(runningBoolName))
        {
            animator.SetBool(runningBoolName, isRunning);
        }
        if (!string.IsNullOrEmpty(jumpingBoolName))
        {
            animator.SetBool(jumpingBoolName, isJumping);
        }
        if (!string.IsNullOrEmpty(fallingBoolName))
        {
            animator.SetBool(fallingBoolName, isFalling);
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!_characterController || !_characterController.enabled || wallContactLinger <= 0f)
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
        if (animator)
        {
            animator.SetBool(walkingBoolName, false);
            animator.SetBool(idleBoolName, true);
            if (!string.IsNullOrEmpty(runningBoolName))
            {
                animator.SetBool(runningBoolName, false);
            }
            if (!string.IsNullOrEmpty(jumpingBoolName))
            {
                animator.SetBool(jumpingBoolName, false);
            }
            if (!string.IsNullOrEmpty(fallingBoolName))
            {
                animator.SetBool(fallingBoolName, false);
            }
        }
        _hasMoveTarget = false;
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
    }
}
