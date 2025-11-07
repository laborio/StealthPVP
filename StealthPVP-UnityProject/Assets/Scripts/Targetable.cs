using System;
using System.Collections.Generic;
using UnityEngine;

public class Targetable : MonoBehaviour
{
    [SerializeField] private GameObject[] outlineObjects;
    [SerializeField] private Transform targetPoint;
    [SerializeField] private NPCCombatHandler combatHandler;

    private bool _isSelected;
    private bool _isAlive = true;

    public bool IsAlive => _isAlive && (combatHandler == null || !combatHandler.IsDead);
    public Vector3 TargetPosition => targetPoint != null ? targetPoint.position : transform.position;

    private void Awake()
    {
        if (outlineObjects == null || outlineObjects.Length == 0)
        {
            outlineObjects = FindOutlineObjects();
        }

        if (combatHandler == null)
        {
            combatHandler = GetComponentInParent<NPCCombatHandler>();
        }

        if (combatHandler != null)
        {
            combatHandler.RegisterTargetable(this);
        }

        SetOutlineActive(false);
    }

    public void Select()
    {
        if (!IsAlive)
        {
            return;
        }

        _isSelected = true;
        SetOutlineActive(true);
    }

    public void Deselect()
    {
        _isSelected = false;
        SetOutlineActive(false);
    }

    public void NotifyDeath()
    {
        _isAlive = false;
        _isSelected = false;
        SetOutlineActive(false);

        TargetableEvents.RaiseTargetDeath(this);
    }

    public void HandleHit()
    {
        if (!IsAlive)
        {
            return;
        }

        if (combatHandler != null)
        {
            combatHandler.Kill();
        }
    }

    private void SetOutlineActive(bool state)
    {
        if (outlineObjects == null)
        {
            return;
        }

        for (int i = 0; i < outlineObjects.Length; i++)
        {
            GameObject outline = outlineObjects[i];
            if (outline == null)
            {
                continue;
            }

            if (outline.activeSelf != state)
            {
                outline.SetActive(state);
            }
        }
    }

    private GameObject[] FindOutlineObjects()
    {
        List<GameObject> matches = new List<GameObject>();
        Transform[] children = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null)
            {
                continue;
            }

            if (string.Equals(child.gameObject.name, "Outline", StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(child.gameObject);
            }
        }

        return matches.ToArray();
    }
}
