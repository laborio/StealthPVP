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
    [Header("Unstuck")]
    [SerializeField, Tooltip("Speed considered stuck when moving slowly.")] private float stuckSpeedThreshold = 0.05f;
    [SerializeField, Tooltip("Seconds of low speed before forcing a new destination.")] private float stuckTime = 1.5f;

    private Vector3 _origin;
    private CharacterHealth _health;
    private Coroutine _loopRoutine;
    private bool _animMoving;
    private float _stuckTimer;

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

        if (_loopRoutine != null)
        {
            StopCoroutine(_loopRoutine);
            _loopRoutine = null;
        }
    }

    private void Update()
    {
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

            CheckStuck();
            yield return wait;
        }
    }

    private void TrySetNextDestination()
    {
        if (!agent || !agent.isOnNavMesh)
        {
            return;
        }

        Vector3 destination = transform.position;
        bool hasDestination = false;

        if (wanderPoints != null && wanderPoints.Count > 0)
        {
            Transform point = wanderPoints[Random.Range(0, wanderPoints.Count)];
            if (point)
            {
                destination = point.position;
                hasDestination = true;
            }
        }

        if (!hasDestination)
        {
            Vector3 random = _origin + Random.insideUnitSphere * wanderRadius;
            random.y = _origin.y;
            if (NavMesh.SamplePosition(random, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                destination = hit.position;
                hasDestination = true;
            }
        }

        if (hasDestination)
        {
            agent.isStopped = false;
            agent.SetDestination(destination);
        }
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
            return;
        }

        float startThreshold = Mathf.Max(0f, moveSpeedThreshold);
        float stopThreshold = moveStopSpeedThreshold > 0f ? moveStopSpeedThreshold : startThreshold * 0.5f;
        float speedSqr = agent.velocity.sqrMagnitude;

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

        SetBoolSafe(walkingBoolName, _animMoving);
        SetBoolSafe(idleBoolName, !_animMoving);
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
