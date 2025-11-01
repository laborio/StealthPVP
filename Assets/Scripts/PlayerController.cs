using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float runMultiplier = 1.5f;

    [Header("Acceleration")]
    [SerializeField] private float walkAcceleration = 20f;
    [SerializeField] private float runAcceleration = 30f;

    [Header("Rotation")]
    [SerializeField] private float walkRotationSpeed = 720f;
    [SerializeField] private float runRotationSpeed = 900f;
    [SerializeField] private float climbRotationSpeed = 540f;
    [SerializeField] private float climbSpeed = 3f;
    [SerializeField] private float climbAnimationBaseSpeed = 1f;
    [SerializeField] private float finishClimbSpeed = 2f;
    [SerializeField] private float finishClimbAnimationBaseSpeed = 1f;

    [Header("Input")]
    [SerializeField] private KeyCode runKey = KeyCode.Space;
    [SerializeField] private KeyCode stopKey = KeyCode.S;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private float walkAnimationBaseSpeed = 1f;
    [SerializeField] private float runAnimationBaseSpeed = 1f;

    [Header("Click To Move")]
    [SerializeField] private float stopDistance = 0.2f;
    [SerializeField] private float maximumRayDistance = 250f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private ClickMoveMarkerPool moveMarkerPool;
    [SerializeField] private float markerHeight = 0.1f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem runParticleSystem;

    private static readonly int IsRunningHash = Animator.StringToHash("isRunning");
    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    private static readonly int IsIdleHash = Animator.StringToHash("isIdle");
    private static readonly int IsClimbingHash = Animator.StringToHash("isClimbing");
    private static readonly int FinishClimbingHash = Animator.StringToHash("FinishClimbing");

    private Vector3 _moveTarget;
    private bool _hasMoveTarget;
    private Vector3 _currentVelocity;
    private bool _runInputActive;
    private bool _isRunningThisFrame;
    private bool _isClimbing;
    private Vector3 _climbFacingDirection;
    private bool _hasClimbFacingDirection;
    private HashSet<Collider> _activeClimbContacts;
    private int _climbableLayer;
    private bool _finishClimbing;

    private void Awake()
    {
        _activeClimbContacts = new HashSet<Collider>();
        _climbableLayer = LayerMask.NameToLayer("Climbable");
    }

    private void Update()
    {
        HandleStopCommand();
        HandleClickToMoveInput();

        if (_isClimbing)
        {
            _hasMoveTarget = false;
        }

        _runInputActive = Input.GetKey(runKey);

        MovePlayer();

        UpdateAnimationState();

        if (_isClimbing || _finishClimbing)
        {
            HandleClimbRotation();
        }
        else
        {
            HandleRotation();
        }
    }

    private void HandleStopCommand()
    {
        if (Input.GetKeyDown(stopKey))
        {
            _hasMoveTarget = false;
            _currentVelocity = Vector3.zero;
        }
    }

    private void HandleClickToMoveInput()
    {
        if (!Input.GetMouseButtonDown(1))
        {
            return;
        }

        Camera currentCamera = Camera.main;
        if (currentCamera == null)
        {
            Debug.LogWarning("PlayerController: No camera tagged as MainCamera found for click-to-move.");
            return;
        }

        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, maximumRayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            _moveTarget = hitInfo.point;
            _hasMoveTarget = true;
            SpawnMoveMarker(hitInfo.point);
        }
    }

    private void MovePlayer()
    {
        if (_isClimbing)
        {
            _currentVelocity = Vector3.up * climbSpeed;
            transform.position += _currentVelocity * Time.deltaTime;
            _isRunningThisFrame = false;
            return;
        }
        if (_finishClimbing)
        {
            _currentVelocity = Vector3.up * finishClimbSpeed;
            transform.position += _currentVelocity * Time.deltaTime;
            _isRunningThisFrame = false;
            return;
        }

        Vector3 desiredVelocity = Vector3.zero;

        bool wantsToRun = _runInputActive && _hasMoveTarget;
        float targetSpeed = walkSpeed * (wantsToRun ? runMultiplier : 1f);
        float currentAcceleration = wantsToRun ? runAcceleration : walkAcceleration;

        if (_hasMoveTarget)
        {
            Vector3 toTarget = _moveTarget - transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude <= stopDistance)
            {
                _hasMoveTarget = false;
            }
            else
            {
                desiredVelocity = toTarget.normalized * targetSpeed;
            }
        }

        _currentVelocity = Vector3.MoveTowards(_currentVelocity, desiredVelocity, currentAcceleration * Time.deltaTime);
        transform.position += _currentVelocity * Time.deltaTime;

        if (_hasMoveTarget)
        {
            Vector3 remaining = _moveTarget - transform.position;
            remaining.y = 0f;
            if (remaining.magnitude <= stopDistance)
            {
                _hasMoveTarget = false;
                _currentVelocity = Vector3.zero;
            }
        }

        _isRunningThisFrame = wantsToRun && _currentVelocity.sqrMagnitude > 0.0001f;
    }

    private void UpdateAnimationState()
    {
        if (_isClimbing)
        {
            UpdateRunParticles(false);

            if (!animator)
            {
                return;
            }

            animator.SetBool(IsClimbingHash, true);
            animator.SetBool(FinishClimbingHash, false);
            animator.SetBool(IsRunningHash, false);
            animator.SetBool(IsWalkingHash, false);
            animator.SetBool(IsIdleHash, false);
            animator.speed = climbAnimationBaseSpeed;
            return;
        }
        if (_finishClimbing)
        {
            UpdateRunParticles(false);

            if (!animator)
            {
                return;
            }

            animator.SetBool(IsClimbingHash, false);
            animator.SetBool(FinishClimbingHash, true);
            animator.SetBool(IsRunningHash, false);
            animator.SetBool(IsWalkingHash, false);
            animator.SetBool(IsIdleHash, false);
            animator.speed = finishClimbAnimationBaseSpeed;
            return;
        }

        Vector3 planarVelocity = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);
        float speed = planarVelocity.magnitude;
        bool isMoving = speed > 0.01f;

        bool isRunning = _isRunningThisFrame && isMoving;
        bool isWalking = !isRunning && isMoving;
        bool isIdle = !isRunning && !isWalking;

        UpdateRunParticles(isRunning);

        if (!animator)
        {
            return;
        }

        animator.SetBool(IsClimbingHash, false);
        animator.SetBool(FinishClimbingHash, false);
        animator.SetBool(IsRunningHash, isRunning);
        animator.SetBool(IsWalkingHash, isWalking);
        animator.SetBool(IsIdleHash, isIdle);

        if (isRunning)
        {
            float runSpeed = Mathf.Max(0.001f, walkSpeed * runMultiplier);
            float normalized = Mathf.Clamp(speed / runSpeed, 0f, 2f);
            animator.speed = runAnimationBaseSpeed * normalized;
        }
        else if (isWalking)
        {
            float normalized = Mathf.Clamp(speed / Mathf.Max(0.001f, walkSpeed), 0f, 2f);
            animator.speed = walkAnimationBaseSpeed * normalized;
        }
        else
        {
            animator.speed = 1f;
        }
    }

    private void UpdateRunParticles(bool shouldBeActive)
    {
        if (!runParticleSystem)
        {
            return;
        }

        if (shouldBeActive)
        {
            ParticleSystem.EmissionModule emission = runParticleSystem.emission;
            if (!emission.enabled)
            {
                emission.enabled = true;
            }
        }
        else
        {
            ParticleSystem.EmissionModule emission = runParticleSystem.emission;
            if (emission.enabled)
            {
                emission.enabled = false;
            }
        }
    }

    private void HandleClimbRotation()
    {
        if (!_hasClimbFacingDirection)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(_climbFacingDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, climbRotationSpeed * Time.deltaTime);
    }

    private void HandleRotation()
    {
        Vector3 planarVelocity = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);
        if (planarVelocity.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float currentRotationSpeed = _isRunningThisFrame ? runRotationSpeed : walkRotationSpeed;
        Quaternion targetRotation = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
        Quaternion newRotation = Quaternion.RotateTowards(transform.rotation, targetRotation, currentRotationSpeed * Time.deltaTime);
        transform.rotation = newRotation;
    }

    private void TryBeginClimb(Collision collision)
    {
        if (_finishClimbing)
        {
            return;
        }

        Collider collider = collision.collider;
        if (!IsClimbableCollider(collider))
        {
            return;
        }

        _activeClimbContacts.Add(collider);
        _isClimbing = true;
        _currentVelocity = Vector3.zero;
        _isRunningThisFrame = false;
        UpdateClimbFacingDirection(collision);
    }

    private void UpdateClimbFacingDirection(Collision collision)
    {
        _hasClimbFacingDirection = false;

        if (collision.contactCount > 0)
        {
            Vector3 averagedNormal = Vector3.zero;
            ContactPoint[] contacts = collision.contacts;
            for (int i = 0; i < contacts.Length; i++)
            {
                averagedNormal += contacts[i].normal;
            }

            if (averagedNormal.sqrMagnitude > 0.0001f)
            {
                averagedNormal.Normalize();
                Vector3 facing = -averagedNormal;
                facing.y = 0f;
                if (facing.sqrMagnitude > 0.0001f)
                {
                    _climbFacingDirection = facing.normalized;
                    _hasClimbFacingDirection = true;
                    return;
                }
            }
        }

        Vector3 directionToCollider = collision.collider.transform.position - transform.position;
        directionToCollider.y = 0f;
        if (directionToCollider.sqrMagnitude > 0.0001f)
        {
            _climbFacingDirection = directionToCollider.normalized;
            _hasClimbFacingDirection = true;
        }
    }

    private bool IsClimbableCollider(Collider collider)
    {
        if (!collider || !collider.CompareTag("Wall"))
        {
            return false;
        }

        if (_climbableLayer != -1 && collider.gameObject.layer != _climbableLayer)
        {
            return false;
        }

        return true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryBeginClimb(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryBeginClimb(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!IsClimbableCollider(collision.collider))
        {
            return;
        }

        _activeClimbContacts.Remove(collision.collider);
        if (_activeClimbContacts.Count == 0 && !_finishClimbing)
        {
            _isClimbing = false;
            _hasClimbFacingDirection = false;
            _climbFacingDirection = Vector3.zero;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("StopClimb"))
        {
            return;
        }

        FinishClimb();
    }

    private void FinishClimb()
    {
        if (_finishClimbing || !_isClimbing)
        {
            return;
        }

        _finishClimbing = true;
        _isClimbing = false;
        _currentVelocity = Vector3.zero;
        _hasMoveTarget = false;
        _activeClimbContacts.Clear();

        if (animator)
        {
            animator.SetBool(IsClimbingHash, false);
            animator.SetBool(FinishClimbingHash, true);
        }
    }

    private void SpawnMoveMarker(Vector3 position)
    {
        if (moveMarkerPool == null)
        {
            return;
        }

        Vector3 markerPosition = position;
        markerPosition.y = markerHeight;
        moveMarkerPool.SpawnMarker(markerPosition);
    }

    private void OnDisable()
    {
        UpdateRunParticles(false);
        _isClimbing = false;
        _hasClimbFacingDirection = false;
        _climbFacingDirection = Vector3.zero;
        _finishClimbing = false;
        _activeClimbContacts?.Clear();

        if (animator)
        {
            animator.SetBool(IsClimbingHash, false);
            animator.SetBool(FinishClimbingHash, false);
        }
    }

    private void OnValidate()
    {
        walkSpeed = Mathf.Max(0f, walkSpeed);
        runMultiplier = Mathf.Max(1f, runMultiplier);
        walkAcceleration = Mathf.Max(0f, walkAcceleration);
        runAcceleration = Mathf.Max(0f, runAcceleration);
        walkRotationSpeed = Mathf.Max(0f, walkRotationSpeed);
        runRotationSpeed = Mathf.Max(0f, runRotationSpeed);
        climbRotationSpeed = Mathf.Max(0f, climbRotationSpeed);
        climbSpeed = Mathf.Max(0f, climbSpeed);
        finishClimbSpeed = Mathf.Max(0f, finishClimbSpeed);
        stopDistance = Mathf.Max(0f, stopDistance);
        maximumRayDistance = Mathf.Max(0f, maximumRayDistance);
        markerHeight = Mathf.Max(0f, markerHeight);
        walkAnimationBaseSpeed = Mathf.Max(0.01f, walkAnimationBaseSpeed);
        runAnimationBaseSpeed = Mathf.Max(0.01f, runAnimationBaseSpeed);
        climbAnimationBaseSpeed = Mathf.Max(0.01f, climbAnimationBaseSpeed);
        finishClimbAnimationBaseSpeed = Mathf.Max(0.01f, finishClimbAnimationBaseSpeed);
    }

    public void ResetFinishClimbFlag()
    {
        _finishClimbing = false;
        _currentVelocity = Vector3.zero;
        _hasClimbFacingDirection = false;
        _climbFacingDirection = Vector3.zero;

        if (animator)
        {
            animator.SetBool(FinishClimbingHash, false);
        }
    }
}
