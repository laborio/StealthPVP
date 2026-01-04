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

    private void Start()
    {
        spawner?.SpawnOrRespawnPlayers(initialSpawn: true);
        visuals?.UpdateRoleIndicators();
        visuals?.UpdateCompasses();
        bindings?.UpdateStunBindings();
        bindings?.UpdateSmokeBindings();
        bindings?.UpdateDashBindings();
        rules?.UpdateScoreboards();
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

}
