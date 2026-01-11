using System;
using UnityEngine;

/// <summary>
/// Detects walls around the player for wall jump and wall slide mechanics.
/// Uses raycasts in multiple directions to find nearby walls.
/// </summary>
public class WallDetection : MonoBehaviour
{
    public event Action OnWallDetected;
    public event Action OnWallLeft;

    [SerializeField] private PlayerConfigSO _Config;
    [SerializeField] private Transform _PlayerRoot;
    [SerializeField] private LayerMask _WallLayer;

    private bool _isAgainstWall = false;
    private Vector3 _wallNormal = Vector3.zero;
    private float _wallAngle = 0f;
    private RaycastHit _wallHit;

    public bool IsAgainstWall => _isAgainstWall;
    public Vector3 WallNormal => _wallNormal;
    public float WallAngle => _wallAngle;
    public RaycastHit WallHit => _wallHit;

    private void Awake()
    {
        if (_Config == null)
        {
            Debug.LogError($"error wall detection config on {gameObject.name}");
            enabled = false;
            return;
        }

        if (_PlayerRoot == null)
        {
            Debug.LogError($"error wall detection player root on {gameObject.name}");
            enabled = false;
        }
    }

    /// <summary>
    /// Updates wall detection. Call this from Update().
    /// </summary>
    public void UpdateDetection()
    {
        bool wasAgainstWall = _isAgainstWall;
        _isAgainstWall = DetectWall(out _wallNormal, out _wallHit);

        if (_isAgainstWall)
        {
            _wallAngle = Vector3.Angle(Vector3.up, _wallNormal);
        }

        if (!wasAgainstWall && _isAgainstWall)
        {
            OnWallDetected?.Invoke();
        }
        else if (wasAgainstWall && !_isAgainstWall)
        {
            OnWallLeft?.Invoke();
        }
    }

    /// <summary>
    /// Check if player can wall jump based on wall angle and detection
    /// </summary>
    public bool CanWallJump()
    {
        if (!_isAgainstWall) return false;

        // Wall must be steep enough (not the ground)
        return _wallAngle >= _Config.WallJumpMinAngle && _wallAngle <= _Config.WallJumpMaxAngle;
    }

    private bool DetectWall(out Vector3 normal, out RaycastHit hit)
    {
        normal = Vector3.zero;
        hit = default;

        Vector3 origin = _PlayerRoot.position + Vector3.up * _Config.WallCheckHeight;
        float distance = _Config.WallCheckDistance;

        // Check forward
        if (Physics.Raycast(origin, _PlayerRoot.forward, out hit, distance, _WallLayer))
        {
            normal = hit.normal;
            return true;
        }

        // Check backward
        if (Physics.Raycast(origin, -_PlayerRoot.forward, out hit, distance, _WallLayer))
        {
            normal = hit.normal;
            return true;
        }

        // Check right
        if (Physics.Raycast(origin, _PlayerRoot.right, out hit, distance, _WallLayer))
        {
            normal = hit.normal;
            return true;
        }

        // Check left
        if (Physics.Raycast(origin, -_PlayerRoot.right, out hit, distance, _WallLayer))
        {
            normal = hit.normal;
            return true;
        }

        return false;
    }

    private void OnDrawGizmos()
    {
        if (_PlayerRoot == null || _Config == null) return;

        Vector3 origin = _PlayerRoot.position + Vector3.up * _Config.WallCheckHeight;
        float distance = _Config.WallCheckDistance;

        Gizmos.color = _isAgainstWall ? Color.green : Color.red;

        // Draw raycasts
        Gizmos.DrawRay(origin, _PlayerRoot.forward * distance);
        Gizmos.DrawRay(origin, -_PlayerRoot.forward * distance);
        Gizmos.DrawRay(origin, _PlayerRoot.right * distance);
        Gizmos.DrawRay(origin, -_PlayerRoot.right * distance);

        // Draw wall normal if detected
        if (_isAgainstWall)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(_wallHit.point, _wallNormal * 0.5f);
        }
    }
}