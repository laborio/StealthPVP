using UnityEngine;

[CreateAssetMenu(fileName = "PlayerConfigSO", menuName = "Scriptable Objects/PlayerConfigSO")]
public class PlayerConfigSO : ScriptableObject
{
    [Header("Speed Values")]
    public float DefaultSpeed = 5f;
    public float WalkSpeed = 2.5f;
    public float Acceleration = 2f;
    public float Deceleration = 2f;
    public float RotationSpeed = 10f;

    public bool RotateTowardMovement = true;

    [Header("Air values")]
    public float Gravity = -9.81f;
    public float GroundedGravity = -2f;
    public float MaxFallSpeed = -8f;

    [Header("Ground Checking")]
    public float GroundCheckDistance = 0.2f;
    public LayerMask GroundMask = -1;

}
