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
    [SerializeField] private JumpModule _JumpModule;
    [SerializeField] private MovementModule _MovementModule;

    [Header("Camera")]
    [SerializeField] private GameObject _IsoPlayerCam;
    #endregion

    #region Private Variables
    private Vector3 _velocity;
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
        _JumpModule.UpdateJumpBuffer();
    }

    private void HandleInput()
    {
        if (_PlayerInputs.IsJumping)
        {
            _JumpModule.RegisterJumpInput();
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
        float effectiveGravity = _PlayerConfig.Gravity * gravityMultiplier;

        _velocity.y += effectiveGravity * Time.deltaTime;
    }
    #endregion

    #region Movement
    private void HandleMovement()
    {
        float speedModifier = _JumpModule.GetLandingSpeedMultiplier();

        _MovementModule.ProcessMovement(
            _PlayerInputs.MoveInput,
            _PlayerInputs.IsWalking,
            _velocity,
            speedModifier
        );
    }
    #endregion

    #region Debug
    private void OnGUI()
    {
        if (!IsOwner) return;

        GUILayout.BeginArea(new Rect(10, 10, 350, 300));
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
        GUILayout.Label($"Move Dir: {_MovementModule.MoveDirection}");
        GUILayout.EndArea();
    }
    #endregion
}