using UnityEngine;

[CreateAssetMenu(fileName = "PlayerConfigSO", menuName = "Scriptable Objects/PlayerConfigSO")]
public class PlayerConfigSO : ScriptableObject
{
    [Header("Speed Values")]
    public float DefaultSpeed = 5f;
    public float WalkSpeed = 2.5f;
    //public float Acceleration = 2f;
    //public float Deceleration = 2f;
    public float RotationSpeed = 10f;

    [Header("Air values")]
    public float Gravity = -9.81f;
    public float GroundedGravity = -2f;
    //public float MaxFallSpeed = -8f;

    [Header("Ground Checking")]
    public float GroundCheckDistance = 0.2f;
    public LayerMask GroundMask = -1;

    [Header("Jump Variables")]
    public float JumpHeight = 2f;
    public float JumpCooldown = 0.5f;

}
