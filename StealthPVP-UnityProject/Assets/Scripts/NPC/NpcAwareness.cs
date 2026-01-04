using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Tracks nearby player movement to build awareness and trigger retaliation.
/// </summary>
[DisallowMultipleComponent]
public class NpcAwareness : MonoBehaviour
{
    [Header("Awareness")]
    [SerializeField, Tooltip("Meters within which player actions are noticed.")] private float awarenessRadius = 8f;
    [SerializeField, Tooltip("Awareness gained per second while a nearby player is running or jumping.")] private float awarenessFillRate = 0.45f;
    [SerializeField, Tooltip("Awareness lost per second while no nearby player is running or jumping.")] private float awarenessDecayRate = 0.3f;
    [SerializeField, Range(0f, 1f), Tooltip("Awareness fraction that triggers alerting.")] private float alertThreshold = 0.33f;
    [SerializeField, Tooltip("Degrees per second to rotate toward the triggering player while alert.")] private float alertRotateSpeed = 360f;
    [SerializeField, Tooltip("Seconds between player scans.")] private float scanInterval = 0.1f;
    [SerializeField, Tooltip("Seconds between refreshes of the player list.")] private float playerRefreshInterval = 1f;
    [SerializeField, Tooltip("Enable debug logs for awareness/facing transitions.")] private bool debugLogs = false;
    [SerializeField, Tooltip("Seconds between facing debug logs.")] private float debugFacingInterval = 0.5f;
    [Header("Follow")]
    [SerializeField, Range(0f, 1f), Tooltip("Awareness fraction that triggers following.")] private float followThreshold = 0.66f;
    [SerializeField, Tooltip("Seconds between follow destination updates.")] private float followRepathInterval = 0.2f;
    [SerializeField, Tooltip("Max path length allowed when following; 0 disables path length cancel.")] private float maxChasePathDistance = 0f;
    [SerializeField, Tooltip("Disable the NPC wander controller while following.")] private bool disableWanderDuringFollow = true;
    [SerializeField, Tooltip("Disable the NPC wander controller while alerting (keeps them stationary).")] private bool disableWanderDuringAlert = true;
    [SerializeField, Tooltip("Seconds to keep reorienting toward wander movement after alert/follow ends.")] private float reorientDuration = 0.35f;
    [SerializeField, Tooltip("Force the NPC to face its movement direction while wandering.")] private bool forceFaceMovementWhenWandering = true;
    [Header("UI")]
    [SerializeField, Tooltip("Fill image used to show awareness amount.")] private Image awarenessFillImage;
    [SerializeField, Tooltip("Optional world-space canvas used for awareness.")] private Canvas awarenessCanvas;
    [SerializeField, Tooltip("Optional billboard component on the awareness canvas.")] private WorldSpaceBillboard awarenessBillboard;
    [SerializeField, Tooltip("If true, hides the parent container when awareness is zero.")] private bool hideWhenEmpty = true;

    [Header("References")]
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private NpcRetaliateStun retaliateStun;
    [SerializeField] private CharacterHealth health;
    [SerializeField] private NpcNavAgent npcNavAgent;

    public float AwarenessNormalized => _awareness;

    private float _awareness;
    private Transform _currentTrigger;
    private bool _threatActive;
    private bool _alertActive;
    private bool _retaliationTriggered;
    private bool _navStopApplied;
    private bool _cachedUpdateRotation;
    private float _nextScanTime;
    private GameObject _awarenessContainer;
    private Transform _awarenessCanvasRoot;
    private Transform _lastTrigger;
    private Camera _awarenessCamera;
    private int _awarenessLayer = -1;
    private bool _following;
    private float _nextFollowRepathTime;
    private bool _wanderSuppressed;
    private float _reorientTimer;
    private float _nextDebugFacingTime;
    private Transform _lastDebugTrigger;

    private static readonly List<SimpleCharacterController> CachedPlayers = new List<SimpleCharacterController>();
    private static float _nextPlayerRefreshTime;

    private void Awake()
    {
        if (!navMeshAgent)
        {
            navMeshAgent = GetComponent<NavMeshAgent>() ?? GetComponentInChildren<NavMeshAgent>(true);
        }

        if (!retaliateStun)
        {
            retaliateStun = GetComponent<NpcRetaliateStun>() ?? GetComponentInChildren<NpcRetaliateStun>(true);
        }

        if (!health)
        {
            health = GetComponent<CharacterHealth>() ?? GetComponentInChildren<CharacterHealth>(true);
        }

        if (!npcNavAgent)
        {
            npcNavAgent = GetComponent<NpcNavAgent>() ?? GetComponentInChildren<NpcNavAgent>(true);
        }

        CacheAwarenessUi();
    }

    private void OnEnable()
    {
        CharacterHealth.AnyDied += HandleAnyDied;
        WeaponDamage.AnyStunned += HandleAnyStunned;
        CacheAwarenessUi();
    }

    private void OnDisable()
    {
        CharacterHealth.AnyDied -= HandleAnyDied;
        WeaponDamage.AnyStunned -= HandleAnyStunned;
        ExitFollow();
        ExitAlert();
        SetAwarenessContainerActive(false);
    }

    private void Update()
    {
        if (health && health.IsDead)
        {
            SetAwareness(0f);
            if (awarenessFillImage)
            {
                awarenessFillImage.fillAmount = 0f;
            }
            SetAwarenessContainerActive(false);
            ExitFollow();
            ExitAlert();
            _reorientTimer = 0f;
            return;
        }

        float now = Time.time;
        if (now >= _nextScanTime)
        {
            ScanPlayers(now);
            LogTriggerChange();
            _nextScanTime = now + Mathf.Max(0.02f, scanInterval);
        }

        UpdateAwareness(Time.deltaTime);
        UpdateAwarenessUi();

        UpdateAwarenessResponse();
        UpdateReorientation(Time.deltaTime);
        UpdateWanderFacing(Time.deltaTime);

        if (!_retaliationTriggered && _awareness >= 0.999f && _currentTrigger)
        {
            TriggerRetaliation();
        }

        if (_awareness < 0.99f)
        {
            _retaliationTriggered = false;
        }
    }

    private void CacheAwarenessUi()
    {
        if (!awarenessFillImage)
        {
            Transform canvas = transform.Find("WScanvas");
            if (!canvas)
            {
                canvas = transform.Find("WSCanvas");
            }
            if (canvas)
            {
                _awarenessCanvasRoot = canvas;
                awarenessFillImage = canvas.GetComponentInChildren<Image>(true);
            }
        }

        if (awarenessFillImage && awarenessFillImage.transform.parent)
        {
            _awarenessContainer = awarenessFillImage.transform.parent.gameObject;
            if (!awarenessCanvas)
            {
                awarenessCanvas = awarenessFillImage.GetComponentInParent<Canvas>();
            }

            if (!awarenessBillboard)
            {
                awarenessBillboard = awarenessFillImage.GetComponentInParent<WorldSpaceBillboard>();
            }
        }

        if (!_awarenessCanvasRoot && awarenessCanvas)
        {
            _awarenessCanvasRoot = awarenessCanvas.transform;
        }
    }

    private void ScanPlayers(float now)
    {
        RefreshPlayerCache(now);

        _threatActive = false;
        Transform bestThreat = null;
        float bestThreatSqr = float.MaxValue;
        float radiusSqr = awarenessRadius * awarenessRadius;

        if (_currentTrigger)
        {
            Vector3 delta = _currentTrigger.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > radiusSqr)
            {
                _currentTrigger = null;
            }
        }

        for (int i = 0; i < CachedPlayers.Count; i++)
        {
            SimpleCharacterController player = CachedPlayers[i];
            if (!player)
            {
                continue;
            }

            Vector3 delta = player.transform.position - transform.position;
            delta.y = 0f;
            float sqr = delta.sqrMagnitude;
            if (sqr > radiusSqr)
            {
                continue;
            }

            bool isThreat = player.IsRunning || player.IsJumping;
            if (!isThreat)
            {
                continue;
            }

            _threatActive = true;
            if (sqr < bestThreatSqr)
            {
                bestThreatSqr = sqr;
                bestThreat = player.transform;
            }
        }

        if (bestThreat)
        {
            _currentTrigger = bestThreat;
        }
    }

    private void UpdateAwareness(float deltaTime)
    {
        if (_threatActive)
        {
            _awareness = Mathf.Clamp01(_awareness + awarenessFillRate * deltaTime);
        }
        else
        {
            _awareness = Mathf.Clamp01(_awareness - awarenessDecayRate * deltaTime);
        }
    }

    private void UpdateAwarenessUi()
    {
        if (!awarenessFillImage)
        {
            return;
        }

        bool show = !hideWhenEmpty || _awareness > 0.001f;
        SetAwarenessContainerActive(show);
        if (show)
        {
            awarenessFillImage.fillAmount = _awareness;
            UpdateAwarenessVisualTarget();
            UpdateAwarenessFacing();
        }
    }

    private void UpdateAwarenessResponse()
    {
        bool canRespond = retaliateStun == null || !retaliateStun.IsRetaliating;
        if (!canRespond || !_currentTrigger)
        {
            ExitFollow();
            ExitAlert();
            return;
        }

        bool wasFollowing = _following;
        if (ShouldFollow())
        {
            ExitAlert();
            EnterFollow();
            UpdateFollow();
            return;
        }

        ExitFollow();

        if (ShouldAlert())
        {
            EnterAlert();
            FaceTrigger();
        }
        else
        {
            bool wasAlerting = _alertActive;
            ExitAlert();
            if ((wasAlerting || wasFollowing) && npcNavAgent)
            {
                npcNavAgent.ForceImmediateDestination();
                StartReorientation();
            }
        }
    }

    private void SetAwarenessContainerActive(bool active)
    {
        if (!_awarenessContainer)
        {
            return;
        }

        if (_awarenessContainer.activeSelf != active)
        {
            _awarenessContainer.SetActive(active);
        }
    }

    private void UpdateAwarenessVisualTarget()
    {
        if (!_currentTrigger)
        {
            if (awarenessCanvas && _awarenessCamera && awarenessCanvas.worldCamera != _awarenessCamera)
            {
                awarenessCanvas.worldCamera = _awarenessCamera;
            }
            return;
        }

        if (_currentTrigger == _lastTrigger && _awarenessCamera)
        {
            return;
        }

        _lastTrigger = _currentTrigger;

        if (!TryResolveTriggerVisuals(_currentTrigger, out Camera camera, out int layer))
        {
            return;
        }

        if (camera)
        {
            _awarenessCamera = camera;
            if (awarenessCanvas)
            {
                awarenessCanvas.worldCamera = camera;
            }
        }

        if (layer >= 0)
        {
            ApplyAwarenessLayer(layer);
        }

        if (awarenessBillboard)
        {
            if (_awarenessCamera)
            {
                awarenessBillboard.SetTargetCamera(_awarenessCamera);
                awarenessBillboard.SetUseRenderingCamera(false);
            }
            else
            {
                awarenessBillboard.SetUseRenderingCamera(true);
            }
        }
    }

    private void UpdateAwarenessFacing()
    {
        if (awarenessBillboard || !_awarenessCamera || !_awarenessCanvasRoot)
        {
            return;
        }

        Vector3 lookDirection = _awarenessCamera.transform.position - _awarenessCanvasRoot.position;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        _awarenessCanvasRoot.rotation = Quaternion.LookRotation(-lookDirection.normalized, Vector3.up);
    }

    private bool TryResolveTriggerVisuals(Transform trigger, out Camera camera, out int layer)
    {
        camera = null;
        layer = -1;
        if (!trigger)
        {
            return false;
        }

        SimpleCharacterController controller = trigger.GetComponent<SimpleCharacterController>()
            ?? trigger.GetComponentInParent<SimpleCharacterController>()
            ?? trigger.GetComponentInChildren<SimpleCharacterController>(true);
        if (controller)
        {
            PlayerInputRouter router = controller.InputRouter;
            if (router)
            {
                camera = router.ResolveCamera();
            }
        }

        LocalVersusGameManager manager = LocalVersusGameManager.Instance;
        if (manager)
        {
            ResolveLocalVersusVisuals(manager, trigger, ref camera, ref layer);
        }

        if (!camera)
        {
            camera = Camera.main;
        }

        return camera || layer >= 0;
    }

    private void ResolveLocalVersusVisuals(LocalVersusGameManager manager, Transform trigger, ref Camera camera, ref int layer)
    {
        if (!manager || !trigger)
        {
            return;
        }

        if (IsTriggerPlayerRoot(trigger, manager._player1Instance))
        {
            layer = LayerMask.NameToLayer(manager.player1OnlyLayer);
            if (!camera)
            {
                camera = manager.player1Camera;
            }
            return;
        }

        if (IsTriggerPlayerRoot(trigger, manager._player2Instance))
        {
            layer = LayerMask.NameToLayer(manager.player2OnlyLayer);
            if (!camera)
            {
                camera = manager.player2Camera;
            }
            return;
        }

        if (IsTriggerPlayerRoot(trigger, manager._player3Instance))
        {
            layer = LayerMask.NameToLayer(manager.player3OnlyLayer);
            if (!camera)
            {
                camera = manager.player3Camera;
            }
        }
    }

    private bool IsTriggerPlayerRoot(Transform trigger, GameObject playerRoot)
    {
        if (!trigger || !playerRoot)
        {
            return false;
        }

        Transform root = playerRoot.transform;
        return trigger == root || trigger.IsChildOf(root) || root.IsChildOf(trigger);
    }

    private void ApplyAwarenessLayer(int layer)
    {
        if (layer < 0 || !_awarenessCanvasRoot)
        {
            return;
        }

        if (_awarenessLayer == layer)
        {
            return;
        }

        SetLayerRecursively(_awarenessCanvasRoot, layer);
        _awarenessLayer = layer;
    }

    private void SetLayerRecursively(Transform root, int layer)
    {
        if (!root)
        {
            return;
        }

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private bool ShouldAlert()
    {
        return _threatActive && _awareness >= Mathf.Clamp01(alertThreshold) && _currentTrigger;
    }

    private bool ShouldFollow()
    {
        return navMeshAgent && _threatActive && _awareness >= Mathf.Clamp01(followThreshold) && _currentTrigger;
    }

    private void EnterAlert()
    {
        if (_alertActive)
        {
            return;
        }

        _alertActive = true;
        LogDebug($"Alert start awareness={_awareness:0.00} trigger={(_currentTrigger ? _currentTrigger.name : "none")}");
        if (navMeshAgent)
        {
            _cachedUpdateRotation = navMeshAgent.updateRotation;
            navMeshAgent.updateRotation = false;
            if (!navMeshAgent.isStopped)
            {
                navMeshAgent.isStopped = true;
                _navStopApplied = true;
            }

            if (navMeshAgent.hasPath)
            {
                navMeshAgent.ResetPath();
            }
        }

        ApplyWanderSuppression();
    }

    private void ExitAlert()
    {
        if (!_alertActive)
        {
            return;
        }

        _alertActive = false;
        LogDebug("Alert end");
        if (navMeshAgent)
        {
            if (_navStopApplied)
            {
                navMeshAgent.isStopped = false;
                _navStopApplied = false;
            }
            navMeshAgent.updateRotation = _cachedUpdateRotation;
        }

        ApplyWanderSuppression();
    }

    private void EnterFollow()
    {
        if (_following)
        {
            return;
        }

        _following = true;
        _nextFollowRepathTime = 0f;
        LogDebug($"Follow start awareness={_awareness:0.00} trigger={(_currentTrigger ? _currentTrigger.name : "none")}");

        if (navMeshAgent)
        {
            if (_navStopApplied)
            {
                navMeshAgent.isStopped = false;
                _navStopApplied = false;
            }
        }

        ApplyWanderSuppression();
    }

    private void UpdateFollow()
    {
        if (!navMeshAgent || !_currentTrigger || !navMeshAgent.isOnNavMesh)
        {
            return;
        }

        if (!navMeshAgent.pathPending && navMeshAgent.hasPath)
        {
            if (navMeshAgent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                ExitFollow();
                return;
            }

            if (maxChasePathDistance > 0f)
            {
                float remainingDistance = navMeshAgent.remainingDistance;
                if (!float.IsNaN(remainingDistance)
                    && !float.IsInfinity(remainingDistance)
                    && remainingDistance > maxChasePathDistance)
                {
                    ExitFollow();
                    return;
                }
            }
        }

        if (Time.time < _nextFollowRepathTime)
        {
            return;
        }

        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(_currentTrigger.position);
        _nextFollowRepathTime = Time.time + Mathf.Max(0.05f, followRepathInterval);
    }

    private void ExitFollow()
    {
        if (!_following)
        {
            return;
        }

        _following = false;
        _nextFollowRepathTime = 0f;
        LogDebug("Follow end");

        if (navMeshAgent && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
        }

        ApplyWanderSuppression();
    }

    private void ApplyWanderSuppression()
    {
        if (!npcNavAgent)
        {
            return;
        }

        bool shouldSuppress = (_alertActive && disableWanderDuringAlert)
            || (_following && disableWanderDuringFollow);
        if (_wanderSuppressed == shouldSuppress)
        {
            return;
        }

        npcNavAgent.SetWanderSuppressed(shouldSuppress);
        _wanderSuppressed = shouldSuppress;
        LogDebug($"WanderSuppressed={_wanderSuppressed}");
    }

    private void FaceTrigger()
    {
        if (!_currentTrigger)
        {
            return;
        }

        Vector3 toTarget = _currentTrigger.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, alertRotateSpeed * Time.deltaTime);
    }

    private void StartReorientation()
    {
        _reorientTimer = Mathf.Max(_reorientTimer, Mathf.Max(0f, reorientDuration));
        LogDebug("Reorient start");
        AlignToMovementDirection(instant: true);
    }

    private void UpdateReorientation(float deltaTime)
    {
        if (_reorientTimer <= 0f)
        {
            return;
        }

        _reorientTimer = Mathf.Max(0f, _reorientTimer - deltaTime);
        LogFacingSnapshot("Reorient");
        AlignToMovementDirection(instant: false);
    }

    private void UpdateWanderFacing(float deltaTime)
    {
        if (!forceFaceMovementWhenWandering || _alertActive || _following || _reorientTimer > 0f)
        {
            return;
        }

        LogFacingSnapshot("Wander");
        AlignToMovementDirection(instant: false);
    }

    private void AlignToMovementDirection(bool instant)
    {
        if (!TryGetMovementDirection(out Vector3 direction))
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        if (instant)
        {
            transform.rotation = targetRotation;
            return;
        }

        float rotationSpeed = navMeshAgent.angularSpeed > 0f ? navMeshAgent.angularSpeed : alertRotateSpeed;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private bool TryGetMovementDirection(out Vector3 direction)
    {
        direction = Vector3.zero;
        if (!navMeshAgent || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        Vector3 target = navMeshAgent.hasPath ? navMeshAgent.steeringTarget : navMeshAgent.destination;
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude >= 0.0001f)
        {
            direction = toTarget.normalized;
            return true;
        }

        Vector3 velocity = navMeshAgent.velocity;
        velocity.y = 0f;
        if (velocity.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        direction = velocity.normalized;
        return true;
    }

    private void LogTriggerChange()
    {
        if (!debugLogs || _currentTrigger == _lastDebugTrigger)
        {
            return;
        }

        _lastDebugTrigger = _currentTrigger;
        LogDebug($"Trigger -> {(_currentTrigger ? _currentTrigger.name : "none")}");
    }

    private void LogFacingSnapshot(string context)
    {
        if (!debugLogs || Time.time < _nextDebugFacingTime)
        {
            return;
        }

        _nextDebugFacingTime = Time.time + Mathf.Max(0.1f, debugFacingInterval);
        if (!TryGetMovementDirection(out Vector3 direction))
        {
            bool hasPath = navMeshAgent != null && navMeshAgent.hasPath;
            LogDebug($"{context}: no movement dir (hasPath={hasPath})");
            return;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        float yawDelta = forward.sqrMagnitude > 0.0001f
            ? Vector3.SignedAngle(forward, direction, Vector3.up)
            : 0f;
        bool updateRotation = navMeshAgent != null && navMeshAgent.updateRotation;
        bool isStopped = navMeshAgent != null && navMeshAgent.isStopped;
        LogDebug($"{context}: yawDelta={yawDelta:0.0} updateRotation={updateRotation} isStopped={isStopped}");
    }

    private void LogDebug(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log($"[NpcAwareness] {name} {message}", this);
    }

    private void TriggerRetaliation()
    {
        _retaliationTriggered = true;
        ExitFollow();
        ExitAlert();

        if (retaliateStun)
        {
            retaliateStun.TriggerAwarenessRetaliation(_currentTrigger);
        }
    }

    private void HandleAnyDied(CharacterHealth dead, DamagePayload payload)
    {
        if (!dead)
        {
            return;
        }

        Transform instigator = ResolvePlayerInstigator(payload.Instigator ? payload.Instigator : payload.Source);
        if (!instigator)
        {
            return;
        }

        if (!IsWithinAwareness(dead.transform.position))
        {
            return;
        }

        SetAwarenessFull(instigator);
    }

    private void HandleAnyStunned(CharacterHealth instigator, CharacterHealth target)
    {
        if (!instigator || !target)
        {
            return;
        }

        Transform instigatorTransform = ResolvePlayerInstigator(instigator.gameObject);
        if (!instigatorTransform)
        {
            return;
        }

        if (!IsWithinAwareness(target.transform.position))
        {
            return;
        }

        SetAwarenessFull(instigatorTransform);
    }

    public void ApplyAwarenessBoost(Transform instigator, float normalized)
    {
        if (!instigator)
        {
            return;
        }

        float clamped = Mathf.Clamp01(normalized);
        if (clamped <= 0f)
        {
            return;
        }

        if (!IsWithinAwareness(instigator.position))
        {
            return;
        }

        _currentTrigger = instigator;
        _retaliationTriggered = false;
        SetAwareness(Mathf.Max(_awareness, clamped));
    }

    private void SetAwarenessFull(Transform instigator)
    {
        _currentTrigger = instigator;
        _threatActive = true;
        _retaliationTriggered = false;
        SetAwareness(1f);
    }

    private void SetAwareness(float value)
    {
        _awareness = Mathf.Clamp01(value);
    }

    private bool IsWithinAwareness(Vector3 position)
    {
        Vector3 delta = position - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= awarenessRadius * awarenessRadius;
    }

    private Transform ResolvePlayerInstigator(GameObject instigator)
    {
        if (!instigator)
        {
            return null;
        }

        SimpleCharacterController controller = instigator.GetComponent<SimpleCharacterController>()
            ?? instigator.GetComponentInParent<SimpleCharacterController>()
            ?? instigator.GetComponentInChildren<SimpleCharacterController>(true);
        if (controller)
        {
            return controller.transform;
        }

        CharacterHealth instigatorHealth = instigator.GetComponent<CharacterHealth>()
            ?? instigator.GetComponentInParent<CharacterHealth>()
            ?? instigator.GetComponentInChildren<CharacterHealth>(true);
        if (instigatorHealth)
        {
            LocalVersusGameManager manager = LocalVersusGameManager.Instance;
            if (manager && manager.IsPlayerHealth(instigatorHealth))
            {
                return instigatorHealth.transform;
            }
        }

        return null;
    }

    private void RefreshPlayerCache(float now)
    {
        float refreshInterval = Mathf.Max(0.1f, playerRefreshInterval);
        if (CachedPlayers.Count > 0 && now < _nextPlayerRefreshTime)
        {
            return;
        }

        CachedPlayers.Clear();
        SimpleCharacterController[] players = Object.FindObjectsByType<SimpleCharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i])
            {
                CachedPlayers.Add(players[i]);
            }
        }

        _nextPlayerRefreshTime = now + refreshInterval;
    }

    private void OnValidate()
    {
        awarenessRadius = Mathf.Max(0f, awarenessRadius);
        awarenessFillRate = Mathf.Max(0f, awarenessFillRate);
        awarenessDecayRate = Mathf.Max(0f, awarenessDecayRate);
        alertThreshold = Mathf.Clamp01(alertThreshold);
        followThreshold = Mathf.Clamp01(followThreshold);
        if (followThreshold < alertThreshold)
        {
            followThreshold = alertThreshold;
        }
        alertRotateSpeed = Mathf.Max(0f, alertRotateSpeed);
        scanInterval = Mathf.Max(0.02f, scanInterval);
        playerRefreshInterval = Mathf.Max(0.1f, playerRefreshInterval);
        followRepathInterval = Mathf.Max(0.05f, followRepathInterval);
        reorientDuration = Mathf.Max(0f, reorientDuration);
        debugFacingInterval = Mathf.Max(0.1f, debugFacingInterval);
    }
}
