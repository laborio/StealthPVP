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
    [SerializeField, Tooltip("Optional root for minimap icons. Falls back to Sections/root.")] private RectTransform iconsRoot;
    [SerializeField, Tooltip("UI frame that represents the playable area.")] private RectTransform playableAreaFrame;
    [SerializeField, Tooltip("Optional collider that defines the playable area bounds for minimap layout.")] private Collider playableAreaCollider;
    [SerializeField, Tooltip("If true, look for a playable area collider at runtime by name.")] private bool autoFindPlayableArea = true;
    [SerializeField, Tooltip("Name of the GameObject that owns the playable area collider.")] private string playableAreaObjectName = "PlayableArea";
    [SerializeField, Tooltip("If true, sizes/positions section images based on their world colliders.")] private bool autoSyncSectionLayout = true;
    [SerializeField, Tooltip("Frames to retry layout sync after enable/resize.")] private int layoutRetryFrames = 5;
    [SerializeField, Tooltip("Log layout sync failures for debugging.")] private bool debugLayoutSync = false;
    [SerializeField] private NpcIdentity targetIdentity;
    [SerializeField] private NpcIdentity ownerIdentity;

    [Header("Colors")]
    [SerializeField] private Color defaultSectionColor = new Color32(91, 91, 91, 255); // #5B5B5B
    [SerializeField] private Color highlightSectionColor = Color.yellow;
    [SerializeField] private Color ownerSectionColor = Color.white;

    private readonly Dictionary<string, Image> _sections = new Dictionary<string, Image>();
    private readonly Dictionary<string, int> _targetPresenceCounts = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _ownerPresenceCounts = new Dictionary<string, int>();
    private Coroutine _layoutSyncRoutine;
    private bool _layoutDirty;
    private int _debugFrameCounter;
    private Vector2 _lastFrameRectSize;
    private Vector2 _lastLayoutRectSize;
    private int _lastAreaCount = -1;

    private void Awake()
    {
        CacheSections();
        StartLayoutSync();
    }

    private void OnEnable()
    {
        MinimapSectionArea.TargetPresenceChanged += HandleAreaPresenceChanged;
        if (_sections.Count == 0)
        {
            CacheSections();
        }
        StartLayoutSync();
        RefreshIdentityStates();
    }

    private void OnDisable()
    {
        MinimapSectionArea.TargetPresenceChanged -= HandleAreaPresenceChanged;
        if (_layoutSyncRoutine != null)
        {
            StopCoroutine(_layoutSyncRoutine);
            _layoutSyncRoutine = null;
        }
    }

    private void LateUpdate()
    {
        if (!autoSyncSectionLayout)
        {
            return;
        }

        TrackLayoutChanges();
        if (!_layoutDirty)
        {
            return;
        }

        if (TrySyncSectionLayout())
        {
            _layoutDirty = false;
        }
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

    public RectTransform IconRoot => iconsRoot ? iconsRoot : ResolveLayoutRoot();

    public bool TryGetMinimapPosition(Vector3 worldPosition, out Vector2 anchoredPosition, bool clampToPlayableArea = true)
    {
        anchoredPosition = default;
        RectTransform layoutRoot = ResolveLayoutRoot();
        RectTransform iconRoot = IconRoot;
        RectTransform frame = ResolvePlayableFrame(layoutRoot);
        if (!layoutRoot || !iconRoot || !frame)
        {
            return false;
        }

        if (!TryResolvePlayableBounds(out Bounds playableBounds))
        {
            return false;
        }

        Vector3 playableSize = playableBounds.size;
        if (playableSize.x <= 0f || playableSize.z <= 0f)
        {
            return false;
        }

        float xNormalized = (worldPosition.x - playableBounds.min.x) / playableSize.x;
        float yNormalized = (worldPosition.z - playableBounds.min.z) / playableSize.z;
        if (clampToPlayableArea)
        {
            xNormalized = Mathf.Clamp01(xNormalized);
            yNormalized = Mathf.Clamp01(yNormalized);
        }

        Vector2 frameLocal = new Vector2(
            Mathf.Lerp(frame.rect.xMin, frame.rect.xMax, xNormalized),
            Mathf.Lerp(frame.rect.yMin, frame.rect.yMax, yNormalized));

        if (iconRoot == frame)
        {
            anchoredPosition = frameLocal;
            return true;
        }

        Vector3 worldPoint = frame.TransformPoint(frameLocal);
        Vector3 localPoint = iconRoot.InverseTransformPoint(worldPoint);
        anchoredPosition = new Vector2(localPoint.x, localPoint.y);
        return true;
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

    public void SyncSectionLayout()
    {
        TrySyncSectionLayout();
    }

    private bool TrySyncSectionLayout()
    {
        if (_sections.Count == 0)
        {
            CacheSections();
        }

        RectTransform layoutRoot = ResolveLayoutRoot();
        RectTransform frame = ResolvePlayableFrame(layoutRoot);
        if (!layoutRoot || !frame)
        {
            LogLayoutFailure("Missing layout root/frame", layoutRoot, frame);
            return false;
        }

        Rect frameRect = frame.rect;
        if (frameRect.width <= 0.01f || frameRect.height <= 0.01f)
        {
            LogLayoutFailure("Frame rect size is zero", layoutRoot, frame);
            return false;
        }

        Rect layoutRect = layoutRoot.rect;
        if (layoutRect.width <= 0.01f || layoutRect.height <= 0.01f)
        {
            LogLayoutFailure("Layout rect size is zero", layoutRoot, frame);
            return false;
        }

        if (!TryResolvePlayableBounds(out Bounds playableBounds))
        {
            LogLayoutFailure("Missing playable bounds", layoutRoot, frame);
            return false;
        }

        Vector3 playableSize = playableBounds.size;
        if (playableSize.x <= 0f || playableSize.z <= 0f)
        {
            LogLayoutFailure("Playable bounds size invalid", layoutRoot, frame);
            return false;
        }

        IReadOnlyList<MinimapSectionArea> areas = MinimapSectionArea.Instances;
        if (areas == null)
        {
            LogLayoutFailure("No section areas registered", layoutRoot, frame);
            return false;
        }

        Vector2 frameSize = frameRect.size;
        for (int i = 0; i < areas.Count; i++)
        {
            MinimapSectionArea area = areas[i];
            if (!area)
            {
                continue;
            }

            string sectionId = area.SectionId;
            if (string.IsNullOrEmpty(sectionId))
            {
                continue;
            }

            if (!_sections.TryGetValue(sectionId, out Image image) || !image)
            {
                continue;
            }

            if (!area.TryGetWorldBounds(out Bounds areaBounds))
            {
                continue;
            }

            float xNormalized = (areaBounds.center.x - playableBounds.min.x) / playableSize.x;
            float yNormalized = (areaBounds.center.z - playableBounds.min.z) / playableSize.z;
            xNormalized = Mathf.Clamp01(xNormalized);
            yNormalized = Mathf.Clamp01(yNormalized);

            float widthNormalized = areaBounds.size.x / playableSize.x;
            float heightNormalized = areaBounds.size.z / playableSize.z;

            Vector2 localPos = new Vector2(
                Mathf.Lerp(frame.rect.xMin, frame.rect.xMax, xNormalized),
                Mathf.Lerp(frame.rect.yMin, frame.rect.yMax, yNormalized));
            Vector2 size = new Vector2(frameSize.x * widthNormalized, frameSize.y * heightNormalized);

            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            ApplyLayoutToRect(rect, layoutRoot, frame, localPos, size);
        }

        return true;
    }

    private void ApplyLayoutToRect(RectTransform rect, RectTransform layoutRoot, RectTransform frame, Vector2 frameLocalPos, Vector2 frameSize)
    {
        Vector2 finalPos = frameLocalPos;
        Vector2 finalSize = frameSize;

        if (layoutRoot != frame)
        {
            Vector3 worldPos = frame.TransformPoint(frameLocalPos);
            Vector3 layoutLocal = layoutRoot.InverseTransformPoint(worldPos);
            finalPos = new Vector2(layoutLocal.x, layoutLocal.y);

            Vector3 frameScale = frame.lossyScale;
            Vector3 layoutScale = layoutRoot.lossyScale;
            float xScale = Mathf.Abs(layoutScale.x) > 0.0001f ? frameScale.x / layoutScale.x : 1f;
            float yScale = Mathf.Abs(layoutScale.y) > 0.0001f ? frameScale.y / layoutScale.y : 1f;
            finalSize = new Vector2(frameSize.x * xScale, frameSize.y * yScale);
        }

        rect.anchoredPosition = finalPos;
        rect.sizeDelta = finalSize;
    }

    private bool TryResolvePlayableBounds(out Bounds bounds)
    {
        if (!playableAreaCollider && autoFindPlayableArea)
        {
            playableAreaCollider = FindPlayableAreaCollider();
        }

        if (playableAreaCollider)
        {
            bounds = playableAreaCollider.bounds;
            return true;
        }

        bounds = default;
        bool hasBounds = false;
        IReadOnlyList<MinimapSectionArea> areas = MinimapSectionArea.Instances;
        if (areas == null)
        {
            return false;
        }

        for (int i = 0; i < areas.Count; i++)
        {
            MinimapSectionArea area = areas[i];
            if (!area)
            {
                continue;
            }

            if (!area.TryGetWorldBounds(out Bounds areaBounds))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = areaBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(areaBounds);
            }
        }

        return hasBounds;
    }

    private Collider FindPlayableAreaCollider()
    {
        if (string.IsNullOrWhiteSpace(playableAreaObjectName))
        {
            return null;
        }

        GameObject found = GameObject.Find(playableAreaObjectName);
        if (!found)
        {
            return null;
        }

        return found.GetComponent<Collider>() ?? found.GetComponentInChildren<Collider>(true);
    }

    private RectTransform ResolveLayoutRoot()
    {
        if (sectionsRoot)
        {
            RectTransform rect = sectionsRoot as RectTransform;
            return rect ? rect : sectionsRoot.GetComponent<RectTransform>();
        }

        return transform as RectTransform;
    }

    private RectTransform ResolvePlayableFrame(RectTransform layoutRoot)
    {
        if (playableAreaFrame)
        {
            return playableAreaFrame;
        }

        if (layoutRoot)
        {
            Image layoutImage = layoutRoot.GetComponent<Image>();
            if (layoutImage)
            {
                return layoutRoot;
            }

            if (layoutRoot.parent)
            {
                RectTransform parentRect = layoutRoot.parent as RectTransform;
                if (parentRect && parentRect.GetComponent<Image>())
                {
                    return parentRect;
                }
            }
        }

        return layoutRoot;
    }

    private void OnRectTransformDimensionsChange()
    {
        StartLayoutSync();
    }

    private void StartLayoutSync()
    {
        if (!autoSyncSectionLayout)
        {
            return;
        }

        _layoutDirty = true;
        if (_layoutSyncRoutine != null)
        {
            StopCoroutine(_layoutSyncRoutine);
        }

        _layoutSyncRoutine = StartCoroutine(LayoutSyncRoutine());
    }

    private void TrackLayoutChanges()
    {
        RectTransform layoutRoot = ResolveLayoutRoot();
        RectTransform frame = ResolvePlayableFrame(layoutRoot);
        if (layoutRoot && frame)
        {
            Vector2 frameSize = frame.rect.size;
            Vector2 layoutSize = layoutRoot.rect.size;
            if (frameSize != _lastFrameRectSize || layoutSize != _lastLayoutRectSize)
            {
                _layoutDirty = true;
                _lastFrameRectSize = frameSize;
                _lastLayoutRectSize = layoutSize;
            }
        }

        IReadOnlyList<MinimapSectionArea> areas = MinimapSectionArea.Instances;
        int areaCount = areas != null ? areas.Count : 0;
        if (areaCount != _lastAreaCount)
        {
            _lastAreaCount = areaCount;
            _layoutDirty = true;
        }
    }

    private System.Collections.IEnumerator LayoutSyncRoutine()
    {
        int retries = Mathf.Max(1, layoutRetryFrames);
        for (int i = 0; i < retries; i++)
        {
            _debugFrameCounter++;
            Canvas.ForceUpdateCanvases();
            if (TrySyncSectionLayout())
            {
                _layoutDirty = false;
                break;
            }
            yield return null;
        }

        _layoutSyncRoutine = null;
    }

    private void LogLayoutFailure(string reason, RectTransform layoutRoot, RectTransform frame)
    {
        if (!debugLayoutSync)
        {
            return;
        }

        string layoutName = layoutRoot ? layoutRoot.name : "null";
        string frameName = frame ? frame.name : "null";
        Rect layoutRect = layoutRoot ? layoutRoot.rect : default;
        Rect frameRect = frame ? frame.rect : default;
        Vector3 layoutScale = layoutRoot ? layoutRoot.lossyScale : Vector3.one;
        Vector3 frameScale = frame ? frame.lossyScale : Vector3.one;

        Debug.Log(
            $"[MinimapController] Layout sync failed: {reason}. " +
            $"layout={layoutName} rect={layoutRect.size} scale={layoutScale} " +
            $"frame={frameName} rect={frameRect.size} scale={frameScale} " +
            $"retryFrame={_debugFrameCounter} object={name}", this);
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
