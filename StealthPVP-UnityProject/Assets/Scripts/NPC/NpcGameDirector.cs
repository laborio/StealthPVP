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
    [SerializeField, Tooltip("UI manager that handles target indicators. Optional.")] private NpcUiManager uiManager;
    [SerializeField, Tooltip("If true, spawn a fresh target prefab when the current target dies; otherwise pick from existing NPCs.")] private bool spawnNewTargetOnDeath = true;
    [SerializeField, Tooltip("Radius used to find a NavMesh position near spawn.")] private float navMeshSampleRadius = 5f;
    [SerializeField, Tooltip("Enable debug logs for target assignment/spawning.")] private bool debugLogs = false;
    [Header("Triangle Targeting")]
    [SerializeField, Tooltip("Enable triangle mode: player + 2 spawned targets each hunt one another.")] private bool useTriangleTargets = true;
    [SerializeField, Tooltip("Player controller participating in the triangle. Optional when triangle mode is off.")] private TriangleAgentController playerAgent;

    private readonly List<NpcIdentity> _activeNpcs = new List<NpcIdentity>();
    private readonly List<TriangleAgentController> _triangleAgents = new List<TriangleAgentController>();
    private TriangleAgentController _triangleTargetA;
    private TriangleAgentController _triangleTargetB;
    private NpcIdentity _currentTarget;

    private void Start()
    {
        if (!uiManager)
        {
            uiManager = Object.FindFirstObjectByType<NpcUiManager>();
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
        uiManager?.ClearTarget();
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
        _triangleTargetA = null;
        _triangleTargetB = null;

        if (targetPrefabs == null || targetPrefabs.Count == 0)
        {
            LogDebug("No target prefabs assigned; skipping triangle target spawn.");
            return;
        }

        List<GameObject> uniquePrefabs = new List<GameObject>(targetPrefabs);
        uniquePrefabs.RemoveAll(p => p == null);

        if (uniquePrefabs.Count < 2)
        {
            LogDebug("Triangle target spawn skipped: need at least 2 unique target prefabs.");
            return;
        }

        int firstIndex = Random.Range(0, uniquePrefabs.Count);
        GameObject firstPrefab = uniquePrefabs[firstIndex];
        uniquePrefabs.RemoveAt(firstIndex);

        GameObject secondPrefab = uniquePrefabs[Random.Range(0, uniquePrefabs.Count)];

        NpcIdentity first = SpawnNpc(firstPrefab);
        NpcIdentity second = SpawnNpc(secondPrefab);
        _triangleTargetA = GetTriangleAgent(first);
        _triangleTargetB = GetTriangleAgent(second);

        RegisterTriangleAgent(playerAgent);
        RegisterTriangleAgent(_triangleTargetA);
        RegisterTriangleAgent(_triangleTargetB);
    }

    private NpcIdentity SpawnNpc(GameObject prefab)
    {
        if (!prefab)
        {
            return null;
        }

        Transform spawn = ResolveSpawnPoint();
        Vector3 position = spawn ? spawn.position : transform.position;
        Quaternion rotation = spawn ? spawn.rotation : Quaternion.identity;
        GameObject instance = Instantiate(prefab, position, rotation);

        TrySnapToNavMesh(instance, navMeshSampleRadius);

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

    private Transform ResolveSpawnPoint()
    {
        if (spawnPoints != null && spawnPoints.Count > 0)
        {
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
        NpcIdentity identity = dead.GetComponent<NpcIdentity>();
        if (!identity)
        {
            return;
        }

        identity.SetTarget(false);
        _activeNpcs.Remove(identity);

        TriangleAgentController triangleAgent = GetTriangleAgent(identity);
        if (useTriangleTargets)
        {
            if (triangleAgent && _triangleAgents.Contains(triangleAgent))
            {
                HandleTriangleAgentDeath(triangleAgent);
            }

            UpdatePlayerUiTarget();
            return;
        }

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
        if (uiManager)
        {
            uiManager.SetTarget(identity);
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
        else if (uiManager)
        {
            // uiManager handles enabling the indicator
        }
    }

    private void ClearUi()
    {
        if (targetImage)
        {
            targetImage.enabled = false;
        }
        uiManager?.ClearTarget();
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

        return identity.GetComponent<TriangleAgentController>() ?? identity.GetComponentInChildren<TriangleAgentController>(true);
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
    }

    private void SetupTriangleMapping()
    {
        if (!useTriangleTargets)
        {
            return;
        }

        if (!playerAgent || !_triangleTargetA || !_triangleTargetB)
        {
            LogDebug("Triangle mapping skipped; missing player or target agents.");
            return;
        }

        playerAgent.ResetForNewTarget(_triangleTargetA, _triangleTargetB);
        _triangleTargetA.ResetForNewTarget(_triangleTargetB, playerAgent);
        _triangleTargetB.ResetForNewTarget(playerAgent, _triangleTargetA);
        UpdatePlayerUiTarget();

        LogDebug("Triangle mapping created: Player -> A -> B -> Player.");
    }

    private void HandleTriangleAgentDeath(TriangleAgentController deadAgent)
    {
        if (!deadAgent)
        {
            return;
        }

        _triangleAgents.Remove(deadAgent);
        if (deadAgent == _triangleTargetA)
        {
            _triangleTargetA = null;
        }
        if (deadAgent == _triangleTargetB)
        {
            _triangleTargetB = null;
        }
        if (deadAgent == playerAgent)
        {
            playerAgent = null;
        }

        List<TriangleAgentController> alive = new List<TriangleAgentController>();
        if (playerAgent && !playerAgent.IsDead)
        {
            alive.Add(playerAgent);
        }
        if (_triangleTargetA && !_triangleTargetA.IsDead)
        {
            alive.Add(_triangleTargetA);
        }
        if (_triangleTargetB && !_triangleTargetB.IsDead)
        {
            alive.Add(_triangleTargetB);
        }

        if (alive.Count == 3)
        {
            SetupTriangleMapping();
            return;
        }

        if (alive.Count == 2)
        {
            TriangleAgentController first = alive[0];
            TriangleAgentController second = alive[1];
            first.ResetForNewTarget(second);
            second.ResetForNewTarget(first);
            UpdatePlayerUiTarget();
            LogDebug("Triangle agent died -> remaining two now hunt each other.");
            return;
        }

        if (alive.Count == 1)
        {
            alive[0].ResetForNewTarget(null);
            if (alive[0] == playerAgent)
            {
                _currentTarget = null;
                ClearUi();
            }
            LogDebug("Only one triangle agent remains; clearing target.");
        }
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
            uiManager?.SetTarget(_currentTarget);
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
