using System;
using UnityEngine;

/// <summary>
/// Handles all jump-related logic independently.
/// Supports jump buffering, coyote time, variable jump height, and landing states.
/// </summary>
public class JumpModule : MonoBehaviour
{
    public event Action OnJumpPerformed;
    public event Action OnLandingStarted;
    public event Action OnLandingEnded;

    [SerializeField] private PlayerConfigSO _config;
    [SerializeField] private GroundDetection _groundDetection;

    private float _lastJumpTime = -999f;
    private float _jumpBufferTimer = 0f;
    private bool _jumpConsumed = false;
    private bool _isLanding = false;
    private float _landingStartTime = 0f;

    public enum JumpPhase
    {
        Grounded,
        Rising,
        Apex,
        Falling
    }

    private JumpPhase _currentJumpPhase = JumpPhase.Grounded;

    public JumpPhase CurrentPhase => _currentJumpPhase;
    public bool IsJumpConsumed => _jumpConsumed;
    public bool IsLanding => _isLanding;
    public float JumpBufferTimer => _jumpBufferTimer;

    private void Awake()
    {
        if (_config == null)
        {
            Debug.LogError($"error jump module config on {gameObject.name}");
            enabled = false;
            return;
        }

        if (_groundDetection == null)
        {
            Debug.LogError($"error jump module ground detection on {gameObject.name}");
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_groundDetection != null)
        {
            _groundDetection.OnGrounded += HandleLanding;
        }
    }

    private void OnDisable()
    {
        if (_groundDetection != null)
        {
            _groundDetection.OnGrounded -= HandleLanding;
        }
    }

    /// <summary>
    /// Call this from Update() to handle jump buffering
    /// </summary>
    public void UpdateJumpBuffer()
    {
        if (_jumpBufferTimer > 0)
        {
            _jumpBufferTimer -= Time.deltaTime;
        }

        if (_isLanding && Time.time - _landingStartTime >= _config.LandingDuration)
        {
            _isLanding = false;
            OnLandingEnded?.Invoke();
        }
    }

    /// <summary>
    /// Call this when jump input is pressed
    /// </summary>
    public void RegisterJumpInput()
    {
        if (_jumpBufferTimer <= 0 && (_groundDetection.IsGrounded || _groundDetection.IsInCoyoteTime(_config.CoyoteTime)))
        {
            _jumpBufferTimer = _config.JumpBufferTime;
        }
    }

    /// <summary>
    /// Attempts to perform a jump. Returns the jump velocity if successful, 0 otherwise.
    /// </summary>
    public float TryJump()
    {
        if (!CanPerformJump())
        {
            return 0f;
        }

        if (Time.time < _lastJumpTime + _config.JumpCooldown)
        {
            return 0f;
        }

        return PerformJump();
    }

    /// <summary>
    /// Calculate gravity multiplier based on current jump phase
    /// </summary>
    public float GetGravityMultiplier(float currentVelocityY)
    {
        if (_groundDetection.IsGrounded)
        {
            _currentJumpPhase = JumpPhase.Grounded;
            return 1f;
        }

        if (currentVelocityY > 0)
        {
            if (currentVelocityY < _config.ApexThreshold)
            {
                _currentJumpPhase = JumpPhase.Apex;
                return _config.ApexGravityMultiplier;
            }
            else
            {
                _currentJumpPhase = JumpPhase.Rising;
                return _config.JumpRiseGravityMultiplier;
            }
        }
        else
        {
            _currentJumpPhase = JumpPhase.Falling;
            return _config.JumpFallGravityMultiplier;
        }
    }

    /// <summary>
    /// Get landing speed multiplier (returns 1.0 if not landing)
    /// </summary>
    public float GetLandingSpeedMultiplier()
    {
        if (!_isLanding)
        {
            return 1f;
        }

        if (Time.time - _landingStartTime < _config.LandingDuration)
        {
            return 1f - _config.LandingSpeedReduction;
        }

        return 1f;
    }

    private bool CanPerformJump()
    {
        bool jumpBuffered = _jumpBufferTimer > 0;
        bool onGroundOrCoyote = _groundDetection.IsGrounded || _groundDetection.IsInCoyoteTime(_config.CoyoteTime);

        return jumpBuffered && onGroundOrCoyote && !_jumpConsumed;
    }

    private float PerformJump()
    {
        float jumpVelocity = Mathf.Sqrt(_config.JumpHeight * -2f * _config.Gravity);

        _lastJumpTime = Time.time;
        _currentJumpPhase = JumpPhase.Rising;
        _jumpConsumed = true;
        _jumpBufferTimer = 0f;

        OnJumpPerformed?.Invoke();

        return jumpVelocity;
    }

    private void HandleLanding()
    {
        _isLanding = true;
        _landingStartTime = Time.time;
        _currentJumpPhase = JumpPhase.Grounded;
        _jumpConsumed = false;

        OnLandingStarted?.Invoke();
    }
}