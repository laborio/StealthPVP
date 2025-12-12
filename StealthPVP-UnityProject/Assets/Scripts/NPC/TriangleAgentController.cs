using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Minimal controller data holder for triangle agents (player + two AI targets).
/// Stores target assignments and basic state placeholders for future AI logic.
/// </summary>
[DisallowMultipleComponent]
public class TriangleAgentController : MonoBehaviour
{
    public enum AgentState
    {
        BlendIn,
        Investigate,
        Hunt,
        SearchLastKnown,
        KillAttempt
    }

    [Header("Targeting")]
    [SerializeField, Tooltip("Who this agent is currently hunting.")] private TriangleAgentController myTarget;
    [SerializeField, Tooltip("Who is hunting this agent. Optional.")] private TriangleAgentController myHunter;
    [SerializeField, Tooltip("Confirmed target the AI believes it sees.")] private TriangleAgentController knownTarget;
    [SerializeField, Tooltip("Cached last known position of the known target.")] private Vector3 lastKnownPosition;
    [SerializeField, Tooltip("Current AI state (behaviour logic to be added).")] private AgentState currentState = AgentState.BlendIn;

    [Header("Movement")]
    [SerializeField, Tooltip("Base walk speed applied to the NavMeshAgent.")] private float walkSpeed = 2.5f;
    [SerializeField, Tooltip("Run speed multiplier applied when hunting.")] private float runMultiplier = 3.5f;
    [SerializeField, Tooltip("If true, disable NpcNavAgent wander component while this controller is driving the agent.")] private bool disableWanderWhileChasing = true;
    [SerializeField, Tooltip("Distance at which the agent tries to attack its target.")] private float killRange = 1.75f;

    [Header("References")]
    [SerializeField, Tooltip("Movement agent used for navigation.")] private NavMeshAgent navMeshAgent;
    [SerializeField, Tooltip("Health component used to detect death.")] private CharacterHealth characterHealth;
    [SerializeField, Tooltip("Identity component used for color/indicator lookup.")] private NpcIdentity identity;
    [SerializeField, Tooltip("Animator controlling visual state.")] private Animator animator;
    [SerializeField, Tooltip("Optional wander driver; will be disabled while chasing a target.")] private NpcNavAgent wanderAgent;
    [Header("Animation Parameters")]
    [SerializeField] private string walkBool = "isWalking";
    [SerializeField] private string runBool = "isRunning";
    [SerializeField] private string idleBool = "isIdle";
    [SerializeField] private string attackTrigger = "Attack";
    [Header("Perception")]
    [SerializeField, Tooltip("Vision source used to determine sight radius (matches fog-of-war radius).")] private VisionSource visionSource;
    [SerializeField, Tooltip("Seconds to keep chasing after losing target in fog.")] private float fogMemoryDuration = 2f;
    [SerializeField, Tooltip("Cooldown for AI reveal ability that gives temporary knowledge of the target.")] private float revealCooldownSeconds = 30f;
    [SerializeField, Tooltip("Seconds the reveal stays fully active.")] private float revealFullVisibilitySeconds = 2f;
    [SerializeField, Tooltip("Seconds to fade out knowledge after reveal ends.")] private float revealFadeSeconds = 1f;
    [SerializeField, Tooltip("If true, AI auto-triggers reveal ability when it has no vision and cooldown is ready.")] private bool autoReveal = true;
    [SerializeField, Tooltip("Distance at which reveal can confirm a target when near suspicious entities.")] private float revealConfirmDistance = 10f;
    [SerializeField, Tooltip("Radius to search for color-matching entities when reveal is active and in proximity.")] private float revealSuspicionRadius = 8f;
    [SerializeField, Tooltip("Animator bool name used to detect running on the target.")] private string targetRunBool = "isRunning";
    [SerializeField, Tooltip("Animator bool name used to detect jumping on the target.")] private string targetJumpBool = "isJumping";
    [SerializeField, Tooltip("Animator state tag used to detect attacks on the target.")] private string targetAttackTag = "Attack";
    [SerializeField, Tooltip("Radius to check for nearby decoys of the same color to justify idling while blending in. <=0 disables.")] private float decoyIdleRadius = 6f;
    [Header("Abilities")]
    [SerializeField, Tooltip("Optional shared ability configuration. NPCs read their reveal tuning here.")] private AbilityConfig abilityConfig;
    [SerializeField, Tooltip("Ability id used for reveal.")] private string revealAbilityId = "Reveal";
    [Header("Difficulty")]
    [Range(0f, 1f)] [SerializeField, Tooltip("0 = easiest, 1 = hardest. Applied globally via director.")] private float difficulty = 0.5f;

    public TriangleAgentController MyTarget => myTarget;
    public TriangleAgentController MyHunter => myHunter;
    public TriangleAgentController KnownTarget => knownTarget;
    public Vector3 LastKnownPosition => lastKnownPosition;
    public AgentState CurrentState => currentState;
    public bool HasTarget => myTarget != null;
    public bool IsDead => characterHealth && characterHealth.IsDead;
    public NpcIdentity Identity => identity;
    public NavMeshAgent NavAgent => navMeshAgent;

    private bool _attackTriggered;
    private float _fogLossTimer;
    private float _revealHoldTimer;
    private float _revealFadeTimer;
    private float _revealCooldownTimer;
    private bool _hasLastKnownPosition;
    private bool _revealHasLockedPosition;
    private FogOfWarManager _fogManager;
    private float _baseFogMemoryDuration;
    private float _baseRevealCooldown;
    private float _baseRevealHold;
    private float _baseRevealFade;
    private float _baseRevealConfirm;
    private float _baseRevealSuspicion;

    private void Awake()
    {
        CacheRefs();
    }

    private void Update()
    {
        DriveBehaviour();
    }

    private void Reset()
    {
        CacheRefs();
    }

    public void ResetForNewTarget(TriangleAgentController target, TriangleAgentController hunter = null)
    {
        myTarget = target;
        myHunter = hunter;
        currentState = target ? AgentState.Investigate : AgentState.BlendIn;
        knownTarget = null;
        lastKnownPosition = Vector3.zero;
        _attackTriggered = false;
        _fogLossTimer = 0f;
        _revealHoldTimer = 0f;
        _revealFadeTimer = 0f;
        _revealCooldownTimer = 0f;
        _hasLastKnownPosition = false;
        _revealHasLockedPosition = false;
    }

    public void SetKnownTarget(TriangleAgentController target)
    {
        knownTarget = target;
        if (target)
        {
            lastKnownPosition = target.transform.position;
        }
    }

    public void UpdateLastKnownPosition(Vector3 position)
    {
        lastKnownPosition = position;
    }

    public void SetState(AgentState state)
    {
        currentState = state;
    }

    public bool HasLineOfSightToTarget()
    {
        return IsTargetVisible(myTarget);
    }

    public bool IsTargetInKillRange()
    {
        TriangleAgentController target = knownTarget ? knownTarget : myTarget;
        if (!target)
        {
            return false;
        }

        float sqrDistance = (target.transform.position - transform.position).sqrMagnitude;
        return sqrDistance <= killRange * killRange;
    }

    private void CacheRefs()
    {
        if (!navMeshAgent)
        {
            navMeshAgent = GetComponent<NavMeshAgent>() ?? GetComponentInChildren<NavMeshAgent>(true);
        }

        if (!characterHealth)
        {
            characterHealth = GetComponent<CharacterHealth>() ?? GetComponentInChildren<CharacterHealth>(true);
        }

        if (!identity)
        {
            identity = GetComponent<NpcIdentity>() ?? GetComponentInChildren<NpcIdentity>(true);
        }

        if (!animator)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (!wanderAgent)
        {
            wanderAgent = GetComponent<NpcNavAgent>() ?? GetComponentInChildren<NpcNavAgent>(true);
        }

        if (!visionSource)
        {
            visionSource = GetComponent<VisionSource>() ?? GetComponentInChildren<VisionSource>(true);
        }

        if (!_fogManager)
        {
            _fogManager = Object.FindFirstObjectByType<FogOfWarManager>();
        }

        CacheBaseAbilityValues();
        ApplyAbilityConfig();
    }

    private void DriveBehaviour()
    {
        float deltaTime = Time.deltaTime;
        UpdateRevealTimers(deltaTime);

        bool hasActiveNavAgent = navMeshAgent && navMeshAgent.enabled && navMeshAgent.isOnNavMesh;

        if (IsDead)
        {
            if (hasActiveNavAgent)
            {
                StopNav();
                navMeshAgent.speed = walkSpeed;
            }

            ToggleWander(true);
            UpdateAnimatorState(false, false, true);
            return;
        }

        if (!hasActiveNavAgent)
        {
            // Let other systems (e.g. player controller) own animation when no NavMeshAgent is available.
            ToggleWander(true);
            return;
        }

        if (!HasTarget)
        {
            StopNav();
            navMeshAgent.speed = walkSpeed;
            ToggleWander(true);
            UpdateAnimatorState(false, false, true);
            return;
        }

        TriangleAgentController target = myTarget;
        if (!target || target.IsDead)
        {
            StopNav();
            navMeshAgent.speed = walkSpeed;
            currentState = AgentState.BlendIn;
            ToggleWander(true);
            UpdateAnimatorState(false, false, true);
            return;
        }

        bool hasKnowledge = UpdatePerception(target, deltaTime);
        bool hasKnownPosition = _hasLastKnownPosition;

        if (!hasKnowledge && !hasKnownPosition)
        {
            StopNav();
            navMeshAgent.speed = walkSpeed;
            currentState = AgentState.BlendIn;
            ToggleWander(true);
            UpdateAnimatorState(false, false, true);
            return;
        }

        ToggleWander(false);

        Vector3 targetPos = hasKnownPosition ? lastKnownPosition : target.transform.position;

        float desiredSpeed = walkSpeed;
        if (currentState != AgentState.KillAttempt)
        {
            _attackTriggered = false;
        }

        float sqrDistance = (targetPos - transform.position).sqrMagnitude;
        bool canKill = hasKnowledge && sqrDistance <= killRange * killRange;

        if (canKill)
        {
            currentState = AgentState.KillAttempt;
        }
        else if (hasKnownPosition && (currentState == AgentState.BlendIn || currentState == AgentState.SearchLastKnown || currentState == AgentState.KillAttempt))
        {
            currentState = AgentState.Hunt;
        }
        else if (!hasKnownPosition)
        {
            currentState = AgentState.SearchLastKnown;
        }

        switch (currentState)
        {
            case AgentState.Hunt:
                desiredSpeed = walkSpeed * runMultiplier;
                DriveChase(targetPos, desiredSpeed);
                break;
            case AgentState.Investigate:
                desiredSpeed = walkSpeed;
                DriveChase(targetPos, desiredSpeed);
                break;
            case AgentState.SearchLastKnown:
                desiredSpeed = walkSpeed;
                DriveChase(lastKnownPosition, desiredSpeed);
                break;
            case AgentState.BlendIn:
                if (ShouldIdleInBlendIn())
                {
                    StopNav();
                    navMeshAgent.speed = walkSpeed;
                    ToggleWander(false);
                    UpdateAnimatorState(false, false, true);
                    return;
                }

                navMeshAgent.speed = walkSpeed;
                ToggleWander(true);
                break;
            case AgentState.KillAttempt:
                StopNav();
                TryTriggerAttack();
                break;
            default:
                break;
        }

        bool moving = navMeshAgent && navMeshAgent.enabled && navMeshAgent.isOnNavMesh && !navMeshAgent.isStopped && navMeshAgent.velocity.sqrMagnitude > 0.0001f;
        // If the NavMeshAgent speed is above the walk speed, force running animation even if state desyncs.
        float navSpeedSetting = navMeshAgent ? navMeshAgent.speed : 0f;
        bool running = currentState == AgentState.Hunt || navSpeedSetting > walkSpeed * 1.05f;
        bool attacking = currentState == AgentState.KillAttempt && _attackTriggered;
        UpdateAnimatorState(moving, running, !moving && !attacking);
    }

    private void UpdateRevealTimers(float deltaTime)
    {
        if (_revealCooldownTimer > 0f)
        {
            _revealCooldownTimer = Mathf.Max(0f, _revealCooldownTimer - deltaTime);
        }

        if (_revealHoldTimer > 0f)
        {
            _revealHoldTimer = Mathf.Max(0f, _revealHoldTimer - deltaTime);
        }
        else if (_revealFadeTimer > 0f)
        {
            _revealFadeTimer = Mathf.Max(0f, _revealFadeTimer - deltaTime);
        }
        else
        {
            _revealHasLockedPosition = false;
        }
    }

    private void TriggerReveal()
    {
        _revealHoldTimer = revealFullVisibilitySeconds;
        _revealFadeTimer = revealFadeSeconds;
        _revealCooldownTimer = revealCooldownSeconds;
        _revealHasLockedPosition = false;
    }

    private bool IsRevealActive()
    {
        return _revealHoldTimer > 0f || _revealFadeTimer > 0f;
    }

    private bool UpdatePerception(TriangleAgentController target, float deltaTime)
    {
        if (!target)
        {
            knownTarget = null;
            _hasLastKnownPosition = false;
            return false;
        }

        bool visible = IsTargetVisible(target);
        bool revealActive = IsRevealActive();

        if (!visible && !revealActive && autoReveal && _revealCooldownTimer <= 0f && _fogLossTimer >= fogMemoryDuration)
        {
            TriggerReveal();
            revealActive = true;
        }

        if (revealActive)
        {
            if (!_revealHasLockedPosition)
            {
                lastKnownPosition = target.transform.position;
                _hasLastKnownPosition = true;
                _revealHasLockedPosition = true;
            }

            _fogLossTimer = 0f;

            float sqrDist = (target.transform.position - transform.position).sqrMagnitude;
            if (sqrDist <= revealConfirmDistance * revealConfirmDistance)
            {
                TryConfirmTargetFromCluster(target.transform.position);
            }
            return knownTarget != null;
        }

        if (visible)
        {
            knownTarget = target;
            lastKnownPosition = target.transform.position;
            _hasLastKnownPosition = true;
            _fogLossTimer = 0f;
            return true;
        }

        _fogLossTimer += deltaTime;
        if (_fogLossTimer >= fogMemoryDuration)
        {
            knownTarget = null;
            _hasLastKnownPosition = false;
        }

        return knownTarget != null || _hasLastKnownPosition;
    }

    private bool IsTargetVisible(TriangleAgentController target)
    {
        if (!target)
        {
            return false;
        }

        if (!IsTargetInVision(target))
        {
            return false;
        }

        if (IsTargetInFog(target))
        {
            return false;
        }

        return IsTargetNonDecoy(target);
    }

    private bool IsTargetInVision(TriangleAgentController target)
    {
        if (!visionSource)
        {
            return false;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        float radius = Mathf.Max(0f, visionSource.CurrentRadius);
        return toTarget.sqrMagnitude <= radius * radius;
    }

    private bool IsTargetInFog(TriangleAgentController target)
    {
        if (!target)
        {
            return false;
        }

        if (!_fogManager)
        {
            _fogManager = Object.FindFirstObjectByType<FogOfWarManager>();
        }

        if (!_fogManager)
        {
            return false;
        }

        float fogValue = _fogManager.SampleFog01(target.transform.position);
        return fogValue <= 0.5f;
    }

    private bool IsTargetNonDecoy(TriangleAgentController target)
    {
        Animator targetAnimator = target ? target.animator : null;
        bool running = false;
        if (targetAnimator && HasParameter(targetAnimator, targetRunBool, AnimatorControllerParameterType.Bool))
        {
            running = targetAnimator.GetBool(targetRunBool);
        }

        bool attacking = false;
        CharacterAnimations targetAnims = target.GetComponentInChildren<CharacterAnimations>(true);
        if (targetAnims && targetAnims.IsInAttackState())
        {
            attacking = true;
        }

        if (!attacking && targetAnimator && !string.IsNullOrEmpty(targetAttackTag))
        {
            attacking = IsAnimatorTagged(targetAnimator, targetAttackTag);
        }

        bool jumping = false;
        if (targetAnimator && HasParameter(targetAnimator, targetJumpBool, AnimatorControllerParameterType.Bool))
        {
            jumping = targetAnimator.GetBool(targetJumpBool);
        }

        bool speedRun = false;
        NavMeshAgent targetNav = target ? target.navMeshAgent : null;
        if (targetNav && targetNav.enabled)
        {
            speedRun = targetNav.velocity.sqrMagnitude > walkSpeed * walkSpeed * runMultiplier * 0.25f || targetNav.speed > walkSpeed * 1.05f;
        }

        bool outOfBoundsY = target.transform.position.y > 10.6f || target.transform.position.y < 10f;

        return running || attacking || jumping || speedRun || outOfBoundsY;
    }

    private void TryConfirmTargetFromCluster(Vector3 suspicionCenter)
    {
        if (!identity)
        {
            return;
        }

        Collider[] hits = Physics.OverlapSphere(suspicionCenter, revealSuspicionRadius);
        List<TriangleAgentController> matchingAgents = new List<TriangleAgentController>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (!col)
            {
                continue;
            }

            TriangleAgentController agent = col.GetComponent<TriangleAgentController>() ?? col.GetComponentInParent<TriangleAgentController>();
            if (!agent || agent == this)
            {
                continue;
            }

            if (agent.Identity && ColorsMatch(identity.IdentifierColor, agent.Identity.IdentifierColor))
            {
                if (!matchingAgents.Contains(agent))
                {
                    matchingAgents.Add(agent);
                }
            }
        }

        if (matchingAgents.Count == 0)
        {
            return;
        }

        // Prefer non-decoys.
        for (int i = 0; i < matchingAgents.Count; i++)
        {
            if (IsTargetNonDecoy(matchingAgents[i]))
            {
                knownTarget = matchingAgents[i];
                return;
            }
        }

        // Otherwise coin-flip: either pick a random matching agent or keep searching.
        if (Random.value > 0.5f)
        {
            int pick = Random.Range(0, matchingAgents.Count);
            knownTarget = matchingAgents[pick];
        }
    }

    private static bool IsAnimatorTagged(Animator targetAnimator, string tag)
    {
        if (!targetAnimator || string.IsNullOrEmpty(tag))
        {
            return false;
        }

        AnimatorStateInfo state = targetAnimator.GetCurrentAnimatorStateInfo(0);
        return state.IsTag(tag);
    }

    private void ApplyAbilityConfig()
    {
        if (!abilityConfig)
        {
            return;
        }

        AbilityDefinition def = abilityConfig.GetAbility(revealAbilityId, isPlayer: false);
        if (def != null)
        {
            revealCooldownSeconds = def.CooldownSeconds;
            revealFullVisibilitySeconds = def.FullDurationSeconds;
            revealFadeSeconds = def.FadeDurationSeconds;
        }
    }

    private void CacheBaseAbilityValues()
    {
        _baseFogMemoryDuration = fogMemoryDuration;
        _baseRevealCooldown = revealCooldownSeconds;
        _baseRevealHold = revealFullVisibilitySeconds;
        _baseRevealFade = revealFadeSeconds;
        _baseRevealConfirm = revealConfirmDistance;
        _baseRevealSuspicion = revealSuspicionRadius;
    }

    public void ApplyDifficulty(float value)
    {
        difficulty = Mathf.Clamp01(value);
        // Easier = longer cooldown, shorter memory, smaller suspicion radius; Harder = shorter cooldown, longer memory.
        revealCooldownSeconds = Mathf.Lerp(_baseRevealCooldown * 1.5f, _baseRevealCooldown * 0.7f, difficulty);
        fogMemoryDuration = Mathf.Lerp(_baseFogMemoryDuration * 0.5f, _baseFogMemoryDuration * 1.5f, difficulty);
        revealFullVisibilitySeconds = Mathf.Lerp(_baseRevealHold * 0.6f, _baseRevealHold * 1.2f, difficulty);
        revealFadeSeconds = Mathf.Lerp(_baseRevealFade * 0.6f, _baseRevealFade * 1.2f, difficulty);
        revealConfirmDistance = Mathf.Lerp(_baseRevealConfirm * 0.6f, _baseRevealConfirm * 1.2f, difficulty);
        revealSuspicionRadius = Mathf.Lerp(_baseRevealSuspicion * 0.5f, _baseRevealSuspicion * 1.3f, difficulty);
        autoReveal = difficulty > 0.35f;
    }

    private bool ShouldIdleInBlendIn()
    {
        if (decoyIdleRadius <= 0f || !identity)
        {
            return false;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, decoyIdleRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (!col)
            {
                continue;
            }

            TriangleAgentController otherAgent = col.GetComponent<TriangleAgentController>() ?? col.GetComponentInParent<TriangleAgentController>();
            if (otherAgent && otherAgent != this)
            {
                continue; // real agents don't count as decoys
            }

            NpcIdentity otherId = col.GetComponent<NpcIdentity>() ?? col.GetComponentInParent<NpcIdentity>();
            if (otherId && ColorsMatch(identity.IdentifierColor, otherId.IdentifierColor))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ColorsMatch(Color a, Color b)
    {
        const float tolerance = 0.01f;
        return Mathf.Abs(a.r - b.r) < tolerance &&
               Mathf.Abs(a.g - b.g) < tolerance &&
               Mathf.Abs(a.b - b.b) < tolerance;
    }

    private void DriveChase(Vector3 destination, float speed)
    {
        if (!navMeshAgent)
        {
            return;
        }

        if (navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.speed = speed;
            navMeshAgent.SetDestination(destination);
        }
    }

    private void StopNav()
    {
        if (navMeshAgent)
        {
            navMeshAgent.isStopped = true;
        }
    }

    private void ToggleWander(bool enable)
    {
        if (!disableWanderWhileChasing || !wanderAgent)
        {
            return;
        }

        if (wanderAgent.enabled == enable)
        {
            return;
        }

        wanderAgent.enabled = enable;
    }

    private void UpdateAnimatorState(bool moving, bool running, bool idle)
    {
        if (!animator)
        {
            return;
        }

        SetBoolSafe(walkBool, moving && !running);
        SetBoolSafe(runBool, moving && running);
        SetBoolSafe(idleBool, idle);
    }

    private void TryTriggerAttack()
    {
        if (!animator || _attackTriggered)
        {
            return;
        }

        _attackTriggered = true;
        if (!string.IsNullOrEmpty(attackTrigger) && HasParameter(animator, attackTrigger, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(attackTrigger);
        }
    }

    private void SetBoolSafe(string parameterName, bool value)
    {
        if (string.IsNullOrEmpty(parameterName))
        {
            return;
        }

        if (HasParameter(animator, parameterName, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(parameterName, value);
        }
    }

    private static bool HasParameter(Animator targetAnimator, string name, AnimatorControllerParameterType type)
    {
        if (!targetAnimator || string.IsNullOrEmpty(name))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = targetAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter param = parameters[i];
            if (param.type == type && param.name == name)
            {
                return true;
            }
        }

        return false;
    }
}
