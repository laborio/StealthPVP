using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Distance-based animator culling for NPCs.
/// </summary>
[DisallowMultipleComponent]
public class NpcAnimatorCulling : MonoBehaviour
{
    [Header("Animators")]
    [SerializeField, Tooltip("Animators to cull. If empty, auto-collect from children.")] private List<Animator> animators = new List<Animator>();
    [SerializeField, Tooltip("Include inactive child animators when auto-collecting.")] private bool includeInactiveAnimators = true;
    [SerializeField, Tooltip("Renderers to cull. If empty, auto-collect from children.")] private List<Renderer> renderers = new List<Renderer>();
    [SerializeField, Tooltip("Include inactive child renderers when auto-collecting.")] private bool includeInactiveRenderers = true;

    [Header("Cameras")]
    [SerializeField, Tooltip("Cameras used to compute distance. If empty, uses MainCamera when enabled.")] private Camera[] cameras;
    [SerializeField, Tooltip("Fallback to Camera.main when no cameras are provided.")] private bool useMainCameraFallback = true;
    [SerializeField, Tooltip("Auto-refresh camera list from active cameras.")] private bool autoFindCameras = false;
    [SerializeField, Tooltip("Seconds between camera refreshes when auto-find is enabled.")] private float cameraRefreshInterval = 1f;

    [Header("View Culling")]
    [SerializeField, Tooltip("If true, cull based on camera view instead of distance.")] private bool useViewBasedCulling = false;
    [SerializeField, Tooltip("Expand the camera view by this world-space offset when testing visibility.")]
    private float viewCullingOffset = 1f;

    [Header("Distances")]
    [SerializeField, Tooltip("Within this distance, animate always.")] private float alwaysAnimateDistance = 18f;
    [SerializeField, Tooltip("Beyond this distance, cull completely. Between distances uses CullUpdateTransforms.")]
    private float cullCompletelyDistance = 45f;
    [SerializeField, Tooltip("Disable Animator component entirely when beyond cull distance (true = real distance cull).")]
    private bool disableAnimatorWhenCulled = true;
    [SerializeField, Tooltip("Disable renderers when beyond cull distance.")] private bool disableRenderersWhenCulled = true;

    [Header("Update")]
    [SerializeField, Tooltip("Seconds between culling checks.")] private float updateInterval = 0.25f;
    [SerializeField, Tooltip("Use unscaled time for updates (ignores Time.timeScale).")] private bool ignoreTimeScale = false;

    private float _nextUpdateTime;
    private float _nextCameraRefreshTime;
    private AnimatorCullingMode _lastMode = AnimatorCullingMode.AlwaysAnimate;
    private bool _lastEnabled = true;
    private bool _lastRenderersEnabled = true;

    private void Awake()
    {
        CollectAnimatorsIfNeeded();
        CollectRenderersIfNeeded();
        RefreshCamerasIfNeeded(force: true);
    }

    private void OnEnable()
    {
        _nextUpdateTime = 0f;
        _nextCameraRefreshTime = 0f;
    }

    private void Update()
    {
        float now = ignoreTimeScale ? Time.unscaledTime : Time.time;
        if (now < _nextUpdateTime)
        {
            return;
        }

        _nextUpdateTime = now + Mathf.Max(0.02f, updateInterval);

        RefreshCamerasIfNeeded(force: false);
        UpdateCulling();
    }

    private void CollectAnimatorsIfNeeded()
    {
        if (animators.Count > 0)
        {
            return;
        }

        Animator[] found = GetComponentsInChildren<Animator>(includeInactiveAnimators);
        if (found != null && found.Length > 0)
        {
            animators.AddRange(found);
        }
    }

    private void CollectRenderersIfNeeded()
    {
        if (renderers.Count > 0)
        {
            return;
        }

        Renderer[] found = GetComponentsInChildren<Renderer>(includeInactiveRenderers);
        if (found != null && found.Length > 0)
        {
            renderers.AddRange(found);
        }
    }

    private void RefreshCamerasIfNeeded(bool force)
    {
        if (!autoFindCameras && !force)
        {
            return;
        }

        float now = ignoreTimeScale ? Time.unscaledTime : Time.time;
        if (!force && now < _nextCameraRefreshTime)
        {
            return;
        }

        _nextCameraRefreshTime = now + Mathf.Max(0.1f, cameraRefreshInterval);

        if (autoFindCameras)
        {
            cameras = Camera.allCameras;
        }
    }

    private void UpdateCulling()
    {
        if (animators.Count == 0 && renderers.Count == 0)
        {
            return;
        }

        float minSqrDistance = float.MaxValue;
        bool hasCamera = false;
        bool isInView = false;

        if (cameras != null && cameras.Length > 0)
        {
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cam = cameras[i];
                if (!cam || !cam.enabled)
                {
                    continue;
                }

                hasCamera = true;
                if (useViewBasedCulling && IsInCameraView(cam, viewCullingOffset))
                {
                    isInView = true;
                }

                if (!useViewBasedCulling)
                {
                    Vector3 delta = cam.transform.position - transform.position;
                    float sqr = delta.sqrMagnitude;
                    if (sqr < minSqrDistance)
                    {
                        minSqrDistance = sqr;
                    }
                }
            }
        }

        if (!hasCamera && useMainCameraFallback)
        {
            Camera cam = Camera.main;
            if (cam && cam.enabled)
            {
                hasCamera = true;
                if (useViewBasedCulling)
                {
                    isInView = IsInCameraView(cam, viewCullingOffset);
                }
                else
                {
                    Vector3 delta = cam.transform.position - transform.position;
                    minSqrDistance = delta.sqrMagnitude;
                }
            }
        }

        if (!hasCamera)
        {
            return;
        }

        if (useViewBasedCulling)
        {
            AnimatorCullingMode viewMode = isInView ? AnimatorCullingMode.AlwaysAnimate : AnimatorCullingMode.CullCompletely;
            bool viewEnabled = isInView || !disableAnimatorWhenCulled;
            bool viewRenderersEnabled = isInView || !disableRenderersWhenCulled;

            if (viewMode != _lastMode || viewEnabled != _lastEnabled || viewRenderersEnabled != _lastRenderersEnabled)
            {
                _lastMode = viewMode;
                _lastEnabled = viewEnabled;
                _lastRenderersEnabled = viewRenderersEnabled;

                for (int i = animators.Count - 1; i >= 0; i--)
                {
                    Animator anim = animators[i];
                    if (!anim)
                    {
                        animators.RemoveAt(i);
                        continue;
                    }

                    anim.cullingMode = viewMode;
                    anim.enabled = viewEnabled;
                }

                for (int i = renderers.Count - 1; i >= 0; i--)
                {
                    Renderer rend = renderers[i];
                    if (!rend)
                    {
                        renderers.RemoveAt(i);
                        continue;
                    }

                    rend.enabled = viewRenderersEnabled;
                }
            }

            return;
        }

        float alwaysSqr = alwaysAnimateDistance * alwaysAnimateDistance;
        float cullSqr = cullCompletelyDistance * cullCompletelyDistance;
        AnimatorCullingMode targetMode = AnimatorCullingMode.CullUpdateTransforms;
        bool targetEnabled = true;
        bool targetRenderersEnabled = true;

        if (minSqrDistance <= alwaysSqr)
        {
            targetMode = AnimatorCullingMode.AlwaysAnimate;
        }
        else if (minSqrDistance >= cullSqr)
        {
            targetMode = AnimatorCullingMode.CullCompletely;
            if (disableAnimatorWhenCulled)
            {
                targetEnabled = false;
            }
            if (disableRenderersWhenCulled)
            {
                targetRenderersEnabled = false;
            }
        }

        if (targetMode == _lastMode && targetEnabled == _lastEnabled && targetRenderersEnabled == _lastRenderersEnabled)
        {
            return;
        }

        _lastMode = targetMode;
        _lastEnabled = targetEnabled;
        _lastRenderersEnabled = targetRenderersEnabled;
        for (int i = animators.Count - 1; i >= 0; i--)
        {
            Animator anim = animators[i];
            if (!anim)
            {
                animators.RemoveAt(i);
                continue;
            }

            anim.cullingMode = targetMode;
            anim.enabled = targetEnabled;
        }

        for (int i = renderers.Count - 1; i >= 0; i--)
        {
            Renderer rend = renderers[i];
            if (!rend)
            {
                renderers.RemoveAt(i);
                continue;
            }

            rend.enabled = targetRenderersEnabled;
        }
    }

    private bool IsInCameraView(Camera cam, float offset)
    {
        if (!TryGetCombinedBounds(out Bounds bounds))
        {
            return false;
        }

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
        if (offset != 0f)
        {
            for (int i = 0; i < planes.Length; i++)
            {
                Plane plane = planes[i];
                plane.distance += offset;
                planes[i] = plane;
            }
        }

        return GeometryUtility.TestPlanesAABB(planes, bounds);
    }

    private bool TryGetCombinedBounds(out Bounds bounds)
    {
        bounds = new Bounds(transform.position, Vector3.one * 0.25f);

        bool hasRenderer = false;
        for (int i = renderers.Count - 1; i >= 0; i--)
        {
            Renderer rend = renderers[i];
            if (!rend)
            {
                renderers.RemoveAt(i);
                continue;
            }

            if (!hasRenderer)
            {
                bounds = rend.bounds;
                hasRenderer = true;
            }
            else
            {
                bounds.Encapsulate(rend.bounds);
            }
        }

        return hasRenderer || renderers.Count == 0;
    }

    private void OnValidate()
    {
        alwaysAnimateDistance = Mathf.Max(0f, alwaysAnimateDistance);
        cullCompletelyDistance = Mathf.Max(alwaysAnimateDistance + 0.1f, cullCompletelyDistance);
        updateInterval = Mathf.Max(0.02f, updateInterval);
        cameraRefreshInterval = Mathf.Max(0.1f, cameraRefreshInterval);
    }
}
