using UnityEngine;

/// <summary>
/// Ensures the correct fog texture/min/max are bound right before a specific camera renders.
/// Use this when multiple FogOfWarManagers exist (e.g., per local player).
/// </summary>
[RequireComponent(typeof(Camera))]
public class FogOfWarCameraBinder : MonoBehaviour
{
    [SerializeField] private FogOfWarManager fogManager;

    public static FogOfWarManager CurrentFog { get; private set; }
    public FogOfWarManager FogManager => fogManager;

    private void OnPreCull()
    {
        if (fogManager)
        {
            ApplyFog();
        }
    }

    private void OnPreRender()
    {
        if (fogManager)
        {
            ApplyFog();
        }
    }

    private void OnPostRender()
    {
        if (CurrentFog == fogManager)
        {
            CurrentFog = null;
        }
    }

    public void SetFogManager(FogOfWarManager manager)
    {
        fogManager = manager;
    }

    private void ApplyFog()
    {
        CurrentFog = fogManager;
        fogManager.PushShaderProperties();
    }
}
