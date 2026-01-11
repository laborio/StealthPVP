using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using PlayerSlot = LocalVersusGameManager.PlayerSlot;

[DisallowMultipleComponent]
public class LocalVersusSpawner : MonoBehaviour
{
    [SerializeField] private LocalVersusGameManager manager;

    private GameObject player1Prefab => manager.player1Prefab;
    private GameObject player2Prefab => manager.player2Prefab;
    private GameObject player3Prefab => manager.player3Prefab;
    private Camera player1Camera => manager.player1Camera;
    private Camera player2Camera => manager.player2Camera;
    private Camera player3Camera => manager.player3Camera;
    private List<Transform> spawnPoints => manager.spawnPoints;
    private float minSpawnSeparation => manager.minSpawnSeparation;
    private float navMeshSampleRadius => manager.navMeshSampleRadius;
    private float respawnDelay => manager.respawnDelay;

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

    private CharacterHealth _player1Health
    {
        get => manager._player1Health;
        set => manager._player1Health = value;
    }

    private CharacterHealth _player2Health
    {
        get => manager._player2Health;
        set => manager._player2Health = value;
    }

    private CharacterHealth _player3Health
    {
        get => manager._player3Health;
        set => manager._player3Health = value;
    }

    private bool _hunterIsPlayer1
    {
        get => manager._hunterIsPlayer1;
        set => manager._hunterIsPlayer1 = value;
    }

    private bool _respawnInProgress
    {
        get => manager._respawnInProgress;
        set => manager._respawnInProgress = value;
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

    private void UpdateFogBindings()
    {
        manager.visuals?.UpdateFogBindings();
    }

    private void UpdateRevealBindings()
    {
        manager.bindings?.UpdateRevealBindings();
    }

    private void UpdatePlayerOnlyVisuals()
    {
        manager.visuals?.UpdatePlayerOnlyVisuals();
    }

    private void UpdateInputAssignments()
    {
        manager.bindings?.UpdateInputAssignments();
    }

    private void UpdateRoleIndicators()
    {
        manager.visuals?.UpdateRoleIndicators();
    }

    private void UpdateCompasses()
    {
        manager.visuals?.UpdateCompasses();
    }

    private void UpdateStunBindings()
    {
        manager.bindings?.UpdateStunBindings();
    }

    private void UpdateSmokeBindings()
    {
        manager.bindings?.UpdateSmokeBindings();
    }

    private void UpdateDashBindings()
    {
        manager.bindings?.UpdateDashBindings();
    }

    private void EnsureVisionSource(GameObject root)
    {
        manager.visuals?.EnsureVisionSource(root);
    }

    private void SubscribeHealth(CharacterHealth health)
    {
        manager.rules?.SubscribeHealth(health);
    }

    private void ConfigureInputRouter(PlayerInputRouter router, PlayerSlot slot)
    {
        manager.bindings?.ConfigureInputRouter(router, slot);
    }

    private PlayerInputRouter EnsureInputRouter(GameObject instance, PlayerSlot slot)
    {
        return manager.bindings ? manager.bindings.EnsureInputRouter(instance, slot) : null;
    }

    internal void SpawnOrRespawnPlayers(bool initialSpawn)
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

        _player1Instance = SpawnPlayer(player1Prefab, _player1Instance, p1, r1, player1Camera, PlayerSlot.Player1, ref manager._player1Health);
        _player2Instance = SpawnPlayer(player2Prefab, _player2Instance, p2, r2, player2Camera, PlayerSlot.Player2, ref manager._player2Health);
        if (usePlayer3)
        {
            _player3Instance = SpawnPlayer(player3Prefab, _player3Instance, p3, r3, player3Camera, PlayerSlot.Player3, ref manager._player3Health);
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

    internal IEnumerator HandleRespawnAndSwap(CharacterHealth dead)
    {
        _respawnInProgress = true;
        if (respawnDelay > 0f)
        {
            yield return new WaitForSeconds(respawnDelay);
        }

        if (manager && manager.IsGameOver)
        {
            _respawnInProgress = false;
            yield break;
        }

        if (manager && manager.IsPhase2Active)
        {
            bool shouldRespawn = manager.TryHandlePhase2Death(dead);
            if (!shouldRespawn)
            {
                _respawnInProgress = false;
                yield break;
            }

            RespawnDeadPlayer(dead);
            UpdateRoleIndicators();
            UpdateCompasses();
            UpdateFogBindings();
            UpdateRevealBindings();
            UpdateStunBindings();
            UpdateSmokeBindings();
            UpdateDashBindings();
            _respawnInProgress = false;
            yield break;
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
        UpdateDashBindings();
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
            _player1Instance = SpawnPlayer(player1Prefab, _player1Instance, position, rotation, player1Camera, PlayerSlot.Player1, ref manager._player1Health);
            ForcePlayerRespawn(_player1Instance, position, rotation);
        }
        else if (isPlayer2)
        {
            _player2Instance = SpawnPlayer(player2Prefab, _player2Instance, position, rotation, player2Camera, PlayerSlot.Player2, ref manager._player2Health);
            ForcePlayerRespawn(_player2Instance, position, rotation);
        }
        else
        {
            _player3Instance = SpawnPlayer(player3Prefab, _player3Instance, position, rotation, player3Camera, PlayerSlot.Player3, ref manager._player3Health);
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

}
