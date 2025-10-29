using System.Collections.Generic;
using UnityEngine;

public class GameSessionManager : MonoBehaviour
{
    [SerializeField] private UIManager uiManager;
    [SerializeField] private PlayerCombatController playerCombat;

    private readonly List<Targetable> _aliveTargets = new List<Targetable>();
    private Targetable _currentTarget;

    private void OnEnable()
    {
        TargetableEvents.OnTargetDeath += HandleTargetDeath;
        ResolveDependencies();
        RefreshTargetList();
        AssignNewTarget();
    }

    private void OnDisable()
    {
        TargetableEvents.OnTargetDeath -= HandleTargetDeath;
    }

    private void HandleTargetDeath(Targetable target)
    {
        _aliveTargets.Remove(target);

        if (_currentTarget == target)
        {
            AssignNewTarget();
        }
    }

    private void ResolveDependencies()
    {
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
        }

        if (playerCombat == null)
        {
            playerCombat = FindObjectOfType<PlayerCombatController>();
        }
    }

    private void RefreshTargetList()
    {
        _aliveTargets.Clear();

        Targetable[] allTargets = FindObjectsOfType<Targetable>();
        for (int i = 0; i < allTargets.Length; i++)
        {
            Targetable targetable = allTargets[i];
            if (targetable != null && targetable.IsAlive && targetable.CompareTag("NPC"))
            {
                _aliveTargets.Add(targetable);
            }
        }
    }

    private void AssignNewTarget()
    {
        _currentTarget = null;
        if (_aliveTargets.Count == 0)
        {
            if (uiManager != null)
            {
                uiManager.UpdateTargetUI(null);
            }

            if (playerCombat != null)
            {
                playerCombat.ClearSelectedTarget();
            }

            return;
        }

        _currentTarget = _aliveTargets[Random.Range(0, _aliveTargets.Count)];

        if (uiManager != null)
        {
            uiManager.UpdateTargetUI(_currentTarget);
        }

        if (playerCombat != null)
        {
            playerCombat.AutoSelectTarget(_currentTarget);
        }
    }
}
