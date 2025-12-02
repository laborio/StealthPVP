using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : NetworkBehaviour
{
    [SerializeField] private CustomPlayerController _Player;
    [SerializeField] private PlayerInput _PlayerInput;

    private InputAction _moveAction;
    private InputAction _walkAction;
    private InputAction _jumpAction;

    private Vector2 _moveInput;
    private bool _isWalking;
    private bool _isJumping;

    public Vector2 MoveInput => _moveInput;
    public bool IsWalking => _isWalking;
    public bool IsJumping => _isJumping;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            if (_PlayerInput != null)
            {
                _PlayerInput.DeactivateInput();
            }
            enabled = false;
            return;
        }
    }

    private void Awake()
    {
        if (_PlayerInput == null)
        {
            return;
        }

        _moveAction = _PlayerInput.actions["Move"];
        _walkAction = _PlayerInput.actions["Walk"];
        _jumpAction = _PlayerInput.actions["Jump"];

        if (_moveAction == null || _walkAction == null || _jumpAction == null)
        {
            Debug.LogError("Actions aren't intialized correctly");
            return;
        }

        _moveAction.performed += OnMove;
        _moveAction.canceled += OnMove;

        _walkAction.performed += OnWalk;
        _walkAction.canceled += OnWalkCanceled;

        _jumpAction.performed += OnJump;
    }

    private void OnEnable()
    {
        if (IsSpawned && !IsOwner)
            return;

        _PlayerInput?.ActivateInput();
    }

    private void OnDisable()
    {
        _PlayerInput?.DeactivateInput();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (_moveAction != null)
        {
            _moveAction.performed -= OnMove;
            _moveAction.canceled -= OnMove;
        }

        if (_walkAction != null)
        {
            _walkAction.performed -= OnWalk;
            _walkAction.canceled -= OnWalkCanceled;
        }
    }

    public void OnMove(InputAction.CallbackContext p_context)
    {
        _moveInput = p_context.ReadValue<Vector2>();
    }

    public void OnWalk(InputAction.CallbackContext p_context)
    {
        _isWalking = true;
    }

    public void OnWalkCanceled(InputAction.CallbackContext p_context)
    {
        _isWalking = false;
    }

    public void OnJump(InputAction.CallbackContext p_context)
    {
        _isJumping = p_context.performed;
    }

    public void ResetJump()
    {
        _isJumping = false;
    }
}