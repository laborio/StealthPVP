using System;
using UnityEngine;

/// <summary>
/// Handles wall jump and wall slide mechanics.
/// Allows player to jump off walls with directional force.
/// </summary>
public class WallJumpModule : MonoBehaviour
{
    public event Action OnWallJumpPerformed;
    public event Action OnWallSlideStarted;
    public event Action OnWallSlideEnded;

    [SerializeField] private PlayerConfigSO _Config;
    [SerializeField] private GroundDetection _GroundDetection;
    [SerializeField] private WallDetection _WallDetection;

    private float _lastWallJumpTime = -999f;
    private float _wallJumpBufferTimer = 0f;
    private bool _wallJumpConsumed = false;
    private bool _isWallSliding = false;
    private float _wallSlideStartTime = 0f;
    private Vector3 _wallJumpDirection = Vector3.zero;

    public bool IsWallSliding => _isWallSliding;
    public float WallJumpBufferTimer => _wallJumpBufferTimer;
    public bool IsWallJumpConsumed => _wallJumpConsumed;
    public float TimeSinceWallJump => Time.time - _lastWallJumpTime;
    public Vector3 WallJumpDirection => _wallJumpDirection;

    private void Awake()
    {
        if (_Config == null)
        {
            Debug.LogError($"error wall jump module config on {gameObject.name}");
            enabled = false;
            return;
        }

        if (_GroundDetection == null)
        {
            Debug.LogError($"error wall jump module ground detection on {gameObject.name}");
            enabled = false;
            return;
        }

        if (_WallDetection == null)
        {
            Debug.LogError($"error wall jump module wall detection on {gameObject.name}");
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_GroundDetection != null)
        {
            _GroundDetection.OnGrounded += HandleGrounded;
        }

        if (_WallDetection != null)
        {
            _WallDetection.OnWallDetected += HandleWallDetected;
            _WallDetection.OnWallLeft += HandleWallLeft;
        }
    }

    private void OnDisable()
    {
        if (_GroundDetection != null)
        {
            _GroundDetection.OnGrounded -= HandleGrounded;
        }

        if (_WallDetection != null)
        {
            _WallDetection.OnWallDetected -= HandleWallDetected;
            _WallDetection.OnWallLeft -= HandleWallLeft;
        }
    }

    /// <summary>
    /// Update wall jump state. Call from Update().
    /// </summary>
    public void UpdateWallJump()
    {
        if (_wallJumpBufferTimer > 0)
        {
            _wallJumpBufferTimer -= Time.deltaTime;
        }

        UpdateWallSlide();
    }

    /// <summary>
    /// Register jump input for wall jump buffering
    /// </summary>
    public void RegisterWallJumpInput()
    {
        if (_wallJumpBufferTimer <= 0 && CanStartWallJump())
        {
            _wallJumpBufferTimer = _Config.WallJumpBufferTime;
        }
    }

    /// <summary>
    /// Attempt wall jump. Returns velocity if successful, Vector3.zero otherwise.
    /// </summary>
    public Vector3 TryWallJump()
    {
        if (!CanPerformWallJump())
        {
            return Vector3.zero;
        }

        if (Time.time < _lastWallJumpTime + _Config.WallJumpCooldown)
        {
            return Vector3.zero;
        }

        return PerformWallJump();
    }

    /// <summary>
    /// Check if player should be wall sliding
    /// </summary>
    public bool ShouldWallSlide(float verticalVelocity)
    {
        if (!_Config.EnableWallSlide) return false;
        if (_GroundDetection.IsGrounded) return false;
        if (!_WallDetection.CanWallJump()) return false;
        if (verticalVelocity > 0) return false; // Only slide when falling

        float wallSlideTime = Time.time - _wallSlideStartTime;
        if (_isWallSliding && wallSlideTime >= _Config.MaxWallSlideTime)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Get wall slide gravity multiplier
    /// </summary>
    public float GetWallSlideGravityMultiplier()
    {
        return _isWallSliding ? _Config.WallSlideGravityMultiplier : 1f;
    }

    /// <summary>
    /// Check if horizontal input should be ignored (just after wall jump)
    /// </summary>
    public bool ShouldLockHorizontalInput()
    {
        return TimeSinceWallJump < _Config.WallJumpInputLockTime;
    }

    private void UpdateWallSlide()
    {
        bool shouldSlide = ShouldWallSlide(0); // We'll pass proper velocity from controller

        if (!_isWallSliding && shouldSlide)
        {
            StartWallSlide();
        }
        else if (_isWallSliding && !shouldSlide)
        {
            StopWallSlide();
        }
    }

    private void StartWallSlide()
    {
        _isWallSliding = true;
        _wallSlideStartTime = Time.time;
        OnWallSlideStarted?.Invoke();
    }

    private void StopWallSlide()
    {
        _isWallSliding = false;
        OnWallSlideEnded?.Invoke();
    }

    private bool CanStartWallJump()
    {
        return !_GroundDetection.IsGrounded && _WallDetection.CanWallJump();
    }

    private bool CanPerformWallJump()
    {
        bool jumpBuffered = _wallJumpBufferTimer > 0;
        bool canWallJump = _WallDetection.CanWallJump();

        return jumpBuffered && canWallJump && !_wallJumpConsumed;
    }

    private Vector3 PerformWallJump()
    {
        Vector3 wallNormal = _WallDetection.WallNormal;

        // Calculate jump direction: away from wall
        _wallJumpDirection = wallNormal.normalized;
        _wallJumpDirection.y = 0;
        _wallJumpDirection.Normalize();

        Vector3 jumpVelocity = Vector3.zero;

        // Horizontal velocity away from wall
        jumpVelocity = _wallJumpDirection * _Config.WallJumpHorizontalForce;

        // Upward velocity
        jumpVelocity.y = _Config.WallJumpUpwardForce;

        _lastWallJumpTime = Time.time;
        _wallJumpConsumed = true;
        _wallJumpBufferTimer = 0f;

        StopWallSlide();

        OnWallJumpPerformed?.Invoke();

        return jumpVelocity;
    }

    /// <summary>
    /// Check if we should reduce air control (just after wall jump)
    /// </summary>
    public bool ShouldReduceAirControl()
    {
        return TimeSinceWallJump < _Config.WallJumpInputLockTime;
    }

    private void HandleGrounded()
    {
        _wallJumpConsumed = false;
        StopWallSlide();
    }

    private void HandleWallDetected()
    {
        _wallJumpConsumed = false;
    }

    private void HandleWallLeft()
    {
        StopWallSlide();
    }
}