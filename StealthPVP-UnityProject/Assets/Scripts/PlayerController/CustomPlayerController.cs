using UnityEngine;
using UnityEngine.InputSystem;


public class CustomPlayerController : MonoBehaviour
{
    [SerializeField] private PlayerConfigSO _PlayerConfig;
    [SerializeField] private PlayerInputHandler _PlayerInputs;
    [SerializeField] private CharacterController _CharacterController;
    [SerializeField] private Transform _PlayerRoot;

    [SerializeField] private Transform _PlayerCam;

    private Vector3 _velocity,
        _cameraForwardAxis,
        _cameraRightAxis,
        _moveDirection;

    private Vector2 _input;

    private float _currentSpeed = 0f;

    private Quaternion _targetRotation = new();

    private void Update()
    {
        HandleMovement();
        HandleGravity();
    }

    private void HandleMovement()
    {
        _input = _PlayerInputs.MoveInput;

        if (_input.sqrMagnitude < .01f)
            return;

        //mouvements
        _cameraForwardAxis = _PlayerCam.forward;
        _cameraRightAxis = _PlayerCam.right;

        _cameraForwardAxis.y = 0;
        _cameraRightAxis.y = 0;
        _cameraForwardAxis.Normalize();
        _cameraRightAxis.Normalize();

        _moveDirection = (_cameraForwardAxis * _input.y + _cameraRightAxis * _input.x).normalized;

        _currentSpeed = _PlayerInputs.IsWalking ? _PlayerConfig.WalkSpeed : _PlayerConfig.DefaultSpeed;

        _CharacterController.Move(_moveDirection * _currentSpeed * Time.deltaTime);

        //rotation
        if (_moveDirection.sqrMagnitude > .01f)
        {
            _targetRotation = Quaternion.LookRotation(_moveDirection);
            _PlayerRoot.rotation = Quaternion.Slerp(
                _PlayerRoot.rotation,
                _targetRotation,
                _PlayerConfig.RotationSpeed * Time.deltaTime);
        }
    }

    private void HandleGravity()
    {
        if (_CharacterController.isGrounded && _velocity.y < 0)
        {
            _velocity.y = _PlayerConfig.GroundedGravity;
        }

        _velocity.y += _PlayerConfig.Gravity * Time.deltaTime;
        _CharacterController.Move(_velocity * Time.deltaTime);
    }
}
