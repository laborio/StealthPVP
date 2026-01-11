using UnityEngine;

/// <summary>
/// Handles all horizontal movement and rotation logic.
/// Supports camera-relative movement, air control, and speed modifiers.
/// </summary>
public class MovementModule : MonoBehaviour
{
    [SerializeField] private PlayerConfigSO _Config;
    [SerializeField] private CharacterController _CharacterController;
    [SerializeField] private GroundDetection _GroundDetection;
    [SerializeField] private Transform _PlayerRoot;
    [SerializeField] private Transform _CameraTransform;

    private Vector3 _cameraForwardAxis;
    private Vector3 _cameraRightAxis;
    private Vector3 _moveDirection;
    private Quaternion _targetRotation;
    private float _currentSpeed;

    public Vector3 MoveDirection => _moveDirection;
    public float CurrentSpeed => _currentSpeed;

    private void Awake()
    {
        if (_Config == null)
        {
            Debug.LogError($"error movement module config on {gameObject.name}");
            enabled = false;
            return;
        }

        if (_CharacterController == null)
        {
            Debug.LogError($"error movement module character controller on {gameObject.name}");
            enabled = false;
            return;
        }

        if (_GroundDetection == null)
        {
            Debug.LogError($"error movement module ground detection on {gameObject.name}");
            enabled = false;
            return;
        }

        if (_PlayerRoot == null)
        {
            Debug.LogError($"error movement module player root on {gameObject.name}");
            enabled = false;
            return;
        }

        if (_CameraTransform == null)
        {
            Debug.LogError($"error movement module camera on {gameObject.name}");
            enabled = false;
        }
    }

    /// <summary>
    /// Process movement input and move the character.
    /// </summary>
    /// <param name="input">Movement input (normalized)</param>
    /// <param name="isWalking">Is walk modifier active</param>
    /// <param name="verticalVelocity">Current vertical velocity vector</param>
    /// <param name="speedModifier">Additional speed multiplier (e.g., from landing)</param>
    /// <summary>
    /// Process movement input and move the character.
    /// </summary>
    /// <param name="input">Movement input (normalized)</param>
    /// <param name="isWalking">Is walk modifier active</param>
    /// <param name="verticalVelocity">Current vertical velocity vector</param>
    /// <param name="horizontalVelocity">External horizontal velocity (e.g., from wall jump)</param>
    /// <param name="speedModifier">Additional speed multiplier (e.g., from landing)</param>
    /// <param name="airControlModifier">Air control reduction (e.g., during wall jump)</param>
    public void ProcessMovement(Vector2 input, bool isWalking, Vector3 verticalVelocity, Vector3 horizontalVelocity, float speedModifier = 1f, float airControlModifier = 1f)
    {
        Vector3 horizontalMove = horizontalVelocity * Time.deltaTime;

        if (input.sqrMagnitude >= 0.01f)
        {
            CalculateCameraRelativeDirection(input);

            float totalSpeedModifier = CalculateSpeedModifier() * speedModifier;

            // Apply air control modifier
            if (!_GroundDetection.IsGrounded)
            {
                totalSpeedModifier *= airControlModifier;
            }

            _currentSpeed = isWalking ? _Config.WalkSpeed : _Config.DefaultSpeed;
            _currentSpeed *= totalSpeedModifier;

            horizontalMove += _moveDirection * _currentSpeed * Time.deltaTime;

            HandleRotation(_moveDirection, _Config.RotationSpeed);
        }
        else if (horizontalVelocity == Vector3.zero)
        {
            _currentSpeed = 0f;
        }

        Vector3 totalMove = horizontalMove + (verticalVelocity * Time.deltaTime);
        _CharacterController.Move(totalMove);
    }

    private void CalculateCameraRelativeDirection(Vector2 input)
    {
        _cameraForwardAxis = _CameraTransform.forward;
        _cameraRightAxis = _CameraTransform.right;

        _cameraForwardAxis.y = 0;
        _cameraRightAxis.y = 0;
        _cameraForwardAxis.Normalize();
        _cameraRightAxis.Normalize();

        _moveDirection = (_cameraForwardAxis * input.y + _cameraRightAxis * input.x).normalized;
    }

    private float CalculateSpeedModifier()
    {
        float modifier = 1f;

        if (!_GroundDetection.IsGrounded)
        {
            modifier *= _Config.AirControlMultiplier;
        }

        return modifier;
    }

    private void HandleRotation(Vector3 moveDir, float rotationSpeed)
    {
        if (moveDir.sqrMagnitude > 0.01f)
        {
            _targetRotation = Quaternion.LookRotation(moveDir);
            _PlayerRoot.rotation = Quaternion.Slerp(
                _PlayerRoot.rotation,
                _targetRotation,
                rotationSpeed * Time.deltaTime);
        }
    }
}