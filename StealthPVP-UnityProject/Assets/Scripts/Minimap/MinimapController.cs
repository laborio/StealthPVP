using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Highlights minimap section images based on the tracked target entering world section areas.
/// </summary>
[DisallowMultipleComponent]
public class MinimapController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform sectionsRoot;
    [SerializeField] private NpcIdentity targetIdentity;
    [SerializeField] private NpcIdentity ownerIdentity;

    [Header("Colors")]
    [SerializeField] private Color defaultSectionColor = new Color32(91, 91, 91, 255); // #5B5B5B
    [SerializeField] private Color highlightSectionColor = Color.yellow;
    [SerializeField] private Color ownerSectionColor = Color.white;

    private readonly Dictionary<string, Image> _sections = new Dictionary<string, Image>();
    private readonly Dictionary<string, int> _targetPresenceCounts = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _ownerPresenceCounts = new Dictionary<string, int>();

    private void Awake()
    {
        CacheSections();
    }

    private void OnEnable()
    {
        MinimapSectionArea.TargetPresenceChanged += HandleAreaPresenceChanged;
        if (_sections.Count == 0)
        {
            CacheSections();
        }
        RefreshIdentityStates();
    }

    private void OnDisable()
    {
        MinimapSectionArea.TargetPresenceChanged -= HandleAreaPresenceChanged;
    }

    public void SetTarget(NpcIdentity identity)
    {
        if (targetIdentity == identity)
        {
            return;
        }

        targetIdentity = identity;
        ClearHighlights();
        RefreshIdentityStates();
    }

    public void SetOwner(NpcIdentity identity)
    {
        if (ownerIdentity == identity)
        {
            return;
        }

        ownerIdentity = identity;
        ClearHighlights();
        RefreshIdentityStates();
    }

    private void CacheSections()
    {
        _sections.Clear();

        if (!sectionsRoot)
        {
            Transform found = transform.Find("Sections");
            sectionsRoot = found ? found : transform;
        }

        if (!sectionsRoot)
        {
            return;
        }

        Image[] images = sectionsRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (!image)
            {
                continue;
            }

            string key = image.gameObject.name;
            if (string.IsNullOrEmpty(key) || _sections.ContainsKey(key))
            {
                continue;
            }

            _sections.Add(key, image);
            image.color = defaultSectionColor;
        }
    }

    private void HandleAreaPresenceChanged(MinimapSectionArea area, NpcIdentity identity, bool entered)
    {
        if (!area || !identity)
        {
            return;
        }

        string sectionId = area.SectionId;
        if (string.IsNullOrEmpty(sectionId))
        {
            return;
        }

        if (!_sections.TryGetValue(sectionId, out Image image) || !image)
        {
            return;
        }

        if (identity == targetIdentity)
        {
            UpdateSectionPresence(sectionId, entered, isTarget: true);
        }

        if (identity == ownerIdentity)
        {
            UpdateSectionPresence(sectionId, entered, isTarget: false);
        }
    }

    private void RefreshIdentityStates()
    {
        IReadOnlyList<MinimapSectionArea> areas = MinimapSectionArea.Instances;
        if (areas == null)
        {
            return;
        }

        for (int i = 0; i < areas.Count; i++)
        {
            MinimapSectionArea area = areas[i];
            if (!area)
            {
                continue;
            }

            if (targetIdentity && area.IsInside(targetIdentity))
            {
                UpdateSectionPresence(area.SectionId, true, isTarget: true);
            }

            if (ownerIdentity && area.IsInside(ownerIdentity))
            {
                UpdateSectionPresence(area.SectionId, true, isTarget: false);
            }
        }
    }

    private void UpdateSectionPresence(string sectionId, bool entered, bool isTarget)
    {
        if (string.IsNullOrEmpty(sectionId))
        {
            return;
        }

        if (!_sections.TryGetValue(sectionId, out Image image) || !image)
        {
            return;
        }

        Dictionary<string, int> counts = isTarget ? _targetPresenceCounts : _ownerPresenceCounts;
        int count = 0;
        counts.TryGetValue(sectionId, out count);
        count = entered ? count + 1 : Mathf.Max(0, count - 1);

        if (count > 0)
        {
            counts[sectionId] = count;
        }
        else
        {
            counts.Remove(sectionId);
        }

        UpdateSectionColor(sectionId, image);
    }

    private void UpdateSectionColor(string sectionId, Image image)
    {
        if (!image)
        {
            return;
        }

        if (_targetPresenceCounts.ContainsKey(sectionId))
        {
            image.color = highlightSectionColor;
            return;
        }

        if (_ownerPresenceCounts.ContainsKey(sectionId))
        {
            image.color = ownerSectionColor;
            return;
        }

        image.color = defaultSectionColor;
    }

    private void ClearHighlights()
    {
        foreach (var pair in _sections)
        {
            if (pair.Value)
            {
                pair.Value.color = defaultSectionColor;
            }
        }
        _targetPresenceCounts.Clear();
        _ownerPresenceCounts.Clear();
    }
}
