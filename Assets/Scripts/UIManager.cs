using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private Image targetPicture;
    [SerializeField] private Image compassImage;
    [SerializeField] private AnimationCurve compassFillCurve = AnimationCurve.EaseInOut(0f, 0.1f, 1f, 1f);
    [SerializeField] private float maxCompassDistance = 30f;
    [SerializeField] private Transform playerTransform;

    private Camera _mainCamera;
    private Targetable _currentTarget;

    public void UpdateTargetUI(Targetable target)
    {
        if (targetPicture == null)
        {
            return;
        }

        if (target == null)
        {
            targetPicture.enabled = false;
            _currentTarget = null;
            if (compassImage != null)
            {
                compassImage.fillAmount = 0f;
                compassImage.rectTransform.localEulerAngles = Vector3.zero;
            }
            return;
        }

        Color color = GetTargetColor(target);
        targetPicture.color = color;
        targetPicture.enabled = true;

        _currentTarget = target;

        if (playerTransform == null && target != null)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }
    }

    private void LateUpdate()
    {
        UpdateCompass();
    }

    private void UpdateCompass()
    {
        if (compassImage == null)
        {
            return;
        }

        if (_currentTarget == null || playerTransform == null)
        {
            compassImage.fillAmount = 0f;
            return;
        }

        Vector3 toTarget = _currentTarget.TargetPosition - playerTransform.position;
        float planarDistance = new Vector3(toTarget.x, 0f, toTarget.z).magnitude;

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        bool isVisible = false;
        if (_mainCamera != null)
        {
            Vector3 viewportPoint = _mainCamera.WorldToViewportPoint(_currentTarget.TargetPosition);
            isVisible = viewportPoint.z > 0f &&
                        viewportPoint.x >= 0f && viewportPoint.x <= 1f &&
                        viewportPoint.y >= 0f && viewportPoint.y <= 1f;
        }

        float normalizedDistance = Mathf.Clamp01(planarDistance / Mathf.Max(0.01f, maxCompassDistance));
        float fillAmount;
        if (isVisible)
        {
            fillAmount = 1f;
        }
        else
        {
            float curveValue = Mathf.Clamp01(compassFillCurve.Evaluate(1f - normalizedDistance));
            fillAmount = Mathf.Clamp(curveValue, 0.01f, 0.49f);
        }
        compassImage.fillAmount = fillAmount;

        Vector3 playerForward = playerTransform.forward;
        playerForward.y = 0f;
        toTarget.y = 0f;

        if (playerForward.sqrMagnitude < 0.0001f || toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        playerForward.Normalize();
        toTarget.Normalize();

        float angle = Vector3.SignedAngle(playerForward, toTarget, Vector3.up);
        compassImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -angle - 90f);
    }

    private Color GetTargetColor(Targetable target)
    {
        Transform bottomTransform = target.transform.Find("Bottom");
        if (bottomTransform == null)
        {
            Renderer renderer = target.GetComponentInChildren<Renderer>();
            return renderer != null ? renderer.material.color : targetPicture.color;
        }

        Renderer bottomRenderer = bottomTransform.GetComponent<Renderer>();
        if (bottomRenderer != null && bottomRenderer.material.HasProperty("_Color"))
        {
            return bottomRenderer.material.color;
        }

        Image bottomImage = bottomTransform.GetComponent<Image>();
        if (bottomImage != null)
        {
            return bottomImage.color;
        }

        return targetPicture.color;
    }
}
