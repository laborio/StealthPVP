using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Morphs the player into a nearby prop for a short duration.
/// </summary>
[DisallowMultipleComponent]
public class MorphAbility : MonoBehaviour
{
    [SerializeField, Tooltip("Seconds the morph stays active.")] private float morphDurationSeconds = 6f;
    [SerializeField, Tooltip("Move speed while morphed.")] private float morphMoveSpeed = 1.25f;
    [SerializeField, Tooltip("Max distance to search for a morph target.")] private float morphSearchRadius = 6f;
    [SerializeField, Tooltip("Optional explicit targets; if empty, uses all active MorphTargets.")] private List<MorphTarget> explicitTargets = new List<MorphTarget>();
    [SerializeField, Tooltip("Optional visual root to host the morphed mesh.")] private Transform morphVisualRoot;
    [SerializeField, Tooltip("Minimum capsule height while morphed.")] private float minMorphHeight = 0.25f;
    [SerializeField, Tooltip("Minimum capsule radius while morphed.")] private float minMorphRadius = 0.1f;
    [SerializeField, Tooltip("Seconds between preview target refreshes.")] private float previewRefreshInterval = 0.25f;
    [SerializeField, Tooltip("Enable debug logs.")] private bool debugLogs = false;

    private CharacterController _characterController;
    private SimpleCharacterController _controller;
    private CharacterHealth _health;
    private PlayerFloatingTextController _floatingText;
    private CharacterAnimations _characterAnimations;
    private SkinnedMeshRenderer[] _playerRenderers;
    private bool[] _playerRendererStates;
    private float _morphTimer;
    private bool _isMorphed;
    private float _originalHeight;
    private float _originalRadius;
    private Vector3 _originalCenter;
    private bool _originalOverlapRecovery;
    private bool _statusActive;
    private float _previewNextUpdateTime;
    private Sprite _previewSprite;

    private const string MorphStatusKey = "Morph";
    private const int MorphStatusPriority = 3;

    public bool IsMorphed => _isMorphed;

    public bool TryGetPreviewSprite(out Sprite sprite)
    {
        if (Time.time >= _previewNextUpdateTime)
        {
            RefreshPreviewSprite();
            _previewNextUpdateTime = Time.time + Mathf.Max(0.05f, previewRefreshInterval);
        }

        sprite = _previewSprite;
        return sprite != null;
    }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _controller = GetComponent<SimpleCharacterController>() ?? GetComponentInChildren<SimpleCharacterController>(true);
        _health = GetComponent<CharacterHealth>() ?? GetComponentInChildren<CharacterHealth>(true);
        _floatingText = GetComponent<PlayerFloatingTextController>()
            ?? GetComponentInChildren<PlayerFloatingTextController>(true);
        _characterAnimations = GetComponent<CharacterAnimations>()
            ?? GetComponentInChildren<CharacterAnimations>(true);

        _playerRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        _playerRendererStates = new bool[_playerRenderers.Length];

        if (_characterController)
        {
            _originalHeight = _characterController.height;
            _originalRadius = _characterController.radius;
            _originalCenter = _characterController.center;
            _originalOverlapRecovery = _characterController.enableOverlapRecovery;
        }

        EnsureVisualRoot();
    }

    private void OnEnable()
    {
        if (_health)
        {
            _health.Damaged += HandleDamaged;
            _health.Died += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (_isMorphed)
        {
            BreakMorph();
        }

        if (_health)
        {
            _health.Damaged -= HandleDamaged;
            _health.Died -= HandleDied;
        }
    }

    private void Update()
    {
        if (!_isMorphed)
        {
            return;
        }

        if (_characterAnimations && _characterAnimations.IsInAttackState())
        {
            BreakMorph();
            return;
        }

        if (_morphTimer > 0f)
        {
            _morphTimer = Mathf.Max(0f, _morphTimer - Time.deltaTime);
            if (_morphTimer <= 0f)
            {
                BreakMorph();
            }
            else
            {
                UpdateMorphStatus();
            }
        }
    }

    public void ApplyMorphConfig(float durationSeconds, float moveSpeed, float searchRadius)
    {
        morphDurationSeconds = Mathf.Max(0f, durationSeconds);
        morphMoveSpeed = Mathf.Max(0f, moveSpeed);
        morphSearchRadius = Mathf.Max(0f, searchRadius);
    }

    public bool TryTrigger()
    {
        if (_isMorphed)
        {
            return false;
        }

        MorphTarget target = FindNearestTarget();
        if (!target)
        {
            LogDebug("No morph target in range.");
            return false;
        }

        BeginMorph(target);
        return true;
    }

    public void BreakMorph()
    {
        if (!_isMorphed)
        {
            return;
        }

        _isMorphed = false;
        _morphTimer = 0f;
        SetMorphStatusActive(false);
        if (morphVisualRoot)
        {
            morphVisualRoot.gameObject.SetActive(false);
        }
        RestorePlayerRenderers();
        ClearMorphVisuals();
        RestoreCollider();
        _controller?.SetMorphState(false, 0f);
    }

    private MorphTarget FindNearestTarget()
    {
        IReadOnlyList<MorphTarget> candidates = explicitTargets != null && explicitTargets.Count > 0
            ? explicitTargets
            : MorphTarget.Active;

        if (candidates == null || candidates.Count == 0)
        {
            return null;
        }

        Vector3 origin = transform.position;
        float maxDistanceSqr = morphSearchRadius * morphSearchRadius;
        float bestDistance = float.MaxValue;
        MorphTarget best = null;

        for (int i = 0; i < candidates.Count; i++)
        {
            MorphTarget target = candidates[i];
            if (!target)
            {
                continue;
            }

            if (!target.TryGetWorldBounds(out Bounds bounds))
            {
                continue;
            }

            Vector3 center = bounds.center;
            float sqrDistance = (center - origin).sqrMagnitude;
            if (sqrDistance > maxDistanceSqr)
            {
                continue;
            }

            if (sqrDistance < bestDistance)
            {
                bestDistance = sqrDistance;
                best = target;
            }
        }

        return best;
    }

    private void BeginMorph(MorphTarget target)
    {
        if (!_characterController || !target)
        {
            return;
        }

        _isMorphed = true;
        _morphTimer = Mathf.Max(0f, morphDurationSeconds);
        SetMorphStatusActive(true);
        transform.rotation = target.transform.rotation;
        if (morphVisualRoot)
        {
            morphVisualRoot.gameObject.SetActive(true);
        }
        HidePlayerRenderers();
        BuildMorphVisuals(target);
        ApplyColliderFromTarget(target);
        _characterController.enableOverlapRecovery = false;
        _controller?.SetMorphState(true, morphMoveSpeed);
    }

    private void RefreshPreviewSprite()
    {
        MorphTarget target = FindNearestTarget();
        _previewSprite = target ? target.PreviewIcon : null;
    }

    private void EnsureVisualRoot()
    {
        if (morphVisualRoot)
        {
            return;
        }

        GameObject root = new GameObject("MorphVisualRoot");
        root.layer = gameObject.layer;
        root.transform.SetParent(transform, false);
        morphVisualRoot = root.transform;
    }

    private void HidePlayerRenderers()
    {
        if (_playerRenderers == null)
        {
            return;
        }

        for (int i = 0; i < _playerRenderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = _playerRenderers[i];
            if (!renderer)
            {
                continue;
            }

            _playerRendererStates[i] = renderer.enabled;
            renderer.enabled = false;
        }
    }

    private void RestorePlayerRenderers()
    {
        if (_playerRenderers == null)
        {
            return;
        }

        for (int i = 0; i < _playerRenderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = _playerRenderers[i];
            if (!renderer)
            {
                continue;
            }

            renderer.enabled = i < _playerRendererStates.Length && _playerRendererStates[i];
        }
    }

    private void ClearMorphVisuals()
    {
        if (!morphVisualRoot)
        {
            return;
        }

        for (int i = morphVisualRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = morphVisualRoot.GetChild(i);
            if (child)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        morphVisualRoot.localScale = Vector3.one;
    }

    private void BuildMorphVisuals(MorphTarget target)
    {
        if (!morphVisualRoot || !target)
        {
            return;
        }

        ClearMorphVisuals();
        morphVisualRoot.localPosition = Vector3.zero;
        morphVisualRoot.localRotation = Quaternion.identity;
        morphVisualRoot.localScale = GetCompensatedScale(target.transform.lossyScale, transform.lossyScale);
        morphVisualRoot.gameObject.layer = gameObject.layer;
        Renderer[] renderers = target.Renderers;
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        Matrix4x4 worldToLocal = target.transform.worldToLocalMatrix;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer sourceRenderer = renderers[i];
            if (!sourceRenderer)
            {
                continue;
            }

            MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
            if (!sourceFilter || !sourceFilter.sharedMesh)
            {
                continue;
            }

            GameObject child = new GameObject(sourceRenderer.name);
            child.transform.SetParent(morphVisualRoot, false);
            child.layer = gameObject.layer;

            Matrix4x4 localMatrix = worldToLocal * sourceRenderer.transform.localToWorldMatrix;
            ApplyMatrix(child.transform, localMatrix);

            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
            meshRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
            meshRenderer.receiveShadows = sourceRenderer.receiveShadows;
            meshRenderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
            meshRenderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
            meshRenderer.motionVectorGenerationMode = sourceRenderer.motionVectorGenerationMode;
        }
    }

    private void ApplyColliderFromTarget(MorphTarget target)
    {
        if (!_characterController || !target)
        {
            return;
        }

        if (!target.TryGetWorldBounds(out Bounds bounds))
        {
            return;
        }

        float height = Mathf.Max(bounds.size.y, minMorphHeight);
        float radius = Mathf.Max(Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f, minMorphRadius);
        radius = Mathf.Min(radius, height * 0.5f);

        float originalBottom = _originalCenter.y - (_originalHeight * 0.5f);
        Vector3 center = _originalCenter;
        center.y = originalBottom + (height * 0.5f);

        _characterController.height = height;
        _characterController.radius = radius;
        _characterController.center = center;
    }

    private void RestoreCollider()
    {
        if (!_characterController)
        {
            return;
        }

        _characterController.height = _originalHeight;
        _characterController.radius = _originalRadius;
        _characterController.center = _originalCenter;
        _characterController.enableOverlapRecovery = _originalOverlapRecovery;
    }

    private void HandleDamaged(DamagePayload payload)
    {
        if (_isMorphed)
        {
            BreakMorph();
        }
    }

    private void HandleDied(CharacterHealth health)
    {
        if (_isMorphed)
        {
            BreakMorph();
        }
    }

    private void LogDebug(string message)
    {
        if (debugLogs)
        {
            Debug.Log($"[MorphAbility:{name}] {message}", this);
        }
    }

    private void SetMorphStatusActive(bool active)
    {
        if (_statusActive == active)
        {
            if (active)
            {
                UpdateMorphStatus();
            }
            return;
        }

        _statusActive = active;

        if (!_floatingText)
        {
            _floatingText = GetComponent<PlayerFloatingTextController>()
                ?? GetComponentInChildren<PlayerFloatingTextController>(true);
        }

        if (!_floatingText)
        {
            return;
        }

        _floatingText.SetStatusActive(MorphStatusKey, active);
        if (active)
        {
            UpdateMorphStatus();
        }
    }

    private void UpdateMorphStatus()
    {
        if (!_floatingText)
        {
            _floatingText = GetComponent<PlayerFloatingTextController>()
                ?? GetComponentInChildren<PlayerFloatingTextController>(true);
        }

        if (!_floatingText)
        {
            return;
        }

        int seconds = Mathf.CeilToInt(_morphTimer);
        string label = seconds.ToString();
        _floatingText.SetStatusLabel(MorphStatusKey, label, MorphStatusPriority);
    }

    private static void ApplyMatrix(Transform target, Matrix4x4 matrix)
    {
        target.localPosition = matrix.GetColumn(3);
        target.localRotation = Quaternion.LookRotation(matrix.GetColumn(2), matrix.GetColumn(1));
        target.localScale = new Vector3(
            new Vector3(matrix.m00, matrix.m10, matrix.m20).magnitude,
            new Vector3(matrix.m01, matrix.m11, matrix.m21).magnitude,
            new Vector3(matrix.m02, matrix.m12, matrix.m22).magnitude);
    }

    private static Vector3 GetCompensatedScale(Vector3 targetScale, Vector3 parentScale)
    {
        float x = Mathf.Abs(parentScale.x) > 0.0001f ? targetScale.x / parentScale.x : targetScale.x;
        float y = Mathf.Abs(parentScale.y) > 0.0001f ? targetScale.y / parentScale.y : targetScale.y;
        float z = Mathf.Abs(parentScale.z) > 0.0001f ? targetScale.z / parentScale.z : targetScale.z;
        return new Vector3(x, y, z);
    }
}
