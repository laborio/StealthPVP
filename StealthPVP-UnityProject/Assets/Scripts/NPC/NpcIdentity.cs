using System;
using UnityEngine;

/// <summary>
/// Identifies an NPC with a color and exposes a target flag that can drive visuals.
/// </summary>
[DisallowMultipleComponent]
public class NpcIdentity : MonoBehaviour
{
    [SerializeField, Tooltip("Color used to identify this NPC in UI.")] private Color identifierColor = Color.white;
    [SerializeField, Tooltip("Optional object toggled when this NPC is the active target.")] private GameObject targetIndicator;
    [SerializeField, Tooltip("Optional UI icon used for morph previews.")] private Sprite previewIcon;

    public Color IdentifierColor => identifierColor;
    public bool IsTarget { get; private set; }
    public Sprite PreviewIcon => previewIcon;

    public event Action<NpcIdentity> BecameTarget;
    public event Action<NpcIdentity> LostTarget;

    public void SetTarget(bool isTarget)
    {
        if (IsTarget == isTarget)
        {
            return;
        }

        IsTarget = isTarget;
        if (targetIndicator)
        {
            targetIndicator.SetActive(isTarget);
        }

        if (isTarget)
        {
            BecameTarget?.Invoke(this);
        }
        else
        {
            LostTarget?.Invoke(this);
        }
    }
}
