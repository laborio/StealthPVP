using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trigger pickup that grants temporary invisibility and respawns at configured points.
/// </summary>
[RequireComponent(typeof(Collider))]
public class InvisibilityBonusPickup : MonoBehaviour
{
    [Header("Invisibility")]
    [SerializeField] private float defaultInvisibilityDuration = 6f;
    [SerializeField] private bool useGameplayTuning = true;
    [SerializeField, Tooltip("Optional tuning override; falls back to LocalVersusGameManager gameplay tuning.")] private GameplayTuning tuningOverride;

    [Header("Respawn")]
    [SerializeField] private float respawnDelay = 10f;
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    [SerializeField] private bool randomizeSpawnOnStart = false;
    [SerializeField] private bool randomizeSpawnOnRespawn = true;
    [SerializeField] private bool alignRotationToSpawnPoint = true;

    [Header("Disable On Pickup")]
    [SerializeField] private bool disableRenderersOnPickup = true;
    [SerializeField] private bool disableCollidersOnPickup = true;

    private Collider[] _colliders;
    private Renderer[] _renderers;
    private Coroutine _respawnRoutine;

    private void Awake()
    {
        CacheComponents();
    }

    private void Start()
    {
        if (randomizeSpawnOnStart)
        {
            MoveToSpawnPoint(PickSpawnPoint(true));
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_respawnRoutine != null)
        {
            return;
        }

        Transform playerRoot = ResolvePlayerRoot(other);
        if (!playerRoot)
        {
            return;
        }

        PlayerInvisibility invisibility = playerRoot.GetComponent<PlayerInvisibility>();
        if (!invisibility)
        {
            invisibility = playerRoot.gameObject.AddComponent<PlayerInvisibility>();
        }

        float duration = ResolveInvisibilityDuration();
        invisibility.ApplyInvisibility(duration);

        BeginRespawn();
    }

    private void CacheComponents()
    {
        _colliders = GetComponentsInChildren<Collider>(true);
        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    private Transform ResolvePlayerRoot(Collider other)
    {
        if (!other)
        {
            return null;
        }

        Transform current = other.transform;
        while (current)
        {
            if (current.CompareTag("Player"))
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }

    private float ResolveInvisibilityDuration()
    {
        if (useGameplayTuning)
        {
            GameplayTuning tuning = tuningOverride;
            if (!tuning)
            {
                LocalVersusGameManager manager = LocalVersusGameManager.Instance;
                if (!manager)
                {
                    manager = FindFirstObjectByType<LocalVersusGameManager>();
                }

                if (manager)
                {
                    manager.ResolveGameplayTuning();
                    tuning = manager.gameplayTuning;
                }
            }

            if (tuning)
            {
                return Mathf.Max(0f, tuning.invisibilityDuration);
            }
        }

        return Mathf.Max(0f, defaultInvisibilityDuration);
    }

    private void BeginRespawn()
    {
        SetPickupActive(false);
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

        MoveToSpawnPoint(PickSpawnPoint(randomizeSpawnOnRespawn));

        SetPickupActive(true);
        _respawnRoutine = null;
    }

    private void MoveToSpawnPoint(Transform spawnPoint)
    {
        if (!spawnPoint)
        {
            return;
        }

        transform.position = spawnPoint.position;
        if (alignRotationToSpawnPoint)
        {
            transform.rotation = spawnPoint.rotation;
        }
    }

    private Transform PickSpawnPoint(bool randomize)
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            return null;
        }

        int startIndex = randomize ? Random.Range(0, spawnPoints.Count) : 0;
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            int index = (startIndex + i) % spawnPoints.Count;
            Transform candidate = spawnPoints[index];
            if (candidate)
            {
                return candidate;
            }
        }

        return null;
    }

    private void SetPickupActive(bool active)
    {
        if (disableCollidersOnPickup && _colliders != null)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider collider = _colliders[i];
                if (collider)
                {
                    collider.enabled = active;
                }
            }
        }

        if (disableRenderersOnPickup && _renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer)
                {
                    renderer.enabled = active;
                }
            }
        }
    }
}
