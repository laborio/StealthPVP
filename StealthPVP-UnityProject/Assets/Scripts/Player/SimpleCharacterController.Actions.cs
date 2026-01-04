using System.Collections.Generic;
using UnityEngine;

public partial class SimpleCharacterController
{
    [Header("Context Actions")]
    [SerializeField, Tooltip("Action pop-up UI attached to the player. Set active while an action is available.")] private GameObject actionHintUI;
    [SerializeField, Tooltip("Pickup pop-up UI attached to the player. Set active while a pickup action is available.")] private GameObject pickupHintUI;

    private readonly List<IContextualAction> _contextActions = new List<IContextualAction>();
    private readonly Dictionary<Collider, List<IContextualAction>> _colliderActions = new Dictionary<Collider, List<IContextualAction>>();

    private void HandleActionInput(bool interactPressed, bool isGrounded)
    {
        if (!interactPressed || _seatingState != SeatingState.Standing)
        {
            return;
        }

        IContextualAction action = GetBestContextAction(isGrounded, false, null);
        if (action != null)
        {
            action.TryExecute(this, isGrounded);
        }
    }

    private void HandlePickupInput(bool pickupPressed, bool isGrounded)
    {
        if (!pickupPressed || _seatingState != SeatingState.Standing)
        {
            return;
        }

        IContextualAction action = GetBestContextAction(isGrounded, false, ContextActionHintType.Pickup);
        if (action != null)
        {
            action.TryExecute(this, isGrounded);
        }
    }

    private void UpdateActionHintDisplay(bool actionKeyAllowed, bool isGrounded)
    {
        if (!actionHintUI && !pickupHintUI)
        {
            return;
        }

        if (_carryActive)
        {
            SetActionHintVisible(false);
            SetPickupHintVisible(false);
            return;
        }

        IContextualAction best = actionKeyAllowed ? GetBestContextAction(isGrounded, true, null) : null;
        bool showPickup = ShouldUsePickupHint(best);
        SetActionHintVisible(best != null && !showPickup);
        SetPickupHintVisible(best != null && showPickup);
    }

    private void SetActionHintVisible(bool visible)
    {
        if (actionHintUI && actionHintUI.activeSelf != visible)
        {
            actionHintUI.SetActive(visible);
        }

    }

    private void SetPickupHintVisible(bool visible)
    {
        if (pickupHintUI && pickupHintUI.activeSelf != visible)
        {
            pickupHintUI.SetActive(visible);
        }
    }

    private bool ShouldUsePickupHint(IContextualAction action)
    {
        if (action == null)
        {
            return false;
        }

        return action is IContextualActionHint hint && hint.HintType == ContextActionHintType.Pickup;
    }

    private IContextualAction GetBestContextAction(bool isGrounded, bool forHint)
    {
        return GetBestContextAction(isGrounded, forHint, null);
    }

    private IContextualAction GetBestContextAction(bool isGrounded, bool forHint, ContextActionHintType? hintFilter)
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

            if (hintFilter.HasValue)
            {
                if (!(action is IContextualActionHint hint) || hint.HintType != hintFilter.Value)
                {
                    continue;
                }
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
            if (action == null)
            {
                continue;
            }

            if (colliderList.Contains(action))
            {
                action.OnEnterRange(this);
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
        SetPickupHintVisible(false);
    }

    public void RefreshContextActionsFromOverlaps()
    {
        if (!_characterController)
        {
            return;
        }

        Vector3 center = _characterController.bounds.center;
        float radius = _characterController.radius;
        float halfHeight = Mathf.Max(0f, (_characterController.height * 0.5f) - radius);
        Vector3 up = transform.up * halfHeight;
        Vector3 point0 = center + up;
        Vector3 point1 = center - up;

        Collider[] overlaps = Physics.OverlapCapsule(point0, point1, radius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < overlaps.Length; i++)
        {
            HandleActionTriggerEnter(overlaps[i]);
        }
    }
}
