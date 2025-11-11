using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] private CustomPlayerController _Player;
    [SerializeField] private PlayerInput _PlayerInput;

    private InputAction _moveAction;
    private InputAction _walkAction;

    private Vector2 _moveInput;
    private bool _isWalking;

    public Vector2 MoveInput => _moveInput;
    public bool IsWalking => _isWalking;

    private void Awake()
    {
        if (_PlayerInput == null)
        {
            return;
        }

        _moveAction = _PlayerInput.actions["Move"];
        _walkAction = _PlayerInput.actions["Walk"];

        if (_moveAction == null || _walkAction == null)
        {
            Debug.LogError("Actions aren't intialized correctly");
            return;
        }

        _moveAction.performed += OnMove;
        _moveAction.canceled += OnMove;

        _walkAction.performed += OnWalk;
        _walkAction.canceled += OnWalkCanceled;
    }

    private void OnEnable()
    {
        _PlayerInput?.ActivateInput();
    }

    private void OnDisable()
    {
        _PlayerInput?.DeactivateInput();
    }

    private void OnDestroy()
    {
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
}