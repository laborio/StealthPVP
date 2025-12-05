using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bridges animation events fired on an Animator's GameObject to WeaponDamage components
/// that may live on child objects (e.g., the weapon). Attach this to the same object
/// as the Animator and point it at the weapon damage components you want to drive.
/// </summary>
[DisallowMultipleComponent]
public class WeaponDamageEventRelay : MonoBehaviour
{
    [SerializeField, Tooltip("WeaponDamage components to notify. If empty and auto-discover is on, children will be searched on Awake.")] private List<WeaponDamage> targets = new List<WeaponDamage>();
    [SerializeField, Tooltip("When true, auto-populates with WeaponDamage components in children if none are assigned.")] private bool autoDiscoverIfEmpty = true;

    private void Awake()
    {
        EnsureTargets();
    }

    public void StartDamageWindow()
    {
        EnsureTargets();
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i])
            {
                targets[i].StartDamageWindow();
            }
        }
    }

    public void EndDamageWindow()
    {
        EnsureTargets();
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i])
            {
                targets[i].EndDamageWindow();
            }
        }
    }

    private void EnsureTargets()
    {
        if (targets != null && targets.Count > 0)
        {
            return;
        }

        if (!autoDiscoverIfEmpty)
        {
            return;
        }

        WeaponDamage[] found = GetComponentsInChildren<WeaponDamage>();
        if (found != null && found.Length > 0)
        {
            if (targets == null)
            {
                targets = new List<WeaponDamage>(found.Length);
            }
            else
            {
                targets.Clear();
            }

            targets.AddRange(found);
        }
    }
}
