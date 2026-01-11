using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boots a two-player local versus mode: spawns players far apart, assigns cameras/compasses, and swaps hunter/hunted roles on each kill.
/// </summary>
[DisallowMultipleComponent]
public class LocalVersusGameManager : MonoBehaviour
{
    public static LocalVersusGameManager Instance { get; private set; }

    internal enum PlayerSlot
    {
        Player1,
        Player2,
        Player3
    }

    internal enum SharedGamepadTarget
    {
        Player2,
        Player3
    }

    [Header("Subsystems")]
    [SerializeField] internal LocalVersusSpawner spawner;
    [SerializeField] internal LocalVersusRules rules;
    [SerializeField] internal LocalVersusBindings bindings;
    [SerializeField] internal LocalVersusVisuals visuals;

    [Header("Players")]
    [SerializeField] internal GameObject player1Prefab;
    [SerializeField, Tooltip("Player 2 prefab created in editor (duplicate of player1 with gamepad input router).")] internal GameObject player2Prefab;
    [SerializeField, Tooltip("Player 3 prefab created in editor (duplicate of player1 with gamepad input router).")] internal GameObject player3Prefab;
    [SerializeField] internal Camera player1Camera;
    [SerializeField] internal Camera player2Camera;
    [SerializeField] internal Camera player3Camera;
    [SerializeField, Tooltip("Compass UI for player 1 (points to hunted target).")] internal RevealIndicatorController player1Compass;
    [SerializeField, Tooltip("Compass UI for player 2 (points to hunted target).")] internal RevealIndicatorController player2Compass;
    [SerializeField, Tooltip("Compass UI for player 3 (points to hunted target).")] internal RevealIndicatorController player3Compass;
    [SerializeField, Tooltip("Optional decoy spawner; will be forced to decoys-only.")] internal NpcGameDirector npcDirector;
    [Header("Fog Of War (optional per-player)")]
    [SerializeField] internal FogOfWarManager player1Fog;
    [SerializeField] internal FogOfWarManager player2Fog;
    [SerializeField] internal FogOfWarManager player3Fog;
    [Header("Player-Only Visuals")]
    [SerializeField, Tooltip("Layer name for player 1-only visuals.")] internal string player1OnlyLayer = "Player1Only";
    [SerializeField, Tooltip("Layer name for player 2-only visuals.")] internal string player2OnlyLayer = "Player2Only";
    [SerializeField, Tooltip("Layer name for player 3-only visuals.")] internal string player3OnlyLayer = "Player3Only";
    [SerializeField, Tooltip("Child object names to restrict to the owning player camera.")] internal string[] playerOnlyObjectNames = { "PlayerCompass", "T_ClickArea", "ClickArea", "WSCanvas", "RangeIndicator" };
    [Header("UI/Reveal")]
    [SerializeField] internal GameUiManager player1Ui;
    [SerializeField] internal GameUiManager player2Ui;
    [SerializeField] internal GameUiManager player3Ui;
    [Header("UI/Targets")]
    [SerializeField, Tooltip("Target image prefab for player 1 (dark).")] internal GameObject targetImageDarkPrefab;
    [SerializeField, Tooltip("Target image prefab for player 2 (green).")] internal GameObject targetImageGreenPrefab;
    [SerializeField, Tooltip("Target image prefab for player 3 (purple).")] internal GameObject targetImagePurplePrefab;
    [Header("UI/Scoreboard")]
    [SerializeField] internal ScoreboardController player1Scoreboard;
    [SerializeField] internal ScoreboardController player2Scoreboard;
    [SerializeField] internal ScoreboardController player3Scoreboard;
    [SerializeField, Tooltip("Fallback points awarded for killing the assigned target (used if GameplayTuning is missing).")] internal int scorePerTargetKill = 100;
    [Header("Minimap")]
    [SerializeField] internal MinimapController player1Minimap;
    [SerializeField] internal MinimapController player2Minimap;
    [SerializeField] internal MinimapController player3Minimap;
    [Header("Tuning")]
    [SerializeField] internal GameplayTuning gameplayTuning;
    [SerializeField, Tooltip("Reveal key for player 1 (keyboard/mouse).")] internal KeyCode player1RevealKey = KeyCode.F;
    [SerializeField, Tooltip("Reveal key for player 2 (gamepad).")] internal KeyCode player2RevealKey = KeyCode.Joystick1Button4;
    [SerializeField, Tooltip("Reveal key for player 3 (gamepad).")] internal KeyCode player3RevealKey = KeyCode.Joystick2Button4;
    [SerializeField, Tooltip("Smoke key for player 1 (keyboard/mouse).")] internal KeyCode player1SmokeKey = KeyCode.C;
    [SerializeField, Tooltip("Smoke key for player 2 (gamepad).")] internal KeyCode player2SmokeKey = KeyCode.Joystick1Button2;
    [SerializeField, Tooltip("Smoke key for player 3 (gamepad).")] internal KeyCode player3SmokeKey = KeyCode.Joystick2Button2;
    [Header("Input Axes")]
    [SerializeField, Tooltip("Keyboard-only horizontal axis name for player 1.")] internal string player1HorizontalAxis = "Horizontal";
    [SerializeField, Tooltip("Keyboard-only vertical axis name for player 1.")] internal string player1VerticalAxis = "Vertical";
    [SerializeField, Tooltip("Gamepad horizontal axis for player 2.")] internal string player2MoveHorizontalAxis = "Horizontal2";
    [SerializeField, Tooltip("Gamepad vertical axis for player 2.")] internal string player2MoveVerticalAxis = "Vertical2";
    [SerializeField, Tooltip("Gamepad aim horizontal axis for player 2.")] internal string player2AimHorizontalAxis = "AimHorizontal2";
    [SerializeField, Tooltip("Gamepad aim vertical axis for player 2.")] internal string player2AimVerticalAxis = "AimVertical2";
    [SerializeField, Tooltip("Gamepad horizontal axis for player 3.")] internal string player3MoveHorizontalAxis = "Horizontal3";
    [SerializeField, Tooltip("Gamepad vertical axis for player 3.")] internal string player3MoveVerticalAxis = "Vertical3";
    [SerializeField, Tooltip("Gamepad aim horizontal axis for player 3.")] internal string player3AimHorizontalAxis = "AimHorizontal3";
    [SerializeField, Tooltip("Gamepad aim vertical axis for player 3.")] internal string player3AimVerticalAxis = "AimVertical3";
    [Header("Gamepad Triggers")]
    [SerializeField, Tooltip("Primary trigger axis (attack) for player 2.")] internal string player2PrimaryTriggerAxis = "Axis6";
    [SerializeField, Tooltip("Primary trigger axis (attack) for player 3.")] internal string player3PrimaryTriggerAxis = "Axis6_P3";
    [Header("Gamepad Axis Inversion")]
    [SerializeField] internal bool invertGamepadMoveX = false;
    [SerializeField] internal bool invertGamepadMoveY = false;
    [SerializeField] internal bool invertGamepadAimX = false;
    [SerializeField] internal bool invertGamepadAimY = false;
    [Header("Player 2 KeyCodes")]
    [SerializeField, Tooltip("If false, primary keycode is ignored so trigger/aim-only can be used.")] internal bool player2UsePrimaryKeycode = true;
    [SerializeField] internal KeyCode player2PrimaryKeyCode = KeyCode.Joystick1Button12;
    [SerializeField] internal KeyCode player2StunKeyCode = KeyCode.Joystick1Button14;
    [SerializeField] internal KeyCode player2JumpKeyCode = KeyCode.Joystick1Button0;
    [SerializeField] internal KeyCode player2DashKeyCode = KeyCode.Joystick1Button1;
    [SerializeField] internal KeyCode player2RunKeyCode = KeyCode.Joystick1Button5;
    [SerializeField] internal KeyCode player2InteractKeyCode = KeyCode.Joystick1Button3;
    [Header("Player 3 KeyCodes")]
    [SerializeField, Tooltip("If false, primary keycode is ignored so trigger/aim-only can be used.")] internal bool player3UsePrimaryKeycode = true;
    [SerializeField] internal KeyCode player3PrimaryKeyCode = KeyCode.Joystick2Button12;
    [SerializeField] internal KeyCode player3StunKeyCode = KeyCode.Joystick2Button14;
    [SerializeField] internal KeyCode player3JumpKeyCode = KeyCode.Joystick2Button0;
    [SerializeField] internal KeyCode player3DashKeyCode = KeyCode.Joystick2Button1;
    [SerializeField] internal KeyCode player3RunKeyCode = KeyCode.Joystick2Button5;
    [SerializeField] internal KeyCode player3InteractKeyCode = KeyCode.Joystick2Button3;
    [Header("Gamepad Assignment")]
    [SerializeField, Tooltip("If true, a single gamepad controls either player 2 or player 3 (toggle below).")] internal bool shareSingleGamepadBetweenPlayer2And3 = true;
    [SerializeField, Tooltip("When sharing a single gamepad, select which player receives input.")] internal SharedGamepadTarget sharedGamepadTarget = SharedGamepadTarget.Player2;
    [SerializeField, Tooltip("If true, player 3 uses player 2 input bindings (useful when sharing one controller).")] internal bool player3UsePlayer2Bindings = true;

    [Header("Spawning")]
    [SerializeField] internal List<Transform> spawnPoints = new List<Transform>();
    [SerializeField, Tooltip("Minimum distance between the two player spawns.")] internal float minSpawnSeparation = 25f;
    [SerializeField, Tooltip("Radius for NavMesh sampling near spawn points.")] internal float navMeshSampleRadius = 6f;

    [Header("Round Flow")]
    [SerializeField, Tooltip("Seconds to wait after a kill before respawning and swapping roles.")] internal float respawnDelay = 1.5f;

    [Header("Phase 2")]
    [SerializeField, Tooltip("Seconds before phase 2 begins.")] internal float phase1Duration = 300f;
    [SerializeField] internal float phase2EmpoweredMaxHealth = 500f;
    [SerializeField] internal float phase2EmpoweredMoveSpeedMultiplier = 1.25f;
    [SerializeField] internal float phase2EmpoweredAttackSpeedMultiplier = 1.25f;
    [SerializeField] internal float phase2EmpoweredNpcKillHeal = 100f;
    [SerializeField, Tooltip("Percent scale increase per NPC kill in phase 2.")] internal float phase2EmpoweredNpcKillScalePercent = 5f;
    [SerializeField] internal int phase2TeamLives = 5;

    internal GameObject _player1Instance;
    internal GameObject _player2Instance;
    internal GameObject _player3Instance;
    internal CharacterHealth _player1Health;
    internal CharacterHealth _player2Health;
    internal CharacterHealth _player3Health;
    internal bool _hunterIsPlayer1 = true;
    internal bool _respawnInProgress;
    internal int _player1Score;
    internal int _player2Score;
    internal int _player3Score;
    internal bool _phase2Active;
    internal bool _gameEnded;
    internal PlayerSlot? _empoweredSlot;
    internal CharacterHealth _empoweredHealth;
    internal int _player1Lives;
    internal int _player2Lives;
    internal int _player3Lives;
    private float _phaseTimer;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
        }
        Instance = this;
        ActivateDisplays();
        if (npcDirector)
        {
            npcDirector.EnableDecoysOnlyMode();
        }
        EnsureComponents();
        visuals?.AutoAssignCompasses();
        ResolveGameplayTuning();
    }

    private void OnEnable()
    {
        CharacterHealth.AnyDied += HandleAnyDied;
    }

    private void OnDisable()
    {
        CharacterHealth.AnyDied -= HandleAnyDied;
    }

    private void Start()
    {
        _phaseTimer = Mathf.Max(0f, GetPhase1Duration());
        spawner?.SpawnOrRespawnPlayers(initialSpawn: true);
        visuals?.UpdateRoleIndicators();
        visuals?.UpdateCompasses();
        bindings?.UpdateStunBindings();
        bindings?.UpdateSmokeBindings();
        bindings?.UpdateDashBindings();
        rules?.UpdateScoreboards();
        if (_phaseTimer <= 0f)
        {
            StartPhase2();
        }
    }

    private void Update()
    {
        if (_gameEnded || _phase2Active)
        {
            return;
        }

        if (_phaseTimer > 0f)
        {
            _phaseTimer = Mathf.Max(0f, _phaseTimer - Time.deltaTime);
            if (_phaseTimer <= 0f)
            {
                StartPhase2();
            }
        }
    }

    private void OnDestroy()
    {
        rules?.UnsubscribeHealth(_player1Health);
        rules?.UnsubscribeHealth(_player2Health);
        rules?.UnsubscribeHealth(_player3Health);
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void EnsureComponents()
    {
        spawner = spawner ? spawner : GetComponent<LocalVersusSpawner>();
        if (!spawner)
        {
            spawner = gameObject.AddComponent<LocalVersusSpawner>();
        }

        rules = rules ? rules : GetComponent<LocalVersusRules>();
        if (!rules)
        {
            rules = gameObject.AddComponent<LocalVersusRules>();
        }

        bindings = bindings ? bindings : GetComponent<LocalVersusBindings>();
        if (!bindings)
        {
            bindings = gameObject.AddComponent<LocalVersusBindings>();
        }

        visuals = visuals ? visuals : GetComponent<LocalVersusVisuals>();
        if (!visuals)
        {
            visuals = gameObject.AddComponent<LocalVersusVisuals>();
        }

        spawner.Initialize(this);
        rules.Initialize(this);
        bindings.Initialize(this);
        visuals.Initialize(this);
    }

    public void TryHandleHumiliation(CharacterHealth attacker, CharacterHealth victim)
    {
        rules?.TryHandleHumiliation(attacker, victim);
    }

    public bool IsPlayerHealth(CharacterHealth health)
    {
        return rules != null && rules.IsPlayerHealth(health);
    }

    public bool CanKillPlayer(CharacterHealth attacker, CharacterHealth victim)
    {
        return rules == null || rules.CanKillPlayer(attacker, victim);
    }

    internal bool IsPhase2Active => _phase2Active;
    internal bool IsGameOver => _gameEnded;
    internal bool IsEmpoweredHealth(CharacterHealth health) => health && health == _empoweredHealth;
    internal float Phase1TimeRemaining => Mathf.Max(_phaseTimer, 0f);
    internal CharacterHealth EmpoweredHealth => _empoweredHealth;
    internal PlayerSlot? EmpoweredSlot => _empoweredSlot;

    internal int GetPhase2Lives(PlayerSlot slot)
    {
        return slot switch
        {
            PlayerSlot.Player1 => _player1Lives,
            PlayerSlot.Player2 => _player2Lives,
            PlayerSlot.Player3 => _player3Lives,
            _ => 0
        };
    }

    internal bool TryHandlePhase2Death(CharacterHealth dead)
    {
        if (!_phase2Active || _gameEnded || !dead)
        {
            return true;
        }

        PlayerSlot? slot = ResolvePlayerSlot(dead);
        if (!slot.HasValue)
        {
            return false;
        }

        if (_empoweredSlot.HasValue && slot.Value == _empoweredSlot.Value)
        {
            EndGame();
            return false;
        }

        int lives = Mathf.Max(0, GetPhase2Lives(slot.Value) - 1);
        SetPhase2Lives(slot.Value, lives);
        rules?.UpdateScoreboards();
        if (lives > 0)
        {
            return true;
        }

        if (AreAllChallengersEliminated())
        {
            EndGame();
        }

        return false;
    }

    private void ActivateDisplays()
    {
        for (int i = 1; i < Display.displays.Length && i <= 2; i++)
        {
            Display.displays[i].Activate();
        }
    }

    internal void ResolveGameplayTuning()
    {
        if (gameplayTuning)
        {
            return;
        }

        GameplayTuningApplier applier = FindFirstObjectByType<GameplayTuningApplier>();
        if (applier && applier.Tuning)
        {
            gameplayTuning = applier.Tuning;
        }
    }

    private float GetPhase1Duration()
    {
        return gameplayTuning ? gameplayTuning.phase1Duration : phase1Duration;
    }

    private float GetPhase2EmpoweredMaxHealth()
    {
        return gameplayTuning ? gameplayTuning.phase2EmpoweredMaxHealth : phase2EmpoweredMaxHealth;
    }

    private float GetPhase2EmpoweredMoveSpeedMultiplier()
    {
        return gameplayTuning ? gameplayTuning.phase2EmpoweredMoveSpeedMultiplier : phase2EmpoweredMoveSpeedMultiplier;
    }

    private float GetPhase2EmpoweredAttackSpeedMultiplier()
    {
        return gameplayTuning ? gameplayTuning.phase2EmpoweredAttackSpeedMultiplier : phase2EmpoweredAttackSpeedMultiplier;
    }

    private float GetPhase2EmpoweredNpcHeal()
    {
        return gameplayTuning ? gameplayTuning.phase2EmpoweredNpcKillHeal : phase2EmpoweredNpcKillHeal;
    }

    private float GetPhase2EmpoweredNpcScalePercent()
    {
        return gameplayTuning ? gameplayTuning.phase2EmpoweredNpcKillScalePercent : phase2EmpoweredNpcKillScalePercent;
    }

    private int GetPhase2TeamLives()
    {
        return gameplayTuning ? gameplayTuning.phase2TeamLives : phase2TeamLives;
    }

    private void StartPhase2()
    {
        if (_phase2Active || _gameEnded)
        {
            return;
        }

        _phase2Active = true;
        _phaseTimer = 0f;

        List<PlayerSlot> candidates = new List<PlayerSlot>(3);
        if (_player1Health)
        {
            candidates.Add(PlayerSlot.Player1);
        }
        if (_player2Health)
        {
            candidates.Add(PlayerSlot.Player2);
        }
        if (_player3Health)
        {
            candidates.Add(PlayerSlot.Player3);
        }

        if (candidates.Count == 0)
        {
            _phase2Active = false;
            return;
        }

        int topScore = int.MinValue;
        List<PlayerSlot> tied = new List<PlayerSlot>(3);
        for (int i = 0; i < candidates.Count; i++)
        {
            PlayerSlot slot = candidates[i];
            int score = GetScoreForSlot(slot);
            if (score > topScore)
            {
                topScore = score;
                tied.Clear();
                tied.Add(slot);
            }
            else if (score == topScore)
            {
                tied.Add(slot);
            }
        }

        PlayerSlot chosen = tied.Count == 1
            ? tied[0]
            : tied[UnityEngine.Random.Range(0, tied.Count)];

        _empoweredSlot = chosen;
        _empoweredHealth = GetPlayerHealth(chosen);

        int teamLives = Mathf.Max(0, GetPhase2TeamLives());
        SetPhase2Lives(PlayerSlot.Player1, chosen == PlayerSlot.Player1 ? 0 : teamLives);
        SetPhase2Lives(PlayerSlot.Player2, chosen == PlayerSlot.Player2 ? 0 : teamLives);
        SetPhase2Lives(PlayerSlot.Player3, chosen == PlayerSlot.Player3 ? 0 : teamLives);

        ApplyPhase2Stats();
        visuals?.UpdateCompasses();
        rules?.UpdateScoreboards();
    }

    private void ApplyPhase2Stats()
    {
        ApplyPhase2StatsForSlot(PlayerSlot.Player1);
        ApplyPhase2StatsForSlot(PlayerSlot.Player2);
        ApplyPhase2StatsForSlot(PlayerSlot.Player3);
    }

    private void ApplyPhase2StatsForSlot(PlayerSlot slot)
    {
        GameObject instance = GetPlayerInstance(slot);
        if (!instance)
        {
            return;
        }

        bool empowered = _empoweredSlot.HasValue && slot == _empoweredSlot.Value;
        SimpleCharacterController controller = instance.GetComponent<SimpleCharacterController>()
            ?? instance.GetComponentInChildren<SimpleCharacterController>(true);
        if (controller)
        {
            float moveMultiplier = empowered ? GetPhase2EmpoweredMoveSpeedMultiplier() : 1f;
            controller.SetExternalMoveSpeedMultiplier(moveMultiplier);
        }

        CharacterAnimations animations = instance.GetComponent<CharacterAnimations>()
            ?? instance.GetComponentInChildren<CharacterAnimations>(true);
        if (animations)
        {
            float attackMultiplier = empowered ? GetPhase2EmpoweredAttackSpeedMultiplier() : 1f;
            animations.SetAttackSpeedMultiplier(attackMultiplier);
        }

        if (empowered && _empoweredHealth)
        {
            _empoweredHealth.SetMaxHealth(GetPhase2EmpoweredMaxHealth(), healToFull: true);
        }
    }

    private void HandleAnyDied(CharacterHealth dead, DamagePayload payload)
    {
        if (!_phase2Active || _gameEnded || !dead)
        {
            return;
        }

        if (IsPlayerHealth(dead))
        {
            return;
        }

        if (!_empoweredHealth)
        {
            return;
        }

        CharacterHealth killer = ResolveInstigatorHealth(payload);
        if (!killer || killer != _empoweredHealth)
        {
            return;
        }

        float heal = GetPhase2EmpoweredNpcHeal();
        if (heal > 0f)
        {
            _empoweredHealth.SetMaxHealth(_empoweredHealth.MaxHealth + heal, healToFull: false);
            _empoweredHealth.Heal(heal);
            PlayerFloatingTextController floatingText = _empoweredHealth.GetComponent<PlayerFloatingTextController>()
                ?? _empoweredHealth.GetComponentInChildren<PlayerFloatingTextController>(true);
            if (floatingText)
            {
                floatingText.ShowHeal(Mathf.RoundToInt(heal));
            }
        }

        float scalePercent = GetPhase2EmpoweredNpcScalePercent();
        if (scalePercent > 0f)
        {
            float scaleMultiplier = 1f + (scalePercent / 100f);
            Transform target = _empoweredHealth.transform;
            if (target)
            {
                target.localScale = Vector3.Scale(target.localScale, Vector3.one * scaleMultiplier);
            }
        }
    }

    private CharacterHealth ResolveInstigatorHealth(DamagePayload payload)
    {
        GameObject instigator = payload.Instigator ? payload.Instigator : payload.Source;
        if (!instigator)
        {
            return null;
        }

        return instigator.GetComponent<CharacterHealth>()
            ?? instigator.GetComponentInParent<CharacterHealth>()
            ?? instigator.GetComponentInChildren<CharacterHealth>(true);
    }

    private PlayerSlot? ResolvePlayerSlot(CharacterHealth health)
    {
        if (!health)
        {
            return null;
        }

        if (health == _player1Health)
        {
            return PlayerSlot.Player1;
        }

        if (health == _player2Health)
        {
            return PlayerSlot.Player2;
        }

        if (health == _player3Health)
        {
            return PlayerSlot.Player3;
        }

        return null;
    }

    private GameObject GetPlayerInstance(PlayerSlot slot)
    {
        return slot switch
        {
            PlayerSlot.Player1 => _player1Instance,
            PlayerSlot.Player2 => _player2Instance,
            PlayerSlot.Player3 => _player3Instance,
            _ => null
        };
    }

    private CharacterHealth GetPlayerHealth(PlayerSlot slot)
    {
        return slot switch
        {
            PlayerSlot.Player1 => _player1Health,
            PlayerSlot.Player2 => _player2Health,
            PlayerSlot.Player3 => _player3Health,
            _ => null
        };
    }

    private int GetScoreForSlot(PlayerSlot slot)
    {
        return slot switch
        {
            PlayerSlot.Player1 => _player1Score,
            PlayerSlot.Player2 => _player2Score,
            PlayerSlot.Player3 => _player3Score,
            _ => 0
        };
    }

    private void SetPhase2Lives(PlayerSlot slot, int lives)
    {
        int clamped = Mathf.Max(0, lives);
        switch (slot)
        {
            case PlayerSlot.Player1:
                _player1Lives = clamped;
                break;
            case PlayerSlot.Player2:
                _player2Lives = clamped;
                break;
            case PlayerSlot.Player3:
                _player3Lives = clamped;
                break;
        }
    }

    private bool AreAllChallengersEliminated()
    {
        bool hasPlayer1 = _player1Health && (!_empoweredSlot.HasValue || _empoweredSlot.Value != PlayerSlot.Player1);
        bool hasPlayer2 = _player2Health && (!_empoweredSlot.HasValue || _empoweredSlot.Value != PlayerSlot.Player2);
        bool hasPlayer3 = _player3Health && (!_empoweredSlot.HasValue || _empoweredSlot.Value != PlayerSlot.Player3);

        bool player1Out = !hasPlayer1 || _player1Lives <= 0;
        bool player2Out = !hasPlayer2 || _player2Lives <= 0;
        bool player3Out = !hasPlayer3 || _player3Lives <= 0;

        return player1Out && player2Out && player3Out;
    }

    private void EndGame()
    {
        if (_gameEnded)
        {
            return;
        }

        _gameEnded = true;
        FreezeAllPlayerControl();
    }

    private void FreezeAllPlayerControl()
    {
        SetPlayerControlEnabled(PlayerSlot.Player1, false);
        SetPlayerControlEnabled(PlayerSlot.Player2, false);
        SetPlayerControlEnabled(PlayerSlot.Player3, false);
    }

    private void SetPlayerControlEnabled(PlayerSlot slot, bool enabled)
    {
        GameObject instance = GetPlayerInstance(slot);
        if (!instance)
        {
            return;
        }

        if (bindings)
        {
            bindings.SetInputEnabledForInstance(instance, slot, enabled);
        }

        SimpleCharacterController controller = instance.GetComponent<SimpleCharacterController>()
            ?? instance.GetComponentInChildren<SimpleCharacterController>(true);
        if (controller)
        {
            controller.SetInputSuppressed(!enabled);
        }

        PlayerInputRouter router = controller ? controller.InputRouter : instance.GetComponentInChildren<PlayerInputRouter>(true);
        if (router)
        {
            router.SetInputSuppressed(!enabled);
        }

        AbilityRunner ability = instance.GetComponent<AbilityRunner>() ?? instance.GetComponentInChildren<AbilityRunner>(true);
        if (ability)
        {
            ability.SetInputEnabled(enabled);
        }

        SmokeAbility smoke = instance.GetComponent<SmokeAbility>() ?? instance.GetComponentInChildren<SmokeAbility>(true);
        if (smoke)
        {
            smoke.SetInputEnabled(enabled);
        }

        DefensiveAbilityCycler defensive = instance.GetComponent<DefensiveAbilityCycler>()
            ?? instance.GetComponentInChildren<DefensiveAbilityCycler>(true);
        if (defensive)
        {
            defensive.SetInputEnabled(enabled);
        }
    }

}
