using UnityEngine;

/// <summary>
/// Encapsulates all player input polling, translating user interactions into high-level commands.
/// </summary>
public class PlayerInputRouter : MonoBehaviour
{
    [Header("Camera/Input Source")]
    [SerializeField, Tooltip("Optional camera to raycast from for click/aim. Defaults to Camera.main.")] private Camera inputCamera;
    [Header("Input Keys")]
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode stopKey = KeyCode.S;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode dashKey = KeyCode.R;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private string horizontalAxis = "Horizontal";
    [SerializeField] private string verticalAxis = "Vertical";
    [Header("Input State")]
    [SerializeField, Tooltip("If false, this router ignores all input.")] private bool inputEnabled = true;
    [Header("Keyboard Only Movement (optional)")]
    [SerializeField, Tooltip("If true, movement axis is built from keyboard keys only (ignores joystick axes).")] private bool keyboardOnlyMovement = false;
    [SerializeField] private KeyCode moveLeftKey = KeyCode.A;
    [SerializeField] private KeyCode moveRightKey = KeyCode.D;
    [SerializeField] private KeyCode moveUpKey = KeyCode.W;
    [SerializeField] private KeyCode moveDownKey = KeyCode.S;
    [Header("Attack")]
    [SerializeField, Tooltip("Maximum ray distance for attack aiming.")] private float attackRayDistance = 250f;
    [SerializeField, Tooltip("Layers considered for attack aim / range indicator.")] private LayerMask attackGroundMask = Physics.DefaultRaycastLayers;

    [Header("Click To Move")]
    [SerializeField] private float maximumRayDistance = 250f;
    [SerializeField] private LayerMask groundMask;

    /// <summary>
    /// Polls the underlying Unity input system and returns a snapshot for this frame.
    /// </summary>
    public virtual PlayerInputSnapshot PollInput()
    {
        if (!inputEnabled)
        {
            return default;
        }

        PlayerInputSnapshot snapshot = new PlayerInputSnapshot
        {
            RunHeld = Input.GetKey(runKey),
            StopPressed = Input.GetKeyDown(stopKey),
            JumpPressed = Input.GetKeyDown(jumpKey),
            DashPressed = Input.GetKeyDown(dashKey),
            InteractPressed = Input.GetKeyDown(interactKey),
            PrimaryPressed = Input.GetMouseButtonDown(0),
            PrimaryHeld = Input.GetMouseButton(0),
            PrimaryReleased = Input.GetMouseButtonUp(0),
            MoveAxis = keyboardOnlyMovement ? BuildKeyboardMoveAxis() : new Vector2(
                string.IsNullOrEmpty(horizontalAxis) ? 0f : Input.GetAxisRaw(horizontalAxis),
                string.IsNullOrEmpty(verticalAxis) ? 0f : Input.GetAxisRaw(verticalAxis))
        };

        if (Input.GetMouseButtonDown(1) && TryResolveMoveTarget(out Vector3 targetPosition))
        {
            snapshot.MoveIssued = true;
            snapshot.MoveTarget = targetPosition;
        }

        if (snapshot.PrimaryHeld || snapshot.PrimaryPressed || snapshot.PrimaryReleased)
        {
            if (TryResolveAimPoint(attackRayDistance, attackGroundMask, out Vector3 aimPoint))
            {
                snapshot.AimPoint = aimPoint;
                snapshot.HasAimPoint = true;
            }
        }

        return snapshot;
    }

    private bool TryResolveMoveTarget(out Vector3 target)
    {
        target = default;

        Camera currentCamera = ResolveCamera();
        if (!currentCamera)
        {
            Debug.LogWarning("PlayerInputRouter: No camera available for click-to-move.", this);
            return false;
        }

        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, maximumRayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            target = hitInfo.point;
            return true;
        }

        return false;
    }

    private bool TryResolveAimPoint(float distance, LayerMask mask, out Vector3 point)
    {
        point = default;

        Camera currentCamera = ResolveCamera();
        if (!currentCamera)
        {
            return false;
        }

        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, distance, mask, QueryTriggerInteraction.Ignore))
        {
            point = hitInfo.point;
            return true;
        }

        return false;
    }

    private void OnValidate()
    {
        maximumRayDistance = Mathf.Max(0f, maximumRayDistance);
        attackRayDistance = Mathf.Max(0f, attackRayDistance);
    }

    public Camera ResolveCamera()
    {
        if (inputCamera)
        {
            return inputCamera;
        }

        return Camera.main;
    }

    public void SetInputCamera(Camera camera)
    {
        inputCamera = camera;
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    protected bool IsInputEnabled => inputEnabled;

    public void SetAxes(string horizontal, string vertical)
    {
        horizontalAxis = horizontal;
        verticalAxis = vertical;
    }

    public void SetKeyboardOnlyMovement(bool value)
    {
        keyboardOnlyMovement = value;
    }

    private Vector2 BuildKeyboardMoveAxis()
    {
        float x = 0f;
        if (Input.GetKey(moveLeftKey))
        {
            x -= 1f;
        }
        if (Input.GetKey(moveRightKey))
        {
            x += 1f;
        }

        float y = 0f;
        if (Input.GetKey(moveDownKey))
        {
            y -= 1f;
        }
        if (Input.GetKey(moveUpKey))
        {
            y += 1f;
        }

        Vector2 axis = new Vector2(x, y);
        return axis.sqrMagnitude > 1f ? axis.normalized : axis;
    }
}

public struct PlayerInputSnapshot
{
    public bool RunHeld;
    public bool StopPressed;
    public bool JumpPressed;
    public bool DashPressed;
    public bool InteractPressed;
    public bool PrimaryPressed;
    public bool PrimaryHeld;
    public bool PrimaryReleased;
    public bool MoveIssued;
    public Vector2 MoveAxis;
    public Vector3 MoveTarget;
    public bool HasAimPoint;
    public Vector3 AimPoint;
}
