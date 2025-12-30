using UnityEngine;

[DisallowMultipleComponent]
public class LocalVersusVisuals : MonoBehaviour
{
    [SerializeField] private LocalVersusGameManager manager;

    private LocalVersusBindings bindings => manager.bindings;
    private LocalVersusRules rules => manager.rules;

    private GameObject _player1Instance => manager._player1Instance;
    private GameObject _player2Instance => manager._player2Instance;
    private GameObject _player3Instance => manager._player3Instance;
    private bool _hunterIsPlayer1 => manager._hunterIsPlayer1;
    private GameObject player3Prefab => manager.player3Prefab;
    private Camera player1Camera => manager.player1Camera;
    private Camera player2Camera => manager.player2Camera;
    private Camera player3Camera => manager.player3Camera;
    private GameUiManager player1Ui => manager.player1Ui;
    private GameUiManager player2Ui => manager.player2Ui;
    private GameUiManager player3Ui => manager.player3Ui;
    private GameObject targetImageDarkPrefab => manager.targetImageDarkPrefab;
    private GameObject targetImageGreenPrefab => manager.targetImageGreenPrefab;
    private GameObject targetImagePurplePrefab => manager.targetImagePurplePrefab;
    private GameplayTuning gameplayTuning => manager.gameplayTuning;
    private string player1OnlyLayer => manager.player1OnlyLayer;
    private string player2OnlyLayer => manager.player2OnlyLayer;
    private string player3OnlyLayer => manager.player3OnlyLayer;
    private string[] playerOnlyObjectNames => manager.playerOnlyObjectNames;

    private RevealIndicatorController player1Compass
    {
        get => manager.player1Compass;
        set => manager.player1Compass = value;
    }

    private RevealIndicatorController player2Compass
    {
        get => manager.player2Compass;
        set => manager.player2Compass = value;
    }

    private RevealIndicatorController player3Compass
    {
        get => manager.player3Compass;
        set => manager.player3Compass = value;
    }

    private FogOfWarManager player1Fog
    {
        get => manager.player1Fog;
        set => manager.player1Fog = value;
    }

    private FogOfWarManager player2Fog
    {
        get => manager.player2Fog;
        set => manager.player2Fog = value;
    }

    private FogOfWarManager player3Fog
    {
        get => manager.player3Fog;
        set => manager.player3Fog = value;
    }

    private MinimapController player1Minimap
    {
        get => manager.player1Minimap;
        set => manager.player1Minimap = value;
    }

    private MinimapController player2Minimap
    {
        get => manager.player2Minimap;
        set => manager.player2Minimap = value;
    }

    private MinimapController player3Minimap
    {
        get => manager.player3Minimap;
        set => manager.player3Minimap = value;
    }

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

    private NpcIdentity GetIdentity(GameObject root)
    {
        return bindings ? bindings.GetIdentity(root) : null;
    }

    private VisionSource GetVision(GameObject root)
    {
        return bindings ? bindings.GetVision(root) : null;
    }

    private AbilityRunner GetAbility(GameObject root)
    {
        return bindings ? bindings.GetAbility(root) : null;
    }

    private void ResolveAssignedTargets(NpcIdentity id1, NpcIdentity id2, NpcIdentity id3,
        out NpcIdentity target1, out NpcIdentity target2, out NpcIdentity target3)
    {
        if (rules)
        {
            rules.ResolveAssignedTargets(id1, id2, id3, out target1, out target2, out target3);
            return;
        }

        target1 = null;
        target2 = null;
        target3 = null;
    }

    private void ResolveGameplayTuning()
    {
        manager.ResolveGameplayTuning();
    }

    internal void UpdateRoleIndicators()
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

    internal void UpdateCompasses()
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

    internal void UpdateFogBindings()
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

    internal void AutoAssignCompasses()
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

    internal void EnsureVisionSource(GameObject root)
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

    internal void UpdatePlayerOnlyVisuals()
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
