using UnityEngine;

/// <summary>
/// Encapsulates all player input polling, translating user interactions into high-level commands.
/// </summary>
public class PlayerInputRouter : MonoBehaviour
{
    [Header("Input Keys")]
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode stopKey = KeyCode.S;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode dashKey = KeyCode.R;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private string horizontalAxis = "Horizontal";
    [SerializeField] private string verticalAxis = "Vertical";

    [Header("Click To Move")]
    [SerializeField] private float maximumRayDistance = 250f;
    [SerializeField] private LayerMask groundMask;

    /// <summary>
    /// Polls the underlying Unity input system and returns a snapshot for this frame.
    /// </summary>
    public PlayerInputSnapshot PollInput()
    {
        PlayerInputSnapshot snapshot = new PlayerInputSnapshot
        {
            RunHeld = Input.GetKey(runKey),
            StopPressed = Input.GetKeyDown(stopKey),
            JumpPressed = Input.GetKeyDown(jumpKey),
            DashPressed = Input.GetKeyDown(dashKey),
            InteractPressed = Input.GetKeyDown(interactKey),
            MoveAxis = new Vector2(
                string.IsNullOrEmpty(horizontalAxis) ? 0f : Input.GetAxisRaw(horizontalAxis),
                string.IsNullOrEmpty(verticalAxis) ? 0f : Input.GetAxisRaw(verticalAxis))
        };

        if (Input.GetMouseButtonDown(1) && TryResolveMoveTarget(out Vector3 targetPosition))
        {
            snapshot.MoveIssued = true;
            snapshot.MoveTarget = targetPosition;
        }

        return snapshot;
    }

    private bool TryResolveMoveTarget(out Vector3 target)
    {
        target = default;

        Camera currentCamera = Camera.main;
        if (!currentCamera)
        {
            Debug.LogWarning("PlayerInputRouter: No camera tagged as MainCamera found for click-to-move.", this);
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

    private void OnValidate()
    {
        maximumRayDistance = Mathf.Max(0f, maximumRayDistance);
    }
}

public struct PlayerInputSnapshot
{
    public bool RunHeld;
    public bool StopPressed;
    public bool JumpPressed;
    public bool DashPressed;
    public bool InteractPressed;
    public bool MoveIssued;
    public Vector2 MoveAxis;
    public Vector3 MoveTarget;
}
