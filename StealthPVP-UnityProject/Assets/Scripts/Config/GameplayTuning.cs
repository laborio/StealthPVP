using UnityEngine;

/// <summary>
/// Centralized tuning knobs for gameplay/reveal/difficulty. Plug into the applier to push values at runtime.
/// </summary>
[CreateAssetMenu(fileName = "GameplayTuning", menuName = "Game/GameplayTuning", order = 0)]
public class GameplayTuning : ScriptableObject
{
    [Header("Difficulty")]
    [Range(0f, 1f)] public float aiDifficulty = 0.5f;

[Header("Reveal - Skill (shared)")]
public float revealCooldown = 30f;
public float revealHold = 2f;
public float revealFade = 1f;

[Header("Stun")]
public float playerStunDuration = 3f;

[Header("Smoke")]
public float smokeCooldown = 90f;

[Header("Morph")]
public float morphDuration = 6f;
public float morphMoveSpeed = 1.25f;
public float morphSearchRadius = 6f;

[Header("Dash")]
public float dashSpeedMultiplier = 3f;
public float dashAirSpeedMultiplier = 2f;
public float dashDuration = 0.25f;
public float dashCooldown = 1f;

[Header("Reveal - Overlook Multipliers (uses VisionSource heights)")]
public float overlookLevel1CooldownMultiplier = 2f;
public float overlookLevel2CooldownMultiplier = 3f;

[Header("Vision (applied to all VisionSources)")]
public float visionBaseRadius = 10f;
public float visionLevel1Radius = 16f;
public float visionLevel2Radius = 22f;
public bool visionUseHeightBasedLevels = true;
    public float visionLevel1MinHeight = 2f;
    public float visionLevel2MinHeight = 6f;
}
