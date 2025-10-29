using UnityEngine;

public class NPCCombatHandler : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string deathParameterName = "isDead";
    [SerializeField] private Collider[] collidersToDisable;

    private bool _isDead;
    private Targetable _targetable;

    public bool IsDead => _isDead;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    internal void RegisterTargetable(Targetable targetable)
    {
        _targetable = targetable;
    }

    public void Kill()
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;

        if (animator != null && !string.IsNullOrEmpty(deathParameterName))
        {
            animator.SetBool(deathParameterName, true);
        }

        if (collidersToDisable != null && collidersToDisable.Length > 0)
        {
            for (int i = 0; i < collidersToDisable.Length; i++)
            {
                Collider collider = collidersToDisable[i];
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }
        }

        if (_targetable != null)
        {
            _targetable.NotifyDeath();
        }
    }
}
