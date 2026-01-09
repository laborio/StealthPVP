using UnityEngine;

/// <summary>
/// Minimaps icon visibility tied to an InvisibilityBonusPickup availability.
/// </summary>
[DisallowMultipleComponent]
public class InvisibilityBonusMinimapIcon : MinimapWorldIcon
{
    [SerializeField] private InvisibilityBonusPickup bonusPickup;

    private void Awake()
    {
        if (!bonusPickup)
        {
            bonusPickup = GetComponent<InvisibilityBonusPickup>();
        }
    }

    protected override bool ShouldShowIcon()
    {
        if (!bonusPickup)
        {
            return true;
        }

        return bonusPickup.IsAvailable;
    }
}
