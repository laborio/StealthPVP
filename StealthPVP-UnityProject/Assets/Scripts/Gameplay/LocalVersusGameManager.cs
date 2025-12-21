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
    [Header("Players")]
    [SerializeField] private GameObject player1Prefab;
    [SerializeField, Tooltip("Player 2 prefab created in editor (duplicate of player1 with gamepad input router).")] private GameObject player2Prefab;
    [SerializeField] private Camera player1Camera;
    [SerializeField] private Camera player2Camera;
    [SerializeField, Tooltip("Compass UI for player 1 (points to hunted target).")] private RevealIndicatorController player1Compass;
    [SerializeField, Tooltip("Compass UI for player 2 (points to hunted target).")] private RevealIndicatorController player2Compass;
    [SerializeField, Tooltip("Optional decoy spawner; will be forced to decoys-only.")] private NpcGameDirector npcDirector;
    [Header("Fog Of War (optional per-player)")]
    [SerializeField] private FogOfWarManager player1Fog;
    [SerializeField] private FogOfWarManager player2Fog;
    [Header("Player-Only Visuals")]
    [SerializeField, Tooltip("Layer name for player 1-only visuals.")] private string player1OnlyLayer = "Player1Only";
    [SerializeField, Tooltip("Layer name for player 2-only visuals.")] private string player2OnlyLayer = "Player2Only";
    [SerializeField, Tooltip("Child object names to restrict to the owning player camera.")] private string[] playerOnlyObjectNames = { "PlayerCompass", "T_ClickArea", "ClickArea", "WSCanvas", "RangeIndicator" };
    [Header("UI/Reveal")]
    [SerializeField] private GameUiManager player1Ui;
    [SerializeField] private GameUiManager player2Ui;
    [SerializeField, Tooltip("Reveal key for player 1 (keyboard/mouse).")] private KeyCode player1RevealKey = KeyCode.F;
    [SerializeField, Tooltip("Reveal key for player 2 (gamepad).")] private KeyCode player2RevealKey = KeyCode.JoystickButton4;
    [Header("Input Axes")]
    [SerializeField, Tooltip("Keyboard-only horizontal axis name for player 1.")] private string player1HorizontalAxis = "Horizontal";
    [SerializeField, Tooltip("Keyboard-only vertical axis name for player 1.")] private string player1VerticalAxis = "Vertical";
    [SerializeField, Tooltip("Gamepad horizontal axis for player 2.")] private string player2MoveHorizontalAxis = "Horizontal2";
    [SerializeField, Tooltip("Gamepad vertical axis for player 2.")] private string player2MoveVerticalAxis = "Vertical2";
    [SerializeField, Tooltip("Gamepad aim horizontal axis for player 2.")] private string player2AimHorizontalAxis = "AimHorizontal2";
    [SerializeField, Tooltip("Gamepad aim vertical axis for player 2.")] private string player2AimVerticalAxis = "AimVertical2";
    [Header("Player 2 KeyCodes")]
    [SerializeField, Tooltip("If false, primary keycode is ignored so trigger/aim-only can be used.")] private bool player2UsePrimaryKeycode = true;
    [SerializeField] private KeyCode player2PrimaryKeyCode = KeyCode.JoystickButton12;
    [SerializeField] private KeyCode player2JumpKeyCode = KeyCode.Joystick1Button0;
    [SerializeField] private KeyCode player2DashKeyCode = KeyCode.Joystick1Button1;
    [SerializeField] private KeyCode player2RunKeyCode = KeyCode.Joystick1Button5;
    [SerializeField] private KeyCode player2InteractKeyCode = KeyCode.Joystick1Button3;

    [Header("Spawning")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    [SerializeField, Tooltip("Minimum distance between the two player spawns.")] private float minSpawnSeparation = 25f;
    [SerializeField, Tooltip("Radius for NavMesh sampling near spawn points.")] private float navMeshSampleRadius = 6f;

    [Header("Round Flow")]
    [SerializeField, Tooltip("Seconds to wait after a kill before respawning and swapping roles.")] private float respawnDelay = 1.5f;

    private GameObject _player1Instance;
    private GameObject _player2Instance;
    private CharacterHealth _player1Health;
    private CharacterHealth _player2Health;
    private bool _hunterIsPlayer1 = true;
    private bool _respawnInProgress;

    private void Awake()
    {
        ActivateSecondDisplay();
        if (npcDirector)
        {
            npcDirector.EnableDecoysOnlyMode();
        }
        AutoAssignCompasses();
    }

    private void Start()
    {
        SpawnOrRespawnPlayers(initialSpawn: true);
        UpdateRoleIndicators();
        UpdateCompasses();
    }

    private void OnDestroy()
    {
        UnsubscribeHealth(_player1Health);
        UnsubscribeHealth(_player2Health);
    }

    private void ActivateSecondDisplay()
    {
        if (Display.displays.Length > 1)
        {
            Display.displays[1].Activate();
        }
    }

    private void SpawnOrRespawnPlayers(bool initialSpawn)
    {
        if (!TryPickSpawnPair(out Vector3 p1, out Vector3 p2, out Quaternion r1, out Quaternion r2))
        {
            Debug.LogWarning("[LocalVersusGameManager] Failed to find two spawn points; using origin offsets.");
            p1 = Vector3.zero;
            p2 = p1 + new Vector3(minSpawnSeparation, 0f, 0f);
            r1 = r2 = Quaternion.identity;
        }

        _player1Instance = SpawnPlayer(player1Prefab, _player1Instance, p1, r1, player1Camera, ref _player1Health);
        _player2Instance = SpawnPlayer(player2Prefab, _player2Instance, p2, r2, player2Camera, ref _player2Health);

        if (initialSpawn)
        {
            _hunterIsPlayer1 = true;
        }

        UpdateFogBindings();
        UpdateRevealBindings();
        UpdatePlayerOnlyVisuals();
    }

    private GameObject SpawnPlayer(GameObject prefab, GameObject existing, Vector3 position, Quaternion rotation, Camera camera, ref CharacterHealth cachedHealth)
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

        SimpleCharacterController controller = instance.GetComponent<SimpleCharacterController>() ?? instance.GetComponentInChildren<SimpleCharacterController>(true);
        if (controller)
        {
            controller.SetCamera(camera);
        }

        PlayerInputRouter inputRouter = EnsureInputRouter(instance, camera == player1Camera);
        if (inputRouter)
        {
            inputRouter.SetInputCamera(camera);
            ConfigureInputRouter(inputRouter, camera == player1Camera);
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

        StartCoroutine(HandleRespawnAndSwap());
    }

    private IEnumerator HandleRespawnAndSwap()
    {
        _respawnInProgress = true;
        if (respawnDelay > 0f)
        {
            yield return new WaitForSeconds(respawnDelay);
        }

        _hunterIsPlayer1 = !_hunterIsPlayer1;
        SpawnOrRespawnPlayers(initialSpawn: false);
        UpdateRoleIndicators();
        UpdateCompasses();
        UpdateFogBindings();
        UpdateRevealBindings();
        _respawnInProgress = false;
    }

    private void UpdateRoleIndicators()
    {
        NpcIdentity id1 = GetIdentity(_player1Instance);
        NpcIdentity id2 = GetIdentity(_player2Instance);

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
        VisionSource vision1 = GetVision(_player1Instance);
        VisionSource vision2 = GetVision(_player2Instance);
        AbilityRunner ability1 = GetAbility(_player1Instance);
        AbilityRunner ability2 = GetAbility(_player2Instance);

        if (player1Compass)
        {
            player1Compass.ConfigurePlayer(_player1Instance ? _player1Instance.transform : null, vision1, ability1, player1Camera);
            player1Compass.SetAlwaysShowWhenTargetSet(false);
            player1Compass.SetTarget(id2);
            if (player1Fog)
            {
                player1Compass.SetFogManager(player1Fog);
            }
        }

        if (player2Compass)
        {
            player2Compass.ConfigurePlayer(_player2Instance ? _player2Instance.transform : null, vision2, ability2, player2Camera);
            player2Compass.SetAlwaysShowWhenTargetSet(false);
            player2Compass.SetTarget(id1);
            if (player2Fog)
            {
                player2Compass.SetFogManager(player2Fog);
            }
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

        if (player1Ui)
        {
            player1Ui.SetRevealAbility(ability1);
        }

        if (player2Ui)
        {
            player2Ui.SetRevealAbility(ability2);
        }
    }

    private void UpdateFogBindings()
    {
        TryAutoAssignFogManagers();

        VisionSource vision1 = GetVision(_player1Instance);
        VisionSource vision2 = GetVision(_player2Instance);

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

        BindCameraToFog(player1Camera, player1Fog);
        BindCameraToFog(player2Camera, player2Fog);

    }

    private void TryAutoAssignFogManagers()
    {
        if (player1Fog && player2Fog)
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

    private void ConfigureInputRouter(PlayerInputRouter router, bool isPlayer1)
    {
        if (!router)
        {
            return;
        }

        if (isPlayer1)
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

        gamepad.SetMoveAxes(player2MoveHorizontalAxis, player2MoveVerticalAxis);
        gamepad.SetAimAxes(player2AimHorizontalAxis, player2AimVerticalAxis);
        // Use keycodes only to avoid keyboard overlap.
        gamepad.SetButtonNames(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        KeyCode primary = player2UsePrimaryKeycode ? player2PrimaryKeyCode : KeyCode.None;
        gamepad.SetButtonKeyCodes(primary, player2JumpKeyCode, player2DashKeyCode, player2RunKeyCode, player2InteractKeyCode);
    }

    private PlayerInputRouter EnsureInputRouter(GameObject instance, bool isPlayer1)
    {
        if (!instance)
        {
            return null;
        }

        PlayerInputRouter router = instance.GetComponent<PlayerInputRouter>() ?? instance.GetComponentInChildren<PlayerInputRouter>(true);
        if (!router)
        {
            router = isPlayer1
                ? instance.AddComponent<PlayerInputRouter>()
                : (PlayerInputRouter)instance.AddComponent<PlayerInputRouterGamepad>();
        }
        else if (isPlayer1 && router is PlayerInputRouterGamepad)
        {
            router = instance.AddComponent<PlayerInputRouter>();
        }
        else if (!isPlayer1 && router is not PlayerInputRouterGamepad)
        {
            router = instance.AddComponent<PlayerInputRouterGamepad>();
        }

        return router;
    }

    private void AutoAssignCompasses()
    {
        if (player1Compass && player2Compass)
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
            }
        }

        // Fallback: just grab the first two distinct ones.
        for (int i = 0; i < indicators.Length && (!player1Compass || !player2Compass); i++)
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
        if (player1LayerId < 0 || player2LayerId < 0)
        {
            Debug.LogWarning($"[LocalVersusGameManager] Missing layers for player-only visuals. Define '{player1OnlyLayer}' and '{player2OnlyLayer}' in TagManager.", this);
            return;
        }

        AssignPlayerOnlyLayer(_player1Instance, player1LayerId);
        AssignPlayerOnlyLayer(_player2Instance, player2LayerId);

        ApplyCameraCullingMask(player1Camera, player1LayerId, player2LayerId);
        ApplyCameraCullingMask(player2Camera, player2LayerId, player1LayerId);
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

    private void ApplyCameraCullingMask(Camera cam, int includeLayer, int excludeLayer)
    {
        if (!cam)
        {
            return;
        }

        int mask = cam.cullingMask;
        mask |= 1 << includeLayer;
        if (excludeLayer >= 0)
        {
            mask &= ~(1 << excludeLayer);
        }
        cam.cullingMask = mask;
    }
}
