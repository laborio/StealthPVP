using UnityEngine;

/// <summary>
/// Ensures the attached camera writes a depth texture so the fog overlay can reconstruct world positions.
/// </summary>
[RequireComponent(typeof(Camera))]
public class FogOfWarCameraDepth : MonoBehaviour
{
    private void OnEnable()
    {
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.depthTextureMode |= DepthTextureMode.Depth;
        }
    }
}
