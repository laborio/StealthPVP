using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Handles per-player invisibility by toggling a shader property and
/// moving renderers to the owning player's camera-only layer.
/// </summary>
public class PlayerInvisibility : MonoBehaviour
{
    [SerializeField] private string invisibleProperty = "_isInvisible";
    [SerializeField] private bool disableShadowsWhileInvisible = true;
    [SerializeField] private bool includeInactiveRenderers = true;

    private Renderer[] _renderers;
    private SkinnedMeshRenderer[] _skinnedRenderers;
    private int[] _originalLayers;
    private ShadowCastingMode[] _originalShadowModes;
    private bool[] _originalReceiveShadows;
    private MaterialPropertyBlock _propertyBlock;
    private int _invisiblePropertyId;
    private bool _isInvisible;
    private float _timer;
    private bool _playerLayerResolved;
    private int _playerOnlyLayer = -1;

    public bool IsInvisible => _isInvisible;

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();
        _invisiblePropertyId = Shader.PropertyToID(invisibleProperty);
        CacheRenderers();
    }

    private void Update()
    {
        if (!_isInvisible)
        {
            return;
        }

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            EndInvisibility();
        }
    }

    public void ApplyInvisibility(float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            EndInvisibility();
            return;
        }

        CacheRenderers();
        ResolvePlayerOnlyLayer();

        if (!_isInvisible)
        {
            CacheOriginalStates();
            ApplyPlayerOnlyLayer();
            SetInvisibleProperty(true);
            _isInvisible = true;
        }

        _timer = Mathf.Max(_timer, durationSeconds);
    }

    public void EndInvisibility()
    {
        if (!_isInvisible)
        {
            return;
        }

        RestoreOriginalStates();
        SetInvisibleProperty(false);
        _isInvisible = false;
        _timer = 0f;
    }

    private void OnDisable()
    {
        EndInvisibility();
    }

    private void CacheRenderers()
    {
        _renderers = GetComponentsInChildren<Renderer>(includeInactiveRenderers);
        _skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(includeInactiveRenderers);
    }

    private void CacheOriginalStates()
    {
        if (_renderers == null || _renderers.Length == 0)
        {
            return;
        }

        _originalLayers = new int[_renderers.Length];
        if (disableShadowsWhileInvisible)
        {
            _originalShadowModes = new ShadowCastingMode[_renderers.Length];
            _originalReceiveShadows = new bool[_renderers.Length];
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer renderer = _renderers[i];
            if (!renderer)
            {
                continue;
            }

            _originalLayers[i] = renderer.gameObject.layer;
            if (disableShadowsWhileInvisible)
            {
                _originalShadowModes[i] = renderer.shadowCastingMode;
                _originalReceiveShadows[i] = renderer.receiveShadows;
            }
        }
    }

    private void ApplyPlayerOnlyLayer()
    {
        if (_renderers == null || _renderers.Length == 0)
        {
            return;
        }

        if (_playerOnlyLayer < 0)
        {
            return;
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer renderer = _renderers[i];
            if (!renderer)
            {
                continue;
            }

            renderer.gameObject.layer = _playerOnlyLayer;
            if (disableShadowsWhileInvisible)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }

    private void RestoreOriginalStates()
    {
        if (_renderers == null || _originalLayers == null)
        {
            return;
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer renderer = _renderers[i];
            if (!renderer)
            {
                continue;
            }

            if (i < _originalLayers.Length)
            {
                renderer.gameObject.layer = _originalLayers[i];
            }

            if (disableShadowsWhileInvisible && _originalShadowModes != null && _originalReceiveShadows != null
                && i < _originalShadowModes.Length && i < _originalReceiveShadows.Length)
            {
                renderer.shadowCastingMode = _originalShadowModes[i];
                renderer.receiveShadows = _originalReceiveShadows[i];
            }
        }
    }

    private void SetInvisibleProperty(bool value)
    {
        if (_skinnedRenderers == null || _skinnedRenderers.Length == 0 || _invisiblePropertyId == 0)
        {
            return;
        }

        float floatValue = value ? 1f : 0f;
        for (int i = 0; i < _skinnedRenderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = _skinnedRenderers[i];
            if (!renderer)
            {
                continue;
            }

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(_invisiblePropertyId, floatValue);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private void ResolvePlayerOnlyLayer()
    {
        if (_playerLayerResolved)
        {
            return;
        }

        _playerLayerResolved = true;
        LocalVersusGameManager manager = LocalVersusGameManager.Instance;
        if (!manager)
        {
            manager = FindFirstObjectByType<LocalVersusGameManager>();
        }

        if (!manager)
        {
            return;
        }

        Transform self = transform;
        if (manager._player1Instance && self.IsChildOf(manager._player1Instance.transform))
        {
            _playerOnlyLayer = LayerMask.NameToLayer(manager.player1OnlyLayer);
        }
        else if (manager._player2Instance && self.IsChildOf(manager._player2Instance.transform))
        {
            _playerOnlyLayer = LayerMask.NameToLayer(manager.player2OnlyLayer);
        }
        else if (manager._player3Instance && self.IsChildOf(manager._player3Instance.transform))
        {
            _playerOnlyLayer = LayerMask.NameToLayer(manager.player3OnlyLayer);
        }

        if (_playerOnlyLayer < 0)
        {
            Debug.LogWarning("PlayerInvisibility: Player-only layer not found. Check Tags and Layers settings.", this);
        }
    }

    private void OnValidate()
    {
        if (!string.IsNullOrEmpty(invisibleProperty))
        {
            _invisiblePropertyId = Shader.PropertyToID(invisibleProperty);
        }
    }
}
