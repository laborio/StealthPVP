using TMPro;
using UnityEngine;

/// <summary>
/// Canvas UI manager for ability cooldowns and related text.
/// </summary>
[DisallowMultipleComponent]
public class GameUiManager : MonoBehaviour
{
    [SerializeField, Tooltip("Ability runner for the reveal ability (player)."),] private AbilityRunner revealAbility;
    [SerializeField, Tooltip("Cooldown text for the reveal ability.")] private TMP_Text revealCooldownText;

    private void Update()
    {
        UpdateRevealCooldown();
    }

    public void SetRevealAbility(AbilityRunner ability)
    {
        revealAbility = ability;
    }

    private void UpdateRevealCooldown()
    {
        if (!revealCooldownText || !revealAbility)
        {
            return;
        }

        float remaining = revealAbility.CooldownRemaining;
        if (remaining > 0f)
        {
            revealCooldownText.text = Mathf.CeilToInt(remaining).ToString();
        }
        else
        {
            revealCooldownText.text = string.Empty;
        }
    }
}
