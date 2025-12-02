using UnityEngine;

/// <summary>
/// Contextual action for spike strips. Triggers the "isOpen" animation when the player presses the action key inside the trigger.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SpikeStripAction : MonoBehaviour, IContextualAction
{
    [SerializeField] private Animator animator;
    [SerializeField] private string triggerName = "isOpen";
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string requiredTag = "SpikeStrip";
    [SerializeField] private int priority = 0;

    private int _triggerHash;
    private bool _isInRange;
    private bool _isAnimating;
    private bool _hasLeftIdle;

    public int Priority => priority;
    public bool IsBusy => _isAnimating;

    private void Awake()
    {
        if (!animator)
        {
            animator = GetComponentInChildren<Animator>();
        }

        _triggerHash = Animator.StringToHash(triggerName);
        WarnIfColliderNotTrigger();
        WarnIfTagMismatch();
    }

    public void OnEnterRange(SimpleCharacterController player)
    {
        _isInRange = true;
    }

    public void OnExitRange(SimpleCharacterController player)
    {
        _isInRange = false;
    }

    public bool CanExecute(SimpleCharacterController player, bool isGrounded)
    {
        return _isInRange && !_isAnimating;
    }

    public bool ShouldShowHint(SimpleCharacterController player, bool isGrounded)
    {
        return CanExecute(player, isGrounded);
    }

    public bool TryExecute(SimpleCharacterController player, bool isGrounded)
    {
        if (!CanExecute(player, isGrounded))
        {
            return false;
        }

        if (!animator)
        {
            Debug.LogWarning($"SpikeStripAction on {name} is missing an Animator reference.", this);
            return false;
        }

        _isAnimating = true;
        animator.ResetTrigger(_triggerHash);
        animator.SetTrigger(_triggerHash);
        StartCoroutine(TrackAnimationToIdle());
        return true;
    }

    private System.Collections.IEnumerator TrackAnimationToIdle()
    {
        _hasLeftIdle = false;
        while (animator && !_hasLeftIdle)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (!stateInfo.IsName(idleStateName))
            {
                _hasLeftIdle = true;
                break;
            }
            yield return null;
        }

        while (animator && _hasLeftIdle)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName(idleStateName) && !animator.IsInTransition(0))
            {
                break;
            }
            yield return null;
        }

        _isAnimating = false;
        _hasLeftIdle = false;
    }

    private void WarnIfColliderNotTrigger()
    {
        Collider col = GetComponent<Collider>();
        if (col && !col.isTrigger)
        {
            Debug.LogWarning($"{name} SpikeStripAction expects a trigger collider for interaction.", this);
        }
    }

    private void WarnIfTagMismatch()
    {
        if (!string.IsNullOrEmpty(requiredTag) && !CompareTag(requiredTag))
        {
            Debug.LogWarning($"{name} SpikeStripAction expects tag \"{requiredTag}\" to match the action setup.", this);
        }
    }
}
