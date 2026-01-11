using UnityEngine;

[CreateAssetMenu(fileName = "PlayerConfigSO", menuName = "Scriptable Objects/PlayerConfigSO")]
public class PlayerConfigSO : ScriptableObject
{
    [Header("Speed Values")]
    public float DefaultSpeed = 5f;
    public float WalkSpeed = 2.5f;
    public float RotationSpeed = 10f;

    [Header("Air Control")]
    [Range(0f, 1f)]
    public float AirControlMultiplier = 0.75f;

    [Header("Air values")]
    public float Gravity = -9.81f;
    public float GroundedGravity = -2f;

    [Header("Ground Checking")]
    public float GroundCheckDistance = 0.2f;
    public LayerMask GroundMask = -1;

    [Header("Jump Variables")]
    public float JumpHeight = 2f;
    public float JumpCooldown = 0.5f;
    [Range(1f, 3f)]
    public float JumpRiseGravityMultiplier = 1f;
    [Range(0.1f, 1f)]
    public float ApexGravityMultiplier = 0.5f;
    [Range(1f, 3f)]
    public float JumpFallGravityMultiplier = 1.8f;
    [Range(0f, 5f)]
    public float ApexThreshold = 2f;
    [Range(0f, 0.3f)]
    public float CoyoteTime = 0.15f;
    [Range(0f, 0.3f)]
    public float JumpBufferTime = 0.2f;

    [Header("Landing")]
    [Range(0f, 0.5f)]
    public float LandingDuration = 0.1f;
    [Range(0f, 1f)]
    public float LandingSpeedReduction = 0.3f;

    [Header("Wall Detection")]
    public float WallCheckDistance = 0.6f;
    public float WallCheckHeight = 1f;
    public LayerMask WallLayer = -1;

    [Header("Wall Jump")]
    public float WallJumpHeight = 2.5f;
    public float WallJumpHorizontalForce = 8f;
    public float WallJumpUpwardForce = 10f;
    [Range(0f, 90f)]
    public float WallJumpMinAngle = 60f;
    [Range(0f, 90f)]
    public float WallJumpMaxAngle = 90f;
    public float WallJumpCooldown = 0.2f;
    [Range(0f, 0.3f)]
    public float WallJumpBufferTime = 0.15f;
    [Range(0f, 0.5f)]
    public float WallJumpInputLockTime = 0.2f;
    [Range(0f, 1f)]
    public float WallJumpAirControlMultiplier = 0.3f;

    [Header("Wall Slide")]
    public bool EnableWallSlide = true;
    public float WallSlideSpeed = 2f;
    public float WallSlideGravityMultiplier = 0.3f;
    public float MaxWallSlideTime = 2f;
}