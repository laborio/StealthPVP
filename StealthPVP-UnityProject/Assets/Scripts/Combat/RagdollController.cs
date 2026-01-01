using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Manages toggling ragdoll physics on death by flipping kinematic state on child rigidbodies.
/// Also handles impulses, collider toggles, blood VFX, and optional camera shake on lethal hits.
/// </summary>
[DisallowMultipleComponent]
public class RagdollController : MonoBehaviour
{
    [SerializeField, Tooltip("Character health that drives ragdoll activation. Defaults to a component on this object.")] private CharacterHealth health;
    [SerializeField, Tooltip("Animator to disable when ragdoll activates. Defaults to a child Animator.")] private Animator animator;
    [SerializeField, Tooltip("Animator that should stay active for VFX while ragdolling. Optional.")] private Animator vfxAnimator;
    [SerializeField, Tooltip("Optional explicit list of rigidbodies to toggle. Leave empty to auto-collect from children.")] private List<Rigidbody> ragdollBodies = new List<Rigidbody>();
    [SerializeField, Tooltip("Whether to disable the animator when ragdolling. Uncheck if you need it left on.")] private bool disableAnimatorOnRagdoll = true;
    [SerializeField, Tooltip("Force kinematic on all ragdoll bodies at Awake.")] private bool initializeAsKinematic = true;
    [SerializeField, Tooltip("Force applied to ragdoll bodies on death.")] private float deathImpulseForce = 0f;
    [SerializeField, Tooltip("Layer mask used when applying death impulse; optional.")] private LayerMask impulseMask = ~0;
    [SerializeField, Tooltip("Colliders to disable on death (typically the root CharacterController or capsule).")] private List<Collider> collidersToDisable = new List<Collider>();
    [SerializeField, Tooltip("If true, impulse direction uses instigator only, ignoring hit point.")] private bool impulseFromInstigatorOnly = false;
    [SerializeField, Tooltip("Colliders that belong to the ragdoll; can be auto-collected.")] private List<Collider> ragdollColliders = new List<Collider>();
    [SerializeField, Tooltip("Colliders converted to triggers on ragdoll (e.g., player capsule) so movement can pass through.")] private List<Collider> collidersToSetTrigger = new List<Collider>();
    [SerializeField, Tooltip("Optional layer to assign to ragdoll colliders on ragdoll activation. -1 leaves unchanged.")] private int ragdollPhysicsLayer = -1;
    [SerializeField, Tooltip("Blood VFX played at the hit point when damaged.")] private ParticleSystem bloodVfx;
    [SerializeField, Tooltip("Character animations to trigger hit reactions. Defaults to a child component.")] private CharacterAnimations characterAnimations;
    [SerializeField, Tooltip("NavMeshAgent to disable when ragdolling. Optional.")] private NavMeshAgent navMeshAgent;
    [SerializeField, Tooltip("Behaviours (e.g., player input/movement scripts) to disable when ragdolling.")] private List<Behaviour> behavioursToDisable = new List<Behaviour>();
    [Header("Camera Shake")]
    [SerializeField, Tooltip("Camera shake to trigger on lethal damage. If empty, will search for one.")] private CameraShake cameraShake;
    [SerializeField, Tooltip("Override shake magnitude on lethal; set < 0 to use CameraShake defaults.")] private float lethalShakeMagnitude = -1f;
    [SerializeField, Tooltip("Override shake duration on lethal; set < 0 to use CameraShake defaults.")] private float lethalShakeDuration = -1f;
    [SerializeField, Tooltip("Enable to print debug logs for ragdoll/camera shake events.")] private bool debugLogs = true;
    [Header("Ragdoll Cleanup")]
    [SerializeField, Tooltip("When enabled, freezes ragdoll bodies shortly after activation and optionally destroys the root after a delay.")] private bool freezeAndCleanupOnRagdoll = false;
    [SerializeField, Tooltip("Frames to wait after ragdoll activation before freezing.")] private int freezeAfterFrames = 3;
    [SerializeField, Tooltip("Seconds to wait after freezing before destroying the GameObject. <= 0 disables auto-destroy.")] private float destroyDelayAfterFreeze = 5f;
    [SerializeField, Tooltip("Trigger fired on the VFX animator when ragdoll is frozen. Leave empty to skip.")] private string npcDeadTriggerName = "NPCDead";
    [Header("Ragdoll Gravity")]
    [SerializeField, Tooltip("Multiplier applied to Physics.gravity for ragdoll bodies. 1 = default physics gravity.")] private float ragdollGravityMultiplier = 1.5f;

    private bool _ragdollActive;
    private DamagePayload _lastDamage;
    private bool _hasLastDamage;
    private Coroutine _freezeRoutine;
    private bool _navAgentWasEnabled;
    private readonly Dictionary<Behaviour, bool> _behaviourStates = new Dictionary<Behaviour, bool>();

    private void Awake()
    {
        if (!health)
        {
            health = GetComponent<CharacterHealth>();
        }

        ResolveAnimators();
        ResolveNavAgent();
        if (!characterAnimations)
        {
            characterAnimations = GetComponentInChildren<CharacterAnimations>();
        }

        ResolveCameraShake();
        EnsureBodies();
        EnsureRagdollColliders();

        if (initializeAsKinematic)
        {
            SetRagdollState(false);
        }

        LogDebug($"Awake: health={(health ? health.name : "null")}, animator={(animator ? animator.name : "null")}, cameraShake={(cameraShake ? cameraShake.name : "null")}");
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            LogDebug("OnEnable: subscribed to health events");
        }
        else
        {
            LogDebug("OnEnable: no health assigned");
        }

        ResolveCameraShake();
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            LogDebug("OnDisable: unsubscribed from health events");
        }

        if (_ragdollActive && collidersToSetTrigger != null && collidersToSetTrigger.Count > 0)
        {
            ToggleCollidersTrigger(collidersToSetTrigger, false);
        }

        LogDebug("OnDisable complete");
    }

    private void OnDied(CharacterHealth _)
    {
        if (_ragdollActive)
        {
            LogDebug("OnDied called but ragdoll already active");
            return;
        }

        LogDebug("OnDied -> triggering ragdoll + camera shake");
        TriggerLethalCameraShake();
        SetRagdollState(true);
    }

    private void OnDamaged(DamagePayload payload)
    {
        _lastDamage = payload;
        _hasLastDamage = true;
        LogDebug($"OnDamaged amount {payload.Amount} from {(payload.Instigator ? payload.Instigator.name : "unknown")}");
        if (!_ragdollActive && characterAnimations)
        {
            Debug.Log("Hit ragdoll");
            characterAnimations.TriggerHit();
        }
        PlayBloodVfx(payload);
    }

    private void FixedUpdate()
    {
        if (_ragdollActive)
        {
            ApplyExtraGravity();
        }
    }

    /// <summary>
    /// Allows manual toggling if needed (true = ragdoll active).
    /// </summary>
    public void SetRagdollState(bool active)
    {
        _ragdollActive = active;

        if (disableAnimatorOnRagdoll)
        {
            SetAnimatorEnabled(animator, !active);
            if (active && vfxAnimator)
            {
                SetAnimatorEnabled(vfxAnimator, true);
            }
        }

        if (active)
        {
            DisableRootColliders();
        }
        else
        {
            EnableRootColliders();
            if (collidersToSetTrigger != null && collidersToSetTrigger.Count > 0)
            {
                ToggleCollidersTrigger(collidersToSetTrigger, false);
            }
        }

        if (navMeshAgent)
        {
            SetNavAgentEnabled(!active);
        }

        ToggleBehaviours(!active);

        for (int i = 0; i < ragdollBodies.Count; i++)
        {
            Rigidbody body = ragdollBodies[i];
            if (!body)
            {
                continue;
            }

            body.isKinematic = !active;
            float impulseForce = GetDeathImpulseForce();
            if (active && impulseForce > 0f && ((1 << body.gameObject.layer) & impulseMask.value) != 0)
            {
                Vector3 dir = GetDeathImpulseDirection(body);
                body.AddForce(dir * impulseForce, ForceMode.Impulse);
            }
        }

        if (active && ragdollColliders != null && ragdollPhysicsLayer >= 0 && ragdollPhysicsLayer <= 31)
        {
            for (int i = 0; i < ragdollColliders.Count; i++)
            {
                Collider col = ragdollColliders[i];
                if (col)
                {
                    col.gameObject.layer = ragdollPhysicsLayer;
                }
            }
        }

        if (active && collidersToSetTrigger != null && collidersToSetTrigger.Count > 0)
        {
        ToggleCollidersTrigger(collidersToSetTrigger, true);
    }

    if (_ragdollActive && freezeAndCleanupOnRagdoll)
    {
            if (_freezeRoutine != null)
            {
                StopCoroutine(_freezeRoutine);
            }
            _freezeRoutine = StartCoroutine(FreezeAndCleanupRoutine());
        }
        else if (!_ragdollActive && _freezeRoutine != null)
        {
            StopCoroutine(_freezeRoutine);
            _freezeRoutine = null;
        }
    }

    private void EnsureBodies()
    {
        if (ragdollBodies != null && ragdollBodies.Count > 0)
        {
            return;
        }

        ragdollBodies = new List<Rigidbody>();
        Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (!bodies[i])
            {
                continue;
            }

            // Skip a rigidbody on the root if present; ragdoll typically only needs child limbs.
            if (bodies[i].gameObject == gameObject)
            {
                continue;
            }

            ragdollBodies.Add(bodies[i]);
        }
    }

    private void EnsureRagdollColliders()
    {
        if (ragdollColliders != null && ragdollColliders.Count > 0)
        {
            return;
        }

        ragdollColliders = new List<Collider>();
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            Collider col = cols[i];
            if (!col)
            {
                continue;
            }

            if (collidersToDisable != null && collidersToDisable.Contains(col))
            {
                continue;
            }

            if (collidersToSetTrigger != null && collidersToSetTrigger.Contains(col))
            {
                continue;
            }

            if (col.gameObject == gameObject)
            {
                continue;
            }

            ragdollColliders.Add(col);
        }
    }

    private void DisableRootColliders()
    {
        if (collidersToDisable == null || collidersToDisable.Count == 0)
        {
            return;
        }

        for (int i = 0; i < collidersToDisable.Count; i++)
        {
            Collider col = collidersToDisable[i];
            if (col)
            {
                col.enabled = false;
            }
        }
    }

    private void EnableRootColliders()
    {
        if (collidersToDisable == null || collidersToDisable.Count == 0)
        {
            return;
        }

        for (int i = 0; i < collidersToDisable.Count; i++)
        {
            Collider col = collidersToDisable[i];
            if (col)
            {
                col.enabled = true;
                col.isTrigger = false;
            }
        }
    }

    private void ToggleCollidersTrigger(List<Collider> list, bool trigger)
    {
        for (int i = 0; i < list.Count; i++)
        {
            Collider col = list[i];
            if (col)
            {
                col.isTrigger = trigger;
            }
        }
    }

    private void PlayBloodVfx(DamagePayload payload)
    {
        if (!bloodVfx)
        {
            return;
        }

        Vector3 hitPoint = payload.HitPoint;
        if (hitPoint.sqrMagnitude < 0.0001f)
        {
            hitPoint = transform.position;
        }

        Vector3 normal = payload.HitNormal.sqrMagnitude > 0.0001f ? payload.HitNormal : Vector3.up;
        bloodVfx.transform.position = hitPoint;
        bloodVfx.transform.rotation = Quaternion.LookRotation(normal);
        bloodVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        bloodVfx.Play(true);
    }

    private void TriggerLethalCameraShake()
    {
        if (!cameraShake)
        {
            ResolveCameraShake();
        }

        if (cameraShake)
        {
            LogDebug($"Camera shake (direct) using {cameraShake.name} mag {lethalShakeMagnitude} dur {lethalShakeDuration}");
            cameraShake.Shake(lethalShakeMagnitude, lethalShakeDuration);
        }
        else
        {
            LogDebug("Camera shake (global) – no direct reference found");
            CameraShake.ShakeGlobal(lethalShakeMagnitude, lethalShakeDuration);
        }

        if (!cameraShake && !CameraShake.Instance)
        {
            Debug.LogWarning($"[RagdollController:{name}] No CameraShake found in scene when trying to shake.", this);
        }
    }

    private Vector3 GetDeathImpulseDirection(Rigidbody body)
    {
        if (_hasLastDamage && _lastDamage.ImpulseDirection.sqrMagnitude > 0.0001f)
        {
            return _lastDamage.ImpulseDirection.normalized;
        }

        if (_hasLastDamage)
        {
            if (!impulseFromInstigatorOnly)
            {
                Vector3 fromHit = body.worldCenterOfMass - _lastDamage.HitPoint;
                fromHit.y = Mathf.Abs(fromHit.y); // favor upward bias but still directional
                if (fromHit.sqrMagnitude > 0.0001f)
                {
                    return fromHit.normalized;
                }
            }

            if (_lastDamage.Instigator)
            {
                Vector3 awayFromInstigator = body.worldCenterOfMass - _lastDamage.Instigator.transform.position;
                awayFromInstigator.y = Mathf.Abs(awayFromInstigator.y);
                if (awayFromInstigator.sqrMagnitude > 0.0001f)
                {
                    return awayFromInstigator.normalized;
                }
            }
        }

        return Vector3.up;
    }

    private float GetDeathImpulseForce()
    {
        if (_hasLastDamage && _lastDamage.ImpulseStrength > 0f)
        {
            return _lastDamage.ImpulseStrength;
        }

        return deathImpulseForce;
    }

    private void OnValidate()
    {
        ragdollPhysicsLayer = Mathf.Clamp(ragdollPhysicsLayer, -1, 31);
        freezeAfterFrames = Mathf.Max(0, freezeAfterFrames);
        destroyDelayAfterFreeze = Mathf.Max(-1f, destroyDelayAfterFreeze);
        ragdollGravityMultiplier = Mathf.Max(0f, ragdollGravityMultiplier);

        if (lethalShakeMagnitude < 0f)
        {
            lethalShakeMagnitude = -1f;
        }
        if (lethalShakeDuration < 0f)
        {
            lethalShakeDuration = -1f;
        }
    }

    private void ResolveCameraShake()
    {
        if (cameraShake)
        {
            return;
        }

        CameraShake found = Object.FindFirstObjectByType<CameraShake>();
        if (!found && Camera.main)
        {
            found = Camera.main.GetComponentInChildren<CameraShake>(true);
        }

        cameraShake = found;
        if (cameraShake)
        {
            LogDebug($"Resolved CameraShake: {cameraShake.name}");
        }
        else
        {
            LogDebug("No CameraShake found in scene");
        }
    }

    private void LogDebug(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log($"[RagdollController:{name}] {message}", this);
    }

    private void ResolveAnimators()
    {
        if (!animator)
        {
            animator = GetComponent<Animator>();
        }

        if (!animator)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (!vfxAnimator || vfxAnimator == animator)
        {
            vfxAnimator = FindSecondaryAnimator(animator);
        }
    }

    private Animator FindSecondaryAnimator(Animator primary)
    {
        Transform meshTransform = transform.Find("Character_Mesh");
        if (meshTransform && meshTransform.TryGetComponent(out Animator meshAnimator) && meshAnimator != primary)
        {
            return meshAnimator;
        }

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator candidate = animators[i];
            if (candidate && candidate != primary)
            {
                return candidate;
            }
        }

        return null;
    }

    private void ResolveNavAgent()
    {
        if (!navMeshAgent)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (!navMeshAgent)
        {
            navMeshAgent = GetComponentInChildren<NavMeshAgent>();
        }

        if (navMeshAgent)
        {
            _navAgentWasEnabled = navMeshAgent.enabled;
        }
    }

    private void ApplyExtraGravity()
    {
        if (ragdollGravityMultiplier <= 1f)
        {
            return;
        }

        Vector3 extraGravity = Physics.gravity * (ragdollGravityMultiplier - 1f);
        for (int i = 0; i < ragdollBodies.Count; i++)
        {
            Rigidbody body = ragdollBodies[i];
            if (body && !body.isKinematic)
            {
                body.AddForce(extraGravity, ForceMode.Acceleration);
            }
        }
    }

    private IEnumerator FreezeAndCleanupRoutine()
    {
        int frames = Mathf.Max(0, freezeAfterFrames);
        for (int i = 0; i < frames; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        FreezeRagdollBodies();

        if (destroyDelayAfterFreeze > 0f)
        {
            yield return new WaitForSeconds(destroyDelayAfterFreeze);
            Destroy(gameObject);
        }

        _freezeRoutine = null;
    }

    private void FreezeRagdollBodies()
    {
        for (int i = 0; i < ragdollBodies.Count; i++)
        {
            Rigidbody body = ragdollBodies[i];
            if (!body)
            {
                continue;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
        }

        if (ragdollColliders != null)
        {
            for (int i = 0; i < ragdollColliders.Count; i++)
            {
                Collider col = ragdollColliders[i];
                if (col)
                {
                    col.enabled = false;
                }
            }
        }

        TriggerNpcDeadVfx();
        LogDebug("Ragdoll bodies frozen for cleanup");
    }

    private void TriggerNpcDeadVfx()
    {
        if (string.IsNullOrEmpty(npcDeadTriggerName))
        {
            return;
        }

        Animator target = vfxAnimator ? vfxAnimator : animator;
        if (!target)
        {
            return;
        }

        int hash = Animator.StringToHash(npcDeadTriggerName);
        if (AnimatorHasTrigger(target, hash, npcDeadTriggerName))
        {
            target.ResetTrigger(hash);
            target.SetTrigger(hash);
            LogDebug($"Triggered VFX death trigger '{npcDeadTriggerName}'");
        }
    }

    private static bool AnimatorHasTrigger(Animator animator, int hash, string name)
    {
        if (!animator)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter param = parameters[i];
            if (param.type != AnimatorControllerParameterType.Trigger)
            {
                continue;
            }

            if ((hash != 0 && param.nameHash == hash) || (!string.IsNullOrEmpty(name) && param.name == name))
            {
                return true;
            }
        }

        return false;
    }

    private static void SetAnimatorEnabled(Animator target, bool enabled)
    {
        if (target)
        {
            target.enabled = enabled;
        }
    }

    private void ToggleBehaviours(bool enable)
    {
        if (behavioursToDisable == null || behavioursToDisable.Count == 0)
        {
            return;
        }

        for (int i = 0; i < behavioursToDisable.Count; i++)
        {
            Behaviour behaviour = behavioursToDisable[i];
            if (!behaviour)
            {
                continue;
            }

            if (!_behaviourStates.ContainsKey(behaviour))
            {
                _behaviourStates.Add(behaviour, behaviour.enabled);
            }

            behaviour.enabled = enable && _behaviourStates[behaviour];
        }
    }

    private void SetNavAgentEnabled(bool enabled)
    {
        if (!navMeshAgent)
        {
            return;
        }

        if (enabled)
        {
            navMeshAgent.enabled = true;
            if (_navAgentWasEnabled)
            {
                navMeshAgent.isStopped = false;
            }
        }
        else
        {
            _navAgentWasEnabled = navMeshAgent.enabled;
            if (navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.ResetPath();
            }
            navMeshAgent.isStopped = true;
            navMeshAgent.enabled = false;
        }
    }
}
