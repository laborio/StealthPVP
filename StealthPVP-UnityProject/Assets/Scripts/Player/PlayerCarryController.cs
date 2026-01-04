using UnityEngine;

/// <summary>
/// Tracks pickup props carried by the player and applies movement penalties.
/// </summary>
[DisallowMultipleComponent]
public class PlayerCarryController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Range(0.1f, 1f), Tooltip("Move speed multiplier while carrying a prop.")] private float carryMoveSpeedMultiplier = 0.75f;

    [Header("Drop")]
    [SerializeField, Tooltip("Forward offset when manually dropping a prop.")] private float dropForwardOffset = 1f;
    [SerializeField, Tooltip("Vertical offset applied when dropping props.")] private float dropHeightOffset = 0f;
    [SerializeField, Tooltip("Random radius used when a prop is dropped due to stun/death.")] private float forcedDropRadius = 0.4f;

    [Header("Awareness")]
    [SerializeField, Range(0f, 1f), Tooltip("Awareness fraction applied to nearby NPCs on pickup.")] private float npcAwarenessBoost = 0.25f;

    [Header("References")]
    [SerializeField] private SimpleCharacterController characterController;
    [SerializeField] private PlayerStunController stunController;
    [SerializeField] private CharacterHealth characterHealth;

    private CarryableProp _carriedProp;
    private bool _wasStunned;

    public bool IsCarrying => _carriedProp != null;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (characterHealth)
        {
            characterHealth.Died += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (characterHealth)
        {
            characterHealth.Died -= HandleDied;
        }

        if (IsCarrying)
        {
            DropInternal(false);
        }
    }

    private void Update()
    {
        if (!stunController)
        {
            stunController = GetComponent<PlayerStunController>()
                ?? GetComponentInChildren<PlayerStunController>(true);
        }

        bool stunned = stunController && stunController.IsStunned;
        if (IsCarrying && stunned && !_wasStunned)
        {
            DropInternal(false);
        }

        _wasStunned = stunned;
    }

    public bool TryPickup(CarryableProp prop)
    {
        if (!prop || IsCarrying)
        {
            return false;
        }

        ResolveReferences();
        if (!prop.TryBeginCarry(this))
        {
            return false;
        }

        _carriedProp = prop;
        if (characterController)
        {
            characterController.SetCarryState(true, carryMoveSpeedMultiplier);
        }

        ApplyNpcAwarenessBoost();
        return true;
    }

    public bool TryDropInFront()
    {
        if (!IsCarrying)
        {
            return false;
        }

        DropInternal(true);
        return true;
    }

    public void ApplyCarryConfig(float moveSpeedMultiplier, float dropForward, float dropHeight, float forcedDrop, float awarenessBoost)
    {
        carryMoveSpeedMultiplier = Mathf.Clamp(moveSpeedMultiplier, 0.1f, 1f);
        dropForwardOffset = Mathf.Max(0f, dropForward);
        dropHeightOffset = dropHeight;
        forcedDropRadius = Mathf.Max(0f, forcedDrop);
        npcAwarenessBoost = Mathf.Clamp01(awarenessBoost);

        if (characterController && IsCarrying)
        {
            characterController.SetCarryState(true, carryMoveSpeedMultiplier);
        }
    }

    private void DropInternal(bool dropInFront)
    {
        CarryableProp prop = _carriedProp;
        _carriedProp = null;

        Vector3 dropPosition = ResolveDropPosition(dropInFront);
        Quaternion dropRotation = prop ? prop.transform.rotation : Quaternion.identity;
        prop?.DropAt(dropPosition, dropRotation);

        if (characterController)
        {
            characterController.SetCarryState(false, 1f);
        }
    }

    private Vector3 ResolveDropPosition(bool dropInFront)
    {
        Vector3 position = transform.position;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        if (dropInFront)
        {
            position += forward * Mathf.Max(0f, dropForwardOffset);
        }
        else if (forcedDropRadius > 0f)
        {
            Vector2 offset = Random.insideUnitCircle * forcedDropRadius;
            position += new Vector3(offset.x, 0f, offset.y);
        }

        position.y += dropHeightOffset;
        return position;
    }

    private void ApplyNpcAwarenessBoost()
    {
        if (npcAwarenessBoost <= 0f)
        {
            return;
        }

        NpcAwareness[] npcs = Object.FindObjectsByType<NpcAwareness>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < npcs.Length; i++)
        {
            NpcAwareness npc = npcs[i];
            if (npc)
            {
                npc.ApplyAwarenessBoost(transform, npcAwarenessBoost);
            }
        }
    }

    private void HandleDied(CharacterHealth health)
    {
        if (health && IsCarrying)
        {
            DropInternal(false);
        }
    }

    private void ResolveReferences()
    {
        if (!characterController)
        {
            characterController = GetComponent<SimpleCharacterController>()
                ?? GetComponentInChildren<SimpleCharacterController>(true);
        }

        if (!stunController)
        {
            stunController = GetComponent<PlayerStunController>()
                ?? GetComponentInChildren<PlayerStunController>(true);
        }

        if (!characterHealth)
        {
            characterHealth = GetComponent<CharacterHealth>()
                ?? GetComponentInChildren<CharacterHealth>(true);
        }
    }

    private void OnValidate()
    {
        carryMoveSpeedMultiplier = Mathf.Clamp(carryMoveSpeedMultiplier, 0.1f, 1f);
        dropForwardOffset = Mathf.Max(0f, dropForwardOffset);
        forcedDropRadius = Mathf.Max(0f, forcedDropRadius);
        npcAwarenessBoost = Mathf.Clamp01(npcAwarenessBoost);
    }
}
