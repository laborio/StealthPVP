using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Boots a two-player local versus mode: spawns players far apart, assigns cameras/compasses, and swaps hunter/hunted roles on each kill.
/// </summary>
[DisallowMultipleComponent]
public class LocalVersusGameManager : MonoBehaviour
{
    public static LocalVersusGameManager Instance { get; private set; }

    private enum PlayerSlot
    {
        Player1,
        Player2,
        Player3
    }

    private enum SharedGamepadTarget
    {
        Player2,
        Player3
    }

    [Header("Players")]
    [SerializeField] private GameObject player1Prefab;
    [SerializeField, Tooltip("Player 2 prefab created in editor (duplicate of player1 with gamepad input router).")] private GameObject player2Prefab;
    [SerializeField, Tooltip("Player 3 prefab created in editor (duplicate of player1 with gamepad input router).")] private GameObject player3Prefab;
    [SerializeField] private Camera player1Camera;
    [SerializeField] private Camera player2Camera;
    [SerializeField] private Camera player3Camera;
    [SerializeField, Tooltip("Compass UI for player 1 (points to hunted target).")] private RevealIndicatorController player1Compass;
    [SerializeField, Tooltip("Compass UI for player 2 (points to hunted target).")] private RevealIndicatorController player2Compass;
    [SerializeField, Tooltip("Compass UI for player 3 (points to hunted target).")] private RevealIndicatorController player3Compass;
    [SerializeField, Tooltip("Optional decoy spawner; will be forced to decoys-only.")] private NpcGameDirector npcDirector;
    [Header("Fog Of War (optional per-player)")]
    [SerializeField] private FogOfWarManager player1Fog;
    [SerializeField] private FogOfWarManager player2Fog;
    [SerializeField] private FogOfWarManager player3Fog;
    [Header("Player-Only Visuals")]
    [SerializeField, Tooltip("Layer name for player 1-only visuals.")] private string player1OnlyLayer = "Player1Only";
    [SerializeField, Tooltip("Layer name for player 2-only visuals.")] private string player2OnlyLayer = "Player2Only";
    [SerializeField, Tooltip("Layer name for player 3-only visuals.")] private string player3OnlyLayer = "Player3Only";
    [SerializeField, Tooltip("Child object names to restrict to the owning player camera.")] private string[] playerOnlyObjectNames = { "PlayerCompass", "T_ClickArea", "ClickArea", "WSCanvas", "RangeIndicator" };
    [Header("UI/Reveal")]
    [SerializeField] private GameUiManager player1Ui;
    [SerializeField] private GameUiManager player2Ui;
    [SerializeField] private GameUiManager player3Ui;
    [Header("UI/Targets")]
    [SerializeField, Tooltip("Target image prefab for player 1 (dark).")] private GameObject targetImageDarkPrefab;
    [SerializeField, Tooltip("Target image prefab for player 2 (green).")] private GameObject targetImageGreenPrefab;
    [SerializeField, Tooltip("Target image prefab for player 3 (purple).")] private GameObject targetImagePurplePrefab;
    [Header("UI/Scoreboard")]
    [SerializeField] private ScoreboardController player1Scoreboard;
    [SerializeField] private ScoreboardController player2Scoreboard;
    [SerializeField] private ScoreboardController player3Scoreboard;
    [SerializeField, Tooltip("Points awarded for killing the assigned target.")] private int scorePerTargetKill = 100;
    [Header("Minimap")]
    [SerializeField] private MinimapController player1Minimap;
    [SerializeField] private MinimapController player2Minimap;
    [SerializeField] private MinimapController player3Minimap;
    [Header("Tuning")]
    [SerializeField] private GameplayTuning gameplayTuning;
    [SerializeField, Tooltip("Reveal key for player 1 (keyboard/mouse).")] private KeyCode player1RevealKey = KeyCode.F;
    [SerializeField, Tooltip("Reveal key for player 2 (gamepad).")] private KeyCode player2RevealKey = KeyCode.JoystickButton4;
    [SerializeField, Tooltip("Reveal key for player 3 (gamepad).")] private KeyCode player3RevealKey = KeyCode.Joystick2Button4;
    [SerializeField, Tooltip("Smoke key for player 1 (keyboard/mouse).")] private KeyCode player1SmokeKey = KeyCode.C;
    [SerializeField, Tooltip("Smoke key for player 2 (gamepad).")] private KeyCode player2SmokeKey = KeyCode.Joystick1Button2;
    [SerializeField, Tooltip("Smoke key for player 3 (gamepad).")] private KeyCode player3SmokeKey = KeyCode.Joystick2Button2;
    [Header("Input Axes")]
    [SerializeField, Tooltip("Keyboard-only horizontal axis name for player 1.")] private string player1HorizontalAxis = "Horizontal";
    [SerializeField, Tooltip("Keyboard-only vertical axis name for player 1.")] private string player1VerticalAxis = "Vertical";
    [SerializeField, Tooltip("Gamepad horizontal axis for player 2.")] private string player2MoveHorizontalAxis = "Horizontal2";
    [SerializeField, Tooltip("Gamepad vertical axis for player 2.")] private string player2MoveVerticalAxis = "Vertical2";
    [SerializeField, Tooltip("Gamepad aim horizontal axis for player 2.")] private string player2AimHorizontalAxis = "AimHorizontal2";
    [SerializeField, Tooltip("Gamepad aim vertical axis for player 2.")] private string player2AimVerticalAxis = "AimVertical2";
    [SerializeField, Tooltip("Gamepad horizontal axis for player 3.")] private string player3MoveHorizontalAxis = "Horizontal3";
    [SerializeField, Tooltip("Gamepad vertical axis for player 3.")] private string player3MoveVerticalAxis = "Vertical3";
    [SerializeField, Tooltip("Gamepad aim horizontal axis for player 3.")] private string player3AimHorizontalAxis = "AimHorizontal3";
    [SerializeField, Tooltip("Gamepad aim vertical axis for player 3.")] private string player3AimVerticalAxis = "AimVertical3";
    [Header("Player 2 KeyCodes")]
    [SerializeField, Tooltip("If false, primary keycode is ignored so trigger/aim-only can be used.")] private bool player2UsePrimaryKeycode = true;
    [SerializeField] private KeyCode player2PrimaryKeyCode = KeyCode.JoystickButton12;
    [SerializeField] private KeyCode player2JumpKeyCode = KeyCode.Joystick1Button0;
    [SerializeField] private KeyCode player2DashKeyCode = KeyCode.Joystick1Button1;
    [SerializeField] private KeyCode player2RunKeyCode = KeyCode.Joystick1Button5;
    [SerializeField] private KeyCode player2InteractKeyCode = KeyCode.Joystick1Button3;
    [Header("Player 3 KeyCodes")]
    [SerializeField, Tooltip("If false, primary keycode is ignored so trigger/aim-only can be used.")] private bool player3UsePrimaryKeycode = true;
    [SerializeField] private KeyCode player3PrimaryKeyCode = KeyCode.Joystick2Button12;
    [SerializeField] private KeyCode player3JumpKeyCode = KeyCode.Joystick2Button0;
    [SerializeField] private KeyCode player3DashKeyCode = KeyCode.Joystick2Button1;
    [SerializeField] private KeyCode player3RunKeyCode = KeyCode.Joystick2Button5;
    [SerializeField] private KeyCode player3InteractKeyCode = KeyCode.Joystick2Button3;
    [Header("Gamepad Assignment")]
    [SerializeField, Tooltip("If true, a single gamepad controls either player 2 or player 3 (toggle below).")] private bool shareSingleGamepadBetweenPlayer2And3 = true;
    [SerializeField, Tooltip("When sharing a single gamepad, select which player receives input.")] private SharedGamepadTarget sharedGamepadTarget = SharedGamepadTarget.Player2;
    [SerializeField, Tooltip("If true, player 3 uses player 2 input bindings (useful when sharing one controller).")] private bool player3UsePlayer2Bindings = true;

    [Header("Spawning")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    [SerializeField, Tooltip("Minimum distance between the two player spawns.")] private float minSpawnSeparation = 25f;
    [SerializeField, Tooltip("Radius for NavMesh sampling near spawn points.")] private float navMeshSampleRadius = 6f;

    [Header("Round Flow")]
    [SerializeField, Tooltip("Seconds to wait after a kill before respawning and swapping roles.")] private float respawnDelay = 1.5f;

    private GameObject _player1Instance;
    private GameObject _player2Instance;
    private GameObject _player3Instance;
    private CharacterHealth _player1Health;
    private CharacterHealth _player2Health;
    private CharacterHealth _player3Health;
    private bool _hunterIsPlayer1 = true;
    private bool _respawnInProgress;
    private int _player1Score;
    private int _player2Score;
    private int _player3Score;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Debug.LogWarning("[LocalVersusGameManager] Multiple instances detected; using the latest.", this);
        }
        Instance = this;
        ActivateDisplays();
        if (npcDirector)
        {
            npcDirector.EnableDecoysOnlyMode();
        }
        AutoAssignCompasses();
        ResolveGameplayTuning();
    }

    private void Start()
    {
        SpawnOrRespawnPlayers(initialSpawn: true);
        UpdateRoleIndicators();
        UpdateCompasses();
        UpdateStunBindings();
        UpdateSmokeBindings();
        UpdateScoreboards();
    }

    private void OnDestroy()
    {
        UnsubscribeHealth(_player1Health);
        UnsubscribeHealth(_player2Health);
        UnsubscribeHealth(_player3Health);
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void ActivateDisplays()
    {
        for (int i = 1; i < Display.displays.Length && i <= 2; i++)
        {
            Display.displays[i].Activate();
        }
    }

    private void ResolveGameplayTuning()
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

    private void SpawnOrRespawnPlayers(bool initialSpawn)
    {
        bool usePlayer3 = player3Prefab;
        Vector3 p1;
        Vector3 p2;
        Vector3 p3 = Vector3.zero;
        Quaternion r1;
        Quaternion r2;
        Quaternion r3 = Quaternion.identity;

        if (usePlayer3)
        {
            if (!TryPickSpawnTriple(out p1, out p2, out p3, out r1, out r2, out r3))
            {
                Debug.LogWarning("[LocalVersusGameManager] Failed to find three spawn points; using origin offsets.");
                p1 = Vector3.zero;
                p2 = p1 + new Vector3(minSpawnSeparation, 0f, 0f);
                p3 = p2 + new Vector3(minSpawnSeparation, 0f, 0f);
                r1 = r2 = r3 = Quaternion.identity;
            }
        }
        else
        {
            if (!TryPickSpawnPair(out p1, out p2, out r1, out r2))
            {
                Debug.LogWarning("[LocalVersusGameManager] Failed to find two spawn points; using origin offsets.");
                p1 = Vector3.zero;
                p2 = p1 + new Vector3(minSpawnSeparation, 0f, 0f);
                r1 = r2 = Quaternion.identity;
            }
        }

        _player1Instance = SpawnPlayer(player1Prefab, _player1Instance, p1, r1, player1Camera, PlayerSlot.Player1, ref _player1Health);
        _player2Instance = SpawnPlayer(player2Prefab, _player2Instance, p2, r2, player2Camera, PlayerSlot.Player2, ref _player2Health);
        if (usePlayer3)
        {
            _player3Instance = SpawnPlayer(player3Prefab, _player3Instance, p3, r3, player3Camera, PlayerSlot.Player3, ref _player3Health);
        }
        else
        {
            _player3Instance = null;
            _player3Health = null;
        }

        if (initialSpawn && !usePlayer3)
        {
            _hunterIsPlayer1 = true;
        }

        UpdateFogBindings();
        UpdateRevealBindings();
        UpdatePlayerOnlyVisuals();
        UpdateInputAssignments();
    }

    private GameObject SpawnPlayer(GameObject prefab, GameObject existing, Vector3 position, Quaternion rotation, Camera camera, PlayerSlot slot, ref CharacterHealth cachedHealth)
    {
        if (!prefab)
        {
            return existing;
        }

        GameObject instance = existing;
        if (!instance)
        {
            instance = Instantiate(prefab, position, rotation);
        }
        else
        {
            instance.transform.SetPositionAndRotation(position, rotation);
        }

        ConfigureRespawnHandler(instance);

        SimpleCharacterController controller = instance.GetComponent<SimpleCharacterController>() ?? instance.GetComponentInChildren<SimpleCharacterController>(true);
        if (controller)
        {
            controller.SetCamera(camera);
        }

        PlayerInputRouter inputRouter = EnsureInputRouter(instance, slot);
        if (inputRouter)
        {
            inputRouter.SetInputCamera(camera);
            ConfigureInputRouter(inputRouter, slot);
            if (controller)
            {
                controller.SetInputRouter(inputRouter);
            }
        }

        EnsureVisionSource(instance);

        AssignCameraTarget(camera, instance.transform);

        cachedHealth = instance.GetComponent<CharacterHealth>() ?? instance.GetComponentInChildren<CharacterHealth>(true);
        if (cachedHealth)
        {
            cachedHealth.Revive();
            SubscribeHealth(cachedHealth);
        }

        return instance;
    }

    private void AssignCameraTarget(Camera camera, Transform target)
    {
        if (!camera)
        {
            return;
        }

        CameraService service = camera.GetComponent<CameraService>();
        if (service)
        {
            service.SetTarget(target, true);
            return;
        }

        CameraController legacy = camera.GetComponent<CameraController>();
        if (legacy)
        {
            legacy.SetTarget(target);
        }
    }

    private bool TryPickSpawnPair(out Vector3 p1, out Vector3 p2, out Quaternion r1, out Quaternion r2)
    {
        p1 = p2 = Vector3.zero;
        r1 = r2 = Quaternion.identity;

        List<Transform> valid = new List<Transform>();
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (spawnPoints[i])
            {
                valid.Add(spawnPoints[i]);
            }
        }

        if (valid.Count == 0)
        {
            return false;
        }

        float minSqr = minSpawnSeparation * minSpawnSeparation;
        Vector3 bestA = valid[0].position;
        Vector3 bestB = valid.Count > 1 ? valid[1].position : bestA + new Vector3(minSpawnSeparation, 0f, 0f);
        Quaternion bestRA = valid[0].rotation;
        Quaternion bestRB = valid.Count > 1 ? valid[1].rotation : Quaternion.identity;
        float bestDist = (bestA - bestB).sqrMagnitude;

        for (int attempt = 0; attempt < 32; attempt++)
        {
            Transform a = valid[Random.Range(0, valid.Count)];
            Transform b = valid[Random.Range(0, valid.Count)];
            if (valid.Count > 1 && b == a)
            {
                continue;
            }

            Vector3 sampleA = SampleNav(a.position);
            Vector3 sampleB = SampleNav(b.position);
            float dist = (sampleA - sampleB).sqrMagnitude;
            if (dist > bestDist)
            {
                bestDist = dist;
                bestA = sampleA;
                bestB = sampleB;
                bestRA = a.rotation;
                bestRB = b.rotation;
            }

            if (dist >= minSqr)
            {
                p1 = sampleA;
                p2 = sampleB;
                r1 = a.rotation;
                r2 = b.rotation;
                return true;
            }
        }

        p1 = bestA;
        p2 = bestB;
        r1 = bestRA;
        r2 = bestRB;
        return true;
    }

    private bool TryPickSpawnTriple(out Vector3 p1, out Vector3 p2, out Vector3 p3, out Quaternion r1, out Quaternion r2, out Quaternion r3)
    {
        p1 = p2 = p3 = Vector3.zero;
        r1 = r2 = r3 = Quaternion.identity;

        List<Transform> valid = new List<Transform>();
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (spawnPoints[i])
            {
                valid.Add(spawnPoints[i]);
            }
        }

        if (valid.Count < 3)
        {
            return false;
        }

        float minSqr = minSpawnSeparation * minSpawnSeparation;
        float bestScore = float.MinValue;
        Vector3 bestP1 = Vector3.zero;
        Vector3 bestP2 = Vector3.zero;
        Vector3 bestP3 = Vector3.zero;
        Quaternion bestR1 = Quaternion.identity;
        Quaternion bestR2 = Quaternion.identity;
        Quaternion bestR3 = Quaternion.identity;

        for (int i = 0; i < valid.Count - 2; i++)
        {
            Transform a = valid[i];
            Vector3 sampleA = SampleNav(a.position);
            for (int j = i + 1; j < valid.Count - 1; j++)
            {
                Transform b = valid[j];
                Vector3 sampleB = SampleNav(b.position);
                for (int k = j + 1; k < valid.Count; k++)
                {
                    Transform c = valid[k];
                    Vector3 sampleC = SampleNav(c.position);

                    float ab = (sampleA - sampleB).sqrMagnitude;
                    float ac = (sampleA - sampleC).sqrMagnitude;
                    float bc = (sampleB - sampleC).sqrMagnitude;
                    float minPair = Mathf.Min(ab, Mathf.Min(ac, bc));

                    if (minPair > bestScore)
                    {
                        bestScore = minPair;
                        bestP1 = sampleA;
                        bestP2 = sampleB;
                        bestP3 = sampleC;
                        bestR1 = a.rotation;
                        bestR2 = b.rotation;
                        bestR3 = c.rotation;
                    }

                    if (minPair >= minSqr)
                    {
                        p1 = sampleA;
                        p2 = sampleB;
                        p3 = sampleC;
                        r1 = a.rotation;
                        r2 = b.rotation;
                        r3 = c.rotation;
                        return true;
                    }
                }
            }
        }

        if (bestScore <= float.MinValue)
        {
            return false;
        }

        p1 = bestP1;
        p2 = bestP2;
        p3 = bestP3;
        r1 = bestR1;
        r2 = bestR2;
        r3 = bestR3;
        return true;
    }

    private Vector3 SampleNav(Vector3 origin)
    {
        if (NavMesh.SamplePosition(origin, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return origin;
    }

    private void SubscribeHealth(CharacterHealth health)
    {
        if (!health)
        {
            return;
        }

        health.Died -= OnPlayerDied;
        health.Died += OnPlayerDied;
    }

    private void UnsubscribeHealth(CharacterHealth health)
    {
        if (!health)
        {
            return;
        }

        health.Died -= OnPlayerDied;
    }

    private void OnPlayerDied(CharacterHealth dead)
    {
        if (_respawnInProgress || !dead)
        {
            return;
        }

        TryAwardScore(dead);
        ResetRevealCooldown(dead);
        StartCoroutine(HandleRespawnAndSwap(dead));
    }

    private void TryAwardScore(CharacterHealth dead)
    {
        if (!dead || scorePerTargetKill <= 0)
        {
            return;
        }

        if (!dead.TryGetLastDamage(out DamagePayload payload))
        {
            return;
        }

        CharacterHealth killerHealth = ResolveInstigatorHealth(payload);
        if (!killerHealth || killerHealth == dead)
        {
            return;
        }

        PlayerSlot? killerSlot = ResolvePlayerSlot(killerHealth);
        if (!killerSlot.HasValue)
        {
            return;
        }

        NpcIdentity deadIdentity = GetIdentity(dead.gameObject);
        if (!deadIdentity)
        {
            return;
        }

        NpcIdentity id1 = GetIdentity(_player1Instance);
        NpcIdentity id2 = GetIdentity(_player2Instance);
        NpcIdentity id3 = GetIdentity(_player3Instance);
        ResolveAssignedTargets(id1, id2, id3, out NpcIdentity target1, out NpcIdentity target2, out NpcIdentity target3);
        NpcIdentity assignedTarget = killerSlot.Value switch
        {
            PlayerSlot.Player1 => target1,
            PlayerSlot.Player2 => target2,
            PlayerSlot.Player3 => target3,
            _ => null
        };

        if (assignedTarget && assignedTarget == deadIdentity)
        {
            AddScore(killerSlot.Value, scorePerTargetKill);
            UpdateScoreboards();
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

    private void AddScore(PlayerSlot slot, int amount)
    {
        switch (slot)
        {
            case PlayerSlot.Player1:
                _player1Score += amount;
                break;
            case PlayerSlot.Player2:
                _player2Score += amount;
                break;
            case PlayerSlot.Player3:
                _player3Score += amount;
                break;
        }
    }

    private void ResetRevealCooldown(CharacterHealth dead)
    {
        AbilityRunner ability = null;
        if (dead == _player1Health)
        {
            ability = GetAbility(_player1Instance);
        }
        else if (dead == _player2Health)
        {
            ability = GetAbility(_player2Instance);
        }
        else if (dead == _player3Health)
        {
            ability = GetAbility(_player3Instance);
        }
        else
        {
            ability = dead.GetComponentInParent<AbilityRunner>() ?? dead.GetComponentInChildren<AbilityRunner>(true);
        }

        if (ability)
        {
            ability.ResetState();
        }
    }

    private IEnumerator HandleRespawnAndSwap(CharacterHealth dead)
    {
        _respawnInProgress = true;
        if (respawnDelay > 0f)
        {
            yield return new WaitForSeconds(respawnDelay);
        }

        if (!_player3Instance)
        {
            _hunterIsPlayer1 = !_hunterIsPlayer1;
        }
        RespawnDeadPlayer(dead);
        UpdateRoleIndicators();
        UpdateCompasses();
        UpdateFogBindings();
        UpdateRevealBindings();
        UpdateStunBindings();
        UpdateSmokeBindings();
        _respawnInProgress = false;
    }

    private void ConfigureRespawnHandler(GameObject instance)
    {
        if (!instance)
        {
            return;
        }

        RespawnHandler respawn = instance.GetComponent<RespawnHandler>() ?? instance.GetComponentInChildren<RespawnHandler>(true);
        if (respawn)
        {
            respawn.SetAutoRespawn(false);
        }
    }

    private void RespawnDeadPlayer(CharacterHealth dead)
    {
        if (!dead)
        {
            return;
        }

        bool isPlayer1 = dead == _player1Health;
        bool isPlayer2 = dead == _player2Health;
        bool isPlayer3 = dead == _player3Health;
        if (!isPlayer1 && !isPlayer2 && !isPlayer3)
        {
            return;
        }

        List<Transform> avoidTransforms = new List<Transform>(2);
        if (isPlayer1)
        {
            if (_player2Instance)
            {
                avoidTransforms.Add(_player2Instance.transform);
            }
            if (_player3Instance)
            {
                avoidTransforms.Add(_player3Instance.transform);
            }
        }
        else if (isPlayer2)
        {
            if (_player1Instance)
            {
                avoidTransforms.Add(_player1Instance.transform);
            }
            if (_player3Instance)
            {
                avoidTransforms.Add(_player3Instance.transform);
            }
        }
        else if (isPlayer3)
        {
            if (_player1Instance)
            {
                avoidTransforms.Add(_player1Instance.transform);
            }
            if (_player2Instance)
            {
                avoidTransforms.Add(_player2Instance.transform);
            }
        }

        if (!TryPickRespawnPoint(avoidTransforms, out Vector3 position, out Quaternion rotation))
        {
            Vector3 fallback = avoidTransforms.Count > 0 && avoidTransforms[0] ? avoidTransforms[0].position : Vector3.zero;
            position = fallback + new Vector3(minSpawnSeparation, 0f, 0f);
            rotation = Quaternion.identity;
        }

        if (isPlayer1)
        {
            _player1Instance = SpawnPlayer(player1Prefab, _player1Instance, position, rotation, player1Camera, PlayerSlot.Player1, ref _player1Health);
            ForcePlayerRespawn(_player1Instance, position, rotation);
        }
        else if (isPlayer2)
        {
            _player2Instance = SpawnPlayer(player2Prefab, _player2Instance, position, rotation, player2Camera, PlayerSlot.Player2, ref _player2Health);
            ForcePlayerRespawn(_player2Instance, position, rotation);
        }
        else
        {
            _player3Instance = SpawnPlayer(player3Prefab, _player3Instance, position, rotation, player3Camera, PlayerSlot.Player3, ref _player3Health);
            ForcePlayerRespawn(_player3Instance, position, rotation);
        }

        UpdatePlayerOnlyVisuals();
        UpdateInputAssignments();
    }

    private bool TryPickRespawnPoint(List<Transform> avoidTransforms, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        List<Transform> valid = new List<Transform>();
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (spawnPoints[i])
            {
                valid.Add(spawnPoints[i]);
            }
        }

        if (valid.Count == 0)
        {
            return false;
        }

        bool hasAvoids = avoidTransforms != null && avoidTransforms.Count > 0;
        float bestDist = float.MinValue;
        Transform best = null;
        Vector3 bestPos = Vector3.zero;
        Quaternion bestRot = Quaternion.identity;
        for (int i = 0; i < valid.Count; i++)
        {
            Transform candidate = valid[i];
            Vector3 sampled = SampleNav(candidate.position);
            float dist = 0f;
            if (hasAvoids)
            {
                float closest = float.MaxValue;
                for (int j = 0; j < avoidTransforms.Count; j++)
                {
                    Transform avoid = avoidTransforms[j];
                    if (!avoid)
                    {
                        continue;
                    }
                    Vector3 avoidSample = SampleNav(avoid.position);
                    float sqr = (sampled - avoidSample).sqrMagnitude;
                    if (sqr < closest)
                    {
                        closest = sqr;
                    }
                }
                dist = closest == float.MaxValue ? 0f : closest;
            }
            if (best == null || dist > bestDist)
            {
                best = candidate;
                bestDist = dist;
                bestPos = sampled;
                bestRot = candidate.rotation;
            }
        }

        if (!best)
        {
            return false;
        }

        position = bestPos;
        rotation = bestRot;
        return true;
    }

    private void ForcePlayerRespawn(GameObject instance, Vector3 position, Quaternion rotation)
    {
        if (!instance)
        {
            return;
        }

        RespawnHandler respawn = instance.GetComponent<RespawnHandler>() ?? instance.GetComponentInChildren<RespawnHandler>(true);
        if (respawn)
        {
            respawn.ForceRespawnAt(position, rotation);
        }
    }

    private void UpdateRoleIndicators()
    {
        NpcIdentity id1 = GetIdentity(_player1Instance);
        NpcIdentity id2 = GetIdentity(_player2Instance);
        NpcIdentity id3 = GetIdentity(_player3Instance);

        if (_player3Instance)
        {
            if (id1)
            {
                id1.SetTarget(true);
            }

            if (id2)
            {
                id2.SetTarget(true);
            }

            if (id3)
            {
                id3.SetTarget(true);
            }
            return;
        }

        if (id1)
        {
            id1.SetTarget(!_hunterIsPlayer1);
        }

        if (id2)
        {
            id2.SetTarget(_hunterIsPlayer1);
        }
    }

    private void UpdateCompasses()
    {
        NpcIdentity id1 = GetIdentity(_player1Instance);
        NpcIdentity id2 = GetIdentity(_player2Instance);
        NpcIdentity id3 = GetIdentity(_player3Instance);
        VisionSource vision1 = GetVision(_player1Instance);
        VisionSource vision2 = GetVision(_player2Instance);
        VisionSource vision3 = GetVision(_player3Instance);
        AbilityRunner ability1 = GetAbility(_player1Instance);
        AbilityRunner ability2 = GetAbility(_player2Instance);
        AbilityRunner ability3 = GetAbility(_player3Instance);
        ResolveAssignedTargets(id1, id2, id3, out NpcIdentity target1, out NpcIdentity target2, out NpcIdentity target3);
        ConfigureRevealIndicators(_player1Instance, player1Compass, target1, vision1, ability1, player1Camera, player1Fog);
        ConfigureRevealIndicators(_player2Instance, player2Compass, target2, vision2, ability2, player2Camera, player2Fog);
        ConfigureRevealIndicators(_player3Instance, player3Compass, target3, vision3, ability3, player3Camera, player3Fog);

        ApplyRevealTuning(ability1, player1Compass);
        ApplyRevealTuning(ability2, player2Compass);
        ApplyRevealTuning(ability3, player3Compass);
        UpdateMinimaps(target1, target2, target3);
        UpdateTargetImages(id1, id2, id3, target1, target2, target3);
    }

    private void ResolveAssignedTargets(NpcIdentity id1, NpcIdentity id2, NpcIdentity id3,
        out NpcIdentity target1, out NpcIdentity target2, out NpcIdentity target3)
    {
        bool hasPlayer3 = _player3Instance != null;
        target1 = id2;
        target2 = hasPlayer3 ? (id3 ? id3 : id1) : id1;
        target3 = hasPlayer3 ? id1 : null;
    }

    private void ResolveKillTargets(NpcIdentity id1, NpcIdentity id2, NpcIdentity id3,
        out NpcIdentity target1, out NpcIdentity target2, out NpcIdentity target3)
    {
        bool hasPlayer3 = _player3Instance != null;
        if (hasPlayer3)
        {
            target1 = id2;
            target2 = id3 ? id3 : id1;
            target3 = id1;
            return;
        }

        if (_hunterIsPlayer1)
        {
            target1 = id2;
            target2 = null;
        }
        else
        {
            target1 = null;
            target2 = id1;
        }
        target3 = null;
    }

    public bool IsPlayerHealth(CharacterHealth health)
    {
        return health && (health == _player1Health || health == _player2Health || health == _player3Health);
    }

    public bool CanKillPlayer(CharacterHealth attacker, CharacterHealth victim)
    {
        if (!attacker || !victim)
        {
            return true;
        }

        PlayerSlot? attackerSlot = ResolvePlayerSlot(attacker);
        PlayerSlot? victimSlot = ResolvePlayerSlot(victim);
        if (!attackerSlot.HasValue || !victimSlot.HasValue)
        {
            return true;
        }

        NpcIdentity id1 = GetIdentity(_player1Instance);
        NpcIdentity id2 = GetIdentity(_player2Instance);
        NpcIdentity id3 = GetIdentity(_player3Instance);
        ResolveKillTargets(id1, id2, id3, out NpcIdentity target1, out NpcIdentity target2, out NpcIdentity target3);
        NpcIdentity victimId = GetIdentity(victim.gameObject);
        NpcIdentity allowedTarget = attackerSlot.Value switch
        {
            PlayerSlot.Player1 => target1,
            PlayerSlot.Player2 => target2,
            PlayerSlot.Player3 => target3,
            _ => null
        };

        return allowedTarget != null && allowedTarget == victimId;
    }

    private void ConfigureRevealIndicators(GameObject playerInstance, RevealIndicatorController primaryCompass, NpcIdentity target,
        VisionSource vision, AbilityRunner ability, Camera camera, FogOfWarManager fog)
    {
        Transform playerTransform = playerInstance ? playerInstance.transform : null;
        ConfigureRevealIndicator(primaryCompass, playerTransform, vision, ability, camera, fog, target);

        if (!playerInstance)
        {
            return;
        }

        RevealIndicatorController[] indicators = playerInstance.GetComponentsInChildren<RevealIndicatorController>(true);
        if (indicators == null || indicators.Length == 0)
        {
            return;
        }

        for (int i = 0; i < indicators.Length; i++)
        {
            RevealIndicatorController indicator = indicators[i];
            if (!indicator || indicator == primaryCompass)
            {
                continue;
            }

            ConfigureRevealIndicator(indicator, playerTransform, vision, ability, camera, fog, target);
        }
    }

    private void ConfigureRevealIndicator(RevealIndicatorController indicator, Transform playerTransform, VisionSource vision, AbilityRunner ability,
        Camera camera, FogOfWarManager fog, NpcIdentity target)
    {
        if (!indicator)
        {
            return;
        }

        indicator.ConfigurePlayer(playerTransform, vision, ability, camera);
        indicator.SetAlwaysShowWhenTargetSet(false);
        indicator.SetTarget(target);
        if (fog)
        {
            indicator.SetFogManager(fog);
        }
        ApplyRevealFade(indicator);
    }

    private void ApplyRevealFade(RevealIndicatorController indicator)
    {
        if (!indicator)
        {
            return;
        }

        if (!gameplayTuning)
        {
            ResolveGameplayTuning();
            if (!gameplayTuning)
            {
                return;
            }
        }

        indicator.ApplyFadeConfig(gameplayTuning.revealFade, gameplayTuning.revealFade);
    }

    private void UpdateMinimaps(NpcIdentity player1Target, NpcIdentity player2Target, NpcIdentity player3Target)
    {
        if (!player1Minimap && player1Ui)
        {
            player1Minimap = player1Ui.GetComponentInChildren<MinimapController>(true);
        }

        if (!player2Minimap && player2Ui)
        {
            player2Minimap = player2Ui.GetComponentInChildren<MinimapController>(true);
        }

        if (!player3Minimap && player3Ui)
        {
            player3Minimap = player3Ui.GetComponentInChildren<MinimapController>(true);
        }

        if (player1Minimap)
        {
            player1Minimap.SetOwner(GetIdentity(_player1Instance));
            player1Minimap.SetTarget(player1Target);
        }

        if (player2Minimap)
        {
            player2Minimap.SetOwner(GetIdentity(_player2Instance));
            player2Minimap.SetTarget(player2Target);
        }

        if (player3Minimap)
        {
            player3Minimap.SetOwner(GetIdentity(_player3Instance));
            player3Minimap.SetTarget(player3Target);
        }
    }

    private void UpdateTargetImages(NpcIdentity id1, NpcIdentity id2, NpcIdentity id3,
        NpcIdentity player1Target, NpcIdentity player2Target, NpcIdentity player3Target)
    {
        UpdateTargetImage(player1Ui, ResolveTargetPrefab(id1, id2, id3, player1Target));
        UpdateTargetImage(player2Ui, ResolveTargetPrefab(id1, id2, id3, player2Target));
        UpdateTargetImage(player3Ui, ResolveTargetPrefab(id1, id2, id3, player3Target));
    }

    private GameObject ResolveTargetPrefab(NpcIdentity id1, NpcIdentity id2, NpcIdentity id3, NpcIdentity target)
    {
        if (!target)
        {
            return null;
        }

        if (target == id1)
        {
            return targetImageDarkPrefab;
        }

        if (target == id2)
        {
            return targetImageGreenPrefab;
        }

        if (target == id3)
        {
            return targetImagePurplePrefab;
        }

        return null;
    }

    private void UpdateTargetImage(GameUiManager ui, GameObject prefab)
    {
        if (ui)
        {
            ui.SetTargetImagePrefab(prefab);
        }
    }

    private void UpdateScoreboards()
    {
        ResolveScoreboards();
        bool updatedAny = false;
        updatedAny |= UpdateScoreboard(player1Scoreboard);
        updatedAny |= UpdateScoreboard(player2Scoreboard);
        updatedAny |= UpdateScoreboard(player3Scoreboard);

        if (updatedAny)
        {
            return;
        }

        ScoreboardController[] scoreboards = FindObjectsByType<ScoreboardController>(FindObjectsSortMode.None);
        if (scoreboards == null || scoreboards.Length == 0)
        {
            return;
        }

        for (int i = 0; i < scoreboards.Length; i++)
        {
            UpdateScoreboard(scoreboards[i]);
        }
    }

    private void ResolveScoreboards()
    {
        if (!player1Scoreboard && player1Ui)
        {
            player1Scoreboard = player1Ui.GetComponentInChildren<ScoreboardController>(true);
        }

        if (!player2Scoreboard && player2Ui)
        {
            player2Scoreboard = player2Ui.GetComponentInChildren<ScoreboardController>(true);
        }

        if (!player3Scoreboard && player3Ui)
        {
            player3Scoreboard = player3Ui.GetComponentInChildren<ScoreboardController>(true);
        }
    }

    private bool UpdateScoreboard(ScoreboardController scoreboard)
    {
        if (!scoreboard)
        {
            return false;
        }

        scoreboard.SetScores(_player1Score, _player2Score, _player3Score);
        return true;
    }

    private void ApplyRevealTuning(AbilityRunner ability, RevealIndicatorController indicator)
    {
        if (!gameplayTuning)
        {
            ResolveGameplayTuning();
            if (!gameplayTuning)
            {
                return;
            }
        }

        if (ability)
        {
            ability.ApplyOverrides(gameplayTuning.revealCooldown, gameplayTuning.revealHold, gameplayTuning.revealFade);
        }

        if (indicator)
        {
            indicator.ApplyFadeConfig(gameplayTuning.revealFade, gameplayTuning.revealFade);
        }
    }


    private NpcIdentity GetIdentity(GameObject root)
    {
        return root ? root.GetComponent<NpcIdentity>() ?? root.GetComponentInChildren<NpcIdentity>(true) : null;
    }

    private VisionSource GetVision(GameObject root)
    {
        return root ? root.GetComponent<VisionSource>() ?? root.GetComponentInChildren<VisionSource>(true) : null;
    }

    private AbilityRunner GetAbility(GameObject root)
    {
        return root ? root.GetComponent<AbilityRunner>() ?? root.GetComponentInChildren<AbilityRunner>(true) : null;
    }


    private void UpdateRevealBindings()
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

    private void UpdateSmokeBindings()
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

        SmokeAbility smoke1 = GetSmokeAbility(_player1Instance, addIfMissing: true);
        SmokeAbility smoke2 = GetSmokeAbility(_player2Instance, addIfMissing: true);
        SmokeAbility smoke3 = GetSmokeAbility(_player3Instance, addIfMissing: true);
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

        if (smoke2)
        {
            smoke2.SetCooldown(cooldown);
            smoke2.SetOverrideKey(player2SmokeKey);
            smoke2.SetInputEnabled(enableSmoke2);
        }

        if (smoke3)
        {
            smoke3.SetCooldown(cooldown);
            smoke3.SetOverrideKey(smoke3Key);
            smoke3.SetInputEnabled(enableSmoke3);
        }

        if (player1Ui)
        {
            player1Ui.SetSmokeAbility(smoke1);
        }

        if (player2Ui)
        {
            player2Ui.SetSmokeAbility(smoke2);
        }

        if (player3Ui)
        {
            player3Ui.SetSmokeAbility(smoke3);
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

    private void UpdateStunBindings()
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

    private void UpdateFogBindings()
    {
        TryAutoAssignFogManagers();

        VisionSource vision1 = GetVision(_player1Instance);
        VisionSource vision2 = GetVision(_player2Instance);
        VisionSource vision3 = GetVision(_player3Instance);

        if (player1Fog)
        {
            player1Fog.autoFindVisionSources = false;
            player1Fog.visionSources.Clear();
            if (vision1)
            {
                player1Fog.visionSources.Add(vision1);
            }
            Debug.Log($"[LocalVersusGameManager] Bound player1 fog to vision {(vision1 ? vision1.name : "null")} worldMin={player1Fog.worldMin} worldMax={player1Fog.worldMax}", player1Fog);
        }

        if (player2Fog)
        {
            player2Fog.autoFindVisionSources = false;
            player2Fog.visionSources.Clear();
            if (vision2)
            {
                player2Fog.visionSources.Add(vision2);
            }
            Debug.Log($"[LocalVersusGameManager] Bound player2 fog to vision {(vision2 ? vision2.name : "null")} worldMin={player2Fog.worldMin} worldMax={player2Fog.worldMax}", player2Fog);
        }

        if (player3Fog)
        {
            player3Fog.autoFindVisionSources = false;
            player3Fog.visionSources.Clear();
            if (vision3)
            {
                player3Fog.visionSources.Add(vision3);
            }
            Debug.Log($"[LocalVersusGameManager] Bound player3 fog to vision {(vision3 ? vision3.name : "null")} worldMin={player3Fog.worldMin} worldMax={player3Fog.worldMax}", player3Fog);
        }

        BindCameraToFog(player1Camera, player1Fog);
        BindCameraToFog(player2Camera, player2Fog);
        BindCameraToFog(player3Camera, player3Fog);

    }

    private void TryAutoAssignFogManagers()
    {
        if (player1Fog && player2Fog && player3Fog)
        {
            return;
        }

        FogOfWarManager[] fogs = FindObjectsByType<FogOfWarManager>(FindObjectsSortMode.None);
        if (fogs == null || fogs.Length == 0)
        {
            Debug.LogWarning("[LocalVersusGameManager] No FogOfWarManager instances found.", this);
            return;
        }

        if (!player1Fog)
        {
            player1Fog = fogs[0];
            Debug.Log($"[LocalVersusGameManager] Auto-assigned player1Fog to {player1Fog.name}", this);
        }

        if (!player2Fog)
        {
            player2Fog = fogs.Length > 1 ? fogs[1] : fogs[0];
            Debug.Log($"[LocalVersusGameManager] Auto-assigned player2Fog to {player2Fog.name}", this);
        }

        if (!player3Fog)
        {
            player3Fog = fogs.Length > 2 ? fogs[2] : (fogs.Length > 1 ? fogs[1] : fogs[0]);
            Debug.Log($"[LocalVersusGameManager] Auto-assigned player3Fog to {player3Fog.name}", this);
        }
    }

    private void BindCameraToFog(Camera cam, FogOfWarManager fog)
    {
        if (!cam || !fog)
        {
            return;
        }

        FogOfWarCameraBinder binder = cam.GetComponent<FogOfWarCameraBinder>();
        if (!binder)
        {
            binder = cam.gameObject.AddComponent<FogOfWarCameraBinder>();
        }
        binder.SetFogManager(fog);
        Debug.Log($"[LocalVersusGameManager] Bound camera {cam.name} to fog {fog.name}", cam);
    }

    private void ConfigureInputRouter(PlayerInputRouter router, PlayerSlot slot)
    {
        if (!router)
        {
            return;
        }

        if (slot == PlayerSlot.Player1)
        {
            router.SetAxes(player1HorizontalAxis, player1VerticalAxis);
            router.SetKeyboardOnlyMovement(true);
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
        gamepad.SetButtonKeyCodes(primary, jump, dash, run, interact);
    }

    private void UpdateInputAssignments()
    {
        AbilityRunner ability2 = GetAbility(_player2Instance);
        AbilityRunner ability3 = GetAbility(_player3Instance);
        SmokeAbility smoke2 = GetSmokeAbility(_player2Instance, addIfMissing: false);
        SmokeAbility smoke3 = GetSmokeAbility(_player3Instance, addIfMissing: false);

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
        }
        else
        {
            SetInputEnabledForInstance(_player2Instance, PlayerSlot.Player2, true);
            SetInputEnabledForInstance(_player3Instance, PlayerSlot.Player3, true);
            SetAbilityInputEnabled(ability2, true);
            SetAbilityInputEnabled(ability3, true);
            SetSmokeInputEnabled(smoke2, true);
            SetSmokeInputEnabled(smoke3, true);
        }
    }

    private void SetInputEnabledForInstance(GameObject instance, PlayerSlot slot, bool enabled)
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

        PlayerInputRouter primary = FindPreferredRouter(routers, slot);
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

    private static void SetAbilityInputEnabled(AbilityRunner ability, bool enabled)
    {
        if (ability)
        {
            ability.SetInputEnabled(enabled);
        }
    }

    private static void SetSmokeInputEnabled(SmokeAbility ability, bool enabled)
    {
        if (ability)
        {
            ability.SetInputEnabled(enabled);
        }
    }

    private static PlayerInputRouter FindPreferredRouter(PlayerInputRouter[] routers, PlayerSlot slot)
    {
        if (routers == null || routers.Length == 0)
        {
            return null;
        }

        if (slot == PlayerSlot.Player1)
        {
            for (int i = 0; i < routers.Length; i++)
            {
                if (routers[i] && routers[i] is not PlayerInputRouterGamepad)
                {
                    return routers[i];
                }
            }
        }
        else
        {
            for (int i = 0; i < routers.Length; i++)
            {
                if (routers[i] && routers[i] is PlayerInputRouterGamepad)
                {
                    return routers[i];
                }
            }
        }

        for (int i = 0; i < routers.Length; i++)
        {
            if (routers[i])
            {
                return routers[i];
            }
        }

        return null;
    }

    private PlayerInputRouter EnsureInputRouter(GameObject instance, PlayerSlot slot)
    {
        if (!instance)
        {
            return null;
        }

        PlayerInputRouter router = instance.GetComponent<PlayerInputRouter>() ?? instance.GetComponentInChildren<PlayerInputRouter>(true);
        if (!router)
        {
            router = slot == PlayerSlot.Player1
                ? instance.AddComponent<PlayerInputRouter>()
                : (PlayerInputRouter)instance.AddComponent<PlayerInputRouterGamepad>();
        }
        else if (slot == PlayerSlot.Player1 && router is PlayerInputRouterGamepad)
        {
            router = instance.AddComponent<PlayerInputRouter>();
        }
        else if (slot != PlayerSlot.Player1 && router is not PlayerInputRouterGamepad)
        {
            router = instance.AddComponent<PlayerInputRouterGamepad>();
        }

        return router;
    }

    private void AutoAssignCompasses()
    {
        if (player1Compass && player2Compass && player3Compass)
        {
            return;
        }

        RevealIndicatorController[] indicators = FindObjectsByType<RevealIndicatorController>(FindObjectsSortMode.None);
        if (indicators == null || indicators.Length == 0)
        {
            return;
        }

        // Prefer by camera target display if possible.
        for (int i = 0; i < indicators.Length; i++)
        {
            RevealIndicatorController c = indicators[i];
            if (!c)
            {
                continue;
            }

            Camera cam = c ? c.GetComponentInParent<Camera>() ?? c.GetComponent<Camera>() : null;
            int display = cam ? cam.targetDisplay : 0;
            if (!player1Compass && player1Camera && display == player1Camera.targetDisplay)
            {
                player1Compass = c;
                continue;
            }
            if (!player2Compass && player2Camera && display == player2Camera.targetDisplay)
            {
                player2Compass = c;
                continue;
            }
            if (!player3Compass && player3Camera && display == player3Camera.targetDisplay)
            {
                player3Compass = c;
            }
        }

        // Fallback: just grab the first three distinct ones.
        for (int i = 0; i < indicators.Length && (!player1Compass || !player2Compass || !player3Compass); i++)
        {
            RevealIndicatorController c = indicators[i];
            if (!c)
            {
                continue;
            }

            if (!player1Compass)
            {
                player1Compass = c;
                continue;
            }

            if (!player2Compass && c != player1Compass)
            {
                player2Compass = c;
                continue;
            }

            if (!player3Compass && c != player1Compass && c != player2Compass)
            {
                player3Compass = c;
            }
        }
    }

    private void EnsureVisionSource(GameObject root)
    {
        if (!root)
        {
            return;
        }

        VisionSource vision = GetVision(root);
        if (!vision)
        {
            vision = root.AddComponent<VisionSource>();
            vision.baseRadius = 12f;
            vision.level1Radius = 18f;
            vision.level2Radius = 22f;
        }
    }

    private void UpdatePlayerOnlyVisuals()
    {
        int player1LayerId = LayerMask.NameToLayer(player1OnlyLayer);
        int player2LayerId = LayerMask.NameToLayer(player2OnlyLayer);
        bool usePlayer3 = _player3Instance || player3Prefab;
        int player3LayerId = usePlayer3 ? LayerMask.NameToLayer(player3OnlyLayer) : -1;
        if (player1LayerId < 0 || player2LayerId < 0 || (usePlayer3 && player3LayerId < 0))
        {
            Debug.LogWarning($"[LocalVersusGameManager] Missing layers for player-only visuals. Define '{player1OnlyLayer}', '{player2OnlyLayer}'{(usePlayer3 ? $" and '{player3OnlyLayer}'" : string.Empty)} in TagManager.", this);
            return;
        }

        AssignPlayerOnlyLayer(_player1Instance, player1LayerId);
        AssignPlayerOnlyLayer(_player2Instance, player2LayerId);
        if (usePlayer3)
        {
            AssignPlayerOnlyLayer(_player3Instance, player3LayerId);
        }

        ApplyCameraCullingMask(player1Camera, player1LayerId, player2LayerId, player3LayerId);
        ApplyCameraCullingMask(player2Camera, player2LayerId, player1LayerId, player3LayerId);
        if (usePlayer3)
        {
            ApplyCameraCullingMask(player3Camera, player3LayerId, player1LayerId, player2LayerId);
        }
    }

    private void AssignPlayerOnlyLayer(GameObject root, int layer)
    {
        if (!root || playerOnlyObjectNames == null || playerOnlyObjectNames.Length == 0)
        {
            return;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (!t)
            {
                continue;
            }

            if (IsPlayerOnlyObjectName(t.name))
            {
                SetLayerRecursively(t, layer);
            }
        }
    }

    private bool IsPlayerOnlyObjectName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        for (int i = 0; i < playerOnlyObjectNames.Length; i++)
        {
            if (objectName == playerOnlyObjectNames[i])
            {
                return true;
            }
        }

        return false;
    }

    private void SetLayerRecursively(Transform root, int layer)
    {
        if (!root)
        {
            return;
        }

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private void ApplyCameraCullingMask(Camera cam, int includeLayer, params int[] excludeLayers)
    {
        if (!cam)
        {
            return;
        }

        int mask = cam.cullingMask;
        mask |= 1 << includeLayer;
        if (excludeLayers != null)
        {
            for (int i = 0; i < excludeLayers.Length; i++)
            {
                int exclude = excludeLayers[i];
                if (exclude >= 0)
                {
                    mask &= ~(1 << exclude);
                }
            }
        }
        cam.cullingMask = mask;
    }
}
