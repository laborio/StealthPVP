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

}
