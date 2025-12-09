using UnityEngine;

/// <summary>
/// Trigger volume that applies a multi-level high ground bonus to VisionSource.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HighGroundZone : MonoBehaviour
{
    [Tooltip("High ground level applied while inside this zone (1 or 2).")]
    [Range(1, 2)] public int levelIndex = 1;

    private void OnTriggerEnter(Collider other)
    {
        VisionSource source = other.GetComponent<VisionSource>();
        if (source != null)
        {
            source.AddZoneLevel(levelIndex);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        VisionSource source = other.GetComponent<VisionSource>();
        if (source != null)
        {
            source.RemoveZoneLevel(levelIndex);
        }
    }
}
