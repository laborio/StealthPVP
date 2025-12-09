using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Automatically spawns and manages minimap icons for all FogHidable units.
/// Icons are created/destroyed as NPCs spawn or die, and hide when their FogHidable says so.
/// </summary>
public class MinimapIconManager : MonoBehaviour
{
    [Header("References")]
    public MinimapController minimap;
    public RectTransform iconsRoot;
    public MinimapIcon iconPrefab;

    [Header("Appearance")]
    public Vector2 iconSize = new Vector2(12f, 12f);
    public Color defaultColor = Color.red;
    public Sprite defaultSprite;

    [Header("Update")]
    [Tooltip("How often (seconds) to rescan the scene for FogHidable units.")]
    public float refreshInterval = 0.5f;

    private readonly Dictionary<FogHidable, MinimapIcon> activeIcons = new Dictionary<FogHidable, MinimapIcon>();
    private float refreshTimer;

    private void Awake()
    {
        if (minimap == null)
        {
            minimap = FindObjectOfType<MinimapController>();
        }

        if (iconsRoot == null && minimap != null)
        {
            iconsRoot = minimap.iconsRoot;
        }
    }

    private void Update()
    {
        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f)
        {
            SyncIcons();
            refreshTimer = Mathf.Max(0.05f, refreshInterval);
        }
    }

    private void SyncIcons()
    {
        if (minimap == null || iconsRoot == null)
        {
            return;
        }

        var found = FindObjectsByType<FogHidable>(FindObjectsSortMode.None);
        var seen = new HashSet<FogHidable>(found);

        // Add new
        foreach (var hidable in found)
        {
            if (hidable == null || activeIcons.ContainsKey(hidable))
            {
                continue;
            }

            MinimapIcon icon = CreateIcon(hidable);
            if (icon != null)
            {
                activeIcons.Add(hidable, icon);
            }
        }

        // Remove missing/destroyed
        var toRemove = new List<FogHidable>();
        foreach (var kvp in activeIcons)
        {
            if (kvp.Key == null || !seen.Contains(kvp.Key))
            {
                DestroyIcon(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        for (int i = 0; i < toRemove.Count; i++)
        {
            activeIcons.Remove(toRemove[i]);
        }
    }

    private MinimapIcon CreateIcon(FogHidable hidable)
    {
        MinimapIcon icon;
        if (iconPrefab != null)
        {
            icon = Instantiate(iconPrefab, iconsRoot);
        }
        else
        {
            // Fallback: create a simple image+icon.
            GameObject go = new GameObject("MinimapIcon_" + hidable.gameObject.name, typeof(RectTransform), typeof(Image), typeof(MinimapIcon));
            go.transform.SetParent(iconsRoot, false);
            icon = go.GetComponent<MinimapIcon>();
        }

        icon.minimap = minimap;
        icon.target = hidable.transform;
        icon.hideInFog = !hidable.isAlly && !hidable.minimapIgnoreFog;

        Color c = hidable.minimapColor != default ? hidable.minimapColor : defaultColor;
        Sprite s = hidable.minimapSprite != null ? hidable.minimapSprite : defaultSprite;
        icon.SetAppearance(c, s, iconSize);

        return icon;
    }

    private void DestroyIcon(MinimapIcon icon)
    {
        if (icon != null)
        {
            Destroy(icon.gameObject);
        }
    }
}
