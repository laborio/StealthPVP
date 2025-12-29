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
    [SerializeField, Tooltip("Smoke ability for this player.")] private SmokeAbility smokeAbility;
    [SerializeField, Tooltip("Cooldown text for the smoke ability.")] private TMP_Text smokeCooldownText;
    [Header("Target UI")]
    [SerializeField, Tooltip("Container that holds the target image prefab.")] private Transform targetContainer;
    [SerializeField, Tooltip("Fallback name used to find the target container if not assigned.")] private string targetContainerName = "TargetContainer";

    private GameObject _activeTargetInstance;
    private GameObject _activeTargetPrefab;

    private void Update()
    {
        UpdateRevealCooldown();
        UpdateSmokeCooldown();
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
    }

    public void SetSmokeAbility(SmokeAbility ability)
    {
        smokeAbility = ability;
    }

    private void UpdateSmokeCooldown()
    {
        if (!smokeCooldownText || !smokeAbility)
        {
            return;
        }

        float remaining = smokeAbility.CooldownRemaining;
        if (remaining > 0f)
        {
            smokeCooldownText.text = Mathf.CeilToInt(remaining).ToString();
        }
        else
        {
            smokeCooldownText.text = string.Empty;
        }
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
