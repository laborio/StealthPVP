using System.Collections;
using UnityEngine;

/// <summary>
/// Handles paired teleporter interactions and integrates with the contextual action system.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Teleporter : MonoBehaviour, IContextualAction
{
    [Header("Pairing")]
    [SerializeField, Tooltip("Destination teleporter for this portal.")] private Teleporter twinTeleporter;

    [Header("Timing")]
    [SerializeField, Range(0.1f, 5f)] private float teleportDuration = 1.5f;

    [Header("Animation")]
    [SerializeField] private Animator teleporterAnimator;
    [SerializeField] private string onBoolName = "On";

    [Header("UI")]
    [SerializeField, Tooltip("Higher number wins when multiple actions overlap.")] private int actionPriority = 200;

    [Header("Camera")]
    [SerializeField, Tooltip("Optional override; defaults to Camera.main.")] private Camera mainCameraOverride;
    [SerializeField, Tooltip("Delay before the camera starts moving toward the destination.")] private float cameraMoveDelay = 0f;
    [SerializeField, Tooltip("Curve applied to camera movement during teleport.")] private AnimationCurve cameraMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Destination Offset")]
    [SerializeField, Tooltip("Local Z offset applied at the destination teleporter.")] private float arrivalOffsetZ = 1f;

    private Coroutine _teleportRoutine;
    private bool _busy;
    private int _onBoolHash;
    private Collider _ownCollider;
    private SimpleCharacterController _activePlayer;
    private Transform _previousCameraTarget;
    private Vector3 _previousCameraPosition;
    private Quaternion _previousCameraRotation;
    private Camera _activeCamera;
    private CameraController _cameraController;
    private Transform _cameraTargetProxy;
    private bool _playerInRange;
    private SimpleCharacterController _playerInTrigger;
    private CharacterController _playerCharacterController;
    private Collider _playerCollider;
    private bool _hasOnBoolParameter;
    private bool _validatedAnimatorParameters;

    public int Priority => actionPriority;
    public bool IsBusy => _busy;

    private void Awake()
    {
        _ownCollider = GetComponent<Collider>();
        if (_ownCollider && !_ownCollider.isTrigger)
        {
            _ownCollider.isTrigger = true;
        }

        CacheHashes();
    }

    public bool CanExecute(SimpleCharacterController player, bool isGrounded)
    {
        if (!ValidatePlayerRange(player))
        {
            return false;
        }

        if (!IsAnimatorOn())
        {
            return false;
        }

        return !_busy && !player.IsTeleportLocked && twinTeleporter && isGrounded;
    }

    public bool ShouldShowHint(SimpleCharacterController player, bool isGrounded)
    {
        return CanExecute(player, isGrounded);
    }

    public bool TryExecute(SimpleCharacterController player, bool isGrounded)
    {
        if (!CanExecute(player, isGrounded))
        {
            return false;
        }

        if (_teleportRoutine != null)
        {
            StopCoroutine(_teleportRoutine);
        }

        _teleportRoutine = StartCoroutine(TeleportRoutine(player));
        return true;
    }

    public void OnEnterRange(SimpleCharacterController player)
    {
        _playerInRange = true;
        _playerInTrigger = player;
        _playerCharacterController = player ? player.GetComponent<CharacterController>() : null;
        _playerCollider = player ? player.GetComponent<Collider>() : null;
    }

    public void OnExitRange(SimpleCharacterController player)
    {
        _playerInRange = false;
        _playerInTrigger = null;
        _playerCharacterController = null;
        _playerCollider = null;
    }

    private IEnumerator TeleportRoutine(SimpleCharacterController player)
    {
        _busy = true;
        _activePlayer = player;
        _playerInRange = false;
        _playerInTrigger = null;
        _playerCharacterController = null;
        _playerCollider = null;
        Teleporter destination = twinTeleporter;
        _activeCamera = mainCameraOverride ? mainCameraOverride : Camera.main;
        _cameraController = _activeCamera ? _activeCamera.GetComponent<CameraController>() : null;
        _previousCameraTarget = _cameraController ? _cameraController.CurrentTarget : null;
        _previousCameraPosition = _activeCamera ? _activeCamera.transform.position : Vector3.zero;
        _previousCameraRotation = _activeCamera ? _activeCamera.transform.rotation : Quaternion.identity;
        Vector3 startPosition = player ? player.transform.position : transform.position;
        Vector3 endPosition = destination ? destination.GetExitPosition() : GetExitPosition();
        Vector3 cameraOffset = _activeCamera ? _activeCamera.transform.position - startPosition : Vector3.zero;

        SetTeleporterActive(true);
        player?.BeginTeleportState();

        if (_cameraController)
        {
            Transform target = GetCameraTargetForDestination(destination, startPosition, endPosition);
            _cameraController.SetTarget(target);
        }

        float elapsed = 0f;
        float duration = Mathf.Max(teleportDuration, 0.0001f);
        float cameraTravelDuration = Mathf.Max(duration - cameraMoveDelay, 0.0001f);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float cameraProgress = Mathf.Clamp01((elapsed - cameraMoveDelay) / cameraTravelDuration);
            float cameraT = EvaluateCameraTravel(cameraProgress);
            if (_cameraTargetProxy)
            {
                _cameraTargetProxy.position = Vector3.Lerp(startPosition, endPosition, cameraT);
                if (!_cameraController && _activeCamera)
                {
                    _activeCamera.transform.position = _cameraTargetProxy.position + cameraOffset;
                    _activeCamera.transform.rotation = _previousCameraRotation;
                }
            }
            yield return null;
        }

        if (player && destination)
        {
            player.TeleportToPosition(endPosition);
            player.RefreshContextActionsFromOverlaps();
        }

        if (_cameraController)
        {
            Transform targetToRestore = player ? player.transform : _previousCameraTarget;
            _cameraController.SetTarget(targetToRestore);
        }
        else if (_activeCamera)
        {
            _activeCamera.transform.position = endPosition + cameraOffset;
            _activeCamera.transform.rotation = _previousCameraRotation;
        }

        player?.EndTeleportState();
        SetTeleporterActive(false);

        _busy = false;
        _activePlayer = null;
        _previousCameraTarget = null;
        _activeCamera = null;
        _cameraController = null;
        _previousCameraPosition = Vector3.zero;
        _previousCameraRotation = Quaternion.identity;
        _teleportRoutine = null;
    }

    public Vector3 GetExitPosition()
    {
        Transform anchor = transform;
        Vector3 localOffset = Vector3.forward;
        localOffset.z = arrivalOffsetZ;
        return anchor.TransformPoint(localOffset);
    }

    public void ForceRevealAnimation()
    {
        SetAnimatorBool(true);
    }

    public void ForceDeactivateAnimation()
    {
        SetAnimatorBool(false);
    }

    private void SetTeleporterActive(bool active)
    {
        EnsureAnimatorCached();
        SetAnimatorBool(active);

        if (twinTeleporter && twinTeleporter != this)
        {
            twinTeleporter.SyncFromPartner(active, this);
        }
    }

    private void SyncFromPartner(bool active, Teleporter source)
    {
        if (!source || source == this)
        {
            return;
        }

        EnsureAnimatorCached();
        SetAnimatorBool(active);
    }

    private void SetAnimatorBool(bool active)
    {
        if (!teleporterAnimator)
        {
            return;
        }

        if (_onBoolHash != 0)
        {
            teleporterAnimator.SetBool(_onBoolHash, active);
        }
        else if (!string.IsNullOrEmpty(onBoolName))
        {
            teleporterAnimator.SetBool(onBoolName, active);
        }
    }

    private bool ValidatePlayerRange(SimpleCharacterController player)
    {
        if (!_playerInRange || !player)
        {
            return false;
        }

        if (_playerInTrigger && _playerInTrigger != player)
        {
            return false;
        }

        if (!_ownCollider)
        {
            return false;
        }

        bool overlaps = false;
        if (_playerCharacterController)
        {
            overlaps = _ownCollider.bounds.Intersects(_playerCharacterController.bounds);
        }
        else if (_playerCollider)
        {
            overlaps = _ownCollider.bounds.Intersects(_playerCollider.bounds);
        }

        if (!overlaps)
        {
            _playerInRange = false;
            _playerInTrigger = null;
            _playerCharacterController = null;
            _playerCollider = null;
        }

        return overlaps;
    }

    private Transform GetCameraTargetForDestination(Teleporter destination)
    {
        return GetCameraTargetForDestination(destination, transform.position, destination ? destination.GetExitPosition() : GetExitPosition());
    }

    private Transform GetCameraTargetForDestination(Teleporter destination, Vector3 startPosition, Vector3 endPosition)
    {
        Transform target = destination ? destination.transform : transform;
        EnsureCameraProxy();
        _cameraTargetProxy.position = startPosition;
        _cameraTargetProxy.rotation = target.rotation;
        return _cameraTargetProxy;
    }

    private void EnsureAnimatorCached()
    {
        if (!teleporterAnimator)
        {
            teleporterAnimator = GetComponentInChildren<Animator>();
        }
    }

    private void CacheHashes()
    {
        _onBoolHash = string.IsNullOrEmpty(onBoolName) ? 0 : Animator.StringToHash(onBoolName);
        _validatedAnimatorParameters = false;
        _hasOnBoolParameter = false;
    }

    private void EnsureCameraProxy()
    {
        if (_cameraTargetProxy)
        {
            return;
        }

        GameObject proxy = new GameObject("TeleporterCameraTarget");
        _cameraTargetProxy = proxy.transform;
    }

    private void OnDisable()
    {
        if (_teleportRoutine != null)
        {
            StopCoroutine(_teleportRoutine);
            _teleportRoutine = null;
        }

        if (_activePlayer)
        {
            _activePlayer.EndTeleportState();
            _activePlayer = null;
        }

        if (_cameraController && _previousCameraTarget)
        {
            _cameraController.SetTarget(_previousCameraTarget);
        }
        else if (_activeCamera)
        {
            _activeCamera.transform.position = _previousCameraPosition;
            _activeCamera.transform.rotation = _previousCameraRotation;
        }

        _previousCameraTarget = null;
        _busy = false;
        SetTeleporterActive(false);
        _playerInRange = false;
        _playerInTrigger = null;
        _playerCharacterController = null;
        _playerCollider = null;
    }

    private float EvaluateCameraTravel(float normalizedTime)
    {
        if (cameraMoveCurve != null && cameraMoveCurve.length > 0)
        {
            return cameraMoveCurve.Evaluate(normalizedTime);
        }

        return Mathf.SmoothStep(0f, 1f, normalizedTime);
    }

    private void OnValidate()
    {
        teleportDuration = Mathf.Max(0.1f, teleportDuration);
        cameraMoveDelay = Mathf.Max(0f, cameraMoveDelay);
        if (cameraMoveCurve == null || cameraMoveCurve.length == 0)
        {
            cameraMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
        CacheHashes();
    }

    private bool IsAnimatorOn()
    {
        EnsureAnimatorCached();
        if (!teleporterAnimator || _onBoolHash == 0)
        {
            return true;
        }

        if (!_validatedAnimatorParameters)
        {
            _hasOnBoolParameter = AnimatorHasBool(teleporterAnimator, _onBoolHash);
            _validatedAnimatorParameters = true;
        }

        if (!_hasOnBoolParameter)
        {
            return true;
        }

        return teleporterAnimator.GetBool(_onBoolHash);
    }

    private static bool AnimatorHasBool(Animator animator, int hash)
    {
        if (!animator)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.nameHash == hash)
            {
                return true;
            }
        }

        return false;
    }
}
