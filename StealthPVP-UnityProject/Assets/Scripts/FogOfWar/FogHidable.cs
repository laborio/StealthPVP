using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Hides the attached renderers whenever the unit is inside fog.
/// </summary>
public class FogHidable : MonoBehaviour
{
    public bool isAlly = false;

    private Renderer[] cachedRenderers;
    private FogOfWarManager fogManager;
    [SerializeField] private bool debugLogs;

    [Header("Minimap")]
    [Tooltip("Color used for the minimap icon if a MinimapIconManager is present.")]
    public Color minimapColor = Color.red;
    [Tooltip("Optional sprite for the minimap icon.")]
    public Sprite minimapSprite;
    [Tooltip("If true, this unit's minimap icon stays visible even when in fog.")]
    public bool minimapIgnoreFog = false;

    private void Awake()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>();
        fogManager = FindObjectOfType<FogOfWarManager>();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
    }

    private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (isAlly)
        {
            return;
        }

        FogOfWarManager activeFog = ResolveFog(cam);
        if (!activeFog)
        {
            if (debugLogs)
            {
                Debug.LogWarning($"[FogHidable] No fog manager for {name} on camera {cam.name}", this);
            }
            return;
        }

        bool visible = activeFog.SampleFog01(transform.position) > 0.5f;
        if (debugLogs)
        {
            Debug.Log($"[FogHidable] {name} cam={cam.name} fog={activeFog.name} visible={visible}");
        }

        ApplyVisibility(!visible);
    }

    private void HandleEndCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        // Reset to visible so the next camera can decide independently.
        ApplyVisibility(false);
    }

    private void ApplyVisibility(bool forceOff)
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer r = cachedRenderers[i];
            if (r != null)
            {
                r.forceRenderingOff = forceOff;
            }
        }
    }

    private FogOfWarManager ResolveFog(Camera cam)
    {
        if (FogOfWarCameraBinder.CurrentFog)
        {
            return FogOfWarCameraBinder.CurrentFog;
        }

        if (cam)
        {
            FogOfWarCameraBinder binder = cam.GetComponent<FogOfWarCameraBinder>();
            if (binder && binder.FogManager)
            {
                return binder.FogManager;
            }
        }

        return fogManager;
    }
}
