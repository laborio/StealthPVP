using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles respawning characters by restoring health, ragdoll, animation, and position after death.
/// Works for both player and NPC triangle hunters.
/// </summary>
[DisallowMultipleComponent]
public class RespawnHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterHealth health;
    [SerializeField] private RagdollController ragdollController;
    [SerializeField] private CharacterAnimations characterAnimations;
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField, Tooltip("Optional explicit respawn point. If null, uses initial position/rotation.")] private Transform respawnPoint;
    [SerializeField, Tooltip("If true, automatically pick the furthest spawn point from live players when respawning.")] private bool autoPickRespawnPoint = true;

    [Header("Settings")]
    [SerializeField, Tooltip("Seconds to wait before respawning after death.")] private float respawnDelay = 3f;
    [SerializeField, Tooltip("If true, warp NavMeshAgent to respawn point instead of setting transform position.")] private bool warpNavAgent = true;

    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private Coroutine _respawnRoutine;
    private readonly List<Transform> _playerTransformsCache = new List<Transform>();

    private void Awake()
    {
        CacheRefs();
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
    }

    private void OnEnable()
    {
        if (health)
        {
            health.Died += OnDied;
        }
    }

    private void OnDisable()
    {
        if (health)
        {
            health.Died -= OnDied;
        }
    }

    private void OnDied(CharacterHealth _)
    {
        if (_respawnRoutine != null)
        {
            StopCoroutine(_respawnRoutine);
        }
        _respawnRoutine = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        float delay = Mathf.Max(0f, respawnDelay);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        DoRespawn();
        _respawnRoutine = null;
    }

    private void DoRespawn()
    {
        Transform chosenRespawn = ResolveRespawnPoint();
        Vector3 targetPos = chosenRespawn ? chosenRespawn.position : _initialPosition;
        Quaternion targetRot = chosenRespawn ? chosenRespawn.rotation : _initialRotation;

        if (navMeshAgent)
        {
            navMeshAgent.enabled = true;
            if (warpNavAgent && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.Warp(targetPos);
            }
            else
            {
                transform.SetPositionAndRotation(targetPos, targetRot);
                navMeshAgent.Warp(targetPos);
            }
            navMeshAgent.isStopped = false;
            navMeshAgent.ResetPath();
        }
        else
        {
            transform.SetPositionAndRotation(targetPos, targetRot);
        }

        if (ragdollController)
        {
            ragdollController.SetRagdollState(false);
        }

        if (animator)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        if (characterAnimations)
        {
            characterAnimations.ResetStates();
        }

        if (health)
        {
            health.Revive();
        }

        NotifyDirector();
    }

    private void NotifyDirector()
    {
        TriangleAgentController triangleAgent = GetComponent<TriangleAgentController>() ?? GetComponentInChildren<TriangleAgentController>(true);
        if (!triangleAgent)
        {
            return;
        }

        NpcGameDirector director = FindFirstObjectByType<NpcGameDirector>();
        if (director)
        {
            director.RegisterRespawnedAgent(triangleAgent);
        }
    }

    private Transform ResolveRespawnPoint()
    {
        if (respawnPoint)
        {
            return respawnPoint;
        }

        Transform candidate = null;
        if (autoPickRespawnPoint)
        {
            NpcGameDirector director = FindFirstObjectByType<NpcGameDirector>();
            if (director)
            {
                _playerTransformsCache.Clear();
                FindPlayerTransforms(_playerTransformsCache);
                candidate = director.GetFurthestSpawnPoint(_playerTransformsCache);
            }
        }

        return candidate;
    }

    private void FindPlayerTransforms(List<Transform> results)
    {
        if (results == null)
        {
            return;
        }

        // Use TriangleAgentController instances as "players" to avoid relying on SimpleCharacterController.
        var agents = FindObjectsByType<TriangleAgentController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < agents.Length; i++)
        {
            TriangleAgentController agent = agents[i];
            if (agent)
            {
                Transform t = agent.transform;
                if (t && !results.Contains(t))
                {
                    results.Add(t);
                }
            }
        }
    }

    private void CacheRefs()
    {
        if (!health)
        {
            health = GetComponent<CharacterHealth>() ?? GetComponentInChildren<CharacterHealth>(true);
        }

        if (!ragdollController)
        {
            ragdollController = GetComponent<RagdollController>() ?? GetComponentInChildren<RagdollController>(true);
        }

        if (!characterAnimations)
        {
            characterAnimations = GetComponent<CharacterAnimations>() ?? GetComponentInChildren<CharacterAnimations>(true);
        }

        if (!animator)
        {
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        }

        if (!navMeshAgent)
        {
            navMeshAgent = GetComponent<NavMeshAgent>() ?? GetComponentInChildren<NavMeshAgent>(true);
        }
    }
}
