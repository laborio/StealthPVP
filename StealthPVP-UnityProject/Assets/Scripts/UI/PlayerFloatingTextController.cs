using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Controls floating world-space texts for status and simple score popups.
/// </summary>
[DisallowMultipleComponent]
public class PlayerFloatingTextController : MonoBehaviour
{
    [Serializable]
    public class StatusDefinition
    {
        public string key = "Status";
        public string label = "Status";
        public int priority = 0;
        public bool visibleToAll = true;
    }

    [Header("References")]
    [SerializeField, Tooltip("Root that contains the floating text TMP objects.")] private Transform floatingTextsRoot;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text pointsTypeText;
    [SerializeField] private TMP_Text pointsValueText;

    [Header("Status")]
    [SerializeField] private List<StatusDefinition> statusDefinitions = new List<StatusDefinition>();

    [Header("Points Popup")]
    [SerializeField] private Color positivePointsColor = new Color(1f, 0.9f, 0.2f, 1f);
    [SerializeField] private Color negativePointsColor = new Color(1f, 0.4f, 0.1f, 1f);

    [Header("Popup Animation")]
    [SerializeField, Tooltip("Total duration for the popup animation.")] private float popupDuration = 1.2f;
    [SerializeField, Tooltip("Seconds to fade in.")] private float popupFadeIn = 0.2f;
    [SerializeField, Tooltip("Seconds to fade out.")] private float popupFadeOut = 0.3f;
    [SerializeField, Tooltip("Vertical rise distance over the popup duration.")] private float popupRise = 0.6f;
    [Header("Visibility")]
    [SerializeField, Tooltip("Layer used for floating texts so all cameras can see them.")] private string sharedLayerName = "Default";
    [SerializeField, Tooltip("If true, enforce the shared layer on the floating text root.")] private bool forceSharedLayer = true;

    private readonly Dictionary<string, int> _statusCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, StatusDefinition> _statusLookup = new Dictionary<string, StatusDefinition>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<TMP_Text, Vector3> _basePositions = new Dictionary<TMP_Text, Vector3>();

    private Coroutine _pointsRoutine;
    private Coroutine _layerRoutine;
    private int _sharedLayer = -1;

    private void Awake()
    {
        ResolveReferences();
        BuildStatusLookup();
        CacheBasePositions();
        ApplySharedLayer();
        SetTextActive(statusText, false);
        SetTextActive(pointsTypeText, false);
        SetTextActive(pointsValueText, false);
    }

    private void OnEnable()
    {
        ApplySharedLayer();
        if (_layerRoutine == null)
        {
            _layerRoutine = StartCoroutine(ApplySharedLayerNextFrame());
        }
    }

    private void LateUpdate()
    {
        if (!statusText || !statusText.gameObject.activeSelf)
        {
            return;
        }

        ApplyStatusLayer(GetCurrentStatus());
    }

    public void SetStatusActive(string key, bool active)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!_statusLookup.ContainsKey(key))
        {
            _statusLookup[key] = new StatusDefinition { key = key, label = key, priority = 0 };
        }

        int count = 0;
        _statusCounts.TryGetValue(key, out count);
        count = active ? count + 1 : Mathf.Max(0, count - 1);
        if (count > 0)
        {
            _statusCounts[key] = count;
        }
        else
        {
            _statusCounts.Remove(key);
        }

        UpdateStatusText();
    }

    public void SetStatusLabel(string key, string label, int priority = 0, bool? visibleToAll = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!_statusLookup.TryGetValue(key, out StatusDefinition def))
        {
            def = new StatusDefinition { key = key };
            _statusLookup[key] = def;
        }

        def.label = string.IsNullOrWhiteSpace(label) ? key : label;
        def.priority = priority;
        if (visibleToAll.HasValue)
        {
            def.visibleToAll = visibleToAll.Value;
        }

        UpdateStatusText();
    }

    public void ShowKill(int points)
    {
        ShowPointsPopup("Elimination", points);
    }

    public void ShowHumiliation(int points)
    {
        ShowPointsPopup("Humiliation", points);
    }

    public void ShowWrongTarget(int points = -100)
    {
        ShowPointsPopup("Wrong Target", points);
    }

    private void ResolveReferences()
    {
        if (!floatingTextsRoot)
        {
            Transform direct = transform.Find("WSCanvas/FloatingTexts");
            if (direct)
            {
                floatingTextsRoot = direct;
            }
        }

        if (!floatingTextsRoot)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate && candidate.name == "FloatingTexts")
                {
                    floatingTextsRoot = candidate;
                    break;
                }
            }
        }

        if (floatingTextsRoot)
        {
            if (!statusText)
            {
                statusText = FindTextChild(floatingTextsRoot, "Status");
            }
            if (!pointsTypeText)
            {
                pointsTypeText = FindTextChild(floatingTextsRoot, "PointsType_Txt")
                    ?? FindTextChild(floatingTextsRoot, "PointsType");
            }
            if (!pointsValueText)
            {
                pointsValueText = FindTextChild(floatingTextsRoot, "Points_Txt")
                    ?? FindTextChild(floatingTextsRoot, "Points");
            }
        }
    }

    private void BuildStatusLookup()
    {
        _statusLookup.Clear();
        if (statusDefinitions == null || statusDefinitions.Count == 0)
        {
            statusDefinitions = new List<StatusDefinition>
            {
                new StatusDefinition { key = "Stunned", label = "Stunned", priority = 10, visibleToAll = true },
                new StatusDefinition { key = "Pacified", label = "Pacified", priority = 5, visibleToAll = false }
            };
        }

        for (int i = 0; i < statusDefinitions.Count; i++)
        {
            StatusDefinition def = statusDefinitions[i];
            if (def == null || string.IsNullOrWhiteSpace(def.key))
            {
                continue;
            }

            if (def.key.Equals("Stunned", StringComparison.OrdinalIgnoreCase))
            {
                def.visibleToAll = true;
            }
            else if (def.key.Equals("Pacified", StringComparison.OrdinalIgnoreCase))
            {
                def.visibleToAll = false;
            }

            _statusLookup[def.key] = def;
        }
    }

    private void UpdateStatusText()
    {
        if (!statusText)
        {
            return;
        }

        StatusDefinition best = null;
        foreach (var pair in _statusCounts)
        {
            if (pair.Value <= 0)
            {
                continue;
            }

            if (!_statusLookup.TryGetValue(pair.Key, out StatusDefinition def))
            {
                continue;
            }

            if (best == null || def.priority > best.priority)
            {
                best = def;
            }
        }

        if (best == null)
        {
            SetTextActive(statusText, false);
            return;
        }

        statusText.text = best.label;
        SetTextActive(statusText, true);
        ApplyStatusLayer(best);
    }

    private void CacheBasePositions()
    {
        CacheBasePosition(statusText);
        CacheBasePosition(pointsTypeText);
        CacheBasePosition(pointsValueText);
    }

    private void CacheBasePosition(TMP_Text text)
    {
        if (!text)
        {
            return;
        }

        RectTransform rect = text.rectTransform;
        if (rect)
        {
            _basePositions[text] = rect.localPosition;
        }
    }

    private void ShowPointsPopup(string label, int points)
    {
        if (!pointsTypeText)
        {
            return;
        }

        pointsTypeText.text = label;

        if (pointsValueText)
        {
            pointsValueText.text = points > 0 ? $"+{points}" : points.ToString();
            pointsValueText.color = points >= 0 ? positivePointsColor : negativePointsColor;
        }

        PlayPopup(pointsTypeText, ref _pointsRoutine, pointsValueText);
    }

    private void PlayPopup(TMP_Text text, ref Coroutine routine, params TMP_Text[] extraTexts)
    {
        if (!text)
        {
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(PopupRoutine(text, extraTexts));
    }

    private System.Collections.IEnumerator PopupRoutine(TMP_Text text, TMP_Text[] extraTexts)
    {
        SetTextActive(text, true);
        List<TMP_Text> fadeTexts = new List<TMP_Text> { text };
        if (extraTexts != null)
        {
            for (int i = 0; i < extraTexts.Length; i++)
            {
                TMP_Text extra = extraTexts[i];
                if (!extra || fadeTexts.Contains(extra))
                {
                    continue;
                }

                fadeTexts.Add(extra);
                SetTextActive(extra, true);
            }
        }

        RectTransform rect = text.rectTransform;
        if (!_basePositions.TryGetValue(text, out Vector3 basePos))
        {
            basePos = rect ? rect.localPosition : Vector3.zero;
            _basePositions[text] = basePos;
        }

        float duration = Mathf.Max(0.05f, popupDuration);
        float fadeIn = Mathf.Clamp(popupFadeIn, 0f, duration);
        float fadeOut = Mathf.Clamp(popupFadeOut, 0f, duration);
        float fadeOutStart = Mathf.Max(0f, duration - fadeOut);
        Color[] baseColors = new Color[fadeTexts.Count];
        for (int i = 0; i < fadeTexts.Count; i++)
        {
            baseColors[i] = fadeTexts[i].color;
        }
        float time = 0f;

        Transform parent = rect ? rect.parent : null;
        while (time < duration)
        {
            float alpha = 1f;
            if (time < fadeIn && fadeIn > 0f)
            {
                alpha = Mathf.Clamp01(time / fadeIn);
            }
            else if (time >= fadeOutStart && fadeOut > 0f)
            {
                alpha = Mathf.Clamp01(1f - ((time - fadeOutStart) / fadeOut));
            }

            if (rect)
            {
                float rise = popupRise * (time / duration);
                Vector3 baseWorld = parent ? parent.TransformPoint(basePos) : basePos;
                rect.position = baseWorld + new Vector3(0f, 0f, rise);
            }

            for (int i = 0; i < fadeTexts.Count; i++)
            {
                TMP_Text fadeText = fadeTexts[i];
                if (!fadeText)
                {
                    continue;
                }

                Color c = baseColors[i];
                c.a = alpha;
                fadeText.color = c;
            }

            time += Time.deltaTime;
            yield return null;
        }

        if (rect)
        {
            rect.localPosition = basePos;
        }

        for (int i = 0; i < fadeTexts.Count; i++)
        {
            TMP_Text fadeText = fadeTexts[i];
            if (!fadeText)
            {
                continue;
            }

            Color c = baseColors[i];
            c.a = 1f;
            fadeText.color = c;
            if (fadeText != text)
            {
                SetTextActive(fadeText, false);
            }
        }

        SetTextActive(text, false);
    }

    private static void SetTextActive(TMP_Text text, bool active)
    {
        if (text && text.gameObject.activeSelf != active)
        {
            text.gameObject.SetActive(active);
        }
    }

    private static TMP_Text FindTextChild(Transform root, string childName)
    {
        if (!root || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        Transform direct = root.Find(childName);
        if (direct)
        {
            return direct.GetComponent<TMP_Text>();
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text && text.name == childName)
            {
                return text;
            }
        }

        return null;
    }

    private void ApplySharedLayer()
    {
        if (!forceSharedLayer || !floatingTextsRoot || string.IsNullOrWhiteSpace(sharedLayerName))
        {
            return;
        }

        int layer = LayerMask.NameToLayer(sharedLayerName);
        if (layer < 0)
        {
            return;
        }

        _sharedLayer = layer;

        Canvas canvas = floatingTextsRoot.GetComponent<Canvas>();
        if (!canvas)
        {
            canvas = floatingTextsRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
        }

        Canvas parentCanvas = floatingTextsRoot.GetComponentInParent<Canvas>();
        if (parentCanvas && canvas != parentCanvas)
        {
            canvas.worldCamera = parentCanvas.worldCamera;
            canvas.sortingLayerID = parentCanvas.sortingLayerID;
            canvas.sortingOrder = parentCanvas.sortingOrder;
        }

        ApplyStatusLayer(GetCurrentStatus());
    }

    private System.Collections.IEnumerator ApplySharedLayerNextFrame()
    {
        yield return null;
        ApplySharedLayer();
        _layerRoutine = null;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (!root)
        {
            return;
        }

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private StatusDefinition GetCurrentStatus()
    {
        StatusDefinition best = null;
        foreach (var pair in _statusCounts)
        {
            if (pair.Value <= 0)
            {
                continue;
            }

            if (!_statusLookup.TryGetValue(pair.Key, out StatusDefinition def))
            {
                continue;
            }

            if (best == null || def.priority > best.priority)
            {
                best = def;
            }
        }

        return best;
    }

    private void ApplyStatusLayer(StatusDefinition status)
    {
        if (!statusText || status == null)
        {
            return;
        }

        int layer = status.visibleToAll ? ResolveSharedLayer() : ResolveOwnerLayer();
        if (layer < 0)
        {
            return;
        }

        SetLayerRecursively(statusText.transform, layer);
    }

    private int ResolveSharedLayer()
    {
        if (_sharedLayer >= 0)
        {
            return _sharedLayer;
        }

        if (!string.IsNullOrWhiteSpace(sharedLayerName))
        {
            _sharedLayer = LayerMask.NameToLayer(sharedLayerName);
        }

        return _sharedLayer;
    }

    private int ResolveOwnerLayer()
    {
        if (floatingTextsRoot && floatingTextsRoot.parent)
        {
            return floatingTextsRoot.parent.gameObject.layer;
        }

        return gameObject.layer;
    }
}
