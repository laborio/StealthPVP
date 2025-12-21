using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Softly nudges an NPC away from the player using a trigger radius,
/// without relying on physics collisions.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public class NpcSoftPush : MonoBehaviour
{
    [SerializeField] private NavMeshAgent agent;
    [SerializeField, Tooltip("Trigger collider used to detect players. If empty, a trigger SphereCollider is added on this GameObject.")]
    private Collider pushTrigger;
    [SerializeField, Tooltip("Distance where the NPC starts being pushed away.")] private float pushRadius = 1.2f;
    [SerializeField, Tooltip("Max push speed (m/s) at full overlap.")] private float pushSpeed = 2.5f;
    [SerializeField, Tooltip("Optional player layer mask filter. Leave empty to accept any CharacterController.")] private LayerMask playerMask;
    [SerializeField, Tooltip("Require a CharacterController or player controller in the collider hierarchy.")] private bool requirePlayerController = true;
    [SerializeField, Tooltip("Auto-create a trigger SphereCollider when missing.")] private bool autoCreateTrigger = true;

    private const float MinPushRadius = 0.01f;
    private const float MinSqrDistance = 0.0001f;

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        EnsureTrigger();
    }

    private void Awake()
    {
        if (!agent)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        EnsureTrigger();
    }

    private void OnValidate()
    {
        pushRadius = Mathf.Max(0f, pushRadius);
        pushSpeed = Mathf.Max(0f, pushSpeed);

        if (!agent)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (pushTrigger)
        {
            pushTrigger.isTrigger = true;
            SphereCollider sphere = pushTrigger as SphereCollider;
            if (sphere)
            {
                sphere.radius = Mathf.Max(MinPushRadius, pushRadius);
            }

            if (pushTrigger.transform != transform)
            {
                Debug.LogWarning("[NpcSoftPush] pushTrigger must be on the same GameObject to receive trigger callbacks. Move NpcSoftPush to the trigger object or leave pushTrigger empty to auto-create.", this);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!agent || !agent.enabled || !agent.isOnNavMesh || pushRadius <= 0f || pushSpeed <= 0f)
        {
            return;
        }

        if (playerMask.value != 0)
        {
            int layerBit = 1 << other.gameObject.layer;
            if ((playerMask.value & layerBit) == 0)
            {
                return;
            }
        }

        if (!TryResolvePlayerRoot(other, out Transform playerRoot))
        {
            if (requirePlayerController)
            {
                return;
            }

            playerRoot = other.transform;
        }

        Vector3 toNpc = transform.position - playerRoot.position;
        toNpc.y = 0f;
        float sqrDistance = toNpc.sqrMagnitude;
        if (sqrDistance < MinSqrDistance)
        {
            return;
        }

        float distance = Mathf.Sqrt(sqrDistance);
        if (distance >= pushRadius)
        {
            return;
        }

        float overlap = pushRadius - distance;
        float pushFactor = overlap / Mathf.Max(pushRadius, MinPushRadius);
        Vector3 push = toNpc.normalized * (pushSpeed * pushFactor * Time.fixedDeltaTime);
        agent.Move(push);
    }

    private void EnsureTrigger()
    {
        if (!pushTrigger)
        {
            SphereCollider existingTrigger = GetComponent<SphereCollider>();
            if (existingTrigger && existingTrigger.isTrigger)
            {
                pushTrigger = existingTrigger;
            }
        }

        if (!pushTrigger && autoCreateTrigger)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            pushTrigger = sphere;
        }

        if (pushTrigger)
        {
            pushTrigger.isTrigger = true;
            SphereCollider sphere = pushTrigger as SphereCollider;
            if (sphere)
            {
                sphere.radius = Mathf.Max(MinPushRadius, pushRadius);
            }

            if (pushTrigger.transform != transform)
            {
                Debug.LogWarning("[NpcSoftPush] pushTrigger must be on the same GameObject to receive trigger callbacks. Move NpcSoftPush to the trigger object or leave pushTrigger empty to auto-create.", this);
            }
        }
    }

    private bool TryResolvePlayerRoot(Collider other, out Transform playerRoot)
    {
        playerRoot = null;

        SimpleCharacterController simple = other.GetComponentInParent<SimpleCharacterController>();
        if (simple)
        {
            playerRoot = simple.transform;
            return true;
        }

        CustomPlayerController custom = other.GetComponentInParent<CustomPlayerController>();
        if (custom)
        {
            playerRoot = custom.transform;
            return true;
        }

        CharacterController character = other.GetComponentInParent<CharacterController>();
        if (character)
        {
            playerRoot = character.transform;
            return true;
        }

        return false;
    }
}
