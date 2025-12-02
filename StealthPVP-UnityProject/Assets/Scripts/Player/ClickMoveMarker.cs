using System.Collections;
using UnityEngine;

public class ClickMoveMarker : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float animationDuration = 0.5f;

    private ClickMoveMarkerPool _pool;
    private Coroutine _disableRoutine;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animationDuration <= 0f && animator != null && animator.runtimeAnimatorController != null)
        {
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            if (clips != null && clips.Length > 0)
            {
                animationDuration = clips[0].length;
            }
        }
    }

    private void OnDisable()
    {
        if (_disableRoutine != null)
        {
            StopCoroutine(_disableRoutine);
            _disableRoutine = null;
        }
    }

    internal void AssignPool(ClickMoveMarkerPool pool)
    {
        _pool = pool;
    }

    internal void PlayAt(Vector3 position)
    {
        transform.position = position;

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (animator != null)
        {
            animator.Play(0, 0, 0f);
        }

        if (_disableRoutine != null)
        {
            StopCoroutine(_disableRoutine);
        }

        _disableRoutine = StartCoroutine(DisableAfterAnimation());
    }

    private IEnumerator DisableAfterAnimation()
    {
        yield return new WaitForSeconds(animationDuration);
        _disableRoutine = null;

        if (_pool != null)
        {
            _pool.ReturnToPool(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
