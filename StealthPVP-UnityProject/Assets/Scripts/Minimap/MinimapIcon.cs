using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives a UI icon that follows a world target and hides when the target is in fog.
/// </summary>
[RequireComponent(typeof(Image))]
public class MinimapIcon : MonoBehaviour
{
    public Transform target;
    public MinimapController minimap;
    public bool hideInFog = true;

    private RectTransform rect;
    private Image image;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        image = GetComponent<Image>();
    }

    private void LateUpdate()
    {
        if (target == null || minimap == null || minimap.mapRect == null)
        {
            return;
        }

        rect.anchoredPosition = minimap.WorldToMinimap(target.position);

        if (!hideInFog || minimap.fog == null)
        {
            return;
        }

        bool visible = minimap.IsVisible(target.position);
        image.enabled = visible;
    }

    public void SetAppearance(Color color, Sprite sprite, Vector2 size)
    {
        if (image == null)
        {
            image = GetComponent<Image>();
        }

        image.color = color;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }

        if (rect == null)
        {
            rect = GetComponent<RectTransform>();
        }
        rect.sizeDelta = size;
    }
}
