using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns a UI icon on each minimap and keeps it aligned to this object's world position.
/// </summary>
[DisallowMultipleComponent]
public class MinimapWorldIcon : MonoBehaviour
{
    [SerializeField] private Image iconPrefab;
    [SerializeField] private Color iconColor = Color.white;
    [SerializeField] private bool overrideIconColor = true;
    [SerializeField] private bool clampToPlayableArea = true;

    private readonly Dictionary<MinimapController, Image> _icons = new Dictionary<MinimapController, Image>();

    private void OnEnable()
    {
        BuildIcons();
        SetIconsActive(true);
    }

    private void OnDisable()
    {
        DestroyIcons();
    }

    private void LateUpdate()
    {
        UpdateIcons();
    }

    protected virtual bool ShouldShowIcon()
    {
        return true;
    }

    protected virtual void BuildIcons()
    {
        if (!iconPrefab)
        {
            Debug.LogWarning("MinimapWorldIcon: Missing icon prefab.", this);
            return;
        }

        DestroyIcons();

        MinimapController[] controllers = FindObjectsByType<MinimapController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            MinimapController controller = controllers[i];
            if (!controller)
            {
                continue;
            }

            RectTransform parent = controller.IconRoot;
            if (!parent)
            {
                continue;
            }

            Image instance = Instantiate(iconPrefab, parent);
            instance.name = $"{iconPrefab.name}_{name}";
            RectTransform rect = instance.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            if (overrideIconColor)
            {
                instance.color = iconColor;
            }

            _icons[controller] = instance;
        }
    }

    protected void SetIconsActive(bool active)
    {
        foreach (var pair in _icons)
        {
            Image icon = pair.Value;
            if (icon)
            {
                icon.gameObject.SetActive(active);
            }
        }
    }

    private void UpdateIcons()
    {
        if (_icons.Count == 0)
        {
            return;
        }

        bool shouldShow = ShouldShowIcon();
        foreach (var pair in _icons)
        {
            MinimapController controller = pair.Key;
            Image icon = pair.Value;
            if (!controller || !icon)
            {
                continue;
            }

            if (!shouldShow)
            {
                if (icon.gameObject.activeSelf)
                {
                    icon.gameObject.SetActive(false);
                }
                continue;
            }

            if (!icon.gameObject.activeSelf)
            {
                icon.gameObject.SetActive(true);
            }

            if (controller.TryGetMinimapPosition(transform.position, out Vector2 anchoredPosition, clampToPlayableArea))
            {
                icon.rectTransform.anchoredPosition = anchoredPosition;
            }
        }
    }

    private void DestroyIcons()
    {
        foreach (var pair in _icons)
        {
            if (pair.Value)
            {
                Destroy(pair.Value.gameObject);
            }
        }

        _icons.Clear();
    }
}
