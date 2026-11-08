using UnityEngine;

/// <summary>
/// Places a bag ahead of the user each time they approach the next
/// distance mark (for example every 50 m). If the user's path turns away
/// from the bag, the bag is moved back in front of them, so it never ends
/// up behind a wall or off the path. Attach to GameManager, next to
/// DistanceTracker. Disable this component to pause it.
/// </summary>
public class BagSpawner : MonoBehaviour
{
    [Tooltip("The AR Camera under AR Session Origin.")]
    [SerializeField] private Transform arCamera;

    [Tooltip("The bag prefab (it must have the CoinBag script on it).")]
    [SerializeField] private CoinBag bagPrefab;

    [Tooltip("A bag is placed at every multiple of this distance, in metres.")]
    [SerializeField] private float bagInterval = 50f;

    [Tooltip("How far ahead of the user a bag appears, in metres.")]
    [SerializeField] private float spawnAhead = 10f;

    [Tooltip("Bag height relative to the device, in metres. 0 = same height as the device.")]
    [SerializeField] private float heightOffset = 0f;

    [Tooltip("Move the bag back in front of the user when they walk away from it.")]
    [SerializeField] private bool keepBagAhead = true;

    [Tooltip("The bag counts as 'not ahead' when it is more than this many degrees off the walking direction.")]
    [SerializeField] private float reaimAngle = 60f;

    [Tooltip("Seconds the bag must stay 'not ahead' before it is moved.")]
    [SerializeField] private float reaimDelay = 1f;

    [Tooltip("Optional. Leave empty to use the built-in chime.")]
    [SerializeField] private AudioClip collectSound;

    /// <summary>Bags the user has collected this walk.</summary>
    public int BagsCollected { get; private set; }

    /// <summary>Bags that have appeared this walk.</summary>
    public int BagsSpawned { get; private set; }

    // The user must walk this far before a new walking direction is trusted.
    private const float HeadingStep = 0.5f;

    // A bag is never moved closer than this, so it stays visible on arrival.
    private const float MinPlaceDistance = 1.5f;

    // A bag this close is about to be collected, so it is left alone.
    private const float NoReaimDistance = 2f;

    private DistanceTracker tracker;
    private AudioSource audioSource;
    private CoinBag activeBag;
    private float activeMark;
    private float nextMark;
    private float offCourseTime;

    private Vector3 headingAnchor;
    private Vector3 walkDirection;
    private bool hasHeadingAnchor;
    private bool hasWalkDirection;

    private void Awake()
    {
        tracker = FindObjectOfType<DistanceTracker>();
        if (tracker == null)
        {
            Debug.LogError("BagSpawner needs a DistanceTracker in the scene.");
        }

        // The AR camera has no audio listener by default, and without one
        // no sound is heard.
        if (FindObjectOfType<AudioListener>() == null && arCamera != null)
        {
            arCamera.gameObject.AddComponent<AudioListener>();
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (collectSound == null)
        {
            collectSound = CreateChime();
        }

        nextMark = bagInterval;
    }

    private void OnEnable()
    {
        // Movement made while paused must not count as a walking direction.
        hasHeadingAnchor = false;

        if (activeBag != null)
        {
            activeBag.gameObject.SetActive(true);
        }
    }

    private void OnDisable()
    {
        // Hide the bag while the walk is paused, so it cannot be collected.
        if (activeBag != null)
        {
            activeBag.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (tracker == null || arCamera == null || bagPrefab == null || bagInterval <= 0f)
        {
            return;
        }

        UpdateWalkDirection();

        // With a short interval (indoor testing) the bag cannot appear
        // further ahead than the interval itself.
        float ahead = Mathf.Min(spawnAhead, bagInterval);

        // Spawn early, so the bag sits at the mark when the user gets there.
        if (tracker.DistanceMetres >= nextMark - ahead)
        {
            SpawnBag(nextMark);
            nextMark += bagInterval;
        }

        if (keepBagAhead && activeBag != null)
        {
            KeepBagAhead();
        }
    }

    /// <summary>
    /// Works out which way the user is walking from how the device has
    /// moved, so it does not matter where the device is pointing.
    /// </summary>
    private void UpdateWalkDirection()
    {
        Vector3 position = arCamera.position;
        position.y = 0f;

        if (!hasHeadingAnchor)
        {
            headingAnchor = position;
            hasHeadingAnchor = true;
            return;
        }

        Vector3 moved = position - headingAnchor;
        if (moved.magnitude >= HeadingStep)
        {
            walkDirection = moved.normalized;
            headingAnchor = position;
            hasWalkDirection = true;
        }
    }

    private Vector3 GetHeading()
    {
        if (hasWalkDirection)
        {
            return walkDirection;
        }

        // Before the user has walked anywhere, use the way the device faces.
        Vector3 forward = arCamera.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.01f)
        {
            // Device is pointing straight up or down: use its top edge.
            forward = arCamera.up;
            forward.y = 0f;
        }

        return forward.normalized;
    }

    private void SpawnBag(float mark)
    {
        // Only one bag at a time. A bag the user never reached is removed.
        if (activeBag != null)
        {
            Destroy(activeBag.gameObject);
        }

        activeMark = mark;
        offCourseTime = 0f;

        activeBag = Instantiate(bagPrefab, GetPlacePosition(), bagPrefab.transform.rotation);
        activeBag.Init(arCamera, this);
        BagsSpawned++;
    }

    /// <summary>
    /// The spot in front of the user where the bag belongs: along the
    /// walking direction, as far away as the distance left to its mark.
    /// </summary>
    private Vector3 GetPlacePosition()
    {
        float remaining = activeMark - tracker.DistanceMetres;
        float distance = Mathf.Max(remaining, MinPlaceDistance);

        Vector3 position = arCamera.position + GetHeading() * distance;
        position.y = arCamera.position.y + heightOffset;
        return position;
    }

    /// <summary>
    /// If the user has been walking away from the bag for a moment (they
    /// turned a corner, or the bag is behind a wall), put it ahead again.
    /// </summary>
    private void KeepBagAhead()
    {
        Vector3 toBag = activeBag.transform.position - arCamera.position;
        toBag.y = 0f;

        if (!hasWalkDirection || toBag.magnitude < NoReaimDistance)
        {
            offCourseTime = 0f;
            return;
        }

        bool isOffCourse = Vector3.Angle(walkDirection, toBag) > reaimAngle;
        if (!isOffCourse)
        {
            offCourseTime = 0f;
            return;
        }

        offCourseTime += Time.deltaTime;
        if (offCourseTime >= reaimDelay)
        {
            activeBag.transform.position = GetPlacePosition();
            offCourseTime = 0f;
        }
    }

    /// <summary>Called by a bag when the user reaches it.</summary>
    public void OnBagCollected(CoinBag bag)
    {
        BagsCollected++;
        audioSource.PlayOneShot(collectSound);

        if (bag == activeBag)
        {
            activeBag = null;
        }

        Destroy(bag.gameObject);
    }

    /// <summary>Call this when a new walk starts.</summary>
    public void ResetBags()
    {
        if (activeBag != null)
        {
            Destroy(activeBag.gameObject);
            activeBag = null;
        }

        BagsCollected = 0;
        BagsSpawned = 0;
        nextMark = bagInterval;
        offCourseTime = 0f;
        hasHeadingAnchor = false;
        hasWalkDirection = false;
    }

    /// <summary>Builds a short two-note chime so no sound file is needed.</summary>
    private static AudioClip CreateChime()
    {
        const int sampleRate = 44100;
        const float firstNoteSeconds = 0.08f;
        const float secondNoteSeconds = 0.40f;
        const float firstNoteHz = 988f;
        const float secondNoteHz = 1319f;

        int total = Mathf.CeilToInt((firstNoteSeconds + secondNoteSeconds) * sampleRate);
        float[] samples = new float[total];
        float phase = 0f;

        for (int i = 0; i < total; i++)
        {
            float time = (float)i / sampleRate;
            bool isFirstNote = time < firstNoteSeconds;
            float frequency = isFirstNote ? firstNoteHz : secondNoteHz;

            // The second note fades out so the chime does not end abruptly.
            float fade = isFirstNote ? 1f : Mathf.Exp(-6f * (time - firstNoteSeconds));

            phase += 2f * Mathf.PI * frequency / sampleRate;
            samples[i] = 0.5f * fade * Mathf.Sin(phase);
        }

        AudioClip clip = AudioClip.Create("BagChime", total, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
