using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private enum MovementMode
    {
        ClickToMove,
        Keyboard
    }

    [Header("General Settings")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float runMultiplier = 1.5f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private KeyCode toggleInputKey = KeyCode.F1;
    [SerializeField] private MovementMode startingMode = MovementMode.ClickToMove;
    [SerializeField] private KeyCode moveLeftKey = KeyCode.Q;
    [SerializeField] private KeyCode moveRightKey = KeyCode.D;
    [SerializeField] private KeyCode moveForwardKey = KeyCode.Z;
    [SerializeField] private KeyCode moveBackwardKey = KeyCode.S;
    [SerializeField] private KeyCode runKey = KeyCode.Space;

    [Header("Click To Move")]
    [SerializeField] private float stopDistance = 0.2f;
    [SerializeField] private float maximumRayDistance = 250f;
    [SerializeField] private ClickMoveMarkerPool moveMarkerPool;
    [SerializeField] private float markerHeight = 0.1f;

    private MovementMode _currentMode;
    private Vector3 _moveTarget;
    private bool _hasMoveTarget;
    private Vector3 _keyboardInput;
    private Vector3 _currentVelocity;

    private void Awake()
    {
        _currentMode = startingMode;
    }

    private void Update()
    {
        HandleModeToggle();

        if (_currentMode == MovementMode.ClickToMove)
        {
            HandleClickToMoveInput();
        }
        else
        {
            HandleKeyboardInput();
        }

        MovePlayer();
        HandleRotation();
    }

    private void MovePlayer()
    {
        Vector3 desiredVelocity = Vector3.zero;
        float currentSpeed = moveSpeed;

        if (Input.GetKey(runKey))
        {
            currentSpeed *= runMultiplier;
        }

        if (_currentMode == MovementMode.ClickToMove)
        {
            if (_hasMoveTarget)
            {
                Vector3 toTarget = _moveTarget - transform.position;
                toTarget.y = 0f;

                if (toTarget.magnitude <= stopDistance)
                {
                    _hasMoveTarget = false;
                }
                else
                {
                    desiredVelocity = toTarget.normalized * currentSpeed;
                }
            }
        }
        else
        {
            desiredVelocity = _keyboardInput.normalized * currentSpeed;
        }

        ApplyMovement(desiredVelocity);
    }

    private void HandleModeToggle()
    {
        if (Input.GetKeyDown(toggleInputKey))
        {
            _currentMode = _currentMode == MovementMode.ClickToMove
                ? MovementMode.Keyboard
                : MovementMode.ClickToMove;

            _hasMoveTarget = false;
            _keyboardInput = Vector3.zero;
            _currentVelocity = Vector3.zero;
        }
    }

    private void HandleClickToMoveInput()
    {
        if (!Input.GetMouseButtonDown(1))
        {
            return;
        }

        Camera currentCamera = Camera.main;
        if (currentCamera == null)
        {
            Debug.LogWarning("PlayerController: No camera tagged as MainCamera found for click-to-move.");
            return;
        }

        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, maximumRayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            _moveTarget = hitInfo.point;
            _hasMoveTarget = true;
            SpawnMoveMarker(hitInfo.point);
        }
    }

    private void HandleKeyboardInput()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(moveLeftKey))
        {
            horizontal -= 1f;
        }
        if (Input.GetKey(moveRightKey))
        {
            horizontal += 1f;
        }
        if (Input.GetKey(moveForwardKey))
        {
            vertical += 1f;
        }
        if (Input.GetKey(moveBackwardKey))
        {
            vertical -= 1f;
        }

        Vector3 planarInput = new Vector3(horizontal, 0f, vertical);
        _keyboardInput = planarInput.magnitude > 1f ? planarInput.normalized : planarInput;
    }

    private void ApplyMovement(Vector3 desiredVelocity)
    {
        desiredVelocity.y = 0f;
        _currentVelocity = Vector3.MoveTowards(_currentVelocity, desiredVelocity, acceleration * Time.deltaTime);

        Vector3 displacement = _currentVelocity * Time.deltaTime;
        transform.position += displacement;

        if (_hasMoveTarget)
        {
            Vector3 toTarget = _moveTarget - transform.position;
            toTarget.y = 0f;
            if (toTarget.magnitude <= stopDistance)
            {
                _hasMoveTarget = false;
                _currentVelocity = Vector3.zero;
            }
        }
    }

    private void HandleRotation()
    {
        Vector3 planarVelocity = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);
        if (planarVelocity.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
        Quaternion newRotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        transform.rotation = newRotation;
    }

    private void SpawnMoveMarker(Vector3 position)
    {
        if (moveMarkerPool == null)
        {
            return;
        }

        Vector3 markerPosition = position;
        markerPosition.y = markerHeight;
        moveMarkerPool.SpawnMarker(markerPosition);
    }
}
