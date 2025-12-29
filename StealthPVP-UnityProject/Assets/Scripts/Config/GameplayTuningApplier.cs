
using UnityEngine;

/// <summary>
/// Pushes gameplay tuning into runtime systems (director difficulty, player reveal ability, etc.).
/// Place this on a scene object under SCRIPTS/Systems.
/// </summary>
[DisallowMultipleComponent]
public class GameplayTuningApplier : MonoBehaviour
{
    [SerializeField] private GameplayTuning tuning;
    [SerializeField] private AbilityRunner playerRevealAbility;
    [SerializeField] private SmokeAbility playerSmokeAbility;
    [SerializeField, Tooltip("Optional player reveal indicator to apply fade settings.")] private RevealIndicatorController playerRevealIndicator;
    [SerializeField, Tooltip("Apply to all VisionSources found in the scene.")] private bool autoApplyVisionSources = true;
    [SerializeField, Tooltip("Optional explicit VisionSources to override when autoApplyVisionSources is false.")] private VisionSource[] visionSources;
    [SerializeField, Tooltip("Optional player high-ground reveal controller.")] private HighGroundRevealController playerHighGroundReveal;

    public GameplayTuning Tuning => tuning;

    private void Start()
    {
        if (!tuning)
        {
            Debug.LogWarning("GameplayTuningApplier: No tuning asset assigned.", this);
            return;
        }

        Apply();
    }

    public void Apply()
    {
        if (playerRevealAbility)
        {
            playerRevealAbility.ApplyOverrides(tuning.revealCooldown, tuning.revealHold, tuning.revealFade);
        }

        if (playerRevealIndicator)
        {
            // Match indicator fades to reveal fade for simplicity.
            playerRevealIndicator.ApplyFadeConfig(tuning.revealFade, tuning.revealFade);
        }

        if (playerSmokeAbility)
        {
            playerSmokeAbility.SetCooldown(tuning.smokeCooldown);
        }

        if (playerHighGroundReveal)
        {
            playerHighGroundReveal.ApplyConfig(
                tuning.overlookLevel1CooldownMultiplier,
                tuning.overlookLevel2CooldownMultiplier);
        }

        ApplyVision();
    }

    private void ApplyVision()
    {
        VisionSource[] targets = autoApplyVisionSources
            ? FindObjectsByType<VisionSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            : visionSources;

        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            VisionSource vs = targets[i];
            if (!vs)
            {
                continue;
            }

            vs.baseRadius = tuning.visionBaseRadius;
            vs.level1Radius = tuning.visionLevel1Radius;
            vs.level2Radius = tuning.visionLevel2Radius;
            vs.useHeightBasedLevels = tuning.visionUseHeightBasedLevels;
            vs.level1MinHeight = tuning.visionLevel1MinHeight;
            vs.level2MinHeight = tuning.visionLevel2MinHeight;
        }
    }
}
