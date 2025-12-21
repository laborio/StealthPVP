using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trigger volume that maps a world area to a minimap section id.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class MinimapSectionArea : MonoBehaviour
{
    public static event Action<MinimapSectionArea, NpcIdentity, bool> TargetPresenceChanged;

    private static readonly List<MinimapSectionArea> InstancesList = new List<MinimapSectionArea>();
    public static IReadOnlyList<MinimapSectionArea> Instances => InstancesList;

    [SerializeField, Tooltip("Id of the minimap section image to highlight (defaults to this object's name).")]
    private string sectionId;
    [SerializeField, Tooltip("If true and Section Id is empty, uses the GameObject name.")]
    private bool useObjectNameWhenEmpty = true;

    private readonly HashSet<NpcIdentity> _inside = new HashSet<NpcIdentity>();

    public string SectionId
        => !string.IsNullOrWhiteSpace(sectionId) ? sectionId.Trim() : (useObjectNameWhenEmpty ? name : string.Empty);

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col && !col.isTrigger)
        {
            col.isTrigger = true;
        }
    }

    private void OnEnable()
    {
        if (!InstancesList.Contains(this))
        {
            InstancesList.Add(this);
        }
    }

    private void OnDisable()
    {
        InstancesList.Remove(this);
        NotifyCleared();
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterIdentity(other);
    }

    private void OnTriggerStay(Collider other)
    {
        RegisterIdentity(other);
    }

    private void OnTriggerExit(Collider other)
    {
        NpcIdentity identity = ResolveIdentity(other);
        if (!identity)
        {
            return;
        }

        if (_inside.Remove(identity))
        {
            TargetPresenceChanged?.Invoke(this, identity, false);
        }
    }

    public bool IsInside(NpcIdentity identity)
    {
        return identity && _inside.Contains(identity);
    }

    private void RegisterIdentity(Collider other)
    {
        NpcIdentity identity = ResolveIdentity(other);
        if (!identity)
        {
            return;
        }

        if (_inside.Add(identity))
        {
            TargetPresenceChanged?.Invoke(this, identity, true);
        }
    }

    private void NotifyCleared()
    {
        if (_inside.Count == 0)
        {
            return;
        }

        NpcIdentity[] identities = new NpcIdentity[_inside.Count];
        _inside.CopyTo(identities);
        _inside.Clear();

        for (int i = 0; i < identities.Length; i++)
        {
            NpcIdentity identity = identities[i];
            if (identity)
            {
                TargetPresenceChanged?.Invoke(this, identity, false);
            }
        }
    }

    private static NpcIdentity ResolveIdentity(Collider other)
    {
        if (!other)
        {
            return null;
        }

        return other.GetComponentInParent<NpcIdentity>() ?? other.GetComponentInChildren<NpcIdentity>();
    }
}
