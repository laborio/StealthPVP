using Unity.Netcode;
using UnityEngine;

public class CustomPlayerController : NetworkBehaviour
{
    #region Serialized Fields
    [Header("Player Components")]
    [SerializeField] private PlayerConfigSO _PlayerConfig;
    [SerializeField] private PlayerInputHandler _PlayerInputs;
    [SerializeField] private CharacterController _CharacterController;
    [SerializeField] private GroundDetection _GroundDetection;
    [SerializeField] private GameObject _IsoPlayerCam;

    [Header("Transforms")]
    [SerializeField] private Transform _PlayerRoot;
    [SerializeField] private Transform _PlayerCam;
    #endregion

    #region Private Variables
    private Vector3 _velocity;
    private Vector3 _cameraForwardAxis;
    private Vector3 _cameraRightAxis;
    private Vector3 _moveDirection;
    private Quaternion _targetRotation = new();
    private float _currentSpeed = 0f;
    private Vector2 _input;

    // Jump state
    private float _lastJumpTime = -999f;
    private float _jumpBufferTimer = 0f;
    private bool _jumpConsumed = false;

    // Landing state
    private bool _isLanding = false;
    private float _landingStartTime = 0f;

    private enum JumpPhase
    {
        Grounded,
        Rising,
        Apex,
        Falling
    }

    private JumpPhase _currentJumpPhase = JumpPhase.Grounded;
    #endregion

    #region Network Lifecycle
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            _PlayerCam.gameObject.SetActive(false);
            _IsoPlayerCam.gameObject.SetActive(false);
            _CharacterController.enabled = false;
            enabled = false;
            return;
        }

        InitializeGroundDetection();
    }
    #endregion

    #region Initialization
    private void Awake()
    {
        if (_GroundDetection == null)
        {
            _GroundDetection = GetComponent<GroundDetection>();
            if (_GroundDetection == null)
            {
                _GroundDetection = gameObject.AddComponent<GroundDetection>();
            }
        }
    }

    private void InitializeGroundDetection()
    {
        _GroundDetection.OnGrounded += HandleLanding;
        _GroundDetection.OnLeftGround += HandleLeftGround;
    }

    private void OnDestroy()
    {
        if (_GroundDetection != null)
        {
            _GroundDetection.OnGrounded -= HandleLanding;
            _GroundDetection.OnLeftGround -= HandleLeftGround;
        }
    }
    #endregion

    #region Update Loop
    private void Update()
    {
        if (!IsOwner) return;

        _GroundDetection.UpdateDetection();

        HandleJumpInput();
        HandleJump();
        HandleGravity();
        HandleMovement();

        if (_jumpBufferTimer > 0)
        {
            _jumpBufferTimer -= Time.deltaTime;
        }
    }
    #endregion

    #region Ground Event Handlers
    private void HandleLanding()
    {
        _isLanding = true;
        _landingStartTime = Time.time;
        _currentJumpPhase = JumpPhase.Grounded;
        _jumpConsumed = false;
    }

    private void HandleLeftGround()
    {
        // Can add logic here if needed (e.g., play jump animation)
    }
    #endregion

    #region Jump System
    private void HandleJumpInput()
    {
        if (_PlayerInputs.IsJumping)
        {
            if (_jumpBufferTimer <= 0 && (_GroundDetection.IsGrounded || _GroundDetection.IsInCoyoteTime(_PlayerConfig.CoyoteTime)))
            {
                _jumpBufferTimer = _PlayerConfig.JumpBufferTime;
            }

            _PlayerInputs.ResetJump();
        }
    }

    private void HandleJump()
    {
        bool canJump = CanPerformJump();

        if (canJump && Time.time >= _lastJumpTime + _PlayerConfig.JumpCooldown)
        {
            PerformJump();
        }
    }

    private bool CanPerformJump()
    {
        bool jumpBuffered = _jumpBufferTimer > 0;
        bool onGroundOrCoyote = _GroundDetection.IsGrounded || _GroundDetection.IsInCoyoteTime(_PlayerConfig.CoyoteTime);

        return jumpBuffered && onGroundOrCoyote && !_jumpConsumed;
    }

    private void PerformJump()
    {
        float jumpVelocity = Mathf.Sqrt(_PlayerConfig.JumpHeight * -2f * _PlayerConfig.Gravity);
        _velocity.y = jumpVelocity;

        _lastJumpTime = Time.time;
        _currentJumpPhase = JumpPhase.Rising;
        _jumpConsumed = true;
        _jumpBufferTimer = 0f;
    }
    #endregion

    #region Physics & Movement
    private void HandleMovement()
    {
        _input = _PlayerInputs.MoveInput;

        Vector3 horizontalMove = Vector3.zero;

        if (_input.sqrMagnitude >= .01f)
        {
            _cameraForwardAxis = _PlayerCam.forward;
            _cameraRightAxis = _PlayerCam.right;

            _cameraForwardAxis.y = 0;
            _cameraRightAxis.y = 0;
            _cameraForwardAxis.Normalize();
            _cameraRightAxis.Normalize();

            _moveDirection = (_cameraForwardAxis * _input.y + _cameraRightAxis * _input.x).normalized;

            float speedMultiplier = 1f;

            if (!_GroundDetection.IsGrounded)
            {
                speedMultiplier *= _PlayerConfig.AirControlMultiplier;
            }

            if (_isLanding && Time.time - _landingStartTime < _PlayerConfig.LandingDuration)
            {
                speedMultiplier *= (1f - _PlayerConfig.LandingSpeedReduction);
            }
            else if (_isLanding)
            {
                _isLanding = false;
            }

            _currentSpeed = _PlayerInputs.IsWalking ? _PlayerConfig.WalkSpeed : _PlayerConfig.DefaultSpeed;
            _currentSpeed *= speedMultiplier;

            horizontalMove = _moveDirection * _currentSpeed * Time.deltaTime;

            HandleRotation(_moveDirection, _PlayerConfig.RotationSpeed);
        }

        Vector3 totalMove = horizontalMove + (_velocity * Time.deltaTime);
        _CharacterController.Move(totalMove);
    }

    private void HandleRotation(Vector3 moveDir, float rotationSpeed)
    {
        if (moveDir.sqrMagnitude > .01f)
        {
            _targetRotation = Quaternion.LookRotation(moveDir);
            _PlayerRoot.rotation = Quaternion.Slerp(
                _PlayerRoot.rotation,
                _targetRotation,
                rotationSpeed * Time.deltaTime);
        }
    }

    private void HandleGravity()
    {
        if (_GroundDetection.IsGrounded && _velocity.y < 0)
        {
            _velocity.y = _PlayerConfig.GroundedGravity;
        }

        float gravityMultiplier = GetGravityMultiplier();
        float effectiveGravity = _PlayerConfig.Gravity * gravityMultiplier;

        _velocity.y += effectiveGravity * Time.deltaTime;
    }

    private float GetGravityMultiplier()
    {
        if (_GroundDetection.IsGrounded)
        {
            return 1f;
        }

        if (_velocity.y > 0)
        {
            if (_velocity.y < _PlayerConfig.ApexThreshold)
            {
                _currentJumpPhase = JumpPhase.Apex;
                return _PlayerConfig.ApexGravityMultiplier;
            }
            else
            {
                _currentJumpPhase = JumpPhase.Rising;
                return _PlayerConfig.JumpRiseGravityMultiplier;
            }
        }
        else
        {
            _currentJumpPhase = JumpPhase.Falling;
            return _PlayerConfig.JumpFallGravityMultiplier;
        }
    }
    #endregion

    #region Debug
    private void OnGUI()
    {
        if (!IsOwner) return;

        GUILayout.BeginArea(new Rect(10, 10, 350, 280));
        GUILayout.Label("=== CLIENT-SIDE MOVEMENT ===");
        GUILayout.Label($"Jump Phase: {_currentJumpPhase}");
        GUILayout.Label($"Velocity Y: {_velocity.y:F2}");
        GUILayout.Label($"Grounded: {_GroundDetection.IsGrounded}");
        GUILayout.Label($"Coyote Time: {_GroundDetection.IsInCoyoteTime(_PlayerConfig.CoyoteTime)}");
        GUILayout.Label($"Jump Buffer: {_jumpBufferTimer:F2}s");
        GUILayout.Label($"Jump Consumed: {_jumpConsumed}");
        GUILayout.Label($"Landing: {_isLanding}");
        GUILayout.Label($"Gravity Mult: {GetGravityMultiplier():F2}x");
        GUILayout.EndArea();
    }
    #endregion
}