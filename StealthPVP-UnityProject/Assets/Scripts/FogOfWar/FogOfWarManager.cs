using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central fog controller that keeps a binary visibility grid and uploads it as a global texture.
/// </summary>
public class FogOfWarManager : MonoBehaviour
{
    [Header("Grid")]
    [Tooltip("Horizontal resolution of the fog grid (higher = sharper edges, more cost).")]
    public int gridSizeX = 1024;

    [Tooltip("Vertical resolution of the fog grid (higher = sharper edges, more cost).")]
    public int gridSizeZ = 1024;

    [Tooltip("Fog world-space bounds derived from terrain (X,Z).")]
    public Vector2 worldMin;
    public Vector2 worldMax;

    [Header("Bounds")]
    [Tooltip("Force the fog bounds to a specific aspect ratio (e.g. screen 16:9).")]
    public bool enforceAspectRatio = true;
    public Vector2 aspectRatio = new Vector2(16f, 9f);

    [Header("Vision")]
    public bool useLineOfSight = true;
    public LayerMask fogBlockerMask = Physics.DefaultRaycastLayers;
    [Tooltip("Optionally assign vision sources manually; otherwise we auto-find them.")]
    public List<VisionSource> visionSources = new List<VisionSource>();
    [Tooltip("If false, only the provided visionSources list is used (no auto-discovery).")]
    public bool autoFindVisionSources = true;

    [Header("Smoothing")]
    [Tooltip("Smooths the fog edges without requiring very high texture resolution.")]
    public bool enableEdgeSmoothing = true;
    [Range(0, 4)] public int blurRadiusCells = 1;
    [Range(1, 4)] public int blurIterations = 1;
    [Header("Debug")]
    [SerializeField, Tooltip("If true, logs vision sources and samples near them each frame.")] private bool debugLogs = false;
    [SerializeField, Tooltip("Optional extra world position to sample for debug.")] private Transform debugSampleTarget;
    [SerializeField, Tooltip("Seconds between debug logs.")] private float debugLogInterval = 1f;

    private bool[,] visible;
    private Texture2D fogTexture;
    private Color32[] pixelBuffer;
    private float[] blurBufferA;
    private float[] blurBufferB;
    private float cellSizeX;
    private float cellSizeZ;
    private bool initialized;
    private int cachedGridSizeX;
    private int cachedGridSizeZ;
    private float _nextDebugLogTime;

    private static readonly int FogTexId = Shader.PropertyToID("_FogOfWarTex");
    private static readonly int FogWorldMinId = Shader.PropertyToID("_FogWorldMin");
    private static readonly int FogWorldMaxId = Shader.PropertyToID("_FogWorldMax");

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (initialized)
        {
            PushGlobalShaderProperties();
        }
    }

    private void Initialize()
    {
        gridSizeX = Mathf.Max(1, gridSizeX);
        gridSizeZ = Mathf.Max(1, gridSizeZ);

        ResolveWorldBounds();
        AllocateBuffers();
        RefreshVisionSourcesList();
        initialized = true;
    }

    private void ResolveWorldBounds()
    {
        Terrain activeTerrain = Terrain.activeTerrain;
        if (activeTerrain != null && activeTerrain.terrainData != null)
        {
            Vector3 terrainPos = activeTerrain.transform.position;
            Vector3 terrainSize = activeTerrain.terrainData.size;
            worldMin = new Vector2(terrainPos.x, terrainPos.z);
            worldMax = new Vector2(terrainPos.x + terrainSize.x, terrainPos.z + terrainSize.z);
        }
        else if (worldMin == worldMax)
        {
            // Fallback bounds if no terrain is present.
            worldMin = new Vector2(-50f, -50f);
            worldMax = new Vector2(50f, 50f);
        }

        ApplyAspectRatioIfNeeded();
    }

    private void ApplyAspectRatioIfNeeded()
    {
        if (!enforceAspectRatio || aspectRatio.y <= 0.0001f)
        {
            return;
        }

        float desired = aspectRatio.x / aspectRatio.y;
        float width = worldMax.x - worldMin.x;
        float height = worldMax.y - worldMin.y;
        if (width <= 0.0001f || height <= 0.0001f)
        {
            return;
        }

        float current = width / height;
        Vector2 center = (worldMin + worldMax) * 0.5f;
        if (current < desired)
        {
            width = height * desired;
        }
        else
        {
            height = width / desired;
        }

        worldMin = new Vector2(center.x - width * 0.5f, center.y - height * 0.5f);
        worldMax = new Vector2(center.x + width * 0.5f, center.y + height * 0.5f);
    }

    private void AllocateBuffers()
    {
        cellSizeX = (worldMax.x - worldMin.x) / Mathf.Max(1, gridSizeX);
        cellSizeZ = (worldMax.y - worldMin.y) / Mathf.Max(1, gridSizeZ);

        visible = new bool[gridSizeX, gridSizeZ];
        pixelBuffer = new Color32[gridSizeX * gridSizeZ];
        blurBufferA = new float[gridSizeX * gridSizeZ];
        blurBufferB = new float[gridSizeX * gridSizeZ];
        cachedGridSizeX = gridSizeX;
        cachedGridSizeZ = gridSizeZ;

        fogTexture = new Texture2D(gridSizeX, gridSizeZ, TextureFormat.R8, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "FogOfWar"
        };

        Shader.SetGlobalTexture(FogTexId, fogTexture);
        PushGlobalShaderProperties();
        UploadTexture(); // Ensure texture is initialized to fully hidden.
    }

    private void RefreshVisionSourcesList()
    {
        if (visionSources == null)
        {
            visionSources = new List<VisionSource>();
        }

        // Remove nulls and duplicates.
        for (int i = visionSources.Count - 1; i >= 0; i--)
        {
            VisionSource src = visionSources[i];
            if (src == null || visionSources.IndexOf(src) != i)
            {
                visionSources.RemoveAt(i);
            }
        }

        if (!autoFindVisionSources)
        {
            return;
        }

        // Merge in any scene VisionSources not already tracked (e.g., players spawned at runtime).
        VisionSource[] found = FindObjectsByType<VisionSource>(FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            VisionSource src = found[i];
            if (src && !visionSources.Contains(src))
            {
                visionSources.Add(src);
            }
        }
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        if (gridSizeX != cachedGridSizeX || gridSizeZ != cachedGridSizeZ)
        {
            Initialize();
        }

        RefreshVisionSourcesList();
        if (visionSources.Count == 0)
        {
            Debug.LogWarning($"[FogOfWarManager:{name}] No vision sources; fog stays fully hidden.");
            return;
        }

        ClearVisibilityGrid();
        StampAllVisionSources();
        ApplyEdgeSmoothingIfNeeded();
        UploadTexture();
        if (debugLogs)
        {
            TryDebugLog();
        }
    }

    private void ClearVisibilityGrid()
    {
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int z = 0; z < gridSizeZ; z++)
            {
                visible[x, z] = false;
            }
        }

        // Reset blur buffer to zero; will be repopulated during smoothing.
        System.Array.Clear(blurBufferA, 0, blurBufferA.Length);
    }

    private void StampAllVisionSources()
    {
        for (int i = 0; i < visionSources.Count; i++)
        {
            VisionSource source = visionSources[i];
            if (source == null)
            {
                continue;
            }

            StampCircleWithOptionalLOS(source.transform.position, source.CurrentRadius);
        }
    }

    private void TryDebugLog()
    {
        if (Time.time < _nextDebugLogTime)
        {
            return;
        }

        _nextDebugLogTime = Time.time + Mathf.Max(0.1f, debugLogInterval);

        string msg = $"[FogOfWarManager:{name}] visionCount={visionSources.Count}";
        for (int i = 0; i < visionSources.Count; i++)
        {
            VisionSource vs = visionSources[i];
            if (!vs)
            {
                continue;
            }

            float sample = SampleFog01(vs.transform.position);
            msg += $" | vs[{i}] {vs.name} pos={vs.transform.position:F1} radius={vs.CurrentRadius:F1} sampleAtSource={sample:F2}";
        }

        if (debugSampleTarget)
        {
            float s = SampleFog01(debugSampleTarget.position);
            msg += $" | sample(debugTarget={debugSampleTarget.name})={s:F2}";
        }

        Debug.Log(msg, this);
    }

    private void StampCircleWithOptionalLOS(Vector3 srcPos, float radius)
    {
        if (radius <= 0.001f)
        {
            return;
        }

        float radiusSq = radius * radius;
        Vector2 gridCoord = WorldToGrid(srcPos);
        int srcX = Mathf.FloorToInt(gridCoord.x);
        int srcZ = Mathf.FloorToInt(gridCoord.y);
        int rCellsX = Mathf.CeilToInt(radius / Mathf.Max(0.0001f, cellSizeX));
        int rCellsZ = Mathf.CeilToInt(radius / Mathf.Max(0.0001f, cellSizeZ));

        int minX = Mathf.Clamp(srcX - rCellsX, 0, gridSizeX - 1);
        int maxX = Mathf.Clamp(srcX + rCellsX, 0, gridSizeX - 1);
        int minZ = Mathf.Clamp(srcZ - rCellsZ, 0, gridSizeZ - 1);
        int maxZ = Mathf.Clamp(srcZ + rCellsZ, 0, gridSizeZ - 1);

        Vector3 eye = srcPos + Vector3.up * 1.5f;
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector3 cellWorld = GridToWorld(x, z);
                Vector2 toCell = new Vector2(cellWorld.x - srcPos.x, cellWorld.z - srcPos.z);
                if (toCell.sqrMagnitude > radiusSq)
                {
                    continue;
                }

                if (useLineOfSight && fogBlockerMask.value != 0)
                {
                    Vector3 target = new Vector3(cellWorld.x, eye.y, cellWorld.z);
                    Vector3 dir = target - eye;
                    float distance = dir.magnitude;
                    if (distance > 0.01f && Physics.Raycast(eye, dir.normalized, distance, fogBlockerMask, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                }

                visible[x, z] = true;
            }
        }
    }

    private void ApplyEdgeSmoothingIfNeeded()
    {
        int total = gridSizeX * gridSizeZ;
        // Populate base buffer with 0/1 from visibility.
        int idx = 0;
        for (int z = 0; z < gridSizeZ; z++)
        {
            for (int x = 0; x < gridSizeX; x++)
            {
                blurBufferA[idx++] = visible[x, z] ? 1f : 0f;
            }
        }

        if (!enableEdgeSmoothing || blurRadiusCells <= 0)
        {
            return;
        }

        int radius = blurRadiusCells;
        int kernelSize = radius * 2 + 1;
        float invKernel = 1f / kernelSize;

        for (int iteration = 0; iteration < blurIterations; iteration++)
        {
            // Horizontal pass
            for (int z = 0; z < gridSizeZ; z++)
            {
                int rowStart = z * gridSizeX;
                float sum = 0f;

                // Initial window
                for (int k = -radius; k <= radius; k++)
                {
                    int sampleX = Mathf.Clamp(k, 0, gridSizeX - 1);
                    sum += blurBufferA[rowStart + sampleX];
                }
                blurBufferB[rowStart] = sum * invKernel;

                for (int x = 1; x < gridSizeX; x++)
                {
                    int addIndex = rowStart + Mathf.Min(gridSizeX - 1, x + radius);
                    int removeIndex = rowStart + Mathf.Max(0, x - radius - 1);
                    sum += blurBufferA[addIndex] - blurBufferA[removeIndex];
                    blurBufferB[rowStart + x] = sum * invKernel;
                }
            }

            // Vertical pass
            float invKernelVert = invKernel;
            for (int x = 0; x < gridSizeX; x++)
            {
                float sum = 0f;

                // Initial window
                for (int k = -radius; k <= radius; k++)
                {
                    int sampleZ = Mathf.Clamp(k, 0, gridSizeZ - 1);
                    sum += blurBufferB[sampleZ * gridSizeX + x];
                }
                blurBufferA[x] = sum * invKernelVert;

                for (int z = 1; z < gridSizeZ; z++)
                {
                    int addZ = Mathf.Min(gridSizeZ - 1, z + radius);
                    int removeZ = Mathf.Max(0, z - radius - 1);
                    sum += blurBufferB[addZ * gridSizeX + x] - blurBufferB[removeZ * gridSizeX + x];
                    blurBufferA[z * gridSizeX + x] = sum * invKernelVert;
                }
            }
        }
    }

    private void UploadTexture()
    {
        int index = 0;
        for (int z = 0; z < gridSizeZ; z++)
        {
            for (int x = 0; x < gridSizeX; x++)
            {
                float v = blurBufferA[index];
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
                pixelBuffer[index++] = new Color32(b, 0, 0, 0);
            }
        }

        fogTexture.SetPixels32(pixelBuffer);
        fogTexture.Apply(false, false);
    }

    public float SampleFog01(Vector3 worldPos)
    {
        if (!initialized)
        {
            return 0f;
        }

        if (worldPos.x < worldMin.x || worldPos.x > worldMax.x || worldPos.z < worldMin.y || worldPos.z > worldMax.y)
        {
            return 0f;
        }

        Vector2 gridPos = WorldToGrid(worldPos);
        int gx = Mathf.Clamp(Mathf.FloorToInt(gridPos.x), 0, gridSizeX - 1);
        int gz = Mathf.Clamp(Mathf.FloorToInt(gridPos.y), 0, gridSizeZ - 1);
        return visible[gx, gz] ? 1f : 0f;
    }

    private Vector2 WorldToGrid(Vector3 worldPos)
    {
        float x = (worldPos.x - worldMin.x) / Mathf.Max(0.0001f, cellSizeX);
        float z = (worldPos.z - worldMin.y) / Mathf.Max(0.0001f, cellSizeZ);
        return new Vector2(x, z);
    }

    private Vector3 GridToWorld(int x, int z)
    {
        float worldX = worldMin.x + (x + 0.5f) * cellSizeX;
        float worldZ = worldMin.y + (z + 0.5f) * cellSizeZ;
        return new Vector3(worldX, 0f, worldZ);
    }

    private void PushGlobalShaderProperties()
    {
        Shader.SetGlobalVector(FogWorldMinId, new Vector4(worldMin.x, 0f, worldMin.y, 0f));
        Shader.SetGlobalVector(FogWorldMaxId, new Vector4(worldMax.x, 0f, worldMax.y, 0f));
        Shader.SetGlobalTexture(FogTexId, fogTexture);
    }

    public void PushShaderProperties()
    {
        PushGlobalShaderProperties();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 min = new Vector3(worldMin.x, 0f, worldMin.y);
        Vector3 max = new Vector3(worldMax.x, 0f, worldMax.y);
        Vector3 center = (min + max) * 0.5f;
        Vector3 size = new Vector3(worldMax.x - worldMin.x, 0.1f, worldMax.y - worldMin.y);

        Gizmos.color = new Color(0f, 0.6f, 1f, 0.4f);
        Gizmos.DrawWireCube(center, size);
    }
}
