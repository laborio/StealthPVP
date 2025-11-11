using System.Collections.Generic;
using UnityEngine;

public partial class SimpleCharacterController
{
    [Header("Bench Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private string benchTag = "Bench";
    [SerializeField] private string sitPointTag = "BenchSitPoint";
    [SerializeField] private string sitPointName = "SitSpot";
    [SerializeField, Range(0.5f, 10f)] private float benchApproachSpeed = 4f;
    [SerializeField, Range(0.01f, 0.5f)] private float seatSnapDistance = 0.05f;
    [SerializeField] private float benchAlignmentSpeed = 720f;
    [SerializeField, Range(0.1f, 3f)] private float standToSitAnimSpeed = 1f;
    [SerializeField, Range(0f, 1f)] private float collisionRestoreDelay = 0.15f;
    [Header("Bench UI")]
    [SerializeField] private GameObject sitHintUI;

    private enum SeatingState
    {
        Standing,
        MovingToSeat,
        PlayingStandToSit
    }

    private SeatingState _seatingState = SeatingState.Standing;
    private Collider _activeBench;
    private readonly List<Transform> _benchSitPoints = new List<Transform>();
    private readonly List<Collider> _benchColliders = new List<Collider>();
    private Transform _currentSitPoint;
    private Vector3 _sitTargetPosition;
    private Quaternion _sitTargetRotation;
    private bool _benchCollisionIgnored;
    private bool _pendingCollisionRestore;
    private float _collisionRestoreTimer;
    private bool _hintSuppressedUntilExit;

    partial void OnBenchAwake()
    {
        _hintSuppressedUntilExit = false;
        SetSitHintVisible(false);
    }

    private void HandleBenchInput(bool movementRequested, bool isGrounded)
    {
        bool interactPressed = Input.GetKeyDown(interactKey);
        bool benchAvailable = _activeBench != null;

        switch (_seatingState)
        {
            case SeatingState.Standing:
                if (interactPressed && benchAvailable && isGrounded)
                {
                    BeginMoveToSeat();
                }
                break;
            case SeatingState.MovingToSeat:
            case SeatingState.PlayingStandToSit:
                if (movementRequested || interactPressed)
                {
                    CancelSeatingSequence();
                }
                break;
        }
    }

    private void BeginMoveToSeat()
    {
        if (!_activeBench)
        {
            return;
        }

        Transform targetPoint = FindNearestSitPoint();
        if (!targetPoint && _activeBench)
        {
            targetPoint = _activeBench.transform;
        }

        if (!targetPoint)
        {
            return;
        }

        _currentSitPoint = targetPoint;
        _sitTargetPosition = targetPoint.position;
        _sitTargetRotation = GetFlatRotation(targetPoint);
        _seatingState = SeatingState.MovingToSeat;
        _hasMoveTarget = false;
        _isDashing = false;
        _isJumping = false;
        _isFalling = false;
        SuppressSitHintUntilExit();
        ApplyBenchCollisionState(true);
    }

    private void SnapToSeatPoint()
    {
        Vector3 targetPosition = _sitTargetPosition;
        if (_currentSitPoint)
        {
            targetPosition = _currentSitPoint.position;
            _sitTargetRotation = GetFlatRotation(_currentSitPoint);
        }
        else if (_activeBench)
        {
            targetPosition = _activeBench.transform.position;
            _sitTargetRotation = GetFlatRotation(_activeBench.transform);
        }

        if (_characterController)
        {
            bool wasEnabled = _characterController.enabled;
            if (wasEnabled)
            {
                _characterController.enabled = false;
            }
            transform.position = targetPosition;
            if (wasEnabled)
            {
                _characterController.enabled = true;
            }
        }
        else
        {
            transform.position = targetPosition;
        }

        _currentPlanarVelocity = Vector3.zero;
        _verticalVelocity = 0f;
        StartStandToSitAnimation();
    }

    private void StartStandToSitAnimation()
    {
        _seatingState = SeatingState.PlayingStandToSit;
        characterAnimations?.SetSittingState(true, standToSitAnimSpeed);
    }

    private void CancelSeatingSequence()
    {
        if (_seatingState == SeatingState.Standing)
        {
            return;
        }

        _seatingState = SeatingState.Standing;
        _currentSitPoint = null;
        _sitTargetRotation = Quaternion.identity;
        ScheduleBenchCollisionRestore();
        characterAnimations?.SetSittingState(false, standToSitAnimSpeed);
        RefreshSitHintVisibility();
    }

    private void UpdateSeatingState(float deltaTime)
    {
        if (_seatingState == SeatingState.Standing)
        {
            return;
        }

        AlignWithSitTarget(deltaTime);
    }

    private void AlignWithSitTarget(float deltaTime)
    {
        if (benchAlignmentSpeed <= 0f)
        {
            return;
        }

        bool movingToSeat = _seatingState == SeatingState.MovingToSeat;
        Vector3 forward;

        if (movingToSeat)
        {
            forward = _sitTargetPosition - transform.position;
            forward.y = 0f;
        }
        else
        {
            Quaternion targetRotation = _sitTargetRotation;
            if (targetRotation == Quaternion.identity)
            {
                Transform fallback = _currentSitPoint ? _currentSitPoint : (_activeBench ? _activeBench.transform : null);
                if (fallback)
                {
                    targetRotation = GetFlatRotation(fallback);
                }
            }

            forward = targetRotation * Vector3.forward;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion flatTarget = Quaternion.LookRotation(forward.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, flatTarget, benchAlignmentSpeed * deltaTime);
    }

    private void ProcessBenchCollisionRestore(float deltaTime)
    {
        if (!_pendingCollisionRestore)
        {
            return;
        }

        _collisionRestoreTimer -= deltaTime;
        if (_collisionRestoreTimer <= 0f)
        {
            _pendingCollisionRestore = false;
            ApplyBenchCollisionState(false);
        }
    }

    private Transform FindNearestSitPoint()
    {
        Transform best = null;
        float bestDistance = float.MaxValue;

        for (int i = _benchSitPoints.Count - 1; i >= 0; i--)
        {
            Transform point = _benchSitPoints[i];
            if (!point)
            {
                _benchSitPoints.RemoveAt(i);
                continue;
            }

            float sqrDistance = (point.position - transform.position).sqrMagnitude;
            if (sqrDistance < bestDistance)
            {
                bestDistance = sqrDistance;
                best = point;
            }
        }

        return best;
    }

    private Quaternion GetFlatRotation(Transform source)
    {
        if (!source)
        {
            return Quaternion.identity;
        }

        Vector3 forward = source.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return Quaternion.identity;
        }

        Vector3 facing = (-forward).normalized;
        return Quaternion.LookRotation(facing, Vector3.up);
    }

    private void CacheBenchColliders(Transform benchTransform)
    {
        if (!benchTransform)
        {
            return;
        }

        Collider[] colliders = benchTransform.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            if (!col || !col.enabled || col.isTrigger)
            {
                continue;
            }

            _benchColliders.Add(col);
        }
    }

    private void CacheBenchSitPoints(Collider benchCollider)
    {
        _benchSitPoints.Clear();
        _benchColliders.Clear();
        _currentSitPoint = null;
        _sitTargetRotation = Quaternion.identity;

        if (!benchCollider)
        {
            return;
        }

        Transform benchTransform = benchCollider.transform;
        if (!benchTransform)
        {
            return;
        }

        CacheBenchColliders(benchTransform);

        Transform[] children = benchTransform.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (!child || child == benchTransform)
            {
                continue;
            }

            bool matchesTag = !string.IsNullOrEmpty(sitPointTag) && child.CompareTag(sitPointTag);
            bool matchesName = string.IsNullOrEmpty(sitPointName) || child.name == sitPointName;
            if ((matchesTag || string.IsNullOrEmpty(sitPointTag)) && matchesName)
            {
                _benchSitPoints.Add(child);
            }
        }

        if (_benchSitPoints.Count == 0 && !string.IsNullOrEmpty(sitPointTag))
        {
            GameObject[] taggedPoints = GameObject.FindGameObjectsWithTag(sitPointTag);
            if (taggedPoints != null && taggedPoints.Length > 0)
            {
                Bounds benchBounds = benchCollider.bounds;
                float flatExtent = Mathf.Max(benchBounds.extents.x, benchBounds.extents.z) + 1.5f;
                float maxDistanceSqr = flatExtent * flatExtent;
                Vector3 benchCenter = benchBounds.center;
                benchCenter.y = 0f;

                foreach (GameObject pointObject in taggedPoints)
                {
                    if (!pointObject)
                    {
                        continue;
                    }

                    Transform pointTransform = pointObject.transform;
                    if (!pointTransform)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(sitPointName) && pointTransform.name != sitPointName)
                    {
                        continue;
                    }

                    Vector3 pointPosition = pointTransform.position;
                    pointPosition.y = 0f;
                    if ((pointPosition - benchCenter).sqrMagnitude <= maxDistanceSqr)
                    {
                        _benchSitPoints.Add(pointTransform);
                    }
                }
            }
        }
    }

    private void ClearBenchTracking()
    {
        ApplyBenchCollisionState(false);
        _benchSitPoints.Clear();
        _benchColliders.Clear();
        _currentSitPoint = null;
        _sitTargetRotation = Quaternion.identity;
        _sitTargetPosition = transform.position;
        _pendingCollisionRestore = false;
        _collisionRestoreTimer = 0f;
        _hintSuppressedUntilExit = false;
        SetSitHintVisible(false);
        characterAnimations?.SetSittingState(false, standToSitAnimSpeed);
    }

    private void ApplyBenchCollisionState(bool ignored)
    {
        if (_benchCollisionIgnored == ignored)
        {
            return;
        }

        _benchCollisionIgnored = ignored;
        _pendingCollisionRestore = false;
        _collisionRestoreTimer = 0f;

        if (!_characterController)
        {
            return;
        }

        foreach (Collider benchCollider in _benchColliders)
        {
            if (!benchCollider)
            {
                continue;
            }

            Physics.IgnoreCollision(_characterController, benchCollider, ignored);
        }
    }

    private void ScheduleBenchCollisionRestore()
    {
        if (!_benchCollisionIgnored)
        {
            return;
        }

        if (collisionRestoreDelay <= 0f)
        {
            ApplyBenchCollisionState(false);
            return;
        }

        _pendingCollisionRestore = true;
        _collisionRestoreTimer = collisionRestoreDelay;
    }

    private void SuppressSitHintUntilExit()
    {
        _hintSuppressedUntilExit = true;
        SetSitHintVisible(false);
    }

    private void RefreshSitHintVisibility()
    {
        if (!sitHintUI)
        {
            return;
        }

        bool shouldShow = !_hintSuppressedUntilExit && _seatingState == SeatingState.Standing && _activeBench;
        SetSitHintVisible(shouldShow);
    }

    private void SetSitHintVisible(bool visible)
    {
        if (!sitHintUI || sitHintUI.activeSelf == visible)
        {
            return;
        }

        sitHintUI.SetActive(visible);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsBenchCollider(other))
        {
            _activeBench = other;
            CacheBenchSitPoints(other);
            RefreshSitHintVisibility();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsBenchCollider(other))
        {
            return;
        }

        CancelSeatingSequence();
        if (_activeBench == other)
        {
            _activeBench = null;
            ClearBenchTracking();
        }
    }

    private bool IsBenchCollider(Collider collider)
    {
        if (!collider || string.IsNullOrEmpty(benchTag))
        {
            return false;
        }

        return collider.CompareTag(benchTag);
    }
}
