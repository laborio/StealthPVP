using System.Collections.Generic;
using UnityEngine;

public class ClickMoveMarkerPool : MonoBehaviour
{
    [SerializeField] private ClickMoveMarker markerPrefab;
    [SerializeField] private int initialPoolSize = 6;

    private readonly Queue<ClickMoveMarker> _pool = new Queue<ClickMoveMarker>();

    private void Awake()
    {
        if (markerPrefab == null)
        {
            Debug.LogError("ClickMoveMarkerPool: Marker prefab is not assigned.", this);
            enabled = false;
            return;
        }

        WarmPool(initialPoolSize);
    }

    public void SpawnMarker(Vector3 position)
    {
        ClickMoveMarker marker = _pool.Count > 0 ? _pool.Dequeue() : CreateMarkerInstance();
        if (marker == null)
        {
            return;
        }

        marker.PlayAt(position);
    }

    internal void ReturnToPool(ClickMoveMarker marker)
    {
        if (marker == null)
        {
            return;
        }

        if (marker.gameObject.activeSelf)
        {
            marker.gameObject.SetActive(false);
        }

        _pool.Enqueue(marker);
    }

    private void WarmPool(int count)
    {
        for (int i = 0; i < count; i++)
        {
            _pool.Enqueue(CreateMarkerInstance());
        }
    }

    private ClickMoveMarker CreateMarkerInstance()
    {
        ClickMoveMarker markerInstance = Instantiate(markerPrefab, transform);
        markerInstance.gameObject.SetActive(false);
        markerInstance.AssignPool(this);
        return markerInstance;
    }
}
