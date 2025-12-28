using UnityEngine;

public partial class SimpleCharacterController
{
    private bool _teleportLocked;

    public bool IsTeleportLocked => _teleportLocked;

    public void BeginTeleportState()
    {
        if (_teleportLocked)
        {
            return;
        }

        CancelAttackCharge();
        _attackLockActive = false;
        _attackAimInProgress = false;
        _attackLockTimer = 0f;
        _teleportLocked = true;
        _hasMoveTarget = false;
        _currentPlanarVelocity = Vector3.zero;
        _verticalVelocity = 0f;
        _isJumping = false;
        _isFalling = false;
        _isDashing = false;
        _dashTimer = 0f;
        _dashCooldownTimer = 0f;
        CancelSeatingSequence();
        characterAnimations?.SetTeleportedState(true);
    }

    public void EndTeleportState()
    {
        if (!_teleportLocked)
        {
            return;
        }

        _teleportLocked = false;
        characterAnimations?.SetTeleportedState(false);
    }

    public void TeleportToPosition(Vector3 newPosition)
    {
        if (!_characterController)
        {
            transform.position = newPosition;
            return;
        }

        bool wasEnabled = _characterController.enabled;
        if (wasEnabled)
        {
            _characterController.enabled = false;
        }

        transform.position = newPosition;

        if (wasEnabled)
        {
            _characterController.enabled = true;
        }

        _currentPlanarVelocity = Vector3.zero;
        _verticalVelocity = 0f;
        _hasMoveTarget = false;
    }

    private void HandleTeleportLockUpdate(bool isGrounded)
    {
        UpdateActionHintDisplay(false, isGrounded);
        ProcessBenchCollisionRestore(Time.deltaTime);

        characterAnimations?.ApplyLocomotion(new CharacterLocomotionAnimationData
        {
            IsWalking = false,
            IsRunning = false,
            IsRunningBackward = false,
            IsStrafing = false,
            StrafeDirection = 0f,
            IsJumping = false,
            IsFalling = false,
            PlanarSpeed = 0f,
            WalkSpeed = moveSpeed,
            RunSpeed = moveSpeed * runMultiplier,
            JumpAnimationSpeed = 1f
        });
    }
}
