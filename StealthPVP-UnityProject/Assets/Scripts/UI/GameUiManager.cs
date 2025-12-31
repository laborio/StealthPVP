using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Canvas UI manager for ability cooldowns and related text.
/// </summary>
[DisallowMultipleComponent]
public class GameUiManager : MonoBehaviour
{
    [SerializeField, Tooltip("Ability runner for the reveal ability (player)."),] private AbilityRunner revealAbility;
    [SerializeField, Tooltip("Ability name text for the reveal slot.")] private TMP_Text revealNameText;
    [SerializeField, Tooltip("Cooldown text for the reveal ability.")] private TMP_Text revealCooldownText;
    [SerializeField, Tooltip("Icon image for the reveal slot.")] private Image revealIcon;
    [SerializeField, Tooltip("Smoke ability for this player.")] private SmokeAbility smokeAbility;
    [FormerlySerializedAs("smokeCooldownText")]
    [SerializeField, Tooltip("Cooldown text for the defensive slot.")] private TMP_Text defensiveCooldownText;
    [SerializeField, Tooltip("Defensive ability cycler for this player.")] private DefensiveAbilityCycler defensiveAbilityCycler;
    [SerializeField, Tooltip("Ability name text for the defensive slot.")] private TMP_Text defensiveNameText;
    [SerializeField, Tooltip("Icon image for the defensive slot.")] private Image defensiveIcon;
    [SerializeField, Tooltip("Ability name text for the movement slot.")] private TMP_Text movementNameText;
    [FormerlySerializedAs("dashCooldownText")]
    [SerializeField, Tooltip("Cooldown text for the movement slot.")] private TMP_Text movementCooldownText;
    [SerializeField, Tooltip("Icon image for the movement slot.")] private Image movementIcon;
    [SerializeField, Tooltip("Dash controller for this player.")] private SimpleCharacterController dashController;
    [Header("Target UI")]
    [SerializeField, Tooltip("Container that holds the target image prefab.")] private Transform targetContainer;
    [SerializeField, Tooltip("Fallback name used to find the target container if not assigned.")] private string targetContainerName = "TargetContainer";

    private GameObject _activeTargetInstance;
    private GameObject _activeTargetPrefab;
    private bool _defensiveCyclerSubscribed;
    private Sprite _defensiveDefaultIcon;
    private bool _defensiveDefaultCached;

    private const string RevealLabel = "Compass";
    private const string DefensiveSmokeLabel = "Smoke";
    private const string DefensiveAltLabel = "Morph";
    private const string MovementLabel = "Dash";

    private void OnEnable()
    {
        BindDefensiveCycler(defensiveAbilityCycler);
        CacheDefensiveDefaultIcon();
        UpdateRevealName();
        UpdateMovementName();
        UpdateDefensiveNameFromCycler();
    }

    private void OnDisable()
    {
        UnbindDefensiveCycler();
    }

    private void Update()
    {
        UpdateRevealCooldown();
        UpdateDefensiveCooldown();
        UpdateMovementCooldown();
    }

    public void SetRevealAbility(AbilityRunner ability)
    {
        revealAbility = ability;
    }

    public void SetTargetImagePrefab(GameObject prefab)
    {
        if (!ResolveTargetContainer())
        {
            return;
        }

        if (_activeTargetPrefab == prefab && _activeTargetInstance)
        {
            return;
        }

        ClearTargetContainer();
        _activeTargetPrefab = prefab;
        if (prefab)
        {
            _activeTargetInstance = Instantiate(prefab, targetContainer);
        }
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

        SetIconVisibility(revealIcon, remaining);
    }

    public void SetSmokeAbility(SmokeAbility ability)
    {
        smokeAbility = ability;
    }

    public void SetDefensiveAbility(DefensiveAbilityCycler ability)
    {
        BindDefensiveCycler(ability);
    }

    public void SetDashController(SimpleCharacterController controller)
    {
        dashController = controller;
    }

    private void UpdateDefensiveCooldown()
    {
        if (!defensiveCooldownText)
        {
            return;
        }

        float smokeRemaining = smokeAbility ? smokeAbility.CooldownRemaining : 0f;
        float defensiveRemaining = defensiveAbilityCycler ? defensiveAbilityCycler.Defensive02CooldownRemaining : 0f;
        float remaining = Mathf.Max(smokeRemaining, defensiveRemaining);
        UpdateCooldownText(defensiveCooldownText, remaining);
        UpdateDefensiveIcon(remaining);
    }

    private void UpdateMovementCooldown()
    {
        if (!movementCooldownText || !dashController)
        {
            return;
        }

        float remaining = dashController.DashCooldownRemaining;
        UpdateCooldownText(movementCooldownText, remaining);
        SetIconVisibility(movementIcon, remaining);
    }

    private void BindDefensiveCycler(DefensiveAbilityCycler ability)
    {
        if (defensiveAbilityCycler == ability && _defensiveCyclerSubscribed)
        {
            UpdateDefensiveName(defensiveAbilityCycler.NextSlot);
            return;
        }

        UnbindDefensiveCycler();
        defensiveAbilityCycler = ability;
        if (!defensiveAbilityCycler)
        {
            return;
        }

        defensiveAbilityCycler.SlotChanged += HandleDefensiveSlotChanged;
        _defensiveCyclerSubscribed = true;
        UpdateDefensiveName(defensiveAbilityCycler.NextSlot);
    }

    private void UnbindDefensiveCycler()
    {
        if (!defensiveAbilityCycler)
        {
            _defensiveCyclerSubscribed = false;
            return;
        }

        defensiveAbilityCycler.SlotChanged -= HandleDefensiveSlotChanged;
        _defensiveCyclerSubscribed = false;
    }

    private void HandleDefensiveSlotChanged(DefensiveAbilityCycler.DefensiveSlot slot)
    {
        UpdateDefensiveName(slot);
    }

    private void UpdateDefensiveName(DefensiveAbilityCycler.DefensiveSlot slot)
    {
        if (!defensiveNameText)
        {
            return;
        }

        defensiveNameText.text = slot == DefensiveAbilityCycler.DefensiveSlot.Smoke
            ? DefensiveSmokeLabel
            : DefensiveAltLabel;
    }

    private void CacheDefensiveDefaultIcon()
    {
        if (_defensiveDefaultCached || !defensiveIcon)
        {
            return;
        }

        _defensiveDefaultIcon = defensiveIcon.sprite;
        _defensiveDefaultCached = true;
    }

    private void UpdateDefensiveIcon(float cooldownRemaining)
    {
        if (!defensiveIcon)
        {
            return;
        }

        CacheDefensiveDefaultIcon();

        bool isMorphSlot = defensiveAbilityCycler
            && defensiveAbilityCycler.NextSlot == DefensiveAbilityCycler.DefensiveSlot.Defensive02;
        if (isMorphSlot && defensiveAbilityCycler.MorphAbility)
        {
            if (defensiveAbilityCycler.MorphAbility.TryGetPreviewSprite(out Sprite previewSprite))
            {
                defensiveIcon.sprite = previewSprite;
                defensiveIcon.enabled = true;
            }
            else
            {
                defensiveIcon.sprite = null;
                defensiveIcon.enabled = false;
            }
        }
        else
        {
            if (_defensiveDefaultCached)
            {
                defensiveIcon.sprite = _defensiveDefaultIcon;
            }
            defensiveIcon.enabled = true;
        }

        SetIconVisibility(defensiveIcon, cooldownRemaining);
    }

    private void UpdateRevealName()
    {
        if (revealNameText)
        {
            revealNameText.text = RevealLabel;
        }
    }

    private void UpdateMovementName()
    {
        if (movementNameText)
        {
            movementNameText.text = MovementLabel;
        }
    }

    private void UpdateDefensiveNameFromCycler()
    {
        if (!defensiveNameText)
        {
            return;
        }

        DefensiveAbilityCycler.DefensiveSlot slot = defensiveAbilityCycler
            ? defensiveAbilityCycler.NextSlot
            : DefensiveAbilityCycler.DefensiveSlot.Smoke;
        UpdateDefensiveName(slot);
    }

    private bool ResolveTargetContainer()
    {
        if (targetContainer)
        {
            return true;
        }

        if (string.IsNullOrEmpty(targetContainerName))
        {
            return false;
        }

        Transform direct = transform.Find(targetContainerName);
        if (direct)
        {
            targetContainer = direct;
            return true;
        }

        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (candidate && candidate.name == targetContainerName)
            {
                targetContainer = candidate;
                return true;
            }
        }

        return false;
    }

    private void UpdateCooldownText(TMP_Text text, float remaining)
    {
        if (!text)
        {
            return;
        }

        if (remaining > 0f)
        {
            text.text = Mathf.CeilToInt(remaining).ToString();
        }
        else
        {
            text.text = string.Empty;
        }
    }

    private void SetIconVisibility(Image image, float cooldownRemaining)
    {
        if (!image)
        {
            return;
        }

        Color color = image.color;
        color.a = cooldownRemaining > 0f ? 0f : 1f;
        image.color = color;
    }

    private void ClearTargetContainer()
    {
        if (!targetContainer)
        {
            return;
        }

        for (int i = targetContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = targetContainer.GetChild(i);
            if (child)
            {
                Destroy(child.gameObject);
            }
        }

        _activeTargetInstance = null;
    }
}
