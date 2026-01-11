using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Orchestrates player movement modules for networked multiplayer.
/// Delegates logic to specialized modules for maintainability and extensibility.
/// </summary>
public class CustomPlayerController : NetworkBehaviour
{
    #region Serialized Fields
    [Header("Configuration")]
    [SerializeField] private PlayerConfigSO _PlayerConfig;

    [Header("Input")]
    [SerializeField] private PlayerInputHandler _PlayerInputs;

    [Header("Modules")]
    [SerializeField] private GroundDetection _GroundDetection;
    [SerializeField] private WallDetection _WallDetection;
    [SerializeField] private JumpModule _JumpModule;
    [SerializeField] private WallJumpModule _WallJumpModule;
    [SerializeField] private MovementModule _MovementModule;

    [Header("Camera")]
    [SerializeField] private GameObject _IsoPlayerCam;
    #endregion

    #region Private Variables
    private Vector3 _velocity;
    private Vector3 _horizontalVelocity;
    #endregion

    #region Network Lifecycle
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            _IsoPlayerCam.gameObject.SetActive(false);
            enabled = false;
        }
    }
    #endregion

    #region Update Loop
    private void Update()
    {
        if (!IsOwner) return;

        UpdateModules();
        HandleInput();
        HandlePhysics();
    }

    private void UpdateModules()
    {
        _GroundDetection.UpdateDetection();
        _WallDetection.UpdateDetection();
        _JumpModule.UpdateJumpBuffer();
        _WallJumpModule.UpdateWallJump();
    }

    private void HandleInput()
    {
        if (_PlayerInputs.IsJumping)
        {
            // Priority: wall jump > normal jump
            if (_WallDetection.IsAgainstWall && !_GroundDetection.IsGrounded)
            {
                _WallJumpModule.RegisterWallJumpInput();
            }
            else
            {
                _JumpModule.RegisterJumpInput();
            }

            _PlayerInputs.ResetJump();
        }
    }

    private void HandlePhysics()
    {
        HandleJump();
        HandleGravity();
        HandleMovement();
    }
    #endregion

    #region Jump
    private void HandleJump()
    {
        // Try wall jump first
        Vector3 wallJumpVelocity = _WallJumpModule.TryWallJump();

        if (wallJumpVelocity != Vector3.zero)
        {
            // Wall jump: set both vertical and horizontal velocity
            _velocity.y = wallJumpVelocity.y;
            _horizontalVelocity = new Vector3(wallJumpVelocity.x, 0, wallJumpVelocity.z);
            return;
        }

        // Try normal jump
        float jumpVelocity = _JumpModule.TryJump();

        if (jumpVelocity > 0)
        {
            _velocity.y = jumpVelocity;
        }
    }
    #endregion

    #region Gravity
    private void HandleGravity()
    {
        if (_GroundDetection.IsGrounded && _velocity.y < 0)
        {
            _velocity.y = _PlayerConfig.GroundedGravity;
        }

        float gravityMultiplier = _JumpModule.GetGravityMultiplier(_velocity.y);

        // Apply wall slide gravity modifier
        if (_WallJumpModule.ShouldWallSlide(_velocity.y))
        {
            gravityMultiplier *= _WallJumpModule.GetWallSlideGravityMultiplier();

            // Clamp wall slide speed
            if (_velocity.y < -_PlayerConfig.WallSlideSpeed)
            {
                _velocity.y = -_PlayerConfig.WallSlideSpeed;
            }
        }

        float effectiveGravity = _PlayerConfig.Gravity * gravityMultiplier;
        _velocity.y += effectiveGravity * Time.deltaTime;
    }
    #endregion

    #region Movement
    private void HandleMovement()
    {
        float speedModifier = _JumpModule.GetLandingSpeedMultiplier();

        // Apply reduced air control during wall jump lock time
        float airControlModifier = 1f;
        if (_WallJumpModule.ShouldReduceAirControl())
        {
            airControlModifier = _PlayerConfig.WallJumpAirControlMultiplier;
        }

        _MovementModule.ProcessMovement(
            _PlayerInputs.MoveInput,
            _PlayerInputs.IsWalking,
            _velocity,
            _horizontalVelocity,
            speedModifier,
            airControlModifier
        );

        // Decay horizontal velocity from wall jump
        _horizontalVelocity = Vector3.Lerp(_horizontalVelocity, Vector3.zero, Time.deltaTime * 2f);
    }
    #endregion

    #region Debug
    private void OnGUI()
    {
        if (!IsOwner) return;

        GUILayout.BeginArea(new Rect(10, 10, 350, 360));
        GUILayout.Label("=== CLIENT-SIDE MOVEMENT ===");
        GUILayout.Label($"Jump Phase: {_JumpModule.CurrentPhase}");
        GUILayout.Label($"Velocity Y: {_velocity.y:F2}");
        GUILayout.Label($"Grounded: {_GroundDetection.IsGrounded}");
        GUILayout.Label($"Coyote Time: {_GroundDetection.IsInCoyoteTime(_PlayerConfig.CoyoteTime)}");
        GUILayout.Label($"Jump Buffer: {_JumpModule.JumpBufferTimer:F2}s");
        GUILayout.Label($"Jump Consumed: {_JumpModule.IsJumpConsumed}");
        GUILayout.Label($"Landing: {_JumpModule.IsLanding}");
        GUILayout.Label($"Gravity Mult: {_JumpModule.GetGravityMultiplier(_velocity.y):F2}x");
        GUILayout.Label($"Move Speed: {_MovementModule.CurrentSpeed:F2}");

        GUILayout.Space(10);
        GUILayout.Label("=== WALL JUMP ===");
        GUILayout.Label($"Against Wall: {_WallDetection.IsAgainstWall}");
        GUILayout.Label($"Can Wall Jump: {_WallDetection.CanWallJump()}");
        GUILayout.Label($"Wall Sliding: {_WallJumpModule.IsWallSliding}");
        GUILayout.Label($"Wall Jump Buffer: {_WallJumpModule.WallJumpBufferTimer:F2}s");
        GUILayout.Label($"Wall Normal: {_WallDetection.WallNormal}");
        GUILayout.EndArea();
    }
    #endregion
}