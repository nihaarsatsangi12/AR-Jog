using UnityEngine;

/// <summary>
/// One collectable bag. Spins on the spot and tells the spawner when the
/// user has walked close enough to collect it. Attach to the bag prefab.
/// </summary>
public class CoinBag : MonoBehaviour
{
    [Tooltip("How fast the bag spins, in degrees per second.")]
    [SerializeField] private float spinSpeed = 120f;

    [Tooltip("The bag is collected when the user is within this many metres of it.")]
    [SerializeField] private float collectRadius = 1f;

    private Transform arCamera;
    private BagSpawner spawner;

    /// <summary>Called by the spawner right after the bag is created.</summary>
    public void Init(Transform cameraTransform, BagSpawner owner)
    {
        arCamera = cameraTransform;
        spawner = owner;
    }

    private void Update()
    {
        // Spin around the vertical axis so it reads as a coin from any side.
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

        if (arCamera == null || spawner == null)
        {
            return;
        }

        // Compare positions on the ground only, so the height the user
        // holds the device at does not matter.
        Vector3 offset = arCamera.position - transform.position;
        offset.y = 0f;

        if (offset.magnitude <= collectRadius)
        {
            spawner.OnBagCollected(this);
        }
    }
}
