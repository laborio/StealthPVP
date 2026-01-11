using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enables lock-on combat for a keyboard/mouse player (middle click to lock target).
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class PlayerLockOnCombat : MonoBehaviour
{
    [SerializeField] private bool enableLockOnCombat = true;
    [SerializeField, Tooltip("Optional input router; defaults to a local component.")] private PlayerInputRouter inputRouter;
    [SerializeField, Tooltip("Optional controller to drive attack facing.")] private SimpleCharacterController controller;
    [SerializeField, Tooltip("Layers that can be locked by the player.")] private LayerMask lockTargetMask = ~0;
    [SerializeField, Tooltip("If true, allow raycasts to hit trigger colliders.")] private bool includeTriggerColliders = true;
    [SerializeField, Tooltip("Seconds the target can stay in fog before unlock.")] private float outOfViewGraceSeconds = 0.5f;
    [SerializeField, Tooltip("Seconds to freeze the target when a lock-on attack fires.")] private float lockFreezeDuration = 0.35f;
    [SerializeField, Tooltip("Child object name used to show lock feedback.")] private string lockIndicatorName = "PlayerLock";
    [SerializeField, Tooltip("Child name used for the lock indicator arrow.")] private string lockArrowName = "Arrow";
    [SerializeField, Tooltip("If true, billboard the lock arrow using the rendering camera.")] private bool useRenderingCameraForIndicator = true;
    [Header("Gamepad Auto Target")]
    [SerializeField, Tooltip("If true, gamepad attacks auto-select the nearest target (no manual lock).")]
    private bool enableGamepadAutoTarget = false;
    [SerializeField, Tooltip("Max distance used when lock-on range is zero (gamepad auto target).")]
    private float gamepadAutoTargetMaxDistance = 12f;
    [SerializeField, Tooltip("If true, auto target only considers units within the facing angle.")]
    private bool gamepadAutoTargetRequiresFacing = true;
    [Header("Hover Cursor")]
    [SerializeField, Tooltip("If true, show a custom cursor when hovering a valid target in range.")] private bool useHoverCursor = true;
    [SerializeField] private Texture2D hoverCursorTexture;
    [SerializeField] private Vector2 hoverCursorHotspot = Vector2.zero;
    [SerializeField] private CursorMode hoverCursorMode = CursorMode.Auto;
    [SerializeField, Tooltip("Fog manager used to validate visibility (optional).")] private FogOfWarManager fogManager;
    [SerializeField, Tooltip("Fog sample threshold to treat a target as visible.")] private float fogVisibleThreshold = 0.5f;
    [Header("Debug")]
    [SerializeField, Tooltip("If true, logs gamepad auto-target decisions.")] private bool debugAutoTargeting = false;

    private CharacterHealth _lockedTarget;
    private GameObject _lockedIndicator;
    private float _outOfViewTimer;
    private CharacterHealth _selfHealth;
    private CharacterHealth _temporaryTarget;
    private bool _pendingClearTemporary;
    private bool _hoverCursorActive;
    private Coroutine _freezeRoutine;
    private SimpleCharacterController _frozenController;
    private PlayerStunController _frozenStunController;
    private NavMeshAgent _frozenAgent;
    private bool _frozenAgentWasStopped;
    private bool _loggedUpdateSkip;

    private void Awake()
    {
        if (!inputRouter)
        {
            inputRouter = GetComponent<PlayerInputRouter>();
        }

        if (!controller)
        {
            controller = GetComponent<SimpleCharacterController>();
        }

        _selfHealth = GetComponent<CharacterHealth>() ?? GetComponentInChildren<CharacterHealth>(true);
        ResolveFogManager();
    }

    private void OnEnable()
    {
        if (controller != null)
        {
            controller.AttackTriggered += HandleAttackTriggered;
            bool requireTarget = enableLockOnCombat || enableGamepadAutoTarget;
            controller.SetRequireLockOnTargetForAttack(requireTarget);
            controller.SetSnapAttackFacing(enableLockOnCombat || enableGamepadAutoTarget);
        }
    }

    private void OnDisable()
    {
        if (controller != null)
        {
            controller.AttackTriggered -= HandleAttackTriggered;
            controller.SetRequireLockOnTargetForAttack(false);
            controller.SetSnapAttackFacing(false);
        }

        ClearLock();
        SetHoverCursor(false, force: true);
    }

    private void Update()
    {
        if (!inputRouter || !controller)
        {
            if (debugAutoTargeting && !_loggedUpdateSkip)
            {
                Debug.Log($"[LockOnCombat] Update skipped on {name}: inputRouter={(inputRouter ? inputRouter.GetType().Name : "null")} controller={(controller ? "ok" : "null")}.", this);
                _loggedUpdateSkip = true;
            }
            return;
        }

        bool isGamepad = inputRouter is PlayerInputRouterGamepad;
        if (!enableLockOnCombat && !(enableGamepadAutoTarget && isGamepad))
        {
            if (debugAutoTargeting && !_loggedUpdateSkip)
            {
                Debug.Log($"[LockOnCombat] Update skipped on {name}: enableLockOnCombat={enableLockOnCombat} enableGamepadAutoTarget={enableGamepadAutoTarget} isGamepad={isGamepad}.", this);
                _loggedUpdateSkip = true;
            }
            return;
        }

        _loggedUpdateSkip = false;
        PlayerInputSnapshot input = inputRouter.PollInput();
        if (isGamepad && enableGamepadAutoTarget)
        {
            if (_lockedTarget)
            {
                ClearLock();
            }
        }

        if (enableLockOnCombat && !isGamepad && input.LockPressed)
        {
            TryToggleLock();
        }

        if (input.PrimaryPressed || input.SecondaryPressed)
        {
            if (isGamepad && enableGamepadAutoTarget)
            {
                if (debugAutoTargeting)
                {
                    Debug.Log($"[LockOnCombat] Gamepad attack input on {name}. Primary={input.PrimaryPressed} Secondary={input.SecondaryPressed}.", this);
                }
                TrySetGamepadAutoTarget();
            }
            else
            {
                TrySetTemporaryTarget();
            }
        }

        if (!_lockedTarget && _temporaryTarget && (input.PrimaryReleased || input.SecondaryReleased))
        {
            _pendingClearTemporary = true;
        }

        UpdateLockState(Time.deltaTime);
        UpdateTemporaryTarget();
        if (!isGamepad)
        {
            UpdateHoverCursor();
        }
    }

    private void LateUpdate()
    {
        if (_pendingClearTemporary)
        {
            _pendingClearTemporary = false;
            ClearTemporaryTarget();
        }
    }

    private void TryToggleLock()
    {
        CharacterHealth target = FindTargetUnderCursor();
        if (!target)
        {
            return;
        }

        if (target == _lockedTarget)
        {
            ClearLock();
            return;
        }

        if (!IsTargetVisible(target.transform.position))
        {
            return;
        }

        SetLock(target);
    }

    private CharacterHealth FindTargetUnderCursor()
    {
        Camera cam = inputRouter.ResolveCamera();
        if (!cam)
        {
            return null;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        QueryTriggerInteraction triggerMode = includeTriggerColliders
            ? QueryTriggerInteraction.Collide
            : QueryTriggerInteraction.Ignore;
        if (!Physics.Raycast(ray, out RaycastHit hitInfo, 500f, lockTargetMask, triggerMode))
        {
            return null;
        }

        CharacterHealth target = hitInfo.collider.GetComponentInParent<CharacterHealth>();
        if (!target || target == _selfHealth)
        {
            return null;
        }

        return target;
    }

    private void UpdateLockState(float deltaTime)
    {
        if (!_lockedTarget)
        {
            return;
        }

        if (_lockedTarget.IsDead)
        {
            ClearLock();
            return;
        }

        if (!IsTargetVisible(_lockedTarget.transform.position))
        {
            _outOfViewTimer += deltaTime;
            if (_outOfViewTimer >= outOfViewGraceSeconds)
            {
                ClearLock();
            }
            return;
        }

        _outOfViewTimer = 0f;
    }

    private void SetLock(CharacterHealth target)
    {
        ClearLock();
        ClearTemporaryTarget();
        _lockedTarget = target;
        _outOfViewTimer = 0f;
        _lockedIndicator = FindIndicator(target);
        SetIndicatorActive(_lockedIndicator, true);
        ConfigureIndicatorBillboard(_lockedIndicator);
        if (controller)
        {
            controller.SetLockOnTarget(target.transform);
        }
    }

    private void ClearLock()
    {
        SetIndicatorActive(_lockedIndicator, false);
        _lockedIndicator = null;
        _lockedTarget = null;
        _outOfViewTimer = 0f;
        _pendingClearTemporary = false;
        if (controller)
        {
            controller.ClearLockOnTarget();
        }
        ClearFreeze();
    }

    private void TrySetTemporaryTarget()
    {
        if (_lockedTarget)
        {
            return;
        }

        CharacterHealth target = FindTargetUnderCursor();
        if (!target)
        {
            ClearTemporaryTarget();
            return;
        }

        if (!IsTargetVisible(target.transform.position) || !IsTargetInRange(target.transform))
        {
            ClearTemporaryTarget();
            return;
        }

        _temporaryTarget = target;
        if (controller)
        {
            controller.SetLockOnTarget(target.transform);
        }
    }

    private void TrySetGamepadAutoTarget()
    {
        if (_lockedTarget)
        {
            if (debugAutoTargeting)
            {
                Debug.Log($"[LockOnCombat] Auto-target skipped on {name}: locked target already set ({_lockedTarget.name}).", this);
            }
            return;
        }

        CharacterHealth target = FindNearestTargetInRange();
        if (!target)
        {
            if (debugAutoTargeting)
            {
                float range = controller ? controller.LockOnAttackRange : 0f;
                if (range <= 0f)
                {
                    range = Mathf.Max(0f, gamepadAutoTargetMaxDistance);
                }
                Debug.Log($"[LockOnCombat] Auto-target failed on {name}: no valid target (range {range:0.##}).", this);
            }
            ClearTemporaryTarget();
            return;
        }

        if (debugAutoTargeting)
        {
            Debug.Log($"[LockOnCombat] Auto-target selected {target.name} for {name}.", this);
        }
        _temporaryTarget = target;
        if (controller)
        {
            controller.SetLockOnTarget(target.transform);
        }
    }

    private void UpdateTemporaryTarget()
    {
        if (_lockedTarget)
        {
            ClearTemporaryTarget();
            return;
        }

        if (!_temporaryTarget)
        {
            return;
        }

        if (_temporaryTarget.IsDead)
        {
            ClearTemporaryTarget();
        }
    }

    private void UpdateHoverCursor()
    {
        if (!useHoverCursor || !hoverCursorTexture || !controller)
        {
            SetHoverCursor(false);
            return;
        }

        CharacterHealth hoverTarget = FindTargetUnderCursor();
        if (!hoverTarget || hoverTarget.IsDead)
        {
            SetHoverCursor(false);
            return;
        }

        if (_lockedTarget && hoverTarget != _lockedTarget)
        {
            SetHoverCursor(false);
            return;
        }

        if (!IsTargetVisible(hoverTarget.transform.position)
            || !IsTargetInRange(hoverTarget.transform)
            || !IsFacingTarget(hoverTarget.transform))
        {
            SetHoverCursor(false);
            return;
        }

        SetHoverCursor(true);
    }

    private void SetHoverCursor(bool active, bool force = false)
    {
        if (!force && _hoverCursorActive == active)
        {
            return;
        }

        _hoverCursorActive = active;
        if (active)
        {
            Cursor.SetCursor(hoverCursorTexture, hoverCursorHotspot, hoverCursorMode);
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }

    private void ClearTemporaryTarget()
    {
        if (!_temporaryTarget)
        {
            return;
        }

        if (controller && controller.LockOnTarget == _temporaryTarget.transform)
        {
            bool keepPull = controller.IsAirAttackPullActive;
            bool keepDamage = controller.IsAirAttackDamageActive;
            controller.ClearLockOnTarget(keepPull, keepDamage);
        }

        _temporaryTarget = null;
    }

    private GameObject FindIndicator(CharacterHealth target)
    {
        if (!target)
        {
            return null;
        }

        Transform[] children = target.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child && child.name == lockIndicatorName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private void SetIndicatorActive(GameObject indicator, bool active)
    {
        if (!indicator)
        {
            return;
        }

        if (indicator.activeSelf != active)
        {
            indicator.SetActive(active);
        }
    }

    private void ConfigureIndicatorBillboard(GameObject indicator)
    {
        if (!indicator)
        {
            return;
        }

        GameObject targetObject = indicator;
        if (!string.IsNullOrEmpty(lockArrowName))
        {
            Transform arrow = indicator.transform.Find(lockArrowName);
            if (!arrow)
            {
                Transform[] children = indicator.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    Transform child = children[i];
                    if (child && child.name == lockArrowName)
                    {
                        arrow = child;
                        break;
                    }
                }
            }

            if (arrow)
            {
                targetObject = arrow.gameObject;
            }
        }

        WorldSpaceBillboard billboard = targetObject.GetComponent<WorldSpaceBillboard>();
        if (!billboard)
        {
            billboard = targetObject.AddComponent<WorldSpaceBillboard>();
        }

        billboard.SetLockYAxis(false);
        billboard.SetInvertFacing(false);
        if (useRenderingCameraForIndicator)
        {
            billboard.SetUseRenderingCamera(true);
        }
        else
        {
            billboard.SetUseRenderingCamera(false);
            Camera cam = inputRouter ? inputRouter.ResolveCamera() : Camera.main;
            if (cam)
            {
                billboard.SetTargetCamera(cam);
            }
        }
    }

    private void HandleAttackTriggered(bool wasSecondary)
    {
        if (wasSecondary)
        {
            return;
        }

        CharacterHealth target = _lockedTarget ? _lockedTarget : _temporaryTarget;
        if (!target || !controller.IsLockOnTargetInRange())
        {
            return;
        }

        BeginFreeze(target);
        _pendingClearTemporary = false;
        ClearTemporaryTarget();
    }

    private void BeginFreeze(CharacterHealth target)
    {
        ClearFreeze();

        _frozenController = target.GetComponentInParent<SimpleCharacterController>()
            ?? target.GetComponentInChildren<SimpleCharacterController>(true);
        if (_frozenController)
        {
            _frozenController.SetInputSuppressed(true);
            _frozenStunController = target.GetComponentInParent<PlayerStunController>()
                ?? target.GetComponentInChildren<PlayerStunController>(true);
        }

        _frozenAgent = target.GetComponentInParent<NavMeshAgent>()
            ?? target.GetComponentInChildren<NavMeshAgent>(true);
        if (_frozenAgent)
        {
            _frozenAgentWasStopped = _frozenAgent.isStopped;
            _frozenAgent.isStopped = true;
        }

        if (lockFreezeDuration > 0f)
        {
            _freezeRoutine = StartCoroutine(FreezeCooldown());
        }
    }

    private IEnumerator FreezeCooldown()
    {
        yield return new WaitForSeconds(lockFreezeDuration);
        ClearFreeze();
    }

    private void ClearFreeze()
    {
        if (_freezeRoutine != null)
        {
            StopCoroutine(_freezeRoutine);
            _freezeRoutine = null;
        }

        if (_frozenController)
        {
            if (_frozenStunController == null || !_frozenStunController.IsStunned)
            {
                _frozenController.SetInputSuppressed(false);
            }
        }

        if (_frozenAgent)
        {
            _frozenAgent.isStopped = _frozenAgentWasStopped;
        }

        _frozenController = null;
        _frozenStunController = null;
        _frozenAgent = null;
        _frozenAgentWasStopped = false;
    }

    private bool IsTargetVisible(Vector3 position)
    {
        ResolveFogManager();
        if (!fogManager)
        {
            return true;
        }

        return fogManager.SampleFog01(position) > fogVisibleThreshold;
    }

    private bool IsTargetInRange(Transform target)
    {
        if (!target || !controller)
        {
            return false;
        }

        float range = controller.LockOnAttackRange;
        if (range <= 0f)
        {
            return true;
        }

        Vector3 delta = target.position - controller.transform.position;
        float verticalDistance = Mathf.Abs(delta.y);
        delta.y = 0f;
        if (delta.sqrMagnitude > range * range)
        {
            return false;
        }

        float verticalRange = controller.AirAttackVerticalRange;
        if (controller.IsAirborne && verticalRange > 0f && verticalDistance > verticalRange)
        {
            return false;
        }

        return true;
    }

    private bool IsFacingTarget(Transform target)
    {
        if (!target || !controller)
        {
            return false;
        }

        if (!controller.RequireFacingForAttack || controller.AttackFacingAngle >= 180f)
        {
            return true;
        }

        Vector3 direction = target.position - controller.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        float angle = Vector3.Angle(controller.transform.forward, direction.normalized);
        return angle <= Mathf.Clamp(controller.AttackFacingAngle, 0f, 180f);
    }

    private CharacterHealth FindNearestTargetInRange()
    {
        if (!controller)
        {
            return null;
        }

        float range = controller.LockOnAttackRange;
        if (range <= 0f)
        {
            range = Mathf.Max(0f, gamepadAutoTargetMaxDistance);
        }

        if (range <= 0f)
        {
            return null;
        }

        QueryTriggerInteraction triggerMode = includeTriggerColliders
            ? QueryTriggerInteraction.Collide
            : QueryTriggerInteraction.Ignore;
        float verticalRange = controller.AirAttackVerticalRange;
        bool useVerticalRange = controller.IsAirborne && verticalRange > 0f;
        Collider[] hits;
        if (useVerticalRange)
        {
            Vector3 center = controller.transform.position;
            Vector3 top = center + Vector3.up * verticalRange;
            Vector3 bottom = center + Vector3.down * verticalRange;
            hits = Physics.OverlapCapsule(top, bottom, range, lockTargetMask, triggerMode);
        }
        else
        {
            hits = Physics.OverlapSphere(controller.transform.position, range, lockTargetMask, triggerMode);
        }
        if (hits == null || hits.Length == 0)
        {
            return null;
        }

        CharacterHealth best = null;
        float bestSqr = float.MaxValue;
        int invalid = 0;
        int self = 0;
        int dead = 0;
        int notVisible = 0;
        int notFacing = 0;
        int notVertical = 0;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (!hit)
            {
                invalid++;
                continue;
            }

            CharacterHealth target = hit.GetComponentInParent<CharacterHealth>();
            if (!target || target == _selfHealth || target.IsDead)
            {
                if (!target)
                {
                    invalid++;
                }
                else if (target == _selfHealth)
                {
                    self++;
                }
                else
                {
                    dead++;
                }
                continue;
            }

            Vector3 pos = target.transform.position;
            if (!IsTargetVisible(pos))
            {
                notVisible++;
                continue;
            }

            if (gamepadAutoTargetRequiresFacing && !IsFacingTarget(target.transform))
            {
                notFacing++;
                continue;
            }

            Vector3 delta = pos - controller.transform.position;
            if (useVerticalRange && Mathf.Abs(delta.y) > verticalRange)
            {
                notVertical++;
                continue;
            }

            delta.y = 0f;
            float sqr = delta.sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = target;
            }
        }

        if (debugAutoTargeting)
        {
            Debug.Log($"[LockOnCombat] Auto-target scan on {name}: hits={hits.Length} invalid={invalid} self={self} dead={dead} notVisible={notVisible} notFacing={notFacing} notVertical={notVertical} range={range:0.##} verticalRange={(useVerticalRange ? verticalRange : 0f):0.##} -> {(best ? best.name : "none")}.", this);
        }
        return best;
    }

    private void ResolveFogManager()
    {
        LocalVersusGameManager manager = LocalVersusGameManager.Instance;
        FogOfWarManager desiredFog = null;
        int bestDistance = int.MaxValue;
        if (manager)
        {
            TryPickFogFromRoot(manager._player1Instance, manager.player1Fog, ref desiredFog, ref bestDistance);
            TryPickFogFromRoot(manager._player2Instance, manager.player2Fog, ref desiredFog, ref bestDistance);
            TryPickFogFromRoot(manager._player3Instance, manager.player3Fog, ref desiredFog, ref bestDistance);
        }

        if (desiredFog)
        {
            fogManager = desiredFog;
            return;
        }

        Camera cam = inputRouter ? inputRouter.ResolveCamera() : Camera.main;
        if (cam)
        {
            FogOfWarCameraBinder binder = cam.GetComponent<FogOfWarCameraBinder>();
            if (binder && binder.FogManager)
            {
                fogManager = binder.FogManager;
                return;
            }
        }

        if (!fogManager)
        {
            fogManager = FindFirstObjectByType<FogOfWarManager>();
        }
    }

    private void TryPickFogFromRoot(GameObject root, FogOfWarManager fog, ref FogOfWarManager bestFog, ref int bestDistance)
    {
        if (!root || !fog)
        {
            return;
        }

        int distance = GetHierarchyDistance(root.transform, transform);
        if (distance < 0 || distance >= bestDistance)
        {
            return;
        }

        bestDistance = distance;
        bestFog = fog;
    }

    private int GetHierarchyDistance(Transform root, Transform child)
    {
        if (!root || !child)
        {
            return -1;
        }

        int distance = 0;
        Transform current = child;
        while (current)
        {
            if (current == root)
            {
                return distance;
            }

            distance++;
            current = current.parent;
        }

        return -1;
    }

    private void OnValidate()
    {
        outOfViewGraceSeconds = Mathf.Max(0f, outOfViewGraceSeconds);
        lockFreezeDuration = Mathf.Max(0f, lockFreezeDuration);
        fogVisibleThreshold = Mathf.Clamp01(fogVisibleThreshold);
        gamepadAutoTargetMaxDistance = Mathf.Max(0f, gamepadAutoTargetMaxDistance);
    }
}
