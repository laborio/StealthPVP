using UnityEngine;

/// <summary>
/// Encapsulates all player input polling, translating user interactions into high-level commands.
/// </summary>
public class PlayerInputRouter : MonoBehaviour
{
    [Header("Input Keys")]
    [SerializeField] private KeyCode runKey = KeyCode.Space;
    [SerializeField] private KeyCode stopKey = KeyCode.S;
    [SerializeField] private KeyCode climbKey = KeyCode.T;

    [Header("Click To Move")]
    [SerializeField] private float maximumRayDistance = 250f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private ClickMoveMarkerPool moveMarkerPool;
    [SerializeField] private float markerHeight = 0.1f;

    /// <summary>
    /// Polls the underlying Unity input system and returns a snapshot for this frame.
    /// </summary>
    public PlayerInputSnapshot PollInput()
    {
        PlayerInputSnapshot snapshot = new PlayerInputSnapshot
        {
            RunHeld = Input.GetKey(runKey),
            StopPressed = Input.GetKeyDown(stopKey),
            ClimbPressed = Input.GetKeyDown(climbKey)
        };

        if (Input.GetMouseButtonDown(1) && TryResolveMoveTarget(out Vector3 targetPosition))
        {
            snapshot.MoveIssued = true;
            snapshot.MoveTarget = targetPosition;
            SpawnMoveMarker(targetPosition);
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

    private void SpawnMoveMarker(Vector3 position)
    {
        if (!moveMarkerPool)
        {
            return;
        }

        Vector3 markerPosition = position;
        markerPosition.y = markerHeight;
        moveMarkerPool.SpawnMarker(markerPosition);
    }

    private void OnValidate()
    {
        maximumRayDistance = Mathf.Max(0f, maximumRayDistance);
        markerHeight = Mathf.Max(0f, markerHeight);
    }
}

public struct PlayerInputSnapshot
{
    public bool RunHeld;
    public bool StopPressed;
    public bool ClimbPressed;
    public bool MoveIssued;
    public Vector3 MoveTarget;
}
