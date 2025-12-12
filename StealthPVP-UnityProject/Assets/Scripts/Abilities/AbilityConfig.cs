using System;
using UnityEngine;

/// <summary>
/// Shared ability configuration for player and NPCs. Allows per-role tuning and expansion with new abilities.
/// </summary>
[CreateAssetMenu(fileName = "AbilityConfig", menuName = "Game/AbilityConfig", order = 0)]
public class AbilityConfig : ScriptableObject
{
    [SerializeField, Tooltip("Abilities used by the human player.")] private AbilityDefinition[] playerAbilities;
    [SerializeField, Tooltip("Abilities used by NPC triangle agents.")] private AbilityDefinition[] npcAbilities;

    public AbilityDefinition GetAbility(string id, bool isPlayer)
    {
        AbilityDefinition[] set = isPlayer ? playerAbilities : npcAbilities;
        if (set == null)
        {
            return null;
        }

        for (int i = 0; i < set.Length; i++)
        {
            if (set[i] != null && string.Equals(set[i].Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return set[i];
            }
        }

        return null;
    }
}

[Serializable]
public class AbilityDefinition
{
    [SerializeField, Tooltip("Identifier used by code to look up this ability. Example: Reveal, AbilityE, AbilityF.")] private string id = "Reveal";
    [SerializeField, Tooltip("Input key for the player. NPCs ignore this but still use cooldown/durations.")] private KeyCode key = KeyCode.F;
    [SerializeField, Tooltip("Cooldown seconds.")] private float cooldownSeconds = 30f;
    [SerializeField, Tooltip("Seconds the effect stays fully active.")] private float fullDurationSeconds = 2f;
    [SerializeField, Tooltip("Seconds to fade the effect out.")] private float fadeDurationSeconds = 1f;

    public string Id => id;
    public KeyCode Key => key;
    public float CooldownSeconds => cooldownSeconds;
    public float FullDurationSeconds => fullDurationSeconds;
    public float FadeDurationSeconds => fadeDurationSeconds;
}
