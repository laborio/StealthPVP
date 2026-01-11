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
    [SerializeField] private JumpModule _JumpModule;
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
        }
    }
    #endregion

    #region Update Loop
    private void Update()
    {
        if (!IsOwner) return;

        _GroundDetection.UpdateDetection();
        _JumpModule.UpdateJumpBuffer();

        HandleJumpInput();
        HandleJump();
        HandleGravity();
        HandleMovement();
    }
    #endregion

    #region Jump System
    private void HandleJumpInput()
    {
        if (_PlayerInputs.IsJumping)
        {
            _JumpModule.RegisterJumpInput();
            _PlayerInputs.ResetJump();
        }
    }

    private void HandleJump()
    {
        float jumpVelocity = _JumpModule.TryJump();

        if (jumpVelocity > 0)
        {
            _velocity.y = jumpVelocity;
        }
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

            speedMultiplier *= _JumpModule.GetLandingSpeedMultiplier();

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

        float gravityMultiplier = _JumpModule.GetGravityMultiplier(_velocity.y);
        float effectiveGravity = _PlayerConfig.Gravity * gravityMultiplier;

        _velocity.y += effectiveGravity * Time.deltaTime;
    }
    #endregion

    #region Debug
    private void OnGUI()
    {
        if (!IsOwner) return;

        GUILayout.BeginArea(new Rect(10, 10, 350, 280));
        GUILayout.Label("=== CLIENT-SIDE MOVEMENT ===");
        GUILayout.Label($"Jump Phase: {_JumpModule.CurrentPhase}");
        GUILayout.Label($"Velocity Y: {_velocity.y:F2}");
        GUILayout.Label($"Grounded: {_GroundDetection.IsGrounded}");
        GUILayout.Label($"Coyote Time: {_GroundDetection.IsInCoyoteTime(_PlayerConfig.CoyoteTime)}");
        GUILayout.Label($"Jump Buffer: {_JumpModule.JumpBufferTimer:F2}s");
        GUILayout.Label($"Jump Consumed: {_JumpModule.IsJumpConsumed}");
        GUILayout.Label($"Landing: {_JumpModule.IsLanding}");
        GUILayout.Label($"Gravity Mult: {_JumpModule.GetGravityMultiplier(_velocity.y):F2}x");
        GUILayout.EndArea();
    }
    #endregion
}