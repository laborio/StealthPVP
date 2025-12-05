using UnityEngine;

/// <summary>
/// Owns all character-facing visual state such as animator parameters and FX triggers.
/// </summary>
[DisallowMultipleComponent]
public class CharacterAnimations : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string walkingBoolName = "isWalking";
    [SerializeField] private string idleBoolName = "isIdle";
    [SerializeField] private string runningBoolName = "isRunning";
    [SerializeField] private string jumpingBoolName = "isJumping";
    [SerializeField] private string fallingBoolName = "isFalling";
    [SerializeField] private string sittingBoolName = "isSitting";
    [SerializeField] private string standToSitSpeedFloatName = "StandToSitSpeed";
    [SerializeField] private string teleportedBoolName = "isPorted";
    [SerializeField, Tooltip("Trigger parameter for basic attacks.")] private string attackTriggerName = "Attack";
    [SerializeField, Tooltip("Animator state tag used to detect active attack animations.")] private string attackStateTag = "Attack";
    [SerializeField, Tooltip("Trigger parameter for taking a hit.")] private string hitTriggerName = "isHit";

    [Header("Animation Speeds")]
    [SerializeField] private float walkAnimationBaseSpeed = 1f;
    [SerializeField] private float runAnimationBaseSpeed = 1f;
    [SerializeField] private float jumpAnimationBaseSpeed = 1f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem runParticleSystem;

    private int _walkingBoolHash;
    private int _idleBoolHash;
    private int _runningBoolHash;
    private int _jumpingBoolHash;
    private int _fallingBoolHash;
    private int _sittingBoolHash;
    private int _standToSitSpeedHash;
    private int _teleportedBoolHash;
    private int _attackTriggerHash;
    private int _attackTagHash;
    private int _hitTriggerHash;

    private void Awake()
    {
        ResolveAnimator();
        CacheHashes();
    }

    private void OnValidate()
    {
        walkAnimationBaseSpeed = Mathf.Max(0.01f, walkAnimationBaseSpeed);
        runAnimationBaseSpeed = Mathf.Max(0.01f, runAnimationBaseSpeed);
        jumpAnimationBaseSpeed = Mathf.Max(0.01f, jumpAnimationBaseSpeed);
        ResolveAnimator();
        CacheHashes();
    }

    public void ApplyLocomotion(CharacterLocomotionAnimationData data)
    {
        if (!animator)
        {
            return;
        }

        SetBool(_walkingBoolHash, walkingBoolName, data.IsWalking);
        SetBool(_runningBoolHash, runningBoolName, data.IsRunning);
        SetBool(_jumpingBoolHash, jumpingBoolName, data.IsJumping);
        SetBool(_fallingBoolHash, fallingBoolName, data.IsFalling);

        bool isIdle = !data.IsWalking && !data.IsRunning && !data.IsJumping && !data.IsFalling;
        SetBool(_idleBoolHash, idleBoolName, isIdle);

        float animatorSpeed = 1f;
        if (data.IsRunning)
        {
            float normalized = Mathf.Clamp(data.PlanarSpeed / Mathf.Max(0.001f, data.RunSpeed), 0f, 2f);
            animatorSpeed = runAnimationBaseSpeed * normalized;
            UpdateRunParticles(true);
        }
        else if (data.IsWalking)
        {
            float normalized = Mathf.Clamp(data.PlanarSpeed / Mathf.Max(0.001f, data.WalkSpeed), 0f, 2f);
            animatorSpeed = walkAnimationBaseSpeed * normalized;
            UpdateRunParticles(false);
        }
        else if (data.IsJumping)
        {
            float jumpSpeed = data.JumpAnimationSpeed > 0f ? data.JumpAnimationSpeed : jumpAnimationBaseSpeed;
            animatorSpeed = Mathf.Max(0.01f, jumpSpeed);
            UpdateRunParticles(false);
        }
        else
        {
            animatorSpeed = 1f;
            UpdateRunParticles(false);
        }

        animator.speed = animatorSpeed;
    }

    public void SetSittingState(bool isSitting, float animationSpeed)
    {
        if (!animator || string.IsNullOrEmpty(sittingBoolName))
        {
            return;
        }

        SetBool(_sittingBoolHash, sittingBoolName, isSitting);
        if (!string.IsNullOrEmpty(standToSitSpeedFloatName))
        {
            if (_standToSitSpeedHash == 0)
            {
                _standToSitSpeedHash = Animator.StringToHash(standToSitSpeedFloatName);
            }
            animator.SetFloat(_standToSitSpeedHash, animationSpeed);
        }
    }

    public void ResetStates()
    {
        UpdateRunParticles(false);
        if (!animator)
        {
            return;
        }

        SetBool(_walkingBoolHash, walkingBoolName, false);
        SetBool(_runningBoolHash, runningBoolName, false);
        SetBool(_jumpingBoolHash, jumpingBoolName, false);
        SetBool(_fallingBoolHash, fallingBoolName, false);
        SetBool(_sittingBoolHash, sittingBoolName, false);
        SetBool(_idleBoolHash, idleBoolName, true);
        SetTeleportedState(false);
        animator.speed = 1f;
    }

    public void SetTeleportedState(bool isPorted)
    {
        SetBool(_teleportedBoolHash, teleportedBoolName, isPorted);
    }

    public void TriggerAttack(string overrideTrigger = null)
    {
        if (!animator)
        {
            return;
        }

        int hash = _attackTriggerHash;
        if (!string.IsNullOrEmpty(overrideTrigger) && overrideTrigger != attackTriggerName)
        {
            hash = Animator.StringToHash(overrideTrigger);
        }
        else if (hash == 0 && !string.IsNullOrEmpty(attackTriggerName))
        {
            hash = _attackTriggerHash = Animator.StringToHash(attackTriggerName);
        }

        if (hash != 0)
        {
            animator.ResetTrigger(hash);
            animator.SetTrigger(hash);
        }
        else if (!string.IsNullOrEmpty(attackTriggerName))
        {
            animator.ResetTrigger(attackTriggerName);
            animator.SetTrigger(attackTriggerName);
        }
    }

    public void TriggerHit(string overrideTrigger = null)
    {
        if (!animator)
        {
            return;
        }

        int hash = _hitTriggerHash;
        if (!string.IsNullOrEmpty(overrideTrigger) && overrideTrigger != hitTriggerName)
        {
            hash = Animator.StringToHash(overrideTrigger);
        }
        else if (hash == 0 && !string.IsNullOrEmpty(hitTriggerName))
        {
            hash = _hitTriggerHash = Animator.StringToHash(hitTriggerName);
        }

        if (hash != 0)
        {
            animator.ResetTrigger(hash);
            animator.SetTrigger(hash);
            Debug.Log("HIT animator");
        }
        else if (!string.IsNullOrEmpty(hitTriggerName))
        {
            animator.ResetTrigger(hitTriggerName);
            animator.SetTrigger(hitTriggerName);
        }
    }

    public bool IsInAttackState(string overrideTag = null)
    {
        if (!animator)
        {
            return false;
        }

        string tag = string.IsNullOrEmpty(overrideTag) ? attackStateTag : overrideTag;
        if (string.IsNullOrEmpty(tag))
        {
            return false;
        }

        int hash = string.IsNullOrEmpty(overrideTag) ? _attackTagHash : Animator.StringToHash(tag);
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (hash != 0 && state.tagHash == hash)
        {
            return true;
        }

        return state.IsTag(tag);
    }

    private void UpdateRunParticles(bool shouldBeActive)
    {
        if (!runParticleSystem)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = runParticleSystem.emission;
        emission.enabled = shouldBeActive;

        if (shouldBeActive)
        {
            if (!runParticleSystem.isPlaying)
            {
                runParticleSystem.Play();
            }
        }
        else if (runParticleSystem.isPlaying)
        {
            runParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void CacheHashes()
    {
        _walkingBoolHash = HashOrZero(walkingBoolName);
        _idleBoolHash = HashOrZero(idleBoolName);
        _runningBoolHash = HashOrZero(runningBoolName);
        _jumpingBoolHash = HashOrZero(jumpingBoolName);
        _fallingBoolHash = HashOrZero(fallingBoolName);
        _sittingBoolHash = HashOrZero(sittingBoolName);
        _standToSitSpeedHash = HashOrZero(standToSitSpeedFloatName);
        _teleportedBoolHash = HashOrZero(teleportedBoolName);
        _attackTriggerHash = HashOrZero(attackTriggerName);
        _attackTagHash = HashOrZero(attackStateTag);
        _hitTriggerHash = HashOrZero(hitTriggerName);
    }

    private void ResolveAnimator()
    {
        if (!animator)
        {
            animator = GetComponent<Animator>();
        }

        if (!animator)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private static int HashOrZero(string parameterName)
    {
        return string.IsNullOrEmpty(parameterName) ? 0 : Animator.StringToHash(parameterName);
    }

    private void SetBool(int hash, string parameterName, bool value)
    {
        if (!animator)
        {
            return;
        }

        if (hash != 0)
        {
            animator.SetBool(hash, value);
        }
        else if (!string.IsNullOrEmpty(parameterName))
        {
            animator.SetBool(parameterName, value);
        }
    }
}

public struct CharacterLocomotionAnimationData
{
    public bool IsWalking;
    public bool IsRunning;
    public bool IsJumping;
    public bool IsFalling;
    public float PlanarSpeed;
    public float WalkSpeed;
    public float RunSpeed;
    public float JumpAnimationSpeed;
}
