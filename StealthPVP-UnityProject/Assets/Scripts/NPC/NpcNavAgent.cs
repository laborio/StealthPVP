using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Simple wander/idle behaviour for an NPC using a NavMeshAgent.
/// Picks random waypoints or nearby positions and sometimes idles.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public class NpcNavAgent : MonoBehaviour
{
    [SerializeField] private NavMeshAgent agent;
    [SerializeField, Tooltip("Optional fixed patrol points. If empty, random points around the start position are used.")] private List<Transform> wanderPoints = new List<Transform>();
    [SerializeField, Tooltip("Radius (meters) around the start position used when picking random points.")] private float wanderRadius = 10f;
    [SerializeField, Tooltip("Seconds between destination checks.")] private float repathInterval = 0.25f;
    [SerializeField, Tooltip("Min/max idle time when arriving at a point.")] private Vector2 idleTimeRange = new Vector2(0.5f, 2f);
    [SerializeField, Tooltip("Stop moving when the NPC dies.")] private bool stopOnDeath = true;
    [Header("Animation")]
    [SerializeField, Tooltip("Animator that owns idle/walk booleans. Defaults to a child animator.")] private Animator animator;
    [SerializeField, Tooltip("Bool set true while walking.")] private string walkingBoolName = "isWalking";
    [SerializeField, Tooltip("Bool set true while idle.")] private string idleBoolName = "isIdle";
    [SerializeField, Tooltip("Speed to start walk animation.")] private float moveSpeedThreshold = 0.15f;
    [SerializeField, Tooltip("Speed to return to idle animation (hysteresis). If <= 0 uses half of start threshold.")] private float moveStopSpeedThreshold = 0.05f;
    [SerializeField, Tooltip("Scale animator speed based on NavMeshAgent velocity.")] private bool scaleAnimatorWithSpeed = false;
    [SerializeField, Tooltip("Animator speed when barely moving (normalized speed near 0).")] private float minAnimatorSpeed = 0.5f;
    [SerializeField, Tooltip("Animator speed when at max agent speed (normalized speed near 1).")] private float maxAnimatorSpeed = 1f;
    [Header("Full-Speed Gating")]
    [SerializeField, Tooltip("Only allow walk when the agent can move at (near) full speed.")] private bool requireFullSpeedToWalk = true;
    [SerializeField, Range(0.5f, 1f), Tooltip("Fraction of agent.speed required to walk.")] private float fullSpeedFraction = 0.98f;
    [SerializeField, Tooltip("Seconds below full speed before forcing idle.")] private float slowToIdleDelay = 0.05f;
    [SerializeField, Tooltip("Seconds to wait before attempting to move again.")] private float resumeDelay = 0.35f;
    [SerializeField, Tooltip("If true, stops the agent while forced idle (prevents slow creeping).")] private bool slowStopsMovement = true;
    [Header("Crowd Avoidance")]
    [SerializeField, Tooltip("If true, repath when too many NPCs are clustered on the next path segment.")] private bool avoidCrowdCongestion = true;
    [SerializeField, Tooltip("Seconds between crowd checks.")] private float crowdCheckInterval = 0.5f;
    [SerializeField, Tooltip("Radius used to count nearby NPCs along the path.")] private float crowdProbeRadius = 1.25f;
    [SerializeField, Tooltip("Neighbor count that triggers a repath.")] private int crowdNeighborThreshold = 3;
    [SerializeField, Range(0f, 1f), Tooltip("Only consider congestion when moving slower than this fraction of agent.speed.")] private float crowdSlowSpeedFraction = 0.9f;
    [SerializeField, Tooltip("Seconds before another crowd-based repath can happen.")] private float crowdRepathCooldown = 1f;
    [SerializeField, Tooltip("Number of destination samples when trying to avoid crowds.")] private int crowdDestinationSamples = 4;
    [Header("Unstuck")]
    [SerializeField, Tooltip("Speed considered stuck when moving slowly.")] private float stuckSpeedThreshold = 0.05f;
    [SerializeField, Tooltip("Seconds of low speed before forcing a new destination.")] private float stuckTime = 1.5f;

    private static readonly List<NpcNavAgent> ActiveAgents = new List<NpcNavAgent>();

    private Vector3 _origin;
    private CharacterHealth _health;
    private Coroutine _loopRoutine;
    private bool _animMoving;
    private float _stuckTimer;
    private bool _forcedIdle;
    private float _forcedIdleTimer;
    private float _resumeTimer;
    private float _crowdCheckTimer;
    private float _crowdCooldownTimer;

    private void Awake()
    {
        if (!agent)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (!animator)
        {
            animator = GetComponentInChildren<Animator>();
        }

        _health = GetComponent<CharacterHealth>();
        _origin = transform.position;
    }

    private void OnEnable()
    {
        if (_health != null)
        {
            _health.Died += OnDied;
        }

        RegisterAgent();

        if (_loopRoutine == null && agent)
        {
            _loopRoutine = StartCoroutine(WanderLoop());
        }
    }

    private void OnDisable()
    {
        if (_health != null)
        {
            _health.Died -= OnDied;
        }

        UnregisterAgent();

        if (_loopRoutine != null)
        {
            StopCoroutine(_loopRoutine);
            _loopRoutine = null;
        }
    }

    private void Update()
    {
        UpdateFullSpeedGate();
        UpdateAnimatorState();
    }

    private void OnDied(CharacterHealth _)
    {
        if (stopOnDeath && agent)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private IEnumerator WanderLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(repathInterval);
        while (true)
        {
            if (agent && agent.isOnNavMesh && !agent.pathPending)
            {
                if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.1f)
                {
                    float idle = Random.Range(idleTimeRange.x, idleTimeRange.y);
                    if (idle > 0f)
                    {
                        yield return new WaitForSeconds(idle);
                    }
                    TrySetNextDestination();
                }
            }

            CheckCrowdCongestion();
            CheckStuck();
            yield return wait;
        }
    }

    private void TrySetNextDestination(bool preferLeastCrowded = false)
    {
        if (!agent || !agent.isOnNavMesh)
        {
            return;
        }

        Vector3 destination = transform.position;
        bool hasDestination = false;
        int bestCrowd = int.MaxValue;

        if (wanderPoints != null && wanderPoints.Count > 0)
        {
            int samples = preferLeastCrowded ? Mathf.Clamp(crowdDestinationSamples, 1, wanderPoints.Count) : 1;
            for (int i = 0; i < samples; i++)
            {
                Transform point = wanderPoints[Random.Range(0, wanderPoints.Count)];
                if (!point)
                {
                    continue;
                }

                int crowd = preferLeastCrowded ? CountNearbyAgents(point.position, crowdProbeRadius, this) : 0;
                if (!hasDestination || crowd < bestCrowd)
                {
                    destination = point.position;
                    bestCrowd = crowd;
                    hasDestination = true;
                }

                if (preferLeastCrowded && bestCrowd <= 0)
                {
                    break;
                }
            }
        }

        if (!hasDestination)
        {
            int samples = preferLeastCrowded ? Mathf.Max(1, crowdDestinationSamples) : 1;
            for (int i = 0; i < samples; i++)
            {
                Vector3 random = _origin + Random.insideUnitSphere * wanderRadius;
                random.y = _origin.y;
                if (!NavMesh.SamplePosition(random, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
                {
                    continue;
                }

                int crowd = preferLeastCrowded ? CountNearbyAgents(hit.position, crowdProbeRadius, this) : 0;
                if (!hasDestination || crowd < bestCrowd)
                {
                    destination = hit.position;
                    bestCrowd = crowd;
                    hasDestination = true;
                }

                if (preferLeastCrowded && bestCrowd <= 0)
                {
                    break;
                }
            }
        }

        if (hasDestination)
        {
            agent.isStopped = false;
            agent.SetDestination(destination);
        }
    }

    private void CheckCrowdCongestion()
    {
        if (!avoidCrowdCongestion || !agent || !agent.enabled || !agent.isOnNavMesh || agent.pathPending)
        {
            return;
        }

        if (_health && _health.IsDead)
        {
            return;
        }

        if (_crowdCooldownTimer > 0f)
        {
            _crowdCooldownTimer = Mathf.Max(0f, _crowdCooldownTimer - repathInterval);
            return;
        }

        _crowdCheckTimer += repathInterval;
        if (_crowdCheckTimer < crowdCheckInterval)
        {
            return;
        }

        _crowdCheckTimer = 0f;

        if (!agent.hasPath)
        {
            return;
        }

        float maxSpeed = Mathf.Max(0.01f, agent.speed);
        float slowThreshold = maxSpeed * Mathf.Clamp01(crowdSlowSpeedFraction);
        if (agent.velocity.magnitude >= slowThreshold)
        {
            return;
        }

        Vector3 probePoint = agent.steeringTarget;
        int neighbors = CountNearbyAgents(probePoint, crowdProbeRadius, this);
        if (neighbors < crowdNeighborThreshold)
        {
            return;
        }

        TrySetNextDestination(preferLeastCrowded: true);
        _crowdCooldownTimer = crowdRepathCooldown;
    }

    private void UpdateAnimatorState()
    {
        if (!animator)
        {
            return;
        }

        if (!(agent && agent.enabled && agent.isOnNavMesh && !agent.isStopped))
        {
            _animMoving = false;
            SetBoolSafe(walkingBoolName, false);
            SetBoolSafe(idleBoolName, true);
            if (scaleAnimatorWithSpeed)
            {
                animator.speed = 1f;
            }
            return;
        }

        float speed = agent.velocity.magnitude;
        bool allowWalk = true;
        if (requireFullSpeedToWalk)
        {
            float maxSpeed = Mathf.Max(0.01f, agent.speed);
            float fullSpeedThreshold = maxSpeed * Mathf.Clamp01(fullSpeedFraction);
            allowWalk = !_forcedIdle && speed >= fullSpeedThreshold;
            _animMoving = allowWalk;
        }
        else
        {
            float startThreshold = Mathf.Max(0f, moveSpeedThreshold);
            float stopThreshold = moveStopSpeedThreshold > 0f ? moveStopSpeedThreshold : startThreshold * 0.5f;
            float speedSqr = speed * speed;

            if (_animMoving)
            {
                if (speedSqr <= stopThreshold * stopThreshold)
                {
                    _animMoving = false;
                }
            }
            else if (speedSqr > startThreshold * startThreshold)
            {
                _animMoving = true;
            }
        }

        SetBoolSafe(walkingBoolName, _animMoving);
        SetBoolSafe(idleBoolName, !_animMoving);

        if (scaleAnimatorWithSpeed && !requireFullSpeedToWalk)
        {
            float maxSpeed = Mathf.Max(0.01f, agent.speed);
            float normalizedSpeed = Mathf.Clamp01(speed / maxSpeed);
            float targetSpeed = Mathf.Lerp(minAnimatorSpeed, maxAnimatorSpeed, normalizedSpeed);
            animator.speed = targetSpeed;
        }
    }

    private void UpdateFullSpeedGate()
    {
        if (!requireFullSpeedToWalk || !agent || !agent.enabled || !agent.isOnNavMesh)
        {
            _forcedIdle = false;
            _forcedIdleTimer = 0f;
            _resumeTimer = 0f;
            return;
        }

        if (_health && _health.IsDead)
        {
            _forcedIdle = true;
            return;
        }

        bool wantsToMove = agent.hasPath && !agent.pathPending &&
                           agent.remainingDistance > agent.stoppingDistance + 0.05f;
        if (!wantsToMove)
        {
            _forcedIdle = false;
            _forcedIdleTimer = 0f;
            _resumeTimer = 0f;
            return;
        }

        if (_forcedIdle)
        {
            if (_resumeTimer > 0f)
            {
                _resumeTimer = Mathf.Max(0f, _resumeTimer - Time.deltaTime);
            }

            if (_resumeTimer <= 0f)
            {
                _forcedIdle = false;
                if (slowStopsMovement)
                {
                    agent.isStopped = false;
                }
            }

            return;
        }

        float maxSpeed = Mathf.Max(0.01f, agent.speed);
        float fullSpeedThreshold = maxSpeed * Mathf.Clamp01(fullSpeedFraction);
        bool belowFull = agent.velocity.magnitude < fullSpeedThreshold;

        if (belowFull)
        {
            _forcedIdleTimer += Time.deltaTime;
            if (_forcedIdleTimer >= slowToIdleDelay)
            {
                _forcedIdle = true;
                _resumeTimer = resumeDelay;
                _forcedIdleTimer = 0f;
                if (slowStopsMovement)
                {
                    agent.isStopped = true;
                }
            }
        }
        else
        {
            _forcedIdleTimer = 0f;
        }
    }

    private void CheckStuck()
    {
        if (!(agent && agent.enabled && agent.isOnNavMesh && agent.hasPath && !agent.pathPending))
        {
            _stuckTimer = 0f;
            return;
        }

        float speed = agent.velocity.magnitude;
        if (speed <= stuckSpeedThreshold)
        {
            _stuckTimer += repathInterval;
            if (_stuckTimer >= stuckTime)
            {
                TrySetNextDestination();
                _stuckTimer = 0f;
            }
        }
        else
        {
            _stuckTimer = 0f;
        }
    }

    private static int CountNearbyAgents(Vector3 point, float radius, NpcNavAgent ignore)
    {
        float radiusSqr = radius * radius;
        int count = 0;
        for (int i = ActiveAgents.Count - 1; i >= 0; i--)
        {
            NpcNavAgent other = ActiveAgents[i];
            if (!other)
            {
                ActiveAgents.RemoveAt(i);
                continue;
            }
            if (other == ignore)
            {
                continue;
            }

            Vector3 delta = other.transform.position - point;
            delta.y = 0f;
            if (delta.sqrMagnitude <= radiusSqr)
            {
                count++;
            }
        }

        return count;
    }

    private void RegisterAgent()
    {
        if (!ActiveAgents.Contains(this))
        {
            ActiveAgents.Add(this);
        }
    }

    private void UnregisterAgent()
    {
        ActiveAgents.Remove(this);
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
        if (!targetAnimator)
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
