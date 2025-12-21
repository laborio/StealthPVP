using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Spawns NPCs/decoys, assigns a target, and updates UI when targets die.
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
    [SerializeField, Tooltip("If true, only decoys are spawned (no target selection).")] private bool decoysOnlyMode = false;

    private readonly List<NpcIdentity> _activeNpcs = new List<NpcIdentity>();
    private readonly List<Vector3> _usedSpawnPositions = new List<Vector3>();
    private NpcIdentity _currentTarget;

    private void Start()
    {
        LogDebug("Start called");
        _usedSpawnPositions.Clear();

        if (decoysOnlyMode)
        {
            SpawnDecoys();
            return;
        }

        if (!playerRevealIndicator)
        {
            playerRevealIndicator = Object.FindFirstObjectByType<RevealIndicatorController>();
            if (playerRevealIndicator)
            {
                LogDebug($"Found RevealIndicatorController in scene: {playerRevealIndicator.name}");
            }
        }

        SpawnInitialNpcs();
        AssignNewTarget();

        if (!_currentTarget)
        {
            TrySetExistingSceneTarget();
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _activeNpcs.Count; i++)
        {
            Unsubscribe(_activeNpcs[i]);
        }
        _activeNpcs.Clear();
    }

    public void EnableDecoysOnlyMode()
    {
        decoysOnlyMode = true;
        spawnNewTargetOnDeath = false;
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
        if (decoysOnlyMode)
        {
            return;
        }

        NpcIdentity identity = dead.GetComponent<NpcIdentity>() ?? dead.GetComponentInChildren<NpcIdentity>(true) ?? dead.GetComponentInParent<NpcIdentity>();
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
            playerRevealIndicator.SetTarget(identity);
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
            LogDebug("targetImage not assigned; UI color not updated.");
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
            playerRevealIndicator.ClearTarget();
        }
    }

    private void TrySetExistingSceneTarget()
    {
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

    private void LogDebug(string message)
    {
        if (debugLogs)
        {
            Debug.Log($"[NpcGameDirector] {message}", this);
        }
    }
}
