using System;
using UnityEngine;

/// <summary>
/// Handles ground detection logic independently from movement controller.
/// Can be reused by multiple systems (jump, landing effects, abilities, etc.)
/// </summary>
public class GroundDetection : MonoBehaviour
{
    public event Action OnGrounded;
    public event Action OnLeftGround;

    [SerializeField] private CharacterController _CharacterController;
    private float _lastGroundedTime = -999f;
    private bool _wasGroundedLastFrame = false;

    public bool IsGrounded => _CharacterController.isGrounded;
    public float TimeSinceGrounded => Time.time - _lastGroundedTime;

    private void Awake()
    {
        if (_CharacterController == null)
        {
            Debug.LogError($"error ground detection on {gameObject.name}");
            enabled = false;
        }
    }

    public void UpdateDetection()
    {
        bool isGroundedNow = _CharacterController.isGrounded;

        if (isGroundedNow)
        {
            _lastGroundedTime = Time.time;
        }

        if (!_wasGroundedLastFrame && isGroundedNow)
        {
            OnGrounded?.Invoke();
        }
        else if (_wasGroundedLastFrame && !isGroundedNow)
        {
            OnLeftGround?.Invoke();
        }

        _wasGroundedLastFrame = isGroundedNow;
    }

    public bool IsInCoyoteTime(float coyoteTimeDuration)
    {
        return !IsGrounded && TimeSinceGrounded <= coyoteTimeDuration;
    }
}