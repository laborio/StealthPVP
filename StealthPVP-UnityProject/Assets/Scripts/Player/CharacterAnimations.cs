using UnityEngine;

/// <summary>
/// Owns all character-facing visual state such as animator parameters and FX triggers.
/// </summary>
[DisallowMultipleComponent]
public class CharacterAnimations : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField, Tooltip("Animator that drives VFX-only animations. Optional; falls back to movement animator if not set.")] private Animator vfxAnimator;
    [SerializeField] private string walkingBoolName = "isWalking";
    [SerializeField] private string idleBoolName = "isIdle";
    [SerializeField] private string runningBoolName = "isRunning";
    [SerializeField, Tooltip("Optional bool for backward run animation.")] private string runningBackwardBoolName = "isRunningBackward";
    [SerializeField, Tooltip("Optional bool for strafing animation.")] private string strafingBoolName = "isStrafing";
    [SerializeField, Tooltip("Optional float used by strafe blend tree (-1 left, 1 right).")] private string strafeDirectionFloatName = "strafeDirection";
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
    [Header("Upper Body Layer")]
    [SerializeField, Tooltip("If true, the upper-body layer weight is driven by attack state.")] private bool controlUpperBodyLayerWeight = true;
    [SerializeField, Tooltip("Animator layer name for upper-body attack animations.")] private string upperBodyLayerName = "Upper Body";
    [SerializeField, Range(0f, 1f), Tooltip("Layer weight when not attacking.")] private float upperBodyIdleWeight = 0f;
    [SerializeField, Range(0f, 1f), Tooltip("Layer weight while attacking.")] private float upperBodyAttackWeight = 1f;
    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private int _walkingBoolHash;
    private int _idleBoolHash;
    private int _runningBoolHash;
    private int _runningBackwardBoolHash;
    private int _strafingBoolHash;
    private int _strafeDirectionFloatHash;
    private int _jumpingBoolHash;
    private int _fallingBoolHash;
    private int _sittingBoolHash;
    private int _standToSitSpeedHash;
    private int _teleportedBoolHash;
    private int _attackTriggerHash;
    private int _attackTagHash;
    private int _hitTriggerHash;
    private int _upperBodyLayerIndex = -1;

    private void Awake()
    {
        ResolveAnimators();
        CacheHashes();
        ResolveUpperBodyLayer();
    }

    private void OnValidate()
    {
        walkAnimationBaseSpeed = Mathf.Max(0.01f, walkAnimationBaseSpeed);
        runAnimationBaseSpeed = Mathf.Max(0.01f, runAnimationBaseSpeed);
        jumpAnimationBaseSpeed = Mathf.Max(0.01f, jumpAnimationBaseSpeed);
        ResolveAnimators();
        CacheHashes();
        ResolveUpperBodyLayer();
    }

    public void ApplyLocomotion(CharacterLocomotionAnimationData data)
    {
        if (!animator)
        {
            return;
        }

        SetBool(animator, _walkingBoolHash, walkingBoolName, data.IsWalking);
        SetBool(animator, _runningBoolHash, runningBoolName, data.IsRunning);
        SetBool(animator, _runningBackwardBoolHash, runningBackwardBoolName, data.IsRunningBackward);
        SetBool(animator, _strafingBoolHash, strafingBoolName, data.IsStrafing);
        SetFloat(animator, _strafeDirectionFloatHash, strafeDirectionFloatName, data.StrafeDirection);
        SetBool(animator, _jumpingBoolHash, jumpingBoolName, data.IsJumping);
        SetBool(animator, _fallingBoolHash, fallingBoolName, data.IsFalling);

        bool isIdle = !data.IsWalking && !data.IsRunning && !data.IsRunningBackward && !data.IsStrafing && !data.IsJumping && !data.IsFalling;
        SetBool(animator, _idleBoolHash, idleBoolName, isIdle);

        float animatorSpeed = 1f;
        if (data.IsRunning || data.IsRunningBackward)
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

        bool isAttacking = !string.IsNullOrEmpty(attackStateTag)
            && IsAnimatorInAttackState(animator, attackStateTag, _attackTagHash);
        animator.speed = isAttacking ? 1f : animatorSpeed;
        UpdateUpperBodyLayerWeight();
    }

    public void SetSittingState(bool isSitting, float animationSpeed)
    {
        if (!animator || string.IsNullOrEmpty(sittingBoolName))
        {
            return;
        }

        SetBool(animator, _sittingBoolHash, sittingBoolName, isSitting);
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

        SetBool(animator, _walkingBoolHash, walkingBoolName, false);
        SetBool(animator, _runningBoolHash, runningBoolName, false);
        SetBool(animator, _runningBackwardBoolHash, runningBackwardBoolName, false);
        SetBool(animator, _strafingBoolHash, strafingBoolName, false);
        SetFloat(animator, _strafeDirectionFloatHash, strafeDirectionFloatName, 0f);
        SetBool(animator, _jumpingBoolHash, jumpingBoolName, false);
        SetBool(animator, _fallingBoolHash, fallingBoolName, false);
        SetBool(animator, _sittingBoolHash, sittingBoolName, false);
        SetBool(animator, _idleBoolHash, idleBoolName, true);
        SetTeleportedState(false);
        animator.speed = 1f;
    }

    public void SetTeleportedState(bool isPorted)
    {
        Animator targetAnimator = vfxAnimator ? vfxAnimator : animator;
        SetBool(targetAnimator, _teleportedBoolHash, teleportedBoolName, isPorted);
    }

    public void TriggerAttack(string overrideTrigger = null)
    {
        string triggerName = string.IsNullOrEmpty(overrideTrigger) ? attackTriggerName : overrideTrigger;
        int hash = HashOrZero(triggerName);
        TriggerOnAnimators(triggerName, hash);
        UpdateUpperBodyLayerWeight();
    }

    public void TriggerHit(string overrideTrigger = null)
    {
        string triggerName = string.IsNullOrEmpty(overrideTrigger) ? hitTriggerName : overrideTrigger;
        int hash = HashOrZero(triggerName);
        TriggerOnAnimators(triggerName, hash);
    }

    public bool IsInAttackState(string overrideTag = null)
    {
        string tag = string.IsNullOrEmpty(overrideTag) ? attackStateTag : overrideTag;
        if (string.IsNullOrEmpty(tag))
        {
            return false;
        }

        int hash = string.IsNullOrEmpty(overrideTag) ? _attackTagHash : Animator.StringToHash(tag);
        if (IsAnimatorInAttackState(animator, tag, hash))
        {
            return true;
        }

        if (vfxAnimator && vfxAnimator != animator && IsAnimatorInAttackState(vfxAnimator, tag, hash))
        {
            return true;
        }

        return false;
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
        _runningBackwardBoolHash = HashOrZero(runningBackwardBoolName);
        _strafingBoolHash = HashOrZero(strafingBoolName);
        _strafeDirectionFloatHash = HashOrZero(strafeDirectionFloatName);
        _jumpingBoolHash = HashOrZero(jumpingBoolName);
        _fallingBoolHash = HashOrZero(fallingBoolName);
        _sittingBoolHash = HashOrZero(sittingBoolName);
        _standToSitSpeedHash = HashOrZero(standToSitSpeedFloatName);
        _teleportedBoolHash = HashOrZero(teleportedBoolName);
        _attackTriggerHash = HashOrZero(attackTriggerName);
        _attackTagHash = HashOrZero(attackStateTag);
        _hitTriggerHash = HashOrZero(hitTriggerName);
    }

    private void ResolveUpperBodyLayer()
    {
        _upperBodyLayerIndex = -1;
        if (!animator || string.IsNullOrEmpty(upperBodyLayerName))
        {
            return;
        }

        _upperBodyLayerIndex = animator.GetLayerIndex(upperBodyLayerName);
    }

    private void UpdateUpperBodyLayerWeight()
    {
        if (!controlUpperBodyLayerWeight || !animator)
        {
            return;
        }

        if (_upperBodyLayerIndex < 0)
        {
            ResolveUpperBodyLayer();
            if (_upperBodyLayerIndex < 0)
            {
                return;
            }
        }

        if (string.IsNullOrEmpty(attackStateTag))
        {
            animator.SetLayerWeight(_upperBodyLayerIndex, upperBodyIdleWeight);
            return;
        }

        bool isAttacking = IsAnimatorInAttackState(animator, attackStateTag, _attackTagHash);
        float targetWeight = isAttacking ? upperBodyAttackWeight : upperBodyIdleWeight;
        animator.SetLayerWeight(_upperBodyLayerIndex, targetWeight);
    }

    private void ResolveAnimators()
    {
        if (!animator)
        {
            animator = GetComponent<Animator>();
        }

        if (!animator)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (!vfxAnimator || vfxAnimator == animator)
        {
            vfxAnimator = FindVfxAnimator();
        }
    }

    private Animator FindVfxAnimator()
    {
        Transform meshTransform = transform.Find("Character_Mesh");
        if (meshTransform && meshTransform.TryGetComponent(out Animator meshAnimator) && meshAnimator != animator)
        {
            return meshAnimator;
        }

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator candidate = animators[i];
            if (candidate && candidate != animator)
            {
                return candidate;
            }
        }

        return null;
    }

    private static int HashOrZero(string parameterName)
    {
        return string.IsNullOrEmpty(parameterName) ? 0 : Animator.StringToHash(parameterName);
    }

    private void TriggerOnAnimators(string parameterName, int hash)
    {
        TriggerAnimator(animator, parameterName, hash, allowMissing: true);
        if (vfxAnimator && vfxAnimator != animator)
        {
            TriggerAnimator(vfxAnimator, parameterName, hash, allowMissing: false);
        }
    }

    private void TriggerAnimator(Animator targetAnimator, string parameterName, int hash, bool allowMissing)
    {
        if (!targetAnimator || string.IsNullOrEmpty(parameterName))
        {
            return;
        }

        bool exists = ParameterExists(targetAnimator, hash, parameterName, AnimatorControllerParameterType.Trigger);
        if (!exists && !allowMissing)
        {
            LogDebug($"Skipped trigger '{parameterName}' on {targetAnimator.name} (parameter missing)");
            return;
        }

        if (!exists && allowMissing)
        {
            LogDebug($"Triggering '{parameterName}' on {targetAnimator.name} without parameter lookup (allowMissing)");
        }

        if (hash != 0)
        {
            targetAnimator.ResetTrigger(hash);
            targetAnimator.SetTrigger(hash);
        }
        else
        {
            targetAnimator.ResetTrigger(parameterName);
            targetAnimator.SetTrigger(parameterName);
        }
    }

    private static bool ParameterExists(Animator animator, int hash, string name, AnimatorControllerParameterType type)
    {
        if (!animator)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter param = parameters[i];
            if (param.type != type)
            {
                continue;
            }

            if ((hash != 0 && param.nameHash == hash) || (!string.IsNullOrEmpty(name) && param.name == name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAnimatorInAttackState(Animator targetAnimator, string tag, int hash)
    {
        if (!targetAnimator)
        {
            return false;
        }

        int layers = targetAnimator.layerCount;
        for (int layerIndex = 0; layerIndex < layers; layerIndex++)
        {
            AnimatorStateInfo state = targetAnimator.GetCurrentAnimatorStateInfo(layerIndex);
            if (hash != 0 && state.tagHash == hash)
            {
                return true;
            }

            if (state.IsTag(tag))
            {
                return true;
            }
        }

        return false;
    }

    private void LogDebug(string message)
    {
        if (debugLogs)
        {
            Debug.Log($"[CharacterAnimations:{name}] {message}", this);
        }
    }

    private static void SetBool(Animator targetAnimator, int hash, string parameterName, bool value)
    {
        if (!targetAnimator)
        {
            return;
        }

        if (hash != 0)
        {
            targetAnimator.SetBool(hash, value);
        }
        else if (!string.IsNullOrEmpty(parameterName))
        {
            targetAnimator.SetBool(parameterName, value);
        }
    }

    private static void SetFloat(Animator targetAnimator, int hash, string parameterName, float value)
    {
        if (!targetAnimator)
        {
            return;
        }

        if (hash != 0)
        {
            targetAnimator.SetFloat(hash, value);
        }
        else if (!string.IsNullOrEmpty(parameterName))
        {
            targetAnimator.SetFloat(parameterName, value);
        }
    }
}

public struct CharacterLocomotionAnimationData
{
    public bool IsWalking;
    public bool IsRunning;
    public bool IsRunningBackward;
    public bool IsStrafing;
    public float StrafeDirection;
    public bool IsJumping;
    public bool IsFalling;
    public float PlanarSpeed;
    public float WalkSpeed;
    public float RunSpeed;
    public float JumpAnimationSpeed;
}
