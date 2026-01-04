using UnityEngine;
using PlayerSlot = LocalVersusGameManager.PlayerSlot;
using SharedGamepadTarget = LocalVersusGameManager.SharedGamepadTarget;

[DisallowMultipleComponent]
public class LocalVersusBindings : MonoBehaviour
{
    [SerializeField] private LocalVersusGameManager manager;

    private GameObject _player1Instance
    {
        get => manager._player1Instance;
        set => manager._player1Instance = value;
    }

    private GameObject _player2Instance
    {
        get => manager._player2Instance;
        set => manager._player2Instance = value;
    }

    private GameObject _player3Instance
    {
        get => manager._player3Instance;
        set => manager._player3Instance = value;
    }

    private GameplayTuning gameplayTuning => manager.gameplayTuning;
    private GameUiManager player1Ui => manager.player1Ui;
    private GameUiManager player2Ui => manager.player2Ui;
    private GameUiManager player3Ui => manager.player3Ui;
    private KeyCode player1RevealKey => manager.player1RevealKey;
    private KeyCode player2RevealKey => manager.player2RevealKey;
    private KeyCode player3RevealKey => manager.player3RevealKey;
    private KeyCode player1SmokeKey => manager.player1SmokeKey;
    private KeyCode player2SmokeKey => manager.player2SmokeKey;
    private KeyCode player3SmokeKey => manager.player3SmokeKey;
    private KeyCode player1InteractKeyCode => manager.player1InteractKeyCode;
    private KeyCode player1InteractAltKeyCode => manager.player1InteractAltKeyCode;
    private bool shareSingleGamepadBetweenPlayer2And3 => manager.shareSingleGamepadBetweenPlayer2And3;
    private bool player3UsePlayer2Bindings => manager.player3UsePlayer2Bindings;
    private SharedGamepadTarget sharedGamepadTarget => manager.sharedGamepadTarget;
    private string player1HorizontalAxis => manager.player1HorizontalAxis;
    private string player1VerticalAxis => manager.player1VerticalAxis;
    private string player2MoveHorizontalAxis => manager.player2MoveHorizontalAxis;
    private string player2MoveVerticalAxis => manager.player2MoveVerticalAxis;
    private string player2AimHorizontalAxis => manager.player2AimHorizontalAxis;
    private string player2AimVerticalAxis => manager.player2AimVerticalAxis;
    private string player3MoveHorizontalAxis => manager.player3MoveHorizontalAxis;
    private string player3MoveVerticalAxis => manager.player3MoveVerticalAxis;
    private string player3AimHorizontalAxis => manager.player3AimHorizontalAxis;
    private string player3AimVerticalAxis => manager.player3AimVerticalAxis;
    private string player2PrimaryTriggerAxis => manager.player2PrimaryTriggerAxis;
    private string player3PrimaryTriggerAxis => manager.player3PrimaryTriggerAxis;
    private bool invertGamepadMoveX => manager.invertGamepadMoveX;
    private bool invertGamepadMoveY => manager.invertGamepadMoveY;
    private bool invertGamepadAimX => manager.invertGamepadAimX;
    private bool invertGamepadAimY => manager.invertGamepadAimY;
    private bool player2UsePrimaryKeycode => manager.player2UsePrimaryKeycode;
    private KeyCode player2PrimaryKeyCode => manager.player2PrimaryKeyCode;
    private KeyCode player2StunKeyCode => manager.player2StunKeyCode;
    private KeyCode player2JumpKeyCode => manager.player2JumpKeyCode;
    private KeyCode player2DashKeyCode => manager.player2DashKeyCode;
    private KeyCode player2RunKeyCode => manager.player2RunKeyCode;
    private KeyCode player2InteractKeyCode => manager.player2InteractKeyCode;
    private KeyCode player2InteractAltKeyCode => manager.player2InteractAltKeyCode;
    private bool player3UsePrimaryKeycode => manager.player3UsePrimaryKeycode;
    private KeyCode player3PrimaryKeyCode => manager.player3PrimaryKeyCode;
    private KeyCode player3StunKeyCode => manager.player3StunKeyCode;
    private KeyCode player3JumpKeyCode => manager.player3JumpKeyCode;
    private KeyCode player3DashKeyCode => manager.player3DashKeyCode;
    private KeyCode player3RunKeyCode => manager.player3RunKeyCode;
    private KeyCode player3InteractKeyCode => manager.player3InteractKeyCode;
    private KeyCode player3InteractAltKeyCode => manager.player3InteractAltKeyCode;

    public void Initialize(LocalVersusGameManager manager)
    {
        this.manager = manager;
    }

    private void Awake()
    {
        if (!manager)
        {
            manager = GetComponent<LocalVersusGameManager>();
        }
    }

    private void ResolveGameplayTuning()
    {
        manager.ResolveGameplayTuning();
    }

    internal NpcIdentity GetIdentity(GameObject root)
    {
        return root ? root.GetComponent<NpcIdentity>() ?? root.GetComponentInChildren<NpcIdentity>(true) : null;
    }

    internal VisionSource GetVision(GameObject root)
    {
        return root ? root.GetComponent<VisionSource>() ?? root.GetComponentInChildren<VisionSource>(true) : null;
    }

    internal SimpleCharacterController GetController(GameObject root)
    {
        return root ? root.GetComponent<SimpleCharacterController>() ?? root.GetComponentInChildren<SimpleCharacterController>(true) : null;
    }

    internal AbilityRunner GetAbility(GameObject root)
    {
        return root ? root.GetComponent<AbilityRunner>() ?? root.GetComponentInChildren<AbilityRunner>(true) : null;
    }

    internal void UpdateRevealBindings()
    {
        AbilityRunner ability1 = GetAbility(_player1Instance);
        AbilityRunner ability2 = GetAbility(_player2Instance);
        AbilityRunner ability3 = GetAbility(_player3Instance);

        if (ability1)
        {
            ability1.SetUseInput(true);
            ability1.SetOverrideKey(player1RevealKey);
        }

        if (ability2)
        {
            ability2.SetUseInput(true);
            ability2.SetOverrideKey(player2RevealKey);
        }

        if (ability3)
        {
            ability3.SetUseInput(true);
            ability3.SetOverrideKey(player3RevealKey);
        }

        if (player1Ui)
        {
            player1Ui.SetRevealAbility(ability1);
        }

        if (player2Ui)
        {
            player2Ui.SetRevealAbility(ability2);
        }

        if (player3Ui)
        {
            player3Ui.SetRevealAbility(ability3);
        }
    }

    internal void UpdateSmokeBindings()
    {
        float cooldown = 0f;
        if (!gameplayTuning)
        {
            ResolveGameplayTuning();
        }
        if (gameplayTuning)
        {
            cooldown = gameplayTuning.smokeCooldown;
        }
        float morphDuration = 0f;
        float morphMoveSpeed = 0f;
        float morphSearchRadius = 0f;
        if (gameplayTuning)
        {
            morphDuration = gameplayTuning.morphDuration;
            morphMoveSpeed = gameplayTuning.morphMoveSpeed;
            morphSearchRadius = gameplayTuning.morphSearchRadius;
        }

        SmokeAbility smoke1 = GetSmokeAbility(_player1Instance, addIfMissing: true);
        SmokeAbility smoke2 = GetSmokeAbility(_player2Instance, addIfMissing: true);
        SmokeAbility smoke3 = GetSmokeAbility(_player3Instance, addIfMissing: true);
        DefensiveAbilityCycler defensive1 = GetDefensiveCycler(_player1Instance, addIfMissing: true);
        DefensiveAbilityCycler defensive2 = GetDefensiveCycler(_player2Instance, addIfMissing: true);
        DefensiveAbilityCycler defensive3 = GetDefensiveCycler(_player3Instance, addIfMissing: true);
        MorphAbility morph1 = GetMorphAbility(_player1Instance, addIfMissing: true);
        MorphAbility morph2 = GetMorphAbility(_player2Instance, addIfMissing: true);
        MorphAbility morph3 = GetMorphAbility(_player3Instance, addIfMissing: true);
        bool useSharedBindings = shareSingleGamepadBetweenPlayer2And3 && player3UsePlayer2Bindings;
        KeyCode smoke3Key = useSharedBindings ? player2SmokeKey : player3SmokeKey;
        bool enableSmoke2 = smoke2 != null;
        bool enableSmoke3 = smoke3 != null;
        if (shareSingleGamepadBetweenPlayer2And3 && _player3Instance)
        {
            enableSmoke2 = sharedGamepadTarget == SharedGamepadTarget.Player2;
            enableSmoke3 = sharedGamepadTarget == SharedGamepadTarget.Player3;
        }

        if (smoke1)
        {
            smoke1.SetCooldown(cooldown);
            smoke1.SetOverrideKey(player1SmokeKey);
            smoke1.SetInputEnabled(true);
        }

        if (defensive1)
        {
            defensive1.SetSmokeAbility(smoke1);
            defensive1.SetMorphAbility(morph1);
            defensive1.SetDefensive02Cooldown(cooldown);
            defensive1.SetOverrideKey(player1SmokeKey);
            defensive1.SetInputEnabled(true);
        }

        if (morph1)
        {
            morph1.ApplyMorphConfig(morphDuration, morphMoveSpeed, morphSearchRadius);
        }

        if (smoke2)
        {
            smoke2.SetCooldown(cooldown);
            smoke2.SetOverrideKey(player2SmokeKey);
            smoke2.SetInputEnabled(enableSmoke2);
        }

        if (defensive2)
        {
            defensive2.SetSmokeAbility(smoke2);
            defensive2.SetMorphAbility(morph2);
            defensive2.SetDefensive02Cooldown(cooldown);
            defensive2.SetOverrideKey(player2SmokeKey);
            defensive2.SetInputEnabled(enableSmoke2);
        }

        if (morph2)
        {
            morph2.ApplyMorphConfig(morphDuration, morphMoveSpeed, morphSearchRadius);
        }

        if (smoke3)
        {
            smoke3.SetCooldown(cooldown);
            smoke3.SetOverrideKey(smoke3Key);
            smoke3.SetInputEnabled(enableSmoke3);
        }

        if (defensive3)
        {
            defensive3.SetSmokeAbility(smoke3);
            defensive3.SetMorphAbility(morph3);
            defensive3.SetDefensive02Cooldown(cooldown);
            defensive3.SetOverrideKey(smoke3Key);
            defensive3.SetInputEnabled(enableSmoke3);
        }

        if (morph3)
        {
            morph3.ApplyMorphConfig(morphDuration, morphMoveSpeed, morphSearchRadius);
        }

        if (player1Ui)
        {
            player1Ui.SetSmokeAbility(smoke1);
            player1Ui.SetDefensiveAbility(defensive1);
        }

        if (player2Ui)
        {
            player2Ui.SetSmokeAbility(smoke2);
            player2Ui.SetDefensiveAbility(defensive2);
        }

        if (player3Ui)
        {
            player3Ui.SetSmokeAbility(smoke3);
            player3Ui.SetDefensiveAbility(defensive3);
        }
    }

    private SmokeAbility GetSmokeAbility(GameObject root, bool addIfMissing)
    {
        if (!root)
        {
            return null;
        }

        SmokeAbility ability = root.GetComponent<SmokeAbility>() ?? root.GetComponentInChildren<SmokeAbility>(true);
        if (!ability && addIfMissing)
        {
            ability = root.AddComponent<SmokeAbility>();
        }

        return ability;
    }

    private DefensiveAbilityCycler GetDefensiveCycler(GameObject root, bool addIfMissing)
    {
        if (!root)
        {
            return null;
        }

        DefensiveAbilityCycler cycler = root.GetComponent<DefensiveAbilityCycler>()
            ?? root.GetComponentInChildren<DefensiveAbilityCycler>(true);
        if (!cycler && addIfMissing)
        {
            cycler = root.AddComponent<DefensiveAbilityCycler>();
        }

        return cycler;
    }

    private MorphAbility GetMorphAbility(GameObject root, bool addIfMissing)
    {
        if (!root)
        {
            return null;
        }

        MorphAbility ability = root.GetComponent<MorphAbility>() ?? root.GetComponentInChildren<MorphAbility>(true);
        if (!ability && addIfMissing)
        {
            ability = root.AddComponent<MorphAbility>();
        }

        return ability;
    }

    private PlayerCarryController GetCarryController(GameObject root, bool addIfMissing)
    {
        if (!root)
        {
            return null;
        }

        PlayerCarryController controller = root.GetComponent<PlayerCarryController>()
            ?? root.GetComponentInChildren<PlayerCarryController>(true);
        if (!controller && addIfMissing)
        {
            controller = root.AddComponent<PlayerCarryController>();
        }

        return controller;
    }

    internal void UpdateStunBindings()
    {
        float duration = 0f;
        if (!gameplayTuning)
        {
            ResolveGameplayTuning();
        }
        if (gameplayTuning)
        {
            duration = gameplayTuning.playerStunDuration;
        }

        ApplyStunDuration(_player1Instance, duration);
        ApplyStunDuration(_player2Instance, duration);
        ApplyStunDuration(_player3Instance, duration);
    }

    internal void UpdateDashBindings()
    {
        float speedMultiplier = 0f;
        float airSpeedMultiplier = 0f;
        float duration = 0f;
        float cooldown = 0f;
        if (!gameplayTuning)
        {
            ResolveGameplayTuning();
        }
        if (gameplayTuning)
        {
            speedMultiplier = gameplayTuning.dashSpeedMultiplier;
            airSpeedMultiplier = gameplayTuning.dashAirSpeedMultiplier;
            duration = gameplayTuning.dashDuration;
            cooldown = gameplayTuning.dashCooldown;
        }

        ApplyDashConfig(_player1Instance, speedMultiplier, airSpeedMultiplier, duration, cooldown);
        ApplyDashConfig(_player2Instance, speedMultiplier, airSpeedMultiplier, duration, cooldown);
        ApplyDashConfig(_player3Instance, speedMultiplier, airSpeedMultiplier, duration, cooldown);

        if (player1Ui)
        {
            player1Ui.SetDashController(GetController(_player1Instance));
        }

        if (player2Ui)
        {
            player2Ui.SetDashController(GetController(_player2Instance));
        }

        if (player3Ui)
        {
            player3Ui.SetDashController(GetController(_player3Instance));
        }
    }

    internal void UpdateCarryBindings()
    {
        float moveSpeedMultiplier = 0f;
        float dropForwardOffset = 0f;
        float dropHeightOffset = 0f;
        float forcedDropRadius = 0f;
        float awarenessBoost = 0f;
        if (!gameplayTuning)
        {
            ResolveGameplayTuning();
        }
        if (gameplayTuning)
        {
            moveSpeedMultiplier = gameplayTuning.carryMoveSpeedMultiplier;
            dropForwardOffset = gameplayTuning.carryDropForwardOffset;
            dropHeightOffset = gameplayTuning.carryDropHeightOffset;
            forcedDropRadius = gameplayTuning.carryForcedDropRadius;
            awarenessBoost = gameplayTuning.carryNpcAwarenessBoost;
        }

        ApplyCarryConfig(_player1Instance, moveSpeedMultiplier, dropForwardOffset, dropHeightOffset, forcedDropRadius, awarenessBoost);
        ApplyCarryConfig(_player2Instance, moveSpeedMultiplier, dropForwardOffset, dropHeightOffset, forcedDropRadius, awarenessBoost);
        ApplyCarryConfig(_player3Instance, moveSpeedMultiplier, dropForwardOffset, dropHeightOffset, forcedDropRadius, awarenessBoost);
    }

    private void ApplyDashConfig(GameObject instance, float speedMultiplier, float airSpeedMultiplier, float duration, float cooldown)
    {
        if (!instance || !gameplayTuning)
        {
            return;
        }

        SimpleCharacterController controller = GetController(instance);
        if (!controller)
        {
            return;
        }

        controller.ApplyDashConfig(speedMultiplier, airSpeedMultiplier, duration, cooldown);
    }

    private void ApplyCarryConfig(GameObject instance, float moveSpeedMultiplier, float dropForwardOffset, float dropHeightOffset,
        float forcedDropRadius, float awarenessBoost)
    {
        if (!instance || !gameplayTuning)
        {
            return;
        }

        PlayerCarryController carryController = GetCarryController(instance, addIfMissing: false);
        if (!carryController)
        {
            return;
        }

        carryController.ApplyCarryConfig(moveSpeedMultiplier, dropForwardOffset, dropHeightOffset, forcedDropRadius, awarenessBoost);
    }

    private void ApplyStunDuration(GameObject instance, float duration)
    {
        if (!instance)
        {
            return;
        }

        PlayerStunController stun = instance.GetComponent<PlayerStunController>()
            ?? instance.GetComponentInChildren<PlayerStunController>(true);
        if (!stun)
        {
            stun = instance.AddComponent<PlayerStunController>();
        }

        if (duration >= 0f)
        {
            stun.SetStunDuration(duration);
        }
    }

    internal void ConfigureInputRouter(PlayerInputRouter router, PlayerSlot slot)
    {
        if (!router)
        {
            return;
        }

        if (slot == PlayerSlot.Player1)
        {
            router.SetAxes(player1HorizontalAxis, player1VerticalAxis);
            router.SetKeyboardOnlyMovement(true);
            router.SetInteractKeys(player1InteractKeyCode, player1InteractAltKeyCode);
            return;
        }

        PlayerInputRouterGamepad gamepad = router as PlayerInputRouterGamepad;
        if (!gamepad)
        {
            return;
        }

        bool isPlayer2 = slot == PlayerSlot.Player2;
        bool usePlayer2Bindings = isPlayer2 || player3UsePlayer2Bindings;

        string moveHorizontal = usePlayer2Bindings ? player2MoveHorizontalAxis : player3MoveHorizontalAxis;
        string moveVertical = usePlayer2Bindings ? player2MoveVerticalAxis : player3MoveVerticalAxis;
        string aimHorizontal = usePlayer2Bindings ? player2AimHorizontalAxis : player3AimHorizontalAxis;
        string aimVertical = usePlayer2Bindings ? player2AimVerticalAxis : player3AimVerticalAxis;
        gamepad.SetMoveAxes(moveHorizontal, moveVertical);
        gamepad.SetAimAxes(aimHorizontal, aimVertical);
        // Use keycodes only to avoid keyboard overlap.
        gamepad.SetButtonNames(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

        bool usePrimaryKeycode = usePlayer2Bindings ? player2UsePrimaryKeycode : player3UsePrimaryKeycode;
        KeyCode primary = usePrimaryKeycode ? (usePlayer2Bindings ? player2PrimaryKeyCode : player3PrimaryKeyCode) : KeyCode.None;
        KeyCode jump = usePlayer2Bindings ? player2JumpKeyCode : player3JumpKeyCode;
        KeyCode dash = usePlayer2Bindings ? player2DashKeyCode : player3DashKeyCode;
        KeyCode run = usePlayer2Bindings ? player2RunKeyCode : player3RunKeyCode;
        KeyCode interact = usePlayer2Bindings ? player2InteractKeyCode : player3InteractKeyCode;
        KeyCode interactAlt = usePlayer2Bindings ? player2InteractAltKeyCode : player3InteractAltKeyCode;
        gamepad.SetButtonKeyCodes(primary, jump, dash, run, interact, interactAlt);
        KeyCode stun = usePlayer2Bindings ? player2StunKeyCode : player3StunKeyCode;
        gamepad.SetSecondaryKeyCode(stun);
        gamepad.SetSecondaryTrigger(string.Empty, false);

        string primaryTrigger = usePlayer2Bindings ? player2PrimaryTriggerAxis : player3PrimaryTriggerAxis;
        if (!string.IsNullOrEmpty(primaryTrigger))
        {
            gamepad.SetPrimaryTrigger(primaryTrigger, true);
        }
        else
        {
            gamepad.SetPrimaryTrigger(string.Empty, false);
        }

        gamepad.SetAxisInversion(invertGamepadMoveX, invertGamepadMoveY, invertGamepadAimX, invertGamepadAimY);
    }

    internal void UpdateInputAssignments()
    {
        AbilityRunner ability2 = GetAbility(_player2Instance);
        AbilityRunner ability3 = GetAbility(_player3Instance);
        SmokeAbility smoke2 = GetSmokeAbility(_player2Instance, addIfMissing: false);
        SmokeAbility smoke3 = GetSmokeAbility(_player3Instance, addIfMissing: false);
        DefensiveAbilityCycler defensive2 = GetDefensiveCycler(_player2Instance, addIfMissing: false);
        DefensiveAbilityCycler defensive3 = GetDefensiveCycler(_player3Instance, addIfMissing: false);

        if (shareSingleGamepadBetweenPlayer2And3 && _player3Instance)
        {
            bool enablePlayer2 = sharedGamepadTarget == SharedGamepadTarget.Player2;
            bool enablePlayer3 = sharedGamepadTarget == SharedGamepadTarget.Player3;
            SetInputEnabledForInstance(_player2Instance, PlayerSlot.Player2, enablePlayer2);
            SetInputEnabledForInstance(_player3Instance, PlayerSlot.Player3, enablePlayer3);
            SetAbilityInputEnabled(ability2, enablePlayer2);
            SetAbilityInputEnabled(ability3, enablePlayer3);
            SetSmokeInputEnabled(smoke2, enablePlayer2);
            SetSmokeInputEnabled(smoke3, enablePlayer3);
            SetDefensiveInputEnabled(defensive2, enablePlayer2);
            SetDefensiveInputEnabled(defensive3, enablePlayer3);
        }
        else
        {
            SetInputEnabledForInstance(_player2Instance, PlayerSlot.Player2, true);
            SetInputEnabledForInstance(_player3Instance, PlayerSlot.Player3, true);
            SetAbilityInputEnabled(ability2, true);
            SetAbilityInputEnabled(ability3, true);
            SetSmokeInputEnabled(smoke2, true);
            SetSmokeInputEnabled(smoke3, true);
            SetDefensiveInputEnabled(defensive2, true);
            SetDefensiveInputEnabled(defensive3, true);
        }
    }

    private void SetAbilityInputEnabled(AbilityRunner ability, bool enabled)
    {
        if (!ability)
        {
            return;
        }

        ability.SetInputEnabled(enabled);
    }

    private void SetSmokeInputEnabled(SmokeAbility ability, bool enabled)
    {
        if (!ability)
        {
            return;
        }

        ability.SetInputEnabled(enabled);
    }

    private void SetDefensiveInputEnabled(DefensiveAbilityCycler cycler, bool enabled)
    {
        if (!cycler)
        {
            return;
        }

        cycler.SetInputEnabled(enabled);
    }

    internal void SetInputEnabledForInstance(GameObject instance, PlayerSlot slot, bool enabled)
    {
        if (!instance)
        {
            return;
        }

        PlayerInputRouter[] routers = instance.GetComponentsInChildren<PlayerInputRouter>(true);
        if (routers == null || routers.Length == 0)
        {
            return;
        }

        SimpleCharacterController controller = instance.GetComponent<SimpleCharacterController>()
            ?? instance.GetComponentInChildren<SimpleCharacterController>(true);
        PlayerInputRouter primary = controller ? controller.InputRouter : null;
        if (primary && !IsRouterTypeValid(primary, slot))
        {
            primary = null;
        }

        if (!primary)
        {
            primary = FindPreferredRouter(routers, slot);
            if (controller && primary && controller.InputRouter != primary)
            {
                controller.SetInputRouter(primary);
            }
        }

        for (int i = 0; i < routers.Length; i++)
        {
            PlayerInputRouter router = routers[i];
            if (!router)
            {
                continue;
            }

            bool shouldEnable = enabled && router == primary;
            router.SetInputEnabled(shouldEnable);
        }
    }

    private bool IsRouterTypeValid(PlayerInputRouter router, PlayerSlot slot)
    {
        if (!router)
        {
            return false;
        }

        if (slot == PlayerSlot.Player1)
        {
            return router is not PlayerInputRouterGamepad;
        }

        return router is PlayerInputRouterGamepad;
    }

    private PlayerInputRouter FindPreferredRouter(PlayerInputRouter[] routers, PlayerSlot slot)
    {
        if (routers == null || routers.Length == 0)
        {
            return null;
        }

        PlayerInputRouter fallback = null;
        for (int i = 0; i < routers.Length; i++)
        {
            PlayerInputRouter router = routers[i];
            if (!router)
            {
                continue;
            }

            if (!fallback)
            {
                fallback = router;
            }

            if (IsRouterTypeValid(router, slot))
            {
                return router;
            }
        }

        return fallback;
    }

    internal PlayerInputRouter EnsureInputRouter(GameObject instance, PlayerSlot slot)
    {
        if (!instance)
        {
            return null;
        }

        if (slot == PlayerSlot.Player1)
        {
            PlayerInputRouter nonGamepad = instance.GetComponent<PlayerInputRouter>();
            if (nonGamepad && nonGamepad is not PlayerInputRouterGamepad)
            {
                return nonGamepad;
            }

            PlayerInputRouter[] routers = instance.GetComponentsInChildren<PlayerInputRouter>(true);
            for (int i = 0; i < routers.Length; i++)
            {
                if (routers[i] && routers[i] is not PlayerInputRouterGamepad)
                {
                    return routers[i];
                }
            }

            return instance.AddComponent<PlayerInputRouter>();
        }

        PlayerInputRouterGamepad gamepad = instance.GetComponent<PlayerInputRouterGamepad>()
            ?? instance.GetComponentInChildren<PlayerInputRouterGamepad>(true);
        if (gamepad)
        {
            return gamepad;
        }

        return instance.AddComponent<PlayerInputRouterGamepad>();
    }

}
