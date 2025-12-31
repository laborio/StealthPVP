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
    [SerializeField] private TMP_Text killText;
    [SerializeField] private TMP_Text humiliationText;

    [Header("Status")]
    [SerializeField] private List<StatusDefinition> statusDefinitions = new List<StatusDefinition>();

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

    private Coroutine _killRoutine;
    private Coroutine _humiliationRoutine;
    private Coroutine _layerRoutine;
    private int _sharedLayer = -1;

    private void Awake()
    {
        ResolveReferences();
        BuildStatusLookup();
        CacheBasePositions();
        ApplySharedLayer();
        SetTextActive(statusText, false);
        SetTextActive(killText, false);
        SetTextActive(humiliationText, false);
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
        if (!killText)
        {
            return;
        }

        string message = $"Elimination\n+{points}";
        PlayPopup(killText, message, ref _killRoutine);
    }

    public void ShowHumiliation(int points)
    {
        if (!humiliationText)
        {
            return;
        }

        string message = $"Humiliation\n+{points}";
        PlayPopup(humiliationText, message, ref _humiliationRoutine);
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
            if (!killText)
            {
                killText = FindTextChild(floatingTextsRoot, "Kills");
            }
            if (!humiliationText)
            {
                humiliationText = FindTextChild(floatingTextsRoot, "Humiliation");
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
        CacheBasePosition(killText);
        CacheBasePosition(humiliationText);
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

    private void PlayPopup(TMP_Text text, string message, ref Coroutine routine)
    {
        if (!text)
        {
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        text.text = message;
        routine = StartCoroutine(PopupRoutine(text));
    }

    private System.Collections.IEnumerator PopupRoutine(TMP_Text text)
    {
        SetTextActive(text, true);
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
        Color baseColor = text.color;
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

            Color c = baseColor;
            c.a = alpha;
            text.color = c;

            time += Time.deltaTime;
            yield return null;
        }

        if (rect)
        {
            rect.localPosition = basePos;
        }

        baseColor.a = 1f;
        text.color = baseColor;
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
