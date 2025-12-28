using UnityEngine;

/// <summary>
/// Disables player input for a short duration when stunned.
/// </summary>
[DisallowMultipleComponent]
public class PlayerStunController : MonoBehaviour
{
    [SerializeField, Tooltip("Default stun duration used when ApplyStun is called without a duration.")] private float defaultStunDuration = 3f;
    [SerializeField] private PlayerInputRouter inputRouter;
    [SerializeField] private AbilityRunner revealAbility;
    [SerializeField] private CharacterAnimations characterAnimations;
    [SerializeField] private SimpleCharacterController characterController;

    private float _stunTimer;
    private bool _isStunned;

    public bool IsStunned => _isStunned;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (!characterController)
        {
            characterController = GetComponent<SimpleCharacterController>() ?? GetComponentInChildren<SimpleCharacterController>(true);
        }

        if (!inputRouter)
        {
            inputRouter = characterController ? characterController.InputRouter : null;
        }

        if (!inputRouter)
        {
            inputRouter = GetComponent<PlayerInputRouter>()
                ?? GetComponentInChildren<PlayerInputRouter>(true)
                ?? GetComponentInParent<PlayerInputRouter>();
        }

        if (!revealAbility)
        {
            revealAbility = GetComponent<AbilityRunner>() ?? GetComponentInChildren<AbilityRunner>(true);
        }

        if (!characterAnimations)
        {
            characterAnimations = GetComponentInChildren<CharacterAnimations>(true);
        }
    }

    private void Update()
    {
        if (!_isStunned)
        {
            return;
        }

        _stunTimer = Mathf.Max(0f, _stunTimer - Time.deltaTime);
        if (_stunTimer <= 0f)
        {
            SetStunned(false);
        }
    }

    public void SetStunDuration(float seconds)
    {
        defaultStunDuration = Mathf.Max(0f, seconds);
    }

    public void ApplyStun(float duration = -1f)
    {
        float applied = duration >= 0f ? duration : defaultStunDuration;
        if (applied <= 0f)
        {
            return;
        }

        _stunTimer = Mathf.Max(_stunTimer, applied);
        if (!_isStunned)
        {
            SetStunned(true);
        }
    }

    private void SetStunned(bool value)
    {
        _isStunned = value;
        if (!inputRouter || !characterAnimations)
        {
            ResolveReferences();
        }
        if (characterAnimations)
        {
            if (value)
            {
                characterAnimations.TriggerStunned();
            }
            else
            {
                characterAnimations.ResetStunned();
            }
        }
        if (inputRouter)
        {
            inputRouter.SetInputSuppressed(value);
        }

        if (characterController)
        {
            characterController.SetInputSuppressed(value);
        }

        if (revealAbility)
        {
            revealAbility.SetInputSuppressed(value);
        }
    }
}
