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

    private NetworkVariable<Vector3> _networkPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Quaternion> _networkRotation = new NetworkVariable<Quaternion>();

    private float _lastJumpTime = -999f;

    #endregion

    #region Mono

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            _PlayerCam.gameObject.SetActive(false);
            _IsoPlayerCam.gameObject.SetActive(false);
            //enabled = false; à remettre si on fait client side
            //return;
        }

        else
        {
            enabled = true;
        }
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        HandleMovement();
        HandleJump();
        HandleGravity();
    }

    #endregion

    #region Physics
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

        MoveAndRotateServerRpc(_moveDirection, _currentSpeed); //à enelevr si on veut faire client side

        //_CharacterController.Move(_moveDirection * _PlayerConfig.RotationSpeed * Time.deltaTime); // à remettre si on veut faire client side
        //HandleRotation(_moveDirection, _PlayerConfig.RotationSpeed);

        //_networkPosition.Value = _moveDirection * _currentSpeed * Time.deltaTime;
        //_networkRotation.Value = _targetRotation;
    }


    private void HandleRotation(Vector3 p_moveDir, float p_rotationSpeed)
    {
        //rotation
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

        _velocity.y += _PlayerConfig.Gravity * Time.deltaTime;
        //_CharacterController.Move(_velocity * Time.deltaTime); //à remettre si on veut client side

        ApplyGravityServerRpc(_velocity); //à enlever si on veut client side
    }

    private void HandleJump()
    {
        if (_PlayerInputs.IsJumping && _CharacterController.isGrounded && Time.time >= _lastJumpTime + _PlayerConfig.JumpCooldown)
        {
            _velocity.y = Mathf.Sqrt(_PlayerConfig.JumpHeight * -2f * _PlayerConfig.Gravity);
            _lastJumpTime = Time.time;
            _PlayerInputs.ResetJump();
        }
    }


    #endregion 

    #region RPC

    [ServerRpc]
    private void MoveAndRotateServerRpc(Vector3 p_moveDir, float p_speed)
    {
        _CharacterController.Move(p_moveDir * p_speed * Time.deltaTime);
        HandleRotation(p_moveDir, _PlayerConfig.RotationSpeed);
    }

    [ServerRpc]
    private void ApplyGravityServerRpc(Vector3 p_velocity)
    {
        _CharacterController.Move(_velocity * Time.deltaTime);
    }

    #endregion
}
