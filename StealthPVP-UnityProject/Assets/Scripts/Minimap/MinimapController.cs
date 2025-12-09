using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bridges world positions to minimap UI space and exposes fog sampling helpers.
/// </summary>
public class MinimapController : MonoBehaviour
{
    [Header("References")]
    public FogOfWarManager fog;
    public RectTransform mapRect;      // RawImage rect covering the minimap area
    public RectTransform iconsRoot;    // Parent for icon RectTransforms
    public RawImage backgroundImage;   // Optional: background render texture or static map

    [Header("World bounds (auto-filled from FogOfWarManager)")]
    public Vector2 worldMin;
    public Vector2 worldMax;

    private void Awake()
    {
        if (fog == null)
        {
            fog = FindObjectOfType<FogOfWarManager>();
        }

        if (fog != null)
        {
            worldMin = fog.worldMin;
            worldMax = fog.worldMax;
        }
    }

    /// <summary>
    /// Converts world position to local anchored position inside the minimap rect.
    /// </summary>
    public Vector2 WorldToMinimap(Vector3 worldPos)
    {
        float nx = Mathf.InverseLerp(worldMin.x, worldMax.x, worldPos.x);
        float nz = Mathf.InverseLerp(worldMin.y, worldMax.y, worldPos.z);
        Vector2 size = mapRect.rect.size;
        return new Vector2((nx - 0.5f) * size.x, (nz - 0.5f) * size.y);
    }

    public bool IsVisible(Vector3 worldPos)
    {
        return fog != null && fog.SampleFog01(worldPos) > 0.5f;
    }
}
