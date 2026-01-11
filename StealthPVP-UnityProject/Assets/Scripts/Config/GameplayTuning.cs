using UnityEngine;

/// <summary>
/// Centralized tuning knobs for gameplay/reveal/difficulty. Plug into the applier to push values at runtime.
/// </summary>
[CreateAssetMenu(fileName = "GameplayTuning", menuName = "Game/GameplayTuning", order = 0)]
public class GameplayTuning : ScriptableObject
{
    [Header("Difficulty")]
    [Range(0f, 1f)] public float aiDifficulty = 0.5f;

[Header("Scoring")]
public int scorePerTargetKill = 100;
public int wrongTargetPenalty = -100;

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
public bool morphAllowNpc = true;
public float morphNpcMoveSpeed = -1f;
public float morphNpcMaterialColorTolerance = 0.05f;

[Header("Dash")]
public float dashSpeedMultiplier = 3f;
public float dashAirSpeedMultiplier = 2f;
public float dashDuration = 0.25f;
public float dashCooldown = 1f;

[Header("Invisibility Bonus")]
public float invisibilityDuration = 6f;

[Header("Phase 2")]
public float phase1Duration = 300f;
public float phase2EmpoweredMaxHealth = 500f;
public float phase2EmpoweredMoveSpeedMultiplier = 1.25f;
public float phase2EmpoweredAttackSpeedMultiplier = 1.25f;
public float phase2EmpoweredNpcKillHeal = 100f;
public float phase2EmpoweredNpcKillScalePercent = 5f;
public int phase2TeamLives = 5;

[Header("Phase 2 Ranged Attack")]
public float phase2RangedDamage = 10f;
public float phase2RangedFireRate = 8f;
public float phase2RangedRange = 30f;
public float phase2RangedLineDuration = 0.05f;

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
