using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;


public class CustomPlayerController : NetworkBehaviour
{
    #region SRs
    [Header("Player variables")]
    [SerializeField] private PlayerConfigSO _PlayerConfig;
    [SerializeField] private PlayerInputHandler _PlayerInputs;
    [SerializeField] private CharacterController _CharacterController;
    [SerializeField] private GameObject _IsoPlayerCam;

    [Header("Transforms")]
    [SerializeField] private Transform _PlayerRoot;
    [SerializeField] private Transform _PlayerCam;
    #endregion

    #region Private var

    private Vector3 _velocity,
        _cameraForwardAxis,
        _cameraRightAxis,
        _moveDirection;

    private Quaternion _targetRotation = new();
    private float _currentSpeed = 0f;

    private Vector2 _input;

    //jump
    private float _lastJumpTime = -999f;
    private float _lastGroundedTime = -999f;
    private float _jumpBufferTimer = 0f;
    private bool _wasGroundedLastFrame = false;
    private bool _isLanding = false;
    private float _landingStartTime = 0f;
    private bool _jumpConsumed = false;

    private enum JumpPhase
    {
        Grounded,
        Rising,
        Apex,
        Falling
    }

    private JumpPhase _currentJumpPhase = JumpPhase.Grounded;

    #endregion

    #region Mono

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            _PlayerCam.gameObject.SetActive(false);
            _IsoPlayerCam.gameObject.SetActive(false);

            _CharacterController.enabled = false;
            enabled = false;
        }
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        HandleGroundDetection();
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

    #region Ground Detection

    private void HandleGroundDetection()
    {
        bool isGroundedNow = _CharacterController.isGrounded;

        if (isGroundedNow)
        {
            _lastGroundedTime = Time.time;
            _currentJumpPhase = JumpPhase.Grounded;
            _jumpConsumed = false;
        }

        if (!_wasGroundedLastFrame && isGroundedNow)
        {
            OnLanded();
        }

        _wasGroundedLastFrame = isGroundedNow;
    }

    private void OnLanded()
    {
        _isLanding = true;
        _landingStartTime = Time.time;
        _currentJumpPhase = JumpPhase.Grounded;
    }

    private bool IsInCoyoteTime()
    {
        return !_CharacterController.isGrounded && Time.time - _lastGroundedTime <= _PlayerConfig.CoyoteTime;
    }

    #endregion

    #region Jump Input Handling

    private void HandleJumpInput()
    {
        if (_PlayerInputs.IsJumping)
        {
            if (_jumpBufferTimer <= 0 && (_CharacterController.isGrounded || IsInCoyoteTime()))
            {
                _jumpBufferTimer = _PlayerConfig.JumpBufferTime;
            }

            _PlayerInputs.ResetJump();
        }
    }

    #endregion

    #region Physics
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

            if (!_CharacterController.isGrounded)
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


    private void HandleRotation(Vector3 p_moveDir, float p_rotationSpeed)
    {
        if (p_moveDir.sqrMagnitude > .01f)
        {
            _targetRotation = Quaternion.LookRotation(p_moveDir);
            _PlayerRoot.rotation = Quaternion.Slerp(
                _PlayerRoot.rotation,
                _targetRotation,
                p_rotationSpeed * Time.deltaTime);
        }
    }

    private void HandleGravity()
    {
        if (_CharacterController.isGrounded && _velocity.y < 0)
        {
            _velocity.y = _PlayerConfig.GroundedGravity;
        }

        float gravityMultiplier = GetGravityMultiplier();
        float effectiveGravity = _PlayerConfig.Gravity * gravityMultiplier;

        _velocity.y += effectiveGravity * Time.deltaTime;
    }

    private float GetGravityMultiplier()
    {
        if (_CharacterController.isGrounded)
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
        bool onGroundOrCoyote = _CharacterController.isGrounded || IsInCoyoteTime();

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

    #region Debug (Optional)

    private void OnGUI()
    {
        if (!IsOwner) return;

        GUILayout.BeginArea(new Rect(10, 10, 350, 280));
        GUILayout.Label($"=== CLIENT-SIDE MOVEMENT ===");
        GUILayout.Label($"Jump Phase: {_currentJumpPhase}");
        GUILayout.Label($"Velocity Y: {_velocity.y:F2}");
        GUILayout.Label($"Grounded: {_CharacterController.isGrounded}");
        GUILayout.Label($"Coyote Time: {IsInCoyoteTime()}");
        GUILayout.Label($"Jump Buffer: {_jumpBufferTimer:F2}s");
        GUILayout.Label($"Jump Consumed: {_jumpConsumed}");
        GUILayout.Label($"Landing: {_isLanding}");
        GUILayout.Label($"Gravity Mult: {GetGravityMultiplier():F2}x");
        GUILayout.EndArea();
    }

    #endregion
}