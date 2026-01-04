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
    [SerializeField, Tooltip("If true, allow morphing into nearby NPCs by copying their materials.")] private bool allowNpcMorph = true;
    [SerializeField, Tooltip("Move speed override while morphed into an NPC. Use -1 for default speed.")] private float npcMorphMoveSpeed = -1f;
    [SerializeField, Tooltip("Color tolerance for considering NPC materials similar to the player.")] private float npcMaterialColorTolerance = 0.05f;
    [SerializeField, Tooltip("Enable debug logs.")] private bool debugLogs = false;

    private CharacterController _characterController;
    private SimpleCharacterController _controller;
    private CharacterHealth _health;
    private PlayerFloatingTextController _floatingText;
    private CharacterAnimations _characterAnimations;
    private SkinnedMeshRenderer[] _playerRenderers;
    private bool[] _playerRendererStates;
    private Material[][] _playerRendererMaterials;
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

    private enum MorphMode
    {
        None,
        Prop,
        Npc
    }

    private MorphMode _activeMorphMode = MorphMode.None;

    public bool IsMorphed => _isMorphed;
    public bool IsNpcMorph => _isMorphed && _activeMorphMode == MorphMode.Npc;

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
        _playerRendererMaterials = new Material[_playerRenderers.Length][];
        CachePlayerRendererData();

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

    public void ApplyNpcMorphConfig(bool allowNpc, float moveSpeedOverride, float materialColorTolerance)
    {
        allowNpcMorph = allowNpc;
        npcMorphMoveSpeed = moveSpeedOverride;
        npcMaterialColorTolerance = Mathf.Max(0f, materialColorTolerance);
    }

    public bool TryTrigger()
    {
        if (_isMorphed)
        {
            return false;
        }

        MorphTarget propTarget = FindNearestPropTarget(out float propDistanceSqr);
        bool hasProp = propTarget != null;
        NpcIdentity npcTarget = null;
        SkinnedMeshRenderer npcRenderer = null;
        float npcDistanceSqr = float.MaxValue;
        bool hasNpc = allowNpcMorph && TryFindNearestNpc(out npcTarget, out npcRenderer, out npcDistanceSqr);

        if (!hasProp && !hasNpc)
        {
            LogDebug("No morph target in range.");
            return false;
        }

        if (hasProp && (!hasNpc || propDistanceSqr <= npcDistanceSqr))
        {
            BeginPropMorph(propTarget);
        }
        else
        {
            BeginNpcMorph(npcTarget, npcRenderer);
        }

        return true;
    }

    public void BreakMorph()
    {
        if (!_isMorphed)
        {
            return;
        }

        MorphMode previousMode = _activeMorphMode;
        _isMorphed = false;
        _morphTimer = 0f;
        SetMorphStatusActive(false);
        if (morphVisualRoot)
        {
            morphVisualRoot.gameObject.SetActive(false);
        }
        if (previousMode == MorphMode.Npc)
        {
            RestorePlayerRendererMaterials();
        }
        RestorePlayerRenderers();
        ClearMorphVisuals();
        RestoreCollider();
        _controller?.SetMorphState(false, 0f);
        _activeMorphMode = MorphMode.None;
    }

    private MorphTarget FindNearestPropTarget(out float bestDistance)
    {
        IReadOnlyList<MorphTarget> candidates = explicitTargets != null && explicitTargets.Count > 0
            ? explicitTargets
            : MorphTarget.Active;

        bestDistance = float.MaxValue;
        if (candidates == null || candidates.Count == 0)
        {
            return null;
        }

        Vector3 origin = transform.position;
        float maxDistanceSqr = morphSearchRadius * morphSearchRadius;
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

    private bool TryFindNearestNpc(out NpcIdentity bestNpc, out SkinnedMeshRenderer bestRenderer, out float bestDistance)
    {
        bestNpc = null;
        bestRenderer = null;
        bestDistance = float.MaxValue;

        CachePlayerRendererData();

        NpcIdentity[] candidates = Object.FindObjectsByType<NpcIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (candidates == null || candidates.Length == 0)
        {
            return false;
        }

        Vector3 origin = transform.position;
        float maxDistanceSqr = morphSearchRadius * morphSearchRadius;

        for (int i = 0; i < candidates.Length; i++)
        {
            NpcIdentity identity = candidates[i];
            if (!identity)
            {
                continue;
            }

            if (identity.transform.root == transform.root)
            {
                continue;
            }

            if (identity.GetComponentInParent<PlayerInputRouter>())
            {
                continue;
            }

            CharacterHealth health = identity.GetComponentInParent<CharacterHealth>()
                ?? identity.GetComponentInChildren<CharacterHealth>(true);
            if (health && health.IsDead)
            {
                continue;
            }

            SkinnedMeshRenderer npcRenderer = identity.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (!npcRenderer)
            {
                continue;
            }

            if (IsNpcMaterialSimilar(npcRenderer))
            {
                continue;
            }

            Vector3 center = npcRenderer.bounds.center;
            float sqrDistance = (center - origin).sqrMagnitude;
            if (sqrDistance > maxDistanceSqr)
            {
                continue;
            }

            if (sqrDistance < bestDistance)
            {
                bestDistance = sqrDistance;
                bestNpc = identity;
                bestRenderer = npcRenderer;
            }
        }

        return bestNpc != null;
    }

    private void BeginPropMorph(MorphTarget target)
    {
        if (!_characterController || !target)
        {
            return;
        }

        _isMorphed = true;
        _activeMorphMode = MorphMode.Prop;
        _morphTimer = Mathf.Max(0f, morphDurationSeconds);
        SetMorphStatusActive(true);
        transform.rotation = target.transform.rotation;
        if (morphVisualRoot)
        {
            morphVisualRoot.gameObject.SetActive(true);
        }
        CachePlayerRendererData();
        HidePlayerRenderers();
        BuildMorphVisuals(target);
        ApplyColliderFromTarget(target);
        _characterController.enableOverlapRecovery = false;
        _controller?.SetMorphState(true, morphMoveSpeed);
    }

    private void BeginNpcMorph(NpcIdentity npcTarget, SkinnedMeshRenderer npcRenderer)
    {
        if (!_characterController || !npcTarget || !npcRenderer)
        {
            return;
        }

        _isMorphed = true;
        _activeMorphMode = MorphMode.Npc;
        _morphTimer = Mathf.Max(0f, morphDurationSeconds);
        SetMorphStatusActive(true);
        if (morphVisualRoot)
        {
            morphVisualRoot.gameObject.SetActive(false);
        }
        CachePlayerRendererData();
        ApplyNpcMaterials(npcRenderer);
        _controller?.SetMorphState(true, npcMorphMoveSpeed);
    }

    private void RefreshPreviewSprite()
    {
        MorphTarget propTarget = FindNearestPropTarget(out float propDistanceSqr);
        bool hasProp = propTarget != null;
        NpcIdentity npcTarget = null;
        float npcDistanceSqr = float.MaxValue;
        bool hasNpc = allowNpcMorph && TryFindNearestNpc(out npcTarget, out _, out npcDistanceSqr);

        if (hasProp && (!hasNpc || propDistanceSqr <= npcDistanceSqr))
        {
            _previewSprite = propTarget.PreviewIcon;
        }
        else if (hasNpc)
        {
            _previewSprite = npcTarget ? npcTarget.PreviewIcon : null;
        }
        else
        {
            _previewSprite = null;
        }
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

    private void CachePlayerRendererData()
    {
        if (_playerRenderers == null)
        {
            return;
        }

        if (_playerRendererStates == null || _playerRendererStates.Length != _playerRenderers.Length)
        {
            _playerRendererStates = new bool[_playerRenderers.Length];
        }

        if (_playerRendererMaterials == null || _playerRendererMaterials.Length != _playerRenderers.Length)
        {
            _playerRendererMaterials = new Material[_playerRenderers.Length][];
        }

        for (int i = 0; i < _playerRenderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = _playerRenderers[i];
            if (!renderer)
            {
                continue;
            }

            _playerRendererStates[i] = renderer.enabled;
            _playerRendererMaterials[i] = renderer.sharedMaterials;
        }
    }

    private void RestorePlayerRendererMaterials()
    {
        if (_playerRenderers == null || _playerRendererMaterials == null)
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

            Material[] materials = i < _playerRendererMaterials.Length ? _playerRendererMaterials[i] : null;
            if (materials != null)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    private void ApplyNpcMaterials(SkinnedMeshRenderer sourceRenderer)
    {
        if (!sourceRenderer || _playerRenderers == null)
        {
            return;
        }

        Material[] sourceMaterials = sourceRenderer.sharedMaterials;
        if (sourceMaterials == null || sourceMaterials.Length == 0)
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

            renderer.sharedMaterials = sourceMaterials;
        }
    }

    private bool IsNpcMaterialSimilar(SkinnedMeshRenderer npcRenderer)
    {
        if (!npcRenderer || _playerRendererMaterials == null || _playerRendererMaterials.Length == 0)
        {
            return false;
        }

        Material[] npcMaterials = npcRenderer.sharedMaterials;
        if (npcMaterials == null || npcMaterials.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < npcMaterials.Length; i++)
        {
            Material npcMaterial = npcMaterials[i];
            if (!npcMaterial)
            {
                continue;
            }

            for (int j = 0; j < _playerRendererMaterials.Length; j++)
            {
                Material[] playerMaterials = _playerRendererMaterials[j];
                if (playerMaterials == null)
                {
                    continue;
                }

                for (int k = 0; k < playerMaterials.Length; k++)
                {
                    Material playerMaterial = playerMaterials[k];
                    if (!playerMaterial)
                    {
                        continue;
                    }

                    if (IsMaterialSimilar(npcMaterial, playerMaterial))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private bool IsMaterialSimilar(Material a, Material b)
    {
        if (!a || !b)
        {
            return false;
        }

        if (a == b)
        {
            return true;
        }

        string aName = StripMaterialInstanceSuffix(a.name);
        string bName = StripMaterialInstanceSuffix(b.name);
        if (!string.IsNullOrEmpty(aName) && aName == bName)
        {
            return true;
        }

        if (a.shader != b.shader)
        {
            return false;
        }

        if (a.mainTexture != b.mainTexture)
        {
            return false;
        }

        if (TryGetMaterialColor(a, out Color aColor) && TryGetMaterialColor(b, out Color bColor))
        {
            Vector3 diff = new Vector3(aColor.r - bColor.r, aColor.g - bColor.g, aColor.b - bColor.b);
            return diff.sqrMagnitude <= npcMaterialColorTolerance * npcMaterialColorTolerance;
        }

        return false;
    }

    private static bool TryGetMaterialColor(Material material, out Color color)
    {
        if (material && material.HasProperty("_BaseColor"))
        {
            color = material.GetColor("_BaseColor");
            return true;
        }

        if (material && material.HasProperty("_Color"))
        {
            color = material.GetColor("_Color");
            return true;
        }

        color = Color.white;
        return false;
    }

    private static string StripMaterialInstanceSuffix(string name)
    {
        const string suffix = " (Instance)";
        if (!string.IsNullOrEmpty(name) && name.EndsWith(suffix))
        {
            return name.Substring(0, name.Length - suffix.Length);
        }

        return name;
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
