using UnityEngine;

/// <summary>
/// Describes an action the player can trigger with the interaction key while inside a collider.
/// </summary>
public interface IContextualAction
{
    /// <summary>
    /// Higher numbers are chosen first when multiple actions are available.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// True while the action is mid-execution and should not be re-triggered.
    /// </summary>
    bool IsBusy { get; }

    void OnEnterRange(SimpleCharacterController player);
    void OnExitRange(SimpleCharacterController player);
    bool CanExecute(SimpleCharacterController player, bool isGrounded);
    bool ShouldShowHint(SimpleCharacterController player, bool isGrounded);

    /// <summary>
    /// Attempts to perform the action. Returns true when the action was triggered.
    /// </summary>
    bool TryExecute(SimpleCharacterController player, bool isGrounded);
}
