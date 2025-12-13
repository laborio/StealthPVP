using UnityEngine;

/// <summary>
/// Automatically triggers reveal when the player is above configured heights. Disables manual input while active and can hide the cooldown UI.
/// </summary>
[DisallowMultipleComponent]
public class HighGroundRevealController : MonoBehaviour
{
    [SerializeField] private AbilityRunner abilityRunner;
    [SerializeField] private VisionSource visionSource;
    [SerializeField] private CharacterController characterController;

    [Header("Overlook Config (driven by GameplayTuning)")]
    [HideInInspector] [SerializeField] private float level1CooldownMultiplier = 2f;
    [HideInInspector] [SerializeField] private float level2CooldownMultiplier = 3f;

    private void Update()
    {
        if (!abilityRunner)
        {
            return;
        }

        float y = visionSource ? visionSource.transform.position.y : transform.position.y;
        bool grounded = characterController ? characterController.isGrounded : true;
        if (!grounded)
        {
            return;
        }

        float multiplier = 1f;
        float level1Min = visionSource ? visionSource.level1MinHeight : float.MaxValue;
        float level2Min = visionSource ? visionSource.level2MinHeight : float.MaxValue;

        if (visionSource && visionSource.useHeightBasedLevels)
        {
            if (y >= level2Min)
            {
                multiplier = level2CooldownMultiplier;
            }
            else if (y >= level1Min)
            {
                multiplier = level1CooldownMultiplier;
            }
        }

        if (multiplier > 1f)
        {
            abilityRunner.AccelerateCooldown(multiplier, Time.deltaTime);
        }
    }

    public void ApplyConfig(float l1Mult, float l2Mult)
    {
        level1CooldownMultiplier = l1Mult;
        level2CooldownMultiplier = l2Mult;
    }
}
