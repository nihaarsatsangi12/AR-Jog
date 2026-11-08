using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Measures how far the user has walked by summing the AR camera's
/// horizontal movement. Attach to GameManager. Disable this component to
/// pause counting, and enable it again to resume.
/// </summary>
public class DistanceTracker : MonoBehaviour
{
    [Tooltip("The AR Camera under AR Session Origin.")]
    [SerializeField] private Transform arCamera;

    [Tooltip("Movement smaller than this (metres) is treated as hand jitter.")]
    [SerializeField] private float minStep = 0.05f;

    [Tooltip("Movement faster than this (m/s) is treated as a tracking jump.")]
    [SerializeField] private float maxSpeed = 4f;

    /// <summary>Total distance walked this walk, in metres.</summary>
    public float DistanceMetres { get; private set; }

    private Vector3 lastPosition;
    private float lastTime;
    private bool hasLastPosition;

    private void OnEnable()
    {
        // Start measuring from wherever the user is now, so movement made
        // while counting was paused is not added.
        hasLastPosition = false;
    }

    private void Update()
    {
        // Only count movement while AR tracking is working.
        if (ARSession.state != ARSessionState.SessionTracking)
        {
            hasLastPosition = false;
            return;
        }

        // Ignore height so raising or lowering the device adds nothing.
        Vector3 position = arCamera.position;
        position.y = 0f;

        if (!hasLastPosition)
        {
            lastPosition = position;
            lastTime = Time.time;
            hasLastPosition = true;
            return;
        }

        float step = Vector3.Distance(position, lastPosition);

        // Wait until the device has really moved, so jitter is not counted.
        if (step < minStep)
        {
            return;
        }

        float elapsed = Time.time - lastTime;
        bool isJump = step > maxSpeed * elapsed;

        // A jump means tracking reset, so skip it instead of adding it.
        if (!isJump)
        {
            DistanceMetres += step;
        }

        lastPosition = position;
        lastTime = Time.time;
    }

    /// <summary>Call this when a new walk starts.</summary>
    public void ResetDistance()
    {
        DistanceMetres = 0f;
        hasLastPosition = false;
    }
}
