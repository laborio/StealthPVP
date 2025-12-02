using System.Collections.Generic;
using UnityEngine;

public partial class SimpleCharacterController
{
    [Header("Context Actions")]
    [SerializeField, Tooltip("Action pop-up UI attached to the player. Set active while an action is available.")] private GameObject actionHintUI;

    private readonly List<IContextualAction> _contextActions = new List<IContextualAction>();
    private readonly Dictionary<Collider, List<IContextualAction>> _colliderActions = new Dictionary<Collider, List<IContextualAction>>();

    private void HandleActionInput(bool interactPressed, bool isGrounded)
    {
        if (!interactPressed || _seatingState != SeatingState.Standing)
        {
            return;
        }

        IContextualAction action = GetBestContextAction(isGrounded, false);
        if (action != null)
        {
            action.TryExecute(this, isGrounded);
        }
    }

    private void UpdateActionHintDisplay(bool actionKeyAllowed, bool isGrounded)
    {
        if (!actionHintUI)
        {
            return;
        }

        IContextualAction best = actionKeyAllowed ? GetBestContextAction(isGrounded, true) : null;
        SetActionHintVisible(best != null);
    }

    private void SetActionHintVisible(bool visible)
    {
        if (actionHintUI && actionHintUI.activeSelf != visible)
        {
            actionHintUI.SetActive(visible);
        }

    }

    private IContextualAction GetBestContextAction(bool isGrounded, bool forHint)
    {
        IContextualAction bestAction = null;
        int bestPriority = int.MinValue;

        for (int i = _contextActions.Count - 1; i >= 0; i--)
        {
            IContextualAction action = _contextActions[i];
            if (action == null)
            {
                _contextActions.RemoveAt(i);
                continue;
            }

            if (action.IsBusy)
            {
                continue;
            }

            bool allowed = forHint ? action.ShouldShowHint(this, isGrounded) : action.CanExecute(this, isGrounded);
            if (!allowed)
            {
                continue;
            }

            int priority = action.Priority;
            if (bestAction == null || priority > bestPriority)
            {
                bestAction = action;
                bestPriority = priority;
            }
        }

        return bestAction;
    }

    private void HandleActionTriggerEnter(Collider other)
    {
        RegisterContextActions(other);
    }

    private void HandleActionTriggerExit(Collider other)
    {
        UnregisterContextActions(other);
    }

    private void RegisterContextActions(Collider other)
    {
        if (!other)
        {
            return;
        }

        IContextualAction[] actions = other.GetComponents<IContextualAction>();
        if (actions == null || actions.Length == 0)
        {
            return;
        }

        if (!_colliderActions.TryGetValue(other, out List<IContextualAction> colliderList))
        {
            colliderList = new List<IContextualAction>();
            _colliderActions.Add(other, colliderList);
        }

        foreach (IContextualAction action in actions)
        {
            if (action == null || colliderList.Contains(action))
            {
                continue;
            }

            colliderList.Add(action);
            if (!_contextActions.Contains(action))
            {
                _contextActions.Add(action);
            }
            action.OnEnterRange(this);
        }
    }

    private void AddManualContextAction(Collider owner, IContextualAction action)
    {
        if (!owner || action == null)
        {
            return;
        }

        if (!_colliderActions.TryGetValue(owner, out List<IContextualAction> list))
        {
            list = new List<IContextualAction>();
            _colliderActions.Add(owner, list);
        }

        if (!list.Contains(action))
        {
            list.Add(action);
        }

        if (!_contextActions.Contains(action))
        {
            _contextActions.Add(action);
            action.OnEnterRange(this);
        }
    }

    private void UnregisterContextActions(Collider other)
    {
        if (!other || !_colliderActions.TryGetValue(other, out List<IContextualAction> actions))
        {
            return;
        }

        foreach (IContextualAction action in actions)
        {
            if (action != null)
            {
                action.OnExitRange(this);
                _contextActions.Remove(action);
            }
        }

        _colliderActions.Remove(other);
    }

    private void ClearContextActions()
    {
        foreach (List<IContextualAction> actions in _colliderActions.Values)
        {
            foreach (IContextualAction action in actions)
            {
                action?.OnExitRange(this);
            }
        }

        _colliderActions.Clear();
        _contextActions.Clear();
        SetActionHintVisible(false);
    }
}
