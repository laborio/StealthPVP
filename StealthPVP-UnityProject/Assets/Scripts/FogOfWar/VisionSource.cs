using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to units that provide fog vision.
/// </summary>
public class VisionSource : MonoBehaviour
{
    [Tooltip("Base vision radius in world units.")]
    public float baseRadius = 10f;

    [Header("High Ground Levels")]
    [Tooltip("Vision radius when on level 1 high ground.")]
    public float level1Radius = 16f;
    [Tooltip("Vision radius when on level 2 high ground.")]
    public float level2Radius = 22f;
    [Tooltip("Enable height-based level detection (uses Y position thresholds).")]
    public bool useHeightBasedLevels = false;
    [Tooltip("Minimum world Y to count as level 1.")]
    public float level1MinHeight = 2f;
    [Tooltip("Minimum world Y to count as level 2.")]
    public float level2MinHeight = 6f;

    [HideInInspector] public bool isOnHighGround;

    private readonly List<int> activeZoneLevels = new List<int>();
    private int currentLevel;

    public float CurrentRadius
    {
        get
        {
            int level = GetEffectiveLevel();
            switch (level)
            {
                case 2:
                    return level2Radius;
                case 1:
                    return level1Radius;
                default:
                    return baseRadius;
            }
        }
    }

    public void AddZoneLevel(int level)
    {
        if (level < 1)
        {
            return;
        }

        if (!activeZoneLevels.Contains(level))
        {
            activeZoneLevels.Add(level);
        }
    }

    public void RemoveZoneLevel(int level)
    {
        activeZoneLevels.Remove(level);
    }

    private int GetEffectiveLevel()
    {
        int level = 0;
        // Priority: highest active zone level, otherwise height-based.
        if (activeZoneLevels.Count > 0)
        {
            for (int i = 0; i < activeZoneLevels.Count; i++)
            {
                if (activeZoneLevels[i] > level)
                {
                    level = activeZoneLevels[i];
                }
            }
        }
        else if (useHeightBasedLevels)
        {
            float y = transform.position.y;
            if (y >= level2MinHeight)
            {
                level = 2;
            }
            else if (y >= level1MinHeight)
            {
                level = 1;
            }
        }

        currentLevel = level;
        isOnHighGround = currentLevel > 0;
        return currentLevel;
    }
}
