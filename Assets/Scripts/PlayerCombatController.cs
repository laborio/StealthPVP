using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerCombatController : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private float selectionRayDistance = 200f;
    [SerializeField] private KeyCode attackKey = KeyCode.R;
    [SerializeField] private float attackRange = 2.5f;
    [SerializeField] private KeyCode copyMaterialKey = KeyCode.Z;
    [SerializeField] private float copyMaterialRadius = 6f;
    [SerializeField] private LayerMask npcLayerMask = ~0;
    [SerializeField] private string[] materialNodes = { "Bottom", "Top", "Cube" };
    [SerializeField] private KeyCode smokeSpellKey = KeyCode.E;
    [SerializeField] private float smokeCooldownSeconds = 5f;
    [SerializeField] private GameObject smokeVfxObject;

    private Camera _camera;
    private Targetable _currentTarget;
    private Material[] _playerMaterials;
    private float _smokeCooldownTimer;

    private void Awake()
    {
        _camera = Camera.main;
        CachePlayerMaterials();

        if (smokeVfxObject == null)
        {
            Transform vfxTransform = FindChildRecursive(transform, "vfx_Smoke");
            if (vfxTransform != null)
            {
                smokeVfxObject = vfxTransform.gameObject;
            }
        }

        if (smokeVfxObject != null)
        {
            smokeVfxObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (_camera == null)
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                return;
            }
        }

        HandleTargetSelection();
        HandleAttackInput();
        HandleMaterialCopy();
        HandleSmokeSpell();

        if (_smokeCooldownTimer > 0f)
        {
            _smokeCooldownTimer -= Time.deltaTime;
        }
    }

    private void HandleTargetSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (!TrySelectTargetUnderCursor())
            {
                ClearTarget();
            }
        }

        if (_currentTarget != null && !_currentTarget.IsAlive)
        {
            ClearTarget();
        }
    }

    private bool TrySelectTargetUnderCursor()
    {
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, selectionRayDistance, targetMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        Targetable candidate = hit.collider.GetComponentInParent<Targetable>();
        if (candidate == null || !candidate.IsAlive)
        {
            return false;
        }

        SetTarget(candidate);
        return true;
    }

    private void HandleAttackInput()
    {
        if (_currentTarget == null)
        {
            return;
        }

        if (Input.GetKeyDown(attackKey))
        {
            if (!_currentTarget.IsAlive)
            {
                ClearTarget();
                return;
            }

            Vector3 toTarget = _currentTarget.TargetPosition - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= attackRange * attackRange)
            {
                _currentTarget.HandleHit();
                ClearTarget();
            }
            else
            {
                Debug.Log("Target out of range for attack.", this);
            }
        }
    }

    private void HandleMaterialCopy()
    {
        if (!Input.GetKeyDown(copyMaterialKey))
        {
            return;
        }

        if (_playerMaterials == null || _playerMaterials.Length == 0)
        {
            CachePlayerMaterials();
            if (_playerMaterials == null || _playerMaterials.Length == 0)
            {
                return;
            }
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, copyMaterialRadius, npcLayerMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return;
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            Targetable targetable = hit.GetComponentInParent<Targetable>();
            if (targetable == null || targetable == _currentTarget)
            {
                continue;
            }

            if (!targetable.IsAlive)
            {
                continue;
            }

            ApplyPlayerMaterials(targetable.transform);
        }
    }

    private void HandleSmokeSpell()
    {
        if (smokeVfxObject == null)
        {
            return;
        }

        if (_smokeCooldownTimer > 0f)
        {
            return;
        }

        if (Input.GetKeyDown(smokeSpellKey))
        {
            StartCoroutine(PlaySmokeSpell());
            _smokeCooldownTimer = smokeCooldownSeconds;
        }
    }

    private System.Collections.IEnumerator PlaySmokeSpell()
    {
        smokeVfxObject.SetActive(true);
        yield return null;

        ParticleSystem particle = smokeVfxObject.GetComponentInChildren<ParticleSystem>();
        if (particle != null)
        {
            particle.Play(true);
        }

        yield return new WaitForSeconds(1f);

        smokeVfxObject.SetActive(false);
    }

    private void CachePlayerMaterials()
    {
        List<Material> materials = new List<Material>();
        for (int i = 0; i < materialNodes.Length; i++)
        {
            string nodeName = materialNodes[i];
            Transform child = FindChildRecursive(transform, nodeName);
            if (child == null)
            {
                continue;
            }

            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer == null)
            {
                continue;
            }

            materials.AddRange(renderer.materials);
        }

        _playerMaterials = materials.ToArray();
    }

    private void ApplyPlayerMaterials(Transform npcRoot)
    {
        if (npcRoot == null)
        {
            return;
        }

        for (int i = 0; i < materialNodes.Length; i++)
        {
            string nodeName = materialNodes[i];
            Transform child = FindChildRecursive(npcRoot, nodeName);
            if (child == null)
            {
                continue;
            }

            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer == null)
            {
                continue;
            }

            renderer.materials = CloneMaterials(_playerMaterials);
        }
    }

    private Material[] CloneMaterials(Material[] source)
    {
        if (source == null)
        {
            return null;
        }

        Material[] clone = new Material[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            clone[i] = source[i];
        }
        return clone;
    }

    private Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform result = FindChildRecursive(child, childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    public void AutoSelectTarget(Targetable target)
    {
        SetTarget(target);
    }

    public void ClearSelectedTarget()
    {
        ClearTarget();
    }

    private void SetTarget(Targetable newTarget)
    {
        if (_currentTarget == newTarget)
        {
            return;
        }

        ClearTarget();

        _currentTarget = newTarget;
        _currentTarget.Select();
    }

    private void ClearTarget()
    {
        if (_currentTarget == null)
        {
            return;
        }

        _currentTarget.Deselect();
        _currentTarget = null;
    }
}
