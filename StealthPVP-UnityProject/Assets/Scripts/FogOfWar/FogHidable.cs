using UnityEngine;

/// <summary>
/// Hides the attached renderers whenever the unit is inside fog.
/// </summary>
public class FogHidable : MonoBehaviour
{
    public bool isAlly = false;

    private Renderer[] cachedRenderers;
    private FogOfWarManager fogManager;

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

    private void LateUpdate()
    {
        if (fogManager == null)
        {
            return;
        }

        if (isAlly)
        {
            return;
        }

        bool visible = fogManager.SampleFog01(transform.position) > 0.5f;
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer r = cachedRenderers[i];
            if (r != null)
            {
                r.enabled = visible;
            }
        }
    }
}
