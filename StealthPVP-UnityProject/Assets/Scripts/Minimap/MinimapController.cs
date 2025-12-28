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

    [Header("Colors")]
    [SerializeField] private Color defaultSectionColor = new Color32(91, 91, 91, 255); // #5B5B5B
    [SerializeField] private Color highlightSectionColor = Color.yellow;

    private readonly Dictionary<string, Image> _sections = new Dictionary<string, Image>();
    private readonly HashSet<string> _highlightedSections = new HashSet<string>();
    private readonly Dictionary<string, int> _sectionPresenceCounts = new Dictionary<string, int>();

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
        RefreshTargetState();
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
        RefreshTargetState();
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
        if (!area || identity != targetIdentity)
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

        UpdateSectionPresence(sectionId, entered);
    }

    private void RefreshTargetState()
    {
        if (!targetIdentity)
        {
            return;
        }

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

            if (area.IsInside(targetIdentity))
            {
                UpdateSectionPresence(area.SectionId, true);
            }
        }
    }

    private void UpdateSectionPresence(string sectionId, bool entered)
    {
        if (string.IsNullOrEmpty(sectionId))
        {
            return;
        }

        if (!_sections.TryGetValue(sectionId, out Image image) || !image)
        {
            return;
        }

        int count = 0;
        _sectionPresenceCounts.TryGetValue(sectionId, out count);
        count = entered ? count + 1 : Mathf.Max(0, count - 1);

        if (count > 0)
        {
            _sectionPresenceCounts[sectionId] = count;
            image.color = highlightSectionColor;
            _highlightedSections.Add(sectionId);
        }
        else
        {
            _sectionPresenceCounts.Remove(sectionId);
            image.color = defaultSectionColor;
            _highlightedSections.Remove(sectionId);
        }
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
        _highlightedSections.Clear();
        _sectionPresenceCounts.Clear();
    }
}
