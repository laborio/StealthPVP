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
    [SerializeField, Tooltip("If true, spawn a fresh target prefab when the current target dies; otherwise pick from existing NPCs.")] private bool spawnNewTargetOnDeath = true;
    [SerializeField, Tooltip("Radius used to find a NavMesh position near spawn.")] private float navMeshSampleRadius = 5f;

    private readonly List<NpcIdentity> _activeNpcs = new List<NpcIdentity>();
    private NpcIdentity _currentTarget;

    private void Start()
    {
        SpawnInitialNpcs();
        AssignNewTarget();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _activeNpcs.Count; i++)
        {
            Unsubscribe(_activeNpcs[i]);
        }
        _activeNpcs.Clear();
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
            health.Died += OnNpcDied;
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
            health.Died -= OnNpcDied;
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
    }

    private void UpdateTargetUi(Color color)
    {
        if (!targetImage)
        {
            return;
        }

        targetImage.color = color;
        targetImage.enabled = true;
    }

    private void ClearUi()
    {
        if (targetImage)
        {
            targetImage.enabled = false;
        }
    }
}
