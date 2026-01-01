using UnityEngine;

/// <summary>
/// Triggers a stun attack animation toward the attacker when this NPC is damaged by a player.
/// </summary>
[DisallowMultipleComponent]
public class NpcRetaliateStun : MonoBehaviour
{
    [SerializeField, Tooltip("Health component to listen to. Defaults to a component on this object.")] private CharacterHealth health;
    [SerializeField, Tooltip("Character animations used to trigger the stun attack. Defaults to a child component.")] private CharacterAnimations characterAnimations;
    [SerializeField, Tooltip("Animator to drive upper-body layer weights. Defaults to a child animator.")] private Animator animator;
    [SerializeField, Tooltip("Trigger name for the stun attack animation.")] private string stunAttackTriggerName = "Stun";
    [SerializeField, Tooltip("Only retaliate when the instigator is a player.")] private bool playersOnly = true;
    [SerializeField, Tooltip("Rotate the NPC to face the attacker before triggering the animation.")] private bool faceAttacker = true;
    [SerializeField, Tooltip("Minimum seconds between retaliations.")] private float retaliationCooldown = 0.35f;
    [SerializeField, Tooltip("Skip retaliation if the NPC is already in an attack/stun animation.")] private bool skipIfAlreadyAttacking = true;
    [Header("Navigation")]
    [SerializeField, Tooltip("Optional NavMeshAgent to stop during the stun animation.")] private UnityEngine.AI.NavMeshAgent navMeshAgent;
    [SerializeField, Tooltip("Seconds to stop navigation after triggering stun.")] private float stopDuration = 0.25f;
    [Header("Animator Speed")]
    [SerializeField, Tooltip("Animator speed while playing the stun animation.")] private float stunAnimatorSpeed = 1.35f;
    [SerializeField, Tooltip("Seconds to keep the animator speed override after triggering.")] private float stunSpeedHoldDuration = 0.15f;
    [Header("Upper Body Layer")]
    [SerializeField, Tooltip("If true, manage the upper body layer weight during stun attacks.")] private bool manageUpperBodyLayer = true;
    [SerializeField] private string upperBodyLayerName = "Upper Body";
    [SerializeField] private float upperBodyAttackWeight = 1f;
    [SerializeField] private float upperBodyIdleWeight = 0f;
    [SerializeField, Tooltip("Animator tag used for regular attacks.")] private string attackStateTag = "Attack";
    [SerializeField, Tooltip("Animator tag used for stun attacks.")] private string stunStateTag = "Stun";
    [SerializeField, Tooltip("Seconds to keep the upper body layer active after triggering.")] private float upperBodyHoldDuration = 0.15f;

    private float _nextAllowedTime;
    private int _upperBodyLayerIndex = -1;
    private float _upperBodyHoldTimer;
    private bool _upperBodyWeightApplied;
    private float _navStopTimer;
    private bool _navWasStopped;
    private float _animSpeedTimer;
    private float _baseAnimatorSpeed = 1f;

    private void Awake()
    {
        if (!health)
        {
            health = GetComponent<CharacterHealth>() ?? GetComponentInChildren<CharacterHealth>(true);
        }

        if (!characterAnimations)
        {
            characterAnimations = GetComponentInChildren<CharacterAnimations>(true);
        }

        if (!animator)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (!navMeshAgent)
        {
            navMeshAgent = GetComponent<UnityEngine.AI.NavMeshAgent>()
                ?? GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true);
        }

        if (animator)
        {
            _baseAnimatorSpeed = animator.speed;
        }

        ResolveUpperBodyLayer();
    }

    private void OnEnable()
    {
        if (health)
        {
            health.Damaged += HandleDamaged;
        }
    }

    private void OnDisable()
    {
        if (health)
        {
            health.Damaged -= HandleDamaged;
        }
    }

    private void HandleDamaged(DamagePayload payload)
    {
        if (!health || health.CurrentHealth <= 0f)
        {
            return;
        }

        if (Time.time < _nextAllowedTime)
        {
            return;
        }

        if (playersOnly && !IsPlayerInstigator(payload.Instigator))
        {
            return;
        }

        if (skipIfAlreadyAttacking && characterAnimations && characterAnimations.IsInAttackState())
        {
            return;
        }

        if (faceAttacker)
        {
            FaceInstigator(payload);
        }

        if (characterAnimations)
        {
            characterAnimations.TriggerAttack(stunAttackTriggerName);
        }
        else if (animator && !string.IsNullOrEmpty(stunAttackTriggerName))
        {
            int hash = Animator.StringToHash(stunAttackTriggerName);
            animator.ResetTrigger(hash);
            animator.SetTrigger(hash);
        }

        if (navMeshAgent && stopDuration > 0f)
        {
            _navStopTimer = Mathf.Max(_navStopTimer, stopDuration);
            if (!navMeshAgent.isStopped)
            {
                navMeshAgent.isStopped = true;
                _navWasStopped = true;
            }
        }

        if (animator && stunAnimatorSpeed > 0f)
        {
            _animSpeedTimer = Mathf.Max(_animSpeedTimer, stunSpeedHoldDuration);
            animator.speed = stunAnimatorSpeed;
        }

        if (manageUpperBodyLayer && animator && _upperBodyLayerIndex >= 0)
        {
            _upperBodyHoldTimer = Mathf.Max(_upperBodyHoldTimer, upperBodyHoldDuration);
            ApplyUpperBodyWeight(upperBodyAttackWeight);
        }

        _nextAllowedTime = Time.time + Mathf.Max(0f, retaliationCooldown);
    }

    private bool IsPlayerInstigator(GameObject instigator)
    {
        if (!instigator)
        {
            return false;
        }

        CharacterHealth instigatorHealth = instigator.GetComponent<CharacterHealth>()
            ?? instigator.GetComponentInParent<CharacterHealth>()
            ?? instigator.GetComponentInChildren<CharacterHealth>(true);
        if (instigatorHealth)
        {
            LocalVersusGameManager manager = LocalVersusGameManager.Instance;
            if (manager && manager.IsPlayerHealth(instigatorHealth))
            {
                return true;
            }
        }

        return instigator.GetComponent<SimpleCharacterController>()
            || instigator.GetComponentInChildren<SimpleCharacterController>(true)
            || instigator.GetComponentInParent<SimpleCharacterController>();
    }

    private void FaceInstigator(DamagePayload payload)
    {
        Vector3 targetPosition = payload.Instigator ? payload.Instigator.transform.position : payload.HitPoint;
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
    }

    private void Update()
    {
        if (!manageUpperBodyLayer || !animator || _upperBodyLayerIndex < 0)
        {
            UpdateNavigationStop();
            UpdateAnimatorSpeed();
            return;
        }

        bool inAttackState = IsInUpperBodyAttackState();
        if (inAttackState)
        {
            _upperBodyHoldTimer = Mathf.Max(_upperBodyHoldTimer, upperBodyHoldDuration);
        }
        else if (_upperBodyHoldTimer > 0f)
        {
            _upperBodyHoldTimer = Mathf.Max(0f, _upperBodyHoldTimer - Time.deltaTime);
        }

        float targetWeight = (inAttackState || _upperBodyHoldTimer > 0f) ? upperBodyAttackWeight : upperBodyIdleWeight;
        if (!_upperBodyWeightApplied || !Mathf.Approximately(animator.GetLayerWeight(_upperBodyLayerIndex), targetWeight))
        {
            ApplyUpperBodyWeight(targetWeight);
        }

        UpdateNavigationStop();
        UpdateAnimatorSpeed();
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

    private bool IsInUpperBodyAttackState()
    {
        if (_upperBodyLayerIndex < 0)
        {
            return false;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(_upperBodyLayerIndex);
        if ((!string.IsNullOrEmpty(attackStateTag) && current.IsTag(attackStateTag))
            || (!string.IsNullOrEmpty(stunStateTag) && current.IsTag(stunStateTag)))
        {
            return true;
        }

        if (animator.IsInTransition(_upperBodyLayerIndex))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(_upperBodyLayerIndex);
            return (!string.IsNullOrEmpty(attackStateTag) && next.IsTag(attackStateTag))
                || (!string.IsNullOrEmpty(stunStateTag) && next.IsTag(stunStateTag));
        }

        return false;
    }

    private void ApplyUpperBodyWeight(float weight)
    {
        if (_upperBodyLayerIndex < 0)
        {
            return;
        }

        animator.SetLayerWeight(_upperBodyLayerIndex, Mathf.Clamp01(weight));
        _upperBodyWeightApplied = true;
    }

    private void UpdateNavigationStop()
    {
        if (!navMeshAgent || stopDuration <= 0f)
        {
            return;
        }

        if (_navStopTimer > 0f)
        {
            _navStopTimer = Mathf.Max(0f, _navStopTimer - Time.deltaTime);
            if (!navMeshAgent.isStopped)
            {
                navMeshAgent.isStopped = true;
                _navWasStopped = true;
            }
        }
        else if (_navWasStopped)
        {
            navMeshAgent.isStopped = false;
            _navWasStopped = false;
        }
    }

    private void UpdateAnimatorSpeed()
    {
        if (!animator || stunAnimatorSpeed <= 0f)
        {
            return;
        }

        if (_animSpeedTimer > 0f)
        {
            _animSpeedTimer = Mathf.Max(0f, _animSpeedTimer - Time.deltaTime);
            animator.speed = stunAnimatorSpeed;
        }
        else
        {
            animator.speed = _baseAnimatorSpeed;
        }
    }

    private void OnValidate()
    {
        retaliationCooldown = Mathf.Max(0f, retaliationCooldown);
        upperBodyAttackWeight = Mathf.Clamp01(upperBodyAttackWeight);
        upperBodyIdleWeight = Mathf.Clamp01(upperBodyIdleWeight);
        upperBodyHoldDuration = Mathf.Max(0f, upperBodyHoldDuration);
        stopDuration = Mathf.Max(0f, stopDuration);
        stunAnimatorSpeed = Mathf.Max(0f, stunAnimatorSpeed);
        stunSpeedHoldDuration = Mathf.Max(0f, stunSpeedHoldDuration);
    }
}
