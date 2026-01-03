using UnityEngine;
using UnityEngine.AI;

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
    [SerializeField, Tooltip("Only retaliate when the hit collider has this tag.")] private string retaliateWeaponTag = "WeaponKill";
    [SerializeField, Tooltip("Rotate the NPC to face the attacker before triggering the animation.")] private bool faceAttacker = true;
    [SerializeField, Tooltip("Minimum seconds between retaliations.")] private float retaliationCooldown = 0.35f;
    [SerializeField, Tooltip("Skip retaliation if the NPC is already in an attack/stun animation.")] private bool skipIfAlreadyAttacking = true;
    [Header("Navigation")]
    [SerializeField, Tooltip("Optional NavMeshAgent to stop during the stun animation.")] private NavMeshAgent navMeshAgent;
    [SerializeField, Tooltip("Optional NPC nav controller to disable while retaliating.")] private NpcNavAgent npcNavAgent;
    [SerializeField, Tooltip("Seconds to stop navigation after triggering stun.")] private float stopDuration = 0.25f;
    [SerializeField, Tooltip("Distance required to trigger stun immediately. Otherwise the NPC will chase.")] private float retaliateRange = 2f;
    [SerializeField, Tooltip("Seconds to chase the attacker when out of range.")] private float chaseDuration = 1.5f;
    [SerializeField, Tooltip("Seconds between chase destination updates.")] private float chaseRepathInterval = 0.15f;
    [SerializeField, Tooltip("Speed multiplier while chasing.")] private float chaseSpeedMultiplier = 1.35f;
    [SerializeField, Tooltip("Disable the NPC wander controller while chasing.")] private bool disableWanderDuringRetaliation = true;
    [Header("Animator Speed")]
    [SerializeField, Tooltip("Upper body speed parameter used to speed up stun without affecting base layer.")] private string upperBodySpeedParam = "UpperBodySpeed";
    [SerializeField, Tooltip("Speed value for upper body stun animation.")] private float stunAnimatorSpeed = 1.35f;
    [SerializeField, Tooltip("Seconds to keep the upper body speed override after triggering.")] private float stunSpeedHoldDuration = 0.15f;
    [Header("Upper Body Layer")]
    [SerializeField, Tooltip("If true, manage the upper body layer weight during stun attacks.")] private bool manageUpperBodyLayer = true;
    [SerializeField] private string upperBodyLayerName = "Upper Body";
    [SerializeField] private float upperBodyAttackWeight = 1f;
    [SerializeField] private float upperBodyIdleWeight = 0f;
    [SerializeField, Tooltip("Animator tag used for regular attacks.")] private string attackStateTag = "Attack";
    [SerializeField, Tooltip("Animator tag used for stun attacks.")] private string stunStateTag = "Stun";
    [SerializeField, Tooltip("Seconds to keep the upper body layer active after triggering.")] private float upperBodyHoldDuration = 0.15f;
    [Header("Chase Animation")]
    [SerializeField, Tooltip("Animator bool set true while chasing.")] private string runningBoolName = "isRunning";

    private float _nextAllowedTime;
    private int _upperBodyLayerIndex = -1;
    private float _upperBodyHoldTimer;
    private bool _upperBodyWeightApplied;
    private float _navStopTimer;
    private bool _navWasStopped;
    private float _animSpeedTimer;
    private bool _hasUpperBodySpeedParam;
    private float _upperBodySpeedDefault = 1f;
    private float _baseAgentSpeed;
    private bool _agentSpeedOverridden;
    private float _chaseTimer;
    private float _nextChaseRepathTime;
    private Transform _chaseTarget;
    private PlayerStunController _chaseTargetStun;
    private DamagePayload _lastPayload;
    private bool _hasLastPayload;
    private bool _navControllerDisabled;
    private int _runningBoolHash;
    private bool _hasRunningBool;
    private bool _runningApplied;
    private bool _hasSwungDuringChase;

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
            navMeshAgent = GetComponent<NavMeshAgent>()
                ?? GetComponentInChildren<NavMeshAgent>(true);
        }

        if (!npcNavAgent)
        {
            npcNavAgent = GetComponent<NpcNavAgent>() ?? GetComponentInChildren<NpcNavAgent>(true);
        }

        if (animator)
        {
            _runningBoolHash = Animator.StringToHash(runningBoolName);
            _hasRunningBool = HasParameter(animator, runningBoolName, AnimatorControllerParameterType.Bool);
            _hasUpperBodySpeedParam = HasParameter(animator, upperBodySpeedParam, AnimatorControllerParameterType.Float);
            if (_hasUpperBodySpeedParam)
            {
                _upperBodySpeedDefault = animator.GetFloat(upperBodySpeedParam);
            }
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

        StopChase();
    }

    public bool IsRetaliating
    {
        get
        {
            if (_chaseTimer > 0f)
            {
                return true;
            }

            if (characterAnimations)
            {
                return characterAnimations.IsInAttackState();
            }

            if (animator && _upperBodyLayerIndex >= 0)
            {
                return IsInUpperBodyAttackState();
            }

            return false;
        }
    }

    public void TriggerAwarenessRetaliation(Transform instigator)
    {
        if (!instigator || !health || health.CurrentHealth <= 0f)
        {
            return;
        }

        if (playersOnly && !IsPlayerInstigator(instigator.gameObject))
        {
            return;
        }

        if (skipIfAlreadyAttacking && characterAnimations && characterAnimations.IsInAttackState())
        {
            return;
        }

        DamagePayload payload = BuildPayloadForTarget(instigator);
        StartChase(instigator, payload);

        if (Time.time >= _nextAllowedTime && !ShouldChase(instigator.position))
        {
            TriggerStun(payload);
        }
    }

    private void HandleDamaged(DamagePayload payload)
    {
        if (!health || health.CurrentHealth <= 0f)
        {
            return;
        }

        if (playersOnly && !IsPlayerInstigator(payload.Instigator))
        {
            return;
        }

        if (!IsRetaliationHit(payload))
        {
            return;
        }

        if (skipIfAlreadyAttacking && characterAnimations && characterAnimations.IsInAttackState())
        {
            return;
        }

        Transform instigatorTransform = payload.Instigator ? payload.Instigator.transform : null;
        if (instigatorTransform)
        {
            StartChase(instigatorTransform, payload);
        }

        if (Time.time >= _nextAllowedTime && instigatorTransform && !ShouldChase(instigatorTransform.position))
        {
            TriggerStun(payload);
        }
    }

    private void TriggerStun(DamagePayload payload)
    {
        if (_chaseTarget)
        {
            _lastPayload = payload;
            _hasLastPayload = true;
            _hasSwungDuringChase = true;
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

        if (navMeshAgent && stopDuration > 0f && !ShouldResumeChase())
        {
            _navStopTimer = Mathf.Max(_navStopTimer, stopDuration);
            if (!navMeshAgent.isStopped)
            {
                navMeshAgent.isStopped = true;
                _navWasStopped = true;
            }
        }

        if (animator && _hasUpperBodySpeedParam && stunAnimatorSpeed > 0f)
        {
            _animSpeedTimer = Mathf.Max(_animSpeedTimer, stunSpeedHoldDuration);
            animator.SetFloat(upperBodySpeedParam, stunAnimatorSpeed);
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

    private bool IsRetaliationHit(DamagePayload payload)
    {
        if (string.IsNullOrEmpty(retaliateWeaponTag))
        {
            return true;
        }

        Collider hitCollider = payload.HitCollider;
        if (hitCollider && hitCollider.CompareTag(retaliateWeaponTag))
        {
            return true;
        }

        if (payload.Source && payload.Source.CompareTag(retaliateWeaponTag))
        {
            return true;
        }

        return false;
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
        UpdateChase();

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

    private void StartChase(Transform instigator, DamagePayload payload)
    {
        if (!instigator || chaseDuration <= 0f)
        {
            return;
        }

        _navStopTimer = 0f;
        _navWasStopped = false;
        _lastPayload = payload;
        _hasLastPayload = true;
        _chaseTarget = instigator;
        _chaseTargetStun = instigator.GetComponent<PlayerStunController>()
            ?? instigator.GetComponentInChildren<PlayerStunController>(true)
            ?? instigator.GetComponentInParent<PlayerStunController>();
        _hasSwungDuringChase = false;
        _chaseTimer = Mathf.Max(_chaseTimer, chaseDuration);
        _nextChaseRepathTime = 0f;

        if (disableWanderDuringRetaliation && npcNavAgent && npcNavAgent.enabled)
        {
            npcNavAgent.enabled = false;
            _navControllerDisabled = true;
        }

        if (navMeshAgent && navMeshAgent.isOnNavMesh)
        {
            if (!_agentSpeedOverridden)
            {
                _baseAgentSpeed = navMeshAgent.speed;
                _agentSpeedOverridden = true;
            }

            if (chaseSpeedMultiplier > 0f)
            {
                navMeshAgent.speed = _baseAgentSpeed * chaseSpeedMultiplier;
            }

            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(instigator.position);
        }
    }

    private void StopChase()
    {
        _chaseTimer = 0f;
        _chaseTarget = null;
        _chaseTargetStun = null;
        _hasSwungDuringChase = false;

        if (navMeshAgent && _agentSpeedOverridden)
        {
            navMeshAgent.speed = _baseAgentSpeed;
            _agentSpeedOverridden = false;
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
        }

        if (_navControllerDisabled && npcNavAgent)
        {
            npcNavAgent.enabled = true;
            _navControllerDisabled = false;
        }

        SetRunning(false);
    }

    private void UpdateChase()
    {
        if (_chaseTimer <= 0f || !_chaseTarget)
        {
            SetRunning(false);
            return;
        }

        if (_hasSwungDuringChase && _chaseTargetStun && _chaseTargetStun.IsStunned)
        {
            StopChase();
            return;
        }

        _chaseTimer = Mathf.Max(0f, _chaseTimer - Time.deltaTime);
        bool shouldChase = ShouldChase(_chaseTarget.position);
        SetRunning(shouldChase);

        if (navMeshAgent && navMeshAgent.isOnNavMesh && Time.time >= _nextChaseRepathTime)
        {
            navMeshAgent.SetDestination(_chaseTarget.position);
            _nextChaseRepathTime = Time.time + Mathf.Max(0.05f, chaseRepathInterval);
        }

        if (Time.time >= _nextAllowedTime && CanTriggerSwing())
        {
            TriggerStun(BuildPayloadForTarget(_chaseTarget));
        }

        if (_chaseTimer <= 0f)
        {
            StopChase();
        }
    }

    private bool ShouldChase(Vector3 instigatorPosition)
    {
        float range = GetRetaliateRange();
        Vector3 toTarget = instigatorPosition - transform.position;
        toTarget.y = 0f;
        return toTarget.sqrMagnitude > range * range;
    }

    private float GetRetaliateRange()
    {
        if (navMeshAgent)
        {
            return Mathf.Max(retaliateRange, navMeshAgent.stoppingDistance);
        }

        return retaliateRange;
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

        if (ShouldResumeChase())
        {
            _navStopTimer = 0f;
            if (navMeshAgent.isStopped)
            {
                navMeshAgent.isStopped = false;
                _navWasStopped = false;
            }
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
        if (!animator || !_hasUpperBodySpeedParam || stunAnimatorSpeed <= 0f)
        {
            return;
        }

        if (_animSpeedTimer > 0f)
        {
            _animSpeedTimer = Mathf.Max(0f, _animSpeedTimer - Time.deltaTime);
            animator.SetFloat(upperBodySpeedParam, stunAnimatorSpeed);
        }
        else
        {
            animator.SetFloat(upperBodySpeedParam, _upperBodySpeedDefault);
        }
    }

    private bool ShouldResumeChase()
    {
        return _chaseTarget && _chaseTimer > 0f && ShouldChase(_chaseTarget.position);
    }

    private bool CanTriggerSwing()
    {
        if (characterAnimations)
        {
            return !characterAnimations.IsInAttackState();
        }

        if (animator && _upperBodyLayerIndex >= 0)
        {
            return !IsInUpperBodyAttackState();
        }

        return true;
    }

    private DamagePayload BuildPayloadForTarget(Transform target)
    {
        DamagePayload payload = _hasLastPayload ? _lastPayload : default;
        if (target)
        {
            payload.Instigator = target.gameObject;
            payload.HitPoint = target.position;
            payload.HitNormal = (transform.position - target.position).normalized;
        }

        return payload;
    }

    private void SetRunning(bool value)
    {
        if (!animator || !_hasRunningBool)
        {
            return;
        }

        if (_runningApplied && animator.GetBool(_runningBoolHash) == value)
        {
            return;
        }

        animator.SetBool(_runningBoolHash, value);
        _runningApplied = true;
    }

    private static bool HasParameter(Animator target, string name, AnimatorControllerParameterType type)
    {
        if (!target || string.IsNullOrEmpty(name))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = target.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter param = parameters[i];
            if (param != null && param.type == type && param.name == name)
            {
                return true;
            }
        }

        return false;
    }

    private void OnValidate()
    {
        retaliationCooldown = Mathf.Max(0f, retaliationCooldown);
        upperBodyAttackWeight = Mathf.Clamp01(upperBodyAttackWeight);
        upperBodyIdleWeight = Mathf.Clamp01(upperBodyIdleWeight);
        upperBodyHoldDuration = Mathf.Max(0f, upperBodyHoldDuration);
        stopDuration = Mathf.Max(0f, stopDuration);
        retaliateRange = Mathf.Max(0f, retaliateRange);
        chaseDuration = Mathf.Max(0f, chaseDuration);
        chaseRepathInterval = Mathf.Max(0f, chaseRepathInterval);
        chaseSpeedMultiplier = Mathf.Max(0f, chaseSpeedMultiplier);
        if (string.IsNullOrWhiteSpace(upperBodySpeedParam))
        {
            upperBodySpeedParam = "UpperBodySpeed";
        }
        stunAnimatorSpeed = Mathf.Max(0f, stunAnimatorSpeed);
        stunSpeedHoldDuration = Mathf.Max(0f, stunSpeedHoldDuration);
    }
}
