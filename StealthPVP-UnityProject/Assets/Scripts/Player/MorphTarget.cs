using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a prop as a valid morph target and registers it for nearby queries.
/// </summary>
[DisallowMultipleComponent]
public class MorphTarget : MonoBehaviour
{
    private static readonly List<MorphTarget> ActiveTargets = new List<MorphTarget>();

    [SerializeField, Tooltip("Optional explicit renderers to use for bounds/visuals.")] private Renderer[] renderers;
    [SerializeField, Tooltip("Optional UI icon used for morph previews.")] private Sprite previewIcon;

    public static IReadOnlyList<MorphTarget> Active => ActiveTargets;
    public Sprite PreviewIcon => previewIcon;

    public Renderer[] Renderers
    {
        get
        {
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }
            return renderers;
        }
    }

    private void OnEnable()
    {
        if (!ActiveTargets.Contains(this))
        {
            ActiveTargets.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveTargets.Remove(this);
    }

    public bool TryGetWorldBounds(out Bounds bounds)
    {
        Renderer[] list = Renderers;
        bounds = default;
        if (list == null || list.Length == 0)
        {
            return false;
        }

        bool hasBounds = false;
        for (int i = 0; i < list.Length; i++)
        {
            Renderer renderer = list[i];
            if (!renderer)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }
}
