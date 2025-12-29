using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Smoke trigger that suppresses attacks for other players inside the volume.
/// </summary>
[DisallowMultipleComponent]
public class SmokeZone : MonoBehaviour
{
    [SerializeField] private CharacterHealth owner;
    [SerializeField, Tooltip("Optional collider to ensure is a trigger.")] private Collider triggerCollider;

    private readonly HashSet<SimpleCharacterController> _suppressed = new HashSet<SimpleCharacterController>();

    private void Awake()
    {
        if (!triggerCollider)
        {
            triggerCollider = GetComponent<Collider>();
        }

        if (triggerCollider)
        {
            triggerCollider.isTrigger = true;
        }

        if (!owner)
        {
            owner = GetComponentInParent<CharacterHealth>();
        }
    }

    private void OnEnable()
    {
        if (triggerCollider)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnDisable()
    {
        ClearSuppressed();
    }

    public void SetOwner(CharacterHealth health)
    {
        owner = health;
    }

    private void OnTriggerEnter(Collider other)
    {
        TrySuppress(other, true);
    }

    private void OnTriggerExit(Collider other)
    {
        TrySuppress(other, false);
    }

    private void TrySuppress(Collider other, bool suppress)
    {
        if (!other)
        {
            return;
        }

        SimpleCharacterController controller = other.GetComponentInParent<SimpleCharacterController>()
            ?? other.GetComponentInChildren<SimpleCharacterController>();
        if (!controller)
        {
            return;
        }

        CharacterHealth targetHealth = controller.GetComponent<CharacterHealth>()
            ?? controller.GetComponentInChildren<CharacterHealth>(true);
        if (owner && targetHealth == owner)
        {
            return;
        }

        if (suppress)
        {
            if (_suppressed.Add(controller))
            {
                controller.SetAttackSuppressed(true);
            }
        }
        else
        {
            if (_suppressed.Remove(controller))
            {
                controller.SetAttackSuppressed(false);
            }
        }
    }

    private void ClearSuppressed()
    {
        foreach (SimpleCharacterController controller in _suppressed)
        {
            if (controller)
            {
                controller.SetAttackSuppressed(false);
            }
        }
        _suppressed.Clear();
    }
}
