using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Spawns NPCs on a NavMesh, designates a target, updates UI, and reassigns targets on death.
/// </summary>
[DisallowMultipleComponent]
public class NpcGameDirector : MonoBehaviour
{
    [SerializeField, Tooltip("Prefabs eligible to become the active target. Each must include NpcIdentity + CharacterHealth + NavMeshAgent.")] private List<GameObject> targetPrefabs = new List<GameObject>();
    [SerializeField, Tooltip("Prefabs used as decoys. Each must include NpcIdentity + CharacterHealth + NavMeshAgent.")] private List<GameObject> decoyPrefabs = new List<GameObject>();
    [SerializeField, Tooltip("Number of decoys to spawn.")] private int decoyCount = 5;
    [SerializeField, Tooltip("Possible spawn points for all NPCs. If empty, uses this transform position.")] private List<Transform> spawnPoints = new List<Transform>();
    [SerializeField, Tooltip("UI image that shows the color of the current target.")] private Image targetImage;
    [SerializeField, Tooltip("Player reveal indicator controller (world-space compass). Optional.")] private RevealIndicatorController playerRevealIndicator;
    [SerializeField, Tooltip("If true, spawn a fresh target prefab when the current target dies; otherwise pick from existing NPCs.")] private bool spawnNewTargetOnDeath = true;
    [SerializeField, Tooltip("Radius used to find a NavMesh position near spawn.")] private float navMeshSampleRadius = 5f;
    [SerializeField, Tooltip("Minimum distance between spawned NPCs when picking spawn points. Ignored if 0 or not enough points.")] private float minSpawnSeparation = 8f;
    [SerializeField, Tooltip("Enable debug logs for target assignment/spawning.")] private bool debugLogs = false;
    [Header("Triangle Targeting")]
    [SerializeField, Tooltip("Enable triangle mode: player + 3 spawned targets each hunt one another in a loop.")] private bool useTriangleTargets = true;
    [SerializeField, Tooltip("Player controller participating in the triangle. Optional when triangle mode is off.")] private TriangleAgentController playerAgent;
    [SerializeField, Tooltip("Optional extra player-controlled agents to include in the hunt loop.")] private List<TriangleAgentController> additionalPlayerAgents = new List<TriangleAgentController>();
    [SerializeField, Tooltip("How many distinct NPC prefabs to spawn for the triangle hunt. Must not exceed unique target prefabs.")] private int triangleTargetCount = 3;
    [SerializeField, Tooltip("Seconds to wait after a triangle kill before remapping hunt targets.")] private float triangleRetargetDelay = 1.5f;
    [Header("Difficulty")]
    [Range(0f, 1f)] [SerializeField, Tooltip("0 = easiest, 1 = hardest. Applied to all triangle agents.")] private float aiDifficulty = 0.5f;
    [Header("Reveal Base (shared for triangle agents)")]
    [SerializeField] private float revealCooldownBase = 30f;
    [SerializeField] private float revealHoldBase = 2f;
    [SerializeField] private float revealFadeBase = 1f;

    private readonly List<NpcIdentity> _activeNpcs = new List<NpcIdentity>();
    private readonly List<TriangleAgentController> _triangleAgents = new List<TriangleAgentController>();
    private readonly List<Vector3> _usedSpawnPositions = new List<Vector3>();
    private readonly List<TriangleAgentController> _triangleTargets = new List<TriangleAgentController>();
    private NpcIdentity _currentTarget;
    private Coroutine _triangleRemapRoutine;

    private void Start()
    {
        Debug.Log("[NpcGameDirector] Start called", this);
        _usedSpawnPositions.Clear();
        RegisterPlayerAgents();
        ApplyDifficultyToAgents();

        if (!playerRevealIndicator)
        {
            playerRevealIndicator = Object.FindFirstObjectByType<RevealIndicatorController>();
            if (playerRevealIndicator)
            {
                Debug.Log($"[NpcGameDirector] Found RevealIndicatorController in scene: {playerRevealIndicator.name}", this);
            }
            else
            {
                Debug.LogWarning("[NpcGameDirector] No RevealIndicatorController found in scene.", this);
            }
        }

        if (useTriangleTargets)
        {
            SpawnDecoys();
            SpawnTriangleTargets();
            SetupTriangleMapping();
            UpdatePlayerUiTarget();
        }
        else
        {
            SpawnInitialNpcs();
            AssignNewTarget();

            if (!_currentTarget)
            {
                // Fallback: try to grab any existing NPC in the scene.
                TrySetExistingSceneTarget();
            }
        }
    }

    private void OnDestroy()
    {
        if (_triangleRemapRoutine != null)
        {
            StopCoroutine(_triangleRemapRoutine);
            _triangleRemapRoutine = null;
        }

        for (int i = 0; i < _activeNpcs.Count; i++)
        {
            Unsubscribe(_activeNpcs[i]);
        }
        _activeNpcs.Clear();
        for (int i = 0; i < _triangleAgents.Count; i++)
        {
            TriangleAgentController agent = _triangleAgents[i];
            if (!agent)
            {
                continue;
            }

            CharacterHealth health = agent.GetComponent<CharacterHealth>() ?? agent.GetComponentInChildren<CharacterHealth>(true);
            UnsubscribeHealth(health);
        }
        _triangleAgents.Clear();
    }

    private void SpawnInitialNpcs()
    {
        SpawnDecoys();
        SpawnTarget();
    }

    private void SpawnDecoys()
    {
        if (decoyPrefabs == null || decoyPrefabs.Count == 0 || decoyCount <= 0)
        {
            return;
        }

        for (int i = 0; i < decoyCount; i++)
        {
            GameObject prefab = decoyPrefabs[Random.Range(0, decoyPrefabs.Count)];
            SpawnNpc(prefab);
        }
    }

    private void SpawnTarget()
    {
        if (targetPrefabs == null || targetPrefabs.Count == 0)
        {
            LogDebug("No target prefabs assigned; skipping target spawn.");
            return;
        }

        GameObject prefab = targetPrefabs[Random.Range(0, targetPrefabs.Count)];
        NpcIdentity identity = SpawnNpc(prefab);
        if (identity)
        {
            SetTarget(identity);
        }
    }

    private void SpawnTriangleTargets()
    {
        _triangleTargets.Clear();

        if (targetPrefabs == null || targetPrefabs.Count == 0)
        {
            LogDebug("No target prefabs assigned; skipping triangle target spawn.");
            return;
        }

        List<GameObject> validPrefabs = new List<GameObject>(targetPrefabs);
        validPrefabs.RemoveAll(p => p == null);
        if (validPrefabs.Count == 0)
        {
            LogDebug("Triangle target spawn skipped: all target prefabs null.");
            return;
        }

        // Enforce uniqueness of prefabs.
        List<GameObject> uniquePrefabs = new List<GameObject>();
        for (int i = 0; i < validPrefabs.Count; i++)
        {
            GameObject prefab = validPrefabs[i];
            if (prefab && !uniquePrefabs.Contains(prefab))
            {
                uniquePrefabs.Add(prefab);
            }
        }

        int desiredTargets = Mathf.Max(1, triangleTargetCount);
        if (uniquePrefabs.Count < desiredTargets)
        {
            LogDebug($"Triangle target count reduced: requested {desiredTargets} unique targets but only found {uniquePrefabs.Count} unique prefabs.");
            desiredTargets = uniquePrefabs.Count;
        }

        List<GameObject> pool = new List<GameObject>(uniquePrefabs);
        for (int i = 0; i < desiredTargets && pool.Count > 0; i++)
        {
            int pickIndex = Random.Range(0, pool.Count);
            GameObject prefab = pool[pickIndex];
            pool.RemoveAt(pickIndex);

            NpcIdentity identity = SpawnNpc(prefab);
            TriangleAgentController agent = GetTriangleAgent(identity);
            RegisterTriangleAgent(agent);
            if (agent)
            {
                _triangleTargets.Add(agent);
            }
        }
    }

    private NpcIdentity SpawnNpc(GameObject prefab)
    {
        if (!prefab)
        {
            return null;
        }

        Transform spawn = ResolveSpawnPointWithSeparation();
        Vector3 position = spawn ? spawn.position : transform.position;
        Quaternion rotation = spawn ? spawn.rotation : Quaternion.identity;
        GameObject instance = Instantiate(prefab, position, rotation);

        TrySnapToNavMesh(instance, navMeshSampleRadius);
        _usedSpawnPositions.Add(instance.transform.position);

        NpcIdentity identity = instance.GetComponent<NpcIdentity>();
        if (!identity)
        {
            identity = instance.GetComponentInChildren<NpcIdentity>(true);
        }
        if (!identity)
        {
            Debug.LogWarning($"Spawned NPC prefab '{prefab.name}' lacks NpcIdentity.", instance);
            return null;
        }

        Subscribe(identity);
        _activeNpcs.Add(identity);
        return identity;
    }

    private Transform ResolveSpawnPointWithSeparation()
    {
        if (spawnPoints != null && spawnPoints.Count > 0)
        {
            if (minSpawnSeparation <= 0f || _usedSpawnPositions.Count == 0 || spawnPoints.Count == 1)
            {
                return spawnPoints[Random.Range(0, spawnPoints.Count)];
            }

            List<Transform> candidates = new List<Transform>();
            float minDistSqr = minSpawnSeparation * minSpawnSeparation;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                Transform point = spawnPoints[i];
                if (!point)
                {
                    continue;
                }

                bool farEnough = true;
                Vector3 pos = point.position;
                for (int j = 0; j < _usedSpawnPositions.Count; j++)
                {
                    if ((pos - _usedSpawnPositions[j]).sqrMagnitude < minDistSqr)
                    {
                        farEnough = false;
                        break;
                    }
                }

                if (farEnough)
                {
                    candidates.Add(point);
                }
            }

            if (candidates.Count > 0)
            {
                return candidates[Random.Range(0, candidates.Count)];
            }

            // Fallback: no sufficiently separated spawn; pick any.
            return spawnPoints[Random.Range(0, spawnPoints.Count)];
        }

        return null;
    }

    private void TrySnapToNavMesh(GameObject instance, float sampleRadius)
    {
        if (!instance)
        {
            return;
        }

        NavMeshAgent agent = instance.GetComponent<NavMeshAgent>() ?? instance.GetComponentInChildren<NavMeshAgent>(true);
        if (!agent)
        {
            return;
        }

        Vector3 origin = agent.transform.position;
        if (NavMesh.SamplePosition(origin, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
        else
        {
            Debug.LogWarning($"NavMesh sample failed for '{instance.name}'. Ensure spawn point is on a baked NavMesh.");
        }
    }

    public Transform GetFurthestSpawnPoint(IReadOnlyList<Transform> avoidTransforms)
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            return null;
        }

        Transform best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            Transform candidate = spawnPoints[i];
            if (!candidate)
            {
                continue;
            }

            float closestSqr = float.MaxValue;
            if (avoidTransforms != null && avoidTransforms.Count > 0)
            {
                for (int j = 0; j < avoidTransforms.Count; j++)
                {
                    Transform t = avoidTransforms[j];
                    if (!t)
                    {
                        continue;
                    }

                    float sqr = (candidate.position - t.position).sqrMagnitude;
                    if (sqr < closestSqr)
                    {
                        closestSqr = sqr;
                    }
                }
            }
            else
            {
                closestSqr = 0f;
            }

            if (closestSqr > bestScore)
            {
                bestScore = closestSqr;
                best = candidate;
            }
        }

        return best;
    }

    private void Subscribe(NpcIdentity identity)
    {
        if (!identity)
        {
            return;
        }

        CharacterHealth health = identity.GetComponent<CharacterHealth>();
        if (health)
        {
            SubscribeHealth(health);
        }
    }

    private void Unsubscribe(NpcIdentity identity)
    {
        if (!identity)
        {
            return;
        }

        CharacterHealth health = identity.GetComponent<CharacterHealth>();
        if (health)
        {
            UnsubscribeHealth(health);
        }
    }

    private void OnNpcDied(CharacterHealth dead)
    {
        if (!dead)
        {
            return;
        }

        dead.Died -= OnNpcDied;
        // Death can fire from a CharacterHealth on a different GameObject than the NpcIdentity (e.g., player identity on a child).
        NpcIdentity identity = dead.GetComponent<NpcIdentity>() ?? dead.GetComponentInChildren<NpcIdentity>(true) ?? dead.GetComponentInParent<NpcIdentity>();

        if (useTriangleTargets)
        {
            // Player has no NpcIdentity; locate triangle agent directly from the dead health hierarchy.
            TriangleAgentController triangleAgent = (identity ? GetTriangleAgent(identity) : null) ??
                                                   dead.GetComponent<TriangleAgentController>() ??
                                                   dead.GetComponentInChildren<TriangleAgentController>(true) ??
                                                   dead.GetComponentInParent<TriangleAgentController>();

            if (triangleAgent && _triangleAgents.Contains(triangleAgent))
            {
                HandleTriangleAgentDeath(triangleAgent);
            }

            if (identity)
            {
                identity.SetTarget(false);
                _activeNpcs.Remove(identity);
            }
            return;
        }

        if (!identity)
        {
            return;
        }

        identity.SetTarget(false);
        _activeNpcs.Remove(identity);

        if (_currentTarget == identity)
        {
            _currentTarget = null;
            if (spawnNewTargetOnDeath)
            {
                SpawnTarget();
            }
            else
            {
                AssignNewTarget();
            }
        }
    }

    private void AssignNewTarget()
    {
        if (useTriangleTargets)
        {
            return;
        }

        if (_currentTarget && _currentTarget.IsTarget)
        {
            return;
        }

        NpcIdentity choice = PickRandomAliveNpc();
        if (choice)
        {
            SetTarget(choice);
        }
        else
        {
            ClearUi();
        }
    }

    private NpcIdentity PickRandomAliveNpc()
    {
        List<NpcIdentity> candidates = new List<NpcIdentity>();
        for (int i = 0; i < _activeNpcs.Count; i++)
        {
            NpcIdentity npc = _activeNpcs[i];
            if (!npc)
            {
                continue;
            }

            CharacterHealth health = npc.GetComponent<CharacterHealth>();
            if (!health || health.IsDead)
            {
                continue;
            }

            if (!npc.IsTarget)
            {
                candidates.Add(npc);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    private void SetTarget(NpcIdentity identity)
    {
        if (!identity)
        {
            return;
        }

        if (_currentTarget && _currentTarget != identity)
        {
            _currentTarget.SetTarget(false);
        }

        _currentTarget = identity;
        _currentTarget.SetTarget(true);
        UpdateTargetUi(_currentTarget.IdentifierColor);

        if (playerRevealIndicator)
        {
            Debug.Log($"[NpcGameDirector] Sending target to RevealIndicatorController: {_currentTarget.name}", this);
            playerRevealIndicator.SetTarget(identity);
        }
        else
        {
            Debug.LogWarning("[NpcGameDirector] playerRevealIndicator is null when setting target.", this);
        }

        LogDebug($"Set target to {identity.name}");
    }

    private void UpdateTargetUi(Color color)
    {
        if (targetImage)
        {
            targetImage.color = color;
            targetImage.enabled = true;
        }
        else
        {
            Debug.LogWarning("[NpcGameDirector] targetImage not assigned; UI color not updated.", this);
        }
    }

    private void ClearUi()
    {
        if (targetImage)
        {
            targetImage.enabled = false;
        }

        if (playerRevealIndicator)
        {
            Debug.Log("[NpcGameDirector] Clearing RevealIndicatorController target.", this);
            playerRevealIndicator.ClearTarget();
        }
    }

    private void TrySetExistingSceneTarget()
    {
        if (useTriangleTargets)
        {
            return;
        }

        NpcIdentity[] identities = Object.FindObjectsByType<NpcIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < identities.Length; i++)
        {
            CharacterHealth health = identities[i].GetComponent<CharacterHealth>();
            if (health && !health.IsDead)
            {
                if (!_activeNpcs.Contains(identities[i]))
                {
                    _activeNpcs.Add(identities[i]);
                    Subscribe(identities[i]);
                }
                SetTarget(identities[i]);
                return;
            }
        }

        LogDebug("No existing scene NpcIdentity found to set as target.");
    }

    private TriangleAgentController GetTriangleAgent(NpcIdentity identity)
    {
        if (!identity)
        {
            return null;
        }

        return identity.GetComponent<TriangleAgentController>() ??
               identity.GetComponentInChildren<TriangleAgentController>(true) ??
               identity.GetComponentInParent<TriangleAgentController>();
    }

    private void RegisterTriangleAgent(TriangleAgentController agent)
    {
        if (!agent || _triangleAgents.Contains(agent))
        {
            return;
        }

        _triangleAgents.Add(agent);
        CharacterHealth health = agent.GetComponent<CharacterHealth>() ?? agent.GetComponentInChildren<CharacterHealth>(true);
        SubscribeHealth(health);
        agent.ApplyDifficulty(aiDifficulty);
        agent.SetRevealBase(revealCooldownBase, revealHoldBase, revealFadeBase);
    }

    public void SetRevealBase(float cooldown, float hold, float fade)
    {
        revealCooldownBase = cooldown;
        revealHoldBase = hold;
        revealFadeBase = fade;
        for (int i = 0; i < _triangleAgents.Count; i++)
        {
            TriangleAgentController agent = _triangleAgents[i];
            if (agent)
            {
                agent.SetRevealBase(cooldown, hold, fade);
            }
        }
    }

    public void RegisterRespawnedAgent(TriangleAgentController agent)
    {
        if (!agent)
        {
            return;
        }

        RegisterTriangleAgent(agent);
        NpcIdentity identity = agent.GetComponent<NpcIdentity>() ?? agent.GetComponentInChildren<NpcIdentity>(true);
        if (identity && !_activeNpcs.Contains(identity))
        {
            _activeNpcs.Add(identity);
            Subscribe(identity);
        }

        SetupTriangleMapping();
        agent.ApplyDifficulty(aiDifficulty);
    }

    private void RegisterPlayerAgents()
    {
        RegisterTriangleAgent(playerAgent);
        if (additionalPlayerAgents == null)
        {
            return;
        }

        for (int i = 0; i < additionalPlayerAgents.Count; i++)
        {
            RegisterTriangleAgent(additionalPlayerAgents[i]);
        }
    }

    private void ApplyDifficultyToAgents()
    {
        for (int i = 0; i < _triangleAgents.Count; i++)
        {
            _triangleAgents[i]?.ApplyDifficulty(aiDifficulty);
        }
    }

    public void SetDifficulty(float value)
    {
        aiDifficulty = Mathf.Clamp01(value);
        ApplyDifficultyToAgents();
    }

    private List<TriangleAgentController> GetAliveTriangleAgents()
    {
        List<TriangleAgentController> alive = new List<TriangleAgentController>();
        for (int i = 0; i < _triangleAgents.Count; i++)
        {
            TriangleAgentController agent = _triangleAgents[i];
            if (agent && !agent.IsDead && !alive.Contains(agent))
            {
                alive.Add(agent);
            }
        }

        return alive;
    }

    private void ConfigureHuntLoop(List<TriangleAgentController> agents)
    {
        if (agents == null || agents.Count == 0)
        {
            _currentTarget = null;
            ClearUi();
            return;
        }

        int count = agents.Count;

        // Prepare targets so assignments can overlap and some agents may temporarily be unhunted.
        List<TriangleAgentController> alive = new List<TriangleAgentController>();
        for (int i = 0; i < count; i++)
        {
            TriangleAgentController a = agents[i];
            if (a && !a.IsDead && !alive.Contains(a))
            {
                alive.Add(a);
            }
        }

        if (alive.Count == 0)
        {
            _currentTarget = null;
            ClearUi();
            return;
        }

        for (int i = 0; i < alive.Count; i++)
        {
            TriangleAgentController agent = alive[i];
            TriangleAgentController target = null;

            if (alive.Count > 1)
            {
                List<TriangleAgentController> options = new List<TriangleAgentController>(alive);
                options.Remove(agent);
                if (options.Count > 0)
                {
                    target = options[Random.Range(0, options.Count)];
                }
            }

            agent.ResetForNewTarget(target, null);
        }

        UpdatePlayerUiTarget();
        LogDebug($"Triangle hunt loop configured (random overlap) for {alive.Count} agents.");
    }

    private void SetupTriangleMapping()
    {
        if (!useTriangleTargets)
        {
            return;
        }

        List<TriangleAgentController> alive = GetAliveTriangleAgents();
        if (alive.Count < 2)
        {
            LogDebug("Triangle mapping skipped; need at least 2 agents alive.");
            return;
        }

        ConfigureHuntLoop(alive);
    }

    private void HandleTriangleAgentDeath(TriangleAgentController deadAgent)
    {
        if (!deadAgent)
        {
            return;
        }

        _triangleAgents.Remove(deadAgent);
        _triangleTargets.Remove(deadAgent);
        if (deadAgent == playerAgent)
        {
            playerAgent = null;
        }

        _currentTarget = null;
        ClearUi();

        if (_triangleRemapRoutine != null)
        {
            StopCoroutine(_triangleRemapRoutine);
        }

        _triangleRemapRoutine = StartCoroutine(RemapTriangleAfterDelay());
    }

    private IEnumerator RemapTriangleAfterDelay()
    {
        float delay = Mathf.Max(0f, triangleRetargetDelay);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        List<TriangleAgentController> alive = GetAliveTriangleAgents();
        ConfigureHuntLoop(alive);
        if (alive.Count == 1)
        {
            LogDebug("Only one triangle agent remains; clearing target.");
        }
        else
        {
            LogDebug($"Triangle agent died -> remapped hunt loop for {alive.Count} agents after delay.");
        }

        _triangleRemapRoutine = null;
    }

    private void SubscribeHealth(CharacterHealth health)
    {
        if (!health)
        {
            return;
        }

        health.Died -= OnNpcDied;
        health.Died += OnNpcDied;
    }

    private void UnsubscribeHealth(CharacterHealth health)
    {
        if (!health)
        {
            return;
        }

        health.Died -= OnNpcDied;
    }

    private void UpdatePlayerUiTarget()
    {
        if (!useTriangleTargets || !playerAgent)
        {
            return;
        }

        NpcIdentity next = playerAgent.MyTarget ? playerAgent.MyTarget.Identity : null;
        if (_currentTarget && _currentTarget != next)
        {
            _currentTarget.SetTarget(false);
        }

        _currentTarget = next;
        if (_currentTarget)
        {
            _currentTarget.SetTarget(true);
            UpdateTargetUi(_currentTarget.IdentifierColor);
            if (playerRevealIndicator)
            {
                Debug.Log($"[NpcGameDirector] UpdatePlayerUiTarget -> setting indicator target {_currentTarget.name}", this);
                playerRevealIndicator.SetTarget(_currentTarget);
            }
            else
            {
                Debug.LogWarning("[NpcGameDirector] UpdatePlayerUiTarget -> playerRevealIndicator is null", this);
            }
        }
        else
        {
            ClearUi();
        }
    }

    private void LogDebug(string message)
    {
        if (debugLogs)
        {
            Debug.Log($"[NpcGameDirector] {message}", this);
        }
    }
}
