using System.Collections.Generic;
using UnityEngine;

public class NPCController : MonoBehaviour
{
    private enum NPCState
    {
        Wandering,
        Idle,
        MovingToGroup,
        InGroup
    }

    private sealed class NPCGroup
    {
        public readonly List<NPCController> Members = new();
        public Vector3 Center;
        public Vector3[] Anchors = System.Array.Empty<Vector3>();
        public float DisbandTime;
    }

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2.2f;
    [SerializeField] private float acceleration = 6f;
    [SerializeField] private float rotationSpeed = 540f;
    [SerializeField] private Vector2 playAreaSize = new Vector2(45f, 45f);
    [SerializeField] private float areaPadding = 0.5f;
    [SerializeField] private float stopDistance = 0.3f;
    [SerializeField] private float separationDistance = 1.2f;
    [SerializeField] private float separationWeight = 3f;

    [Header("Behaviour")]
    [SerializeField] private Vector2 regroupDecisionInterval = new Vector2(6f, 10f);
    [SerializeField] private Vector2 groupDuration = new Vector2(5f, 12f);
    [SerializeField] private float groupSearchRadius = 4f;
    [SerializeField] private float groupSpacing = 1.3f;
    [SerializeField] private Vector2 idleDuration = new Vector2(2f, 4f);
    [SerializeField, Range(0f, 1f)] private float idleChance = 0.35f;

    [Header("Avoidance")]
    [SerializeField] private float wallCheckHeightOffset = 0.5f;
    [SerializeField] private float wallAvoidanceRadius = 0.4f;
    [SerializeField] private float wallAvoidanceDistance = 1.5f;
    [SerializeField] private LayerMask wallCollisionMask = ~0;
    [SerializeField, Range(1, 10)] private int avoidanceScanSteps = 4;
    [SerializeField] private float avoidanceAngleStep = 30f;
    [SerializeField] private float stuckTimeout = 1.5f;
    [SerializeField, Range(1, 20)] private int wanderSampleAttempts = 8;

    private static readonly List<NPCController> ActiveNPCs = new();
    private static readonly List<NPCGroup> ActiveGroups = new();
    private static readonly Collider[] OverlapBuffer = new Collider[16];
    private static readonly int[] AllowedGroupSizes = { 5, 3, 2 };

    private NPCState _state = NPCState.Wandering;
    private Vector3 _targetPosition;
    private Vector3 _currentVelocity;
    private float _stateEndTime;
    private float _nextDecisionTime;
    private float _stuckTimer;

    private NPCGroup _currentGroup;
    private Vector3 _groupAnchor;

    private void OnEnable()
    {
        ActiveNPCs.Add(this);
        PickNewWanderTarget();
        ScheduleNextDecision();
    }

    private void OnDisable()
    {
        ActiveNPCs.Remove(this);
        if (_currentGroup != null)
        {
            NPCGroup group = _currentGroup;
            group.Members.Remove(this);
            if (group.Members.Count == 0)
            {
                ActiveGroups.Remove(group);
            }
        }
    }

    private void Update()
    {
        switch (_state)
        {
            case NPCState.InGroup:
                MaintainGroupPosition();
                break;
            case NPCState.Idle:
                // Stay put, but still run separation so others can push away slightly.
                _currentVelocity = Vector3.zero;
                break;
            default:
                MoveTowardsTarget();
                break;
        }

        ApplySeparation();
        UpdateRotation();
        CheckStateProgress();

        if (_state == NPCState.Wandering && Time.time >= _nextDecisionTime)
        {
            if (!TryStartGroupMoment())
            {
                TryStartIdleMoment();
            }

            ScheduleNextDecision();
        }
    }

    private void MoveTowardsTarget()
    {
        Vector3 desired = _targetPosition - transform.position;
        desired.y = 0f;

        if (desired.sqrMagnitude > 0.0001f)
        {
            desired = desired.normalized * walkSpeed;
        }
        else
        {
            desired = Vector3.zero;
        }

        Vector3 resolved = ResolveWallAvoidance(desired);
        bool hasDesiredMovement = resolved.sqrMagnitude > 0.0001f;

        _currentVelocity = Vector3.MoveTowards(_currentVelocity, resolved, acceleration * Time.deltaTime);
        transform.position += _currentVelocity * Time.deltaTime;

        bool isAdvancing = hasDesiredMovement && _currentVelocity.sqrMagnitude > 0.01f;
        HandlePotentialStuck(isAdvancing);
    }

    private void MaintainGroupPosition()
    {
        Vector3 toAnchor = _groupAnchor - transform.position;
        toAnchor.y = 0f;

        bool isAdvancing = false;

        if (toAnchor.sqrMagnitude > 0.0001f)
        {
            Vector3 desired = toAnchor.normalized * walkSpeed;
            desired = ResolveWallAvoidance(desired);
            bool hasDesired = desired.sqrMagnitude > 0.0001f;

            _currentVelocity = Vector3.MoveTowards(_currentVelocity, desired, acceleration * Time.deltaTime);
            transform.position += _currentVelocity * Time.deltaTime;

            isAdvancing = hasDesired && _currentVelocity.sqrMagnitude > 0.01f;
        }
        else
        {
            _currentVelocity = Vector3.zero;
        }

        if (_currentGroup != null)
        {
            UpdateGroupCenter(_currentGroup);
            Vector3 lookTarget = _currentGroup.Center - transform.position;
            lookTarget.y = 0f;
            if (lookTarget.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookTarget.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        HandlePotentialStuck(isAdvancing);
    }

    private void ApplySeparation()
    {
        Vector3 separation = Vector3.zero;

        for (int i = 0; i < ActiveNPCs.Count; i++)
        {
            NPCController other = ActiveNPCs[i];
            if (other == this)
            {
                continue;
            }

            Vector3 toOther = transform.position - other.transform.position;
            toOther.y = 0f;
            float distance = toOther.magnitude;

            if (distance < 0.001f || distance > separationDistance)
            {
                continue;
            }

            float strength = (separationDistance - distance) / separationDistance;
            separation += toOther.normalized * strength;
        }

        if (separation.sqrMagnitude > 0.0001f)
        {
            _currentVelocity += separation * separationWeight * Time.deltaTime;
            Vector3 planar = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);
            if (planar.magnitude > walkSpeed)
            {
                planar = planar.normalized * walkSpeed;
                _currentVelocity = new Vector3(planar.x, _currentVelocity.y, planar.z);
            }
        }
    }

    private void UpdateRotation()
    {
        if (_state == NPCState.Idle && _currentVelocity.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 planarVelocity = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);
        if (planarVelocity.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void CheckStateProgress()
    {
        switch (_state)
        {
            case NPCState.Wandering:
                if (HasReached(_targetPosition))
                {
                    PickNewWanderTarget();
                }
                break;

            case NPCState.MovingToGroup:
                if (_currentGroup == null || Time.time >= _currentGroup.DisbandTime)
                {
                    LeaveGroup();
                }
                else if (HasReached(_groupAnchor))
                {
                    _state = NPCState.InGroup;
                    _currentVelocity = Vector3.zero;
                }
                break;

            case NPCState.InGroup:
                if (_currentGroup == null || Time.time >= _currentGroup.DisbandTime)
                {
                    LeaveGroup();
                }
                break;

            case NPCState.Idle:
                if (Time.time >= _stateEndTime)
                {
                    ResumeWandering();
                }
                break;
        }
    }

    private void PickNewWanderTarget()
    {
        Vector2 halfSize = playAreaSize * 0.5f - Vector2.one * areaPadding;
        Vector3 chosen = transform.position;

        for (int attempt = 0; attempt < wanderSampleAttempts; attempt++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(-halfSize.x, halfSize.x),
                transform.position.y,
                Random.Range(-halfSize.y, halfSize.y));

            if (!IsPositionBlocked(candidate))
            {
                chosen = candidate;
                break;
            }

            if (attempt == wanderSampleAttempts - 1)
            {
                chosen = candidate;
            }
        }

        _targetPosition = chosen;
        _stuckTimer = 0f;
    }

    private void ResumeWandering()
    {
        _currentGroup = null;
        _currentVelocity = Vector3.zero;
        _state = NPCState.Wandering;
        PickNewWanderTarget();
        ScheduleNextDecision();
    }

    private void ScheduleNextDecision()
    {
        _nextDecisionTime = Time.time + Random.Range(regroupDecisionInterval.x, regroupDecisionInterval.y);
    }

    private void TryStartIdleMoment()
    {
        if (Random.value > idleChance)
        {
            return;
        }

        _state = NPCState.Idle;
        _currentVelocity = Vector3.zero;
        _stateEndTime = Time.time + Random.Range(idleDuration.x, idleDuration.y);
    }

    private bool TryStartGroupMoment()
    {
        List<NPCController> nearby = GatherNearbyCandidates();
        if (nearby.Count == 0)
        {
            return false;
        }

        foreach (int size in AllowedGroupSizes)
        {
            if (size - 1 > nearby.Count)
            {
                continue;
            }

            List<NPCController> members = new(size) { this };
            for (int i = 0; i < size - 1; i++)
            {
                members.Add(nearby[i]);
            }

            CreateGroup(members);
            return true;
        }

        return false;
    }

    private List<NPCController> GatherNearbyCandidates()
    {
        List<NPCController> candidates = new();

        for (int i = 0; i < ActiveNPCs.Count; i++)
        {
            NPCController other = ActiveNPCs[i];
            if (other == this)
            {
                continue;
            }

            if (other._state != NPCState.Wandering)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, other.transform.position);
            if (distance > groupSearchRadius)
            {
                continue;
            }

            candidates.Add(other);
        }

        candidates.Sort((a, b) =>
            Vector3.SqrMagnitude(a.transform.position - transform.position)
                .CompareTo(Vector3.SqrMagnitude(b.transform.position - transform.position)));

        return candidates;
    }

    private void CreateGroup(List<NPCController> members)
    {
        Vector3 center = Vector3.zero;
        for (int i = 0; i < members.Count; i++)
        {
            center += members[i].transform.position;
        }

        center /= members.Count;

        NPCGroup group = new NPCGroup
        {
            Center = center,
            DisbandTime = Time.time + Random.Range(groupDuration.x, groupDuration.y),
            Anchors = CreateGroupAnchors(members.Count, center, Random.Range(0f, 360f))
        };

        for (int i = 0; i < members.Count; i++)
        {
            NPCController member = members[i];
            member.EnterGroup(group, i);
            group.Members.Add(member);
        }

        ActiveGroups.Add(group);
    }

    private Vector3[] CreateGroupAnchors(int count, Vector3 center, float rotationDegrees)
    {
        Vector3[] anchors = new Vector3[count];
        float angleStep = Mathf.PI * 2f / count;
        float radius = Mathf.Max(groupSpacing, groupSpacing * 0.5f * count / Mathf.PI);
        Quaternion rotation = Quaternion.Euler(0f, rotationDegrees, 0f);

        for (int i = 0; i < count; i++)
        {
            float angle = angleStep * i;
            Vector3 localOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            anchors[i] = center + rotation * localOffset;
        }

        return anchors;
    }

    private void EnterGroup(NPCGroup group, int index)
    {
        _currentGroup = group;
        _groupAnchor = group.Anchors[index];
        _state = NPCState.MovingToGroup;
        _targetPosition = _groupAnchor;
    }

    private void LeaveGroup()
    {
        if (_currentGroup != null)
        {
            _currentGroup.Members.Remove(this);
            if (_currentGroup.Members.Count == 0)
            {
                ActiveGroups.Remove(_currentGroup);
            }
        }

        ResumeWandering();
    }

    private void UpdateGroupCenter(NPCGroup group)
    {
        if (group.Members.Count == 0)
        {
            return;
        }

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < group.Members.Count; i++)
        {
            sum += group.Members[i].transform.position;
        }

        group.Center = sum / group.Members.Count;
    }

    private bool HasReached(Vector3 target)
    {
        Vector3 delta = target - transform.position;
        delta.y = 0f;
        return delta.magnitude <= stopDistance;
    }

    private Vector3 ResolveWallAvoidance(Vector3 desiredVelocity)
    {
        if (desiredVelocity.sqrMagnitude < 0.0001f)
        {
            return desiredVelocity;
        }

        Vector3 direction = desiredVelocity.normalized;
        float speed = desiredVelocity.magnitude;

        if (!DetectWallInDirection(direction, out RaycastHit hit))
        {
            return desiredVelocity;
        }

        Vector3 planarNormal = hit.normal;
        planarNormal.y = 0f;

        if (planarNormal.sqrMagnitude > 0.0001f)
        {
            Vector3 tangent = Vector3.Cross(Vector3.up, planarNormal).normalized;
            if (Vector3.Dot(tangent, direction) < 0f)
            {
                tangent = -tangent;
            }

            if (!DetectWallInDirection(tangent))
            {
                return tangent * speed;
            }
        }

        Vector3 bestDirection = Vector3.zero;
        float bestDot = -1f;

        for (int step = 1; step <= avoidanceScanSteps; step++)
        {
            float angle = avoidanceAngleStep * step;
            Vector3 dirA = Quaternion.Euler(0f, angle, 0f) * direction;
            Vector3 dirB = Quaternion.Euler(0f, -angle, 0f) * direction;

            if (!DetectWallInDirection(dirA))
            {
                float dot = Vector3.Dot(dirA, direction);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestDirection = dirA;
                }
            }

            if (!DetectWallInDirection(dirB))
            {
                float dot = Vector3.Dot(dirB, direction);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestDirection = dirB;
                }
            }
        }

        if (bestDot > 0f)
        {
            return bestDirection.normalized * speed;
        }

        return Vector3.zero;
    }

    private bool DetectWallInDirection(Vector3 direction)
    {
        return DetectWallInDirection(direction, out _);
    }

    private bool DetectWallInDirection(Vector3 direction, out RaycastHit hit)
    {
        Vector3 origin = transform.position + Vector3.up * wallCheckHeightOffset;
        if (Physics.SphereCast(origin, wallAvoidanceRadius, direction, out hit, wallAvoidanceDistance, wallCollisionMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && hit.collider.CompareTag("Wall"))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPositionBlocked(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * wallCheckHeightOffset;
        int count = Physics.OverlapSphereNonAlloc(origin, wallAvoidanceRadius, OverlapBuffer, wallCollisionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = OverlapBuffer[i];
            if (hit != null && hit.CompareTag("Wall"))
            {
                return true;
            }
        }

        return false;
    }

    private void HandlePotentialStuck(bool isAdvancing)
    {
        if (_state == NPCState.InGroup || _state == NPCState.Idle)
        {
            _stuckTimer = 0f;
            return;
        }

        if (isAdvancing)
        {
            _stuckTimer = 0f;
            return;
        }

        _stuckTimer += Time.deltaTime;
        if (_stuckTimer < stuckTimeout)
        {
            return;
        }

        _stuckTimer = 0f;

        switch (_state)
        {
            case NPCState.Wandering:
                PickNewWanderTarget();
                break;
            case NPCState.MovingToGroup:
                LeaveGroup();
                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(playAreaSize.x, 0.1f, playAreaSize.y));
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, groupSearchRadius);
    }
}
