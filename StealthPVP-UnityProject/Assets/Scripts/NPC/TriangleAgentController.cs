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
        // Perception stub; extend with FOV and raycasts later.
        return false;
    }

    public bool IsTargetInKillRange()
    {
        // Placeholder until kill range logic is added.
        return false;
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
    }

    private void DriveBehaviour()
    {
        if (!navMeshAgent || IsDead)
        {
            StopNav();
            ToggleWander(true);
            UpdateAnimatorState(false, false, true);
            return;
        }

        if (!HasTarget)
        {
            ToggleWander(true);
            UpdateAnimatorState(false, false, true);
            return;
        }

        TriangleAgentController target = myTarget;
        if (!target || target.IsDead)
        {
            ToggleWander(true);
            UpdateAnimatorState(false, false, true);
            return;
        }

        ToggleWander(false);

        Vector3 targetPos = target.transform.position;
        lastKnownPosition = targetPos;

        float desiredSpeed = walkSpeed;
        if (currentState != AgentState.KillAttempt)
        {
            _attackTriggered = false;
        }

        float sqrDistance = (targetPos - transform.position).sqrMagnitude;
        bool canKill = sqrDistance <= killRange * killRange;

        if (canKill)
        {
            currentState = AgentState.KillAttempt;
        }
        else if (currentState == AgentState.BlendIn || currentState == AgentState.SearchLastKnown || currentState == AgentState.KillAttempt)
        {
            currentState = AgentState.Hunt;
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
                desiredSpeed = walkSpeed;
                DriveChase(lastKnownPosition, desiredSpeed);
                break;
            case AgentState.KillAttempt:
                StopNav();
                TryTriggerAttack();
                break;
            default:
                break;
        }

        bool running = currentState == AgentState.Hunt;
        bool moving = navMeshAgent && navMeshAgent.enabled && navMeshAgent.isOnNavMesh && !navMeshAgent.isStopped && navMeshAgent.velocity.sqrMagnitude > 0.0001f;
        bool attacking = currentState == AgentState.KillAttempt && _attackTriggered;
        UpdateAnimatorState(moving, running, !moving && !attacking);
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
