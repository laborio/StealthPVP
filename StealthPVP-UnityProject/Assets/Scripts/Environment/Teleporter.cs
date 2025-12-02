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
    [SerializeField, Tooltip("Optional override; defaults to the main camera service.")] private CameraService cameraService;
    [SerializeField, Tooltip("Delay before the camera starts moving toward the destination.")] private float cameraMoveDelay = 0f;

    [Header("Destination Offset")]
    [SerializeField, Tooltip("Local Z offset applied at the destination teleporter.")] private float arrivalOffsetZ = 1f;

    private Coroutine _teleportRoutine;
    private bool _busy;
    private int _onBoolHash;
    private Collider _ownCollider;
    private SimpleCharacterController _activePlayer;
    private Transform _previousCameraTarget;
    private Transform _cameraTargetProxy;
    private bool _playerInRange;

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
        return !_busy && _playerInRange && player && !player.IsTeleportLocked && twinTeleporter && isGrounded;
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
    }

    public void OnExitRange(SimpleCharacterController player)
    {
        _playerInRange = false;
    }

    private IEnumerator TeleportRoutine(SimpleCharacterController player)
    {
        _busy = true;
        _activePlayer = player;
        _playerInRange = false;
        Teleporter destination = twinTeleporter;
        CameraService controller = ResolveCameraService();
        Transform previousCameraTarget = controller ? controller.CurrentTarget : null;
        _previousCameraTarget = previousCameraTarget;
        Vector3 startPosition = player ? player.transform.position : transform.position;
        Vector3 endPosition = destination ? destination.GetExitPosition() : GetExitPosition();

        SetTeleporterActive(true);
        player?.BeginTeleportState();

        if (controller)
        {
            Transform target = GetCameraTargetForDestination(destination, startPosition, endPosition);
            controller.SetTarget(target);
        }

        float elapsed = 0f;
        float duration = Mathf.Max(teleportDuration, 0.0001f);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float cameraT = Mathf.Clamp01((elapsed - cameraMoveDelay) / duration);
            if (_cameraTargetProxy)
            {
                _cameraTargetProxy.position = Vector3.Lerp(startPosition, endPosition, cameraT);
            }
            yield return null;
        }

        if (player && destination)
        {
            player.TeleportToPosition(endPosition);
        }

        if (controller)
        {
            Transform targetToRestore = player ? player.transform : previousCameraTarget;
            controller.SetTarget(targetToRestore);
        }

        player?.EndTeleportState();
        SetTeleporterActive(false);

        _busy = false;
        _activePlayer = null;
        _previousCameraTarget = null;
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

    private CameraService ResolveCameraService()
    {
        if (cameraService)
        {
            return cameraService;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera && mainCamera.TryGetComponent(out CameraService attachedService))
        {
            cameraService = attachedService;
            return cameraService;
        }

        cameraService = Object.FindFirstObjectByType<CameraService>();
        return cameraService;
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

        CameraService controller = ResolveCameraService();
        if (controller && _previousCameraTarget)
        {
            controller.SetTarget(_previousCameraTarget);
        }

        _previousCameraTarget = null;
        _busy = false;
        SetTeleporterActive(false);
        _playerInRange = false;
    }

    private void OnValidate()
    {
        teleportDuration = Mathf.Max(0.1f, teleportDuration);
        cameraMoveDelay = Mathf.Max(0f, cameraMoveDelay);
        CacheHashes();
    }
}
