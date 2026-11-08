using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Runs the walk from start to finish: the start pop-up where the user
/// picks a distance, the small display during the walk, the Stop button,
/// and the summary at the end. It builds its own pop-ups and buttons, so
/// nothing has to be created by hand. Attach to GameManager.
/// </summary>
public class GameFlow : MonoBehaviour
{
    [Tooltip("Walk lengths offered on the start screen, in metres.")]
    [SerializeField] private float[] distanceOptions = { 100f, 1000f, 2000f };

    [Tooltip("Optional. The small text at the top of the screen. Found automatically if left empty.")]
    [SerializeField] private TMP_Text hudLabel;

    private enum State { Choosing, Walking, Paused, Finished }

    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.8f);
    private static readonly Color ButtonColor = new Color(0.25f, 0.25f, 0.3f, 1f);
    private static readonly Color SelectedColor = new Color(1f, 0.77f, 0f, 1f);
    private static readonly Color GoColor = new Color(0.15f, 0.6f, 0.3f, 1f);
    private static readonly Color StopColor = new Color(0.8f, 0.2f, 0.2f, 0.9f);

    private DistanceTracker tracker;
    private BagSpawner spawner;
    private Canvas canvas;

    private GameObject startPanel;
    private GameObject pausePanel;
    private GameObject summaryPanel;
    private GameObject stopButton;

    private Image[] optionImages;
    private TMP_Text[] optionLabels;
    private TMP_Text pauseStats;
    private TMP_Text summaryTitle;
    private TMP_Text summaryStats;

    private State state;
    private int selectedOption;
    private float targetMetres;

    private void Awake()
    {
        tracker = FindObjectOfType<DistanceTracker>();
        spawner = FindObjectOfType<BagSpawner>();
        canvas = FindObjectOfType<Canvas>();

        if (tracker == null || spawner == null || canvas == null)
        {
            Debug.LogError("GameFlow needs a DistanceTracker, a BagSpawner and a Canvas in the scene.");
            enabled = false;
            return;
        }

        // Look for the existing top text before any other text is created.
        if (hudLabel == null)
        {
            hudLabel = FindObjectOfType<TMP_Text>();
        }

        // Keep the screen on during a walk, since the user is not touching it.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        BuildScreens();
    }

    private void Start()
    {
        ShowStart();
    }

    private void Update()
    {
        if (state != State.Walking)
        {
            return;
        }

        float walked = Mathf.Min(tracker.DistanceMetres, targetMetres);
        hudLabel.text = $"{walked:F0} / {targetMetres:F0} m    Coins: {spawner.BagsCollected}";

        if (tracker.DistanceMetres >= targetMetres)
        {
            FinishWalk(true);
        }
    }

    // ---------------------------------------------------------------
    // Walk states
    // ---------------------------------------------------------------

    private void ShowStart()
    {
        state = State.Choosing;
        SetCounting(false);
        tracker.ResetDistance();
        spawner.ResetBags();
        RefreshScreens();
    }

    private void StartWalk()
    {
        targetMetres = distanceOptions[selectedOption];
        tracker.ResetDistance();
        spawner.ResetBags();

        state = State.Walking;
        SetCounting(true);
        RefreshScreens();
    }

    private void PauseWalk()
    {
        if (state != State.Walking)
        {
            return;
        }

        state = State.Paused;
        SetCounting(false);
        pauseStats.text = $"{tracker.DistanceMetres:F0} of {targetMetres:F0} m    Coins: {spawner.BagsCollected}";
        RefreshScreens();
    }

    private void ResumeWalk()
    {
        state = State.Walking;
        SetCounting(true);
        RefreshScreens();
    }

    private void EndWalkEarly()
    {
        FinishWalk(false);
    }

    private void FinishWalk(bool reachedTarget)
    {
        // Read the results before the bags are cleared.
        summaryTitle.text = reachedTarget ? "Walk complete!" : "Walk ended";
        summaryStats.text =
            $"Distance: {tracker.DistanceMetres:F0} m\n" +
            $"Coins: {spawner.BagsCollected} of {spawner.BagsSpawned}";

        state = State.Finished;
        SetCounting(false);
        spawner.ResetBags();
        RefreshScreens();
    }

    /// <summary>Turns distance counting and bag spawning on or off together.</summary>
    private void SetCounting(bool isCounting)
    {
        tracker.enabled = isCounting;
        spawner.enabled = isCounting;
    }

    /// <summary>Shows only what belongs to the current state.</summary>
    private void RefreshScreens()
    {
        startPanel.SetActive(state == State.Choosing);
        pausePanel.SetActive(state == State.Paused);
        summaryPanel.SetActive(state == State.Finished);

        // During the walk the view stays clear: only the top text and Stop.
        stopButton.SetActive(state == State.Walking);
        hudLabel.gameObject.SetActive(state == State.Walking);
    }

    private void SelectOption(int index)
    {
        selectedOption = index;

        for (int i = 0; i < optionImages.Length; i++)
        {
            bool isSelected = i == index;
            optionImages[i].color = isSelected ? SelectedColor : ButtonColor;
            optionLabels[i].color = isSelected ? Color.black : Color.white;
        }
    }

    // ---------------------------------------------------------------
    // Building the screens
    // ---------------------------------------------------------------

    private void BuildScreens()
    {
        if (hudLabel == null)
        {
            hudLabel = CreateText(canvas.transform, "", 40f, Vector2.zero, new Vector2(700f, 80f));
            RectTransform hudRect = hudLabel.rectTransform;
            hudRect.anchorMin = hudRect.anchorMax = hudRect.pivot = new Vector2(0.5f, 1f);
            hudRect.anchoredPosition = new Vector2(0f, -30f);
        }

        // One line only, so the text never grows down into the view.
        hudLabel.enableWordWrapping = false;

        BuildStartPanel();
        BuildPausePanel();
        BuildSummaryPanel();

        Button stop = CreateButton(canvas.transform, "Stop", Vector2.zero, new Vector2(150f, 70f), StopColor, PauseWalk);
        RectTransform stopRect = (RectTransform)stop.transform;
        stopRect.anchorMin = stopRect.anchorMax = stopRect.pivot = new Vector2(1f, 0f);
        stopRect.anchoredPosition = new Vector2(-20f, 20f);
        stopButton = stop.gameObject;
    }

    private void BuildStartPanel()
    {
        RectTransform panel = CreatePanel("Start Panel", new Vector2(620f, 380f));
        startPanel = panel.gameObject;

        CreateText(panel, "Choose your distance", 36f, new Vector2(0f, 130f), new Vector2(580f, 60f));

        const float buttonWidth = 170f;
        const float gap = 20f;
        int count = distanceOptions.Length;
        float totalWidth = count * buttonWidth + (count - 1) * gap;

        optionImages = new Image[count];
        optionLabels = new TMP_Text[count];

        for (int i = 0; i < count; i++)
        {
            int index = i;
            float x = -totalWidth / 2f + buttonWidth / 2f + i * (buttonWidth + gap);

            Button option = CreateButton(panel, FormatDistance(distanceOptions[i]), new Vector2(x, 30f),
                new Vector2(buttonWidth, 80f), ButtonColor, () => SelectOption(index));

            optionImages[i] = option.GetComponent<Image>();
            optionLabels[i] = option.GetComponentInChildren<TMP_Text>();
        }

        CreateButton(panel, "Start", new Vector2(0f, -110f), new Vector2(300f, 90f), GoColor, StartWalk);
        SelectOption(0);
    }

    private void BuildPausePanel()
    {
        RectTransform panel = CreatePanel("Pause Panel", new Vector2(540f, 320f));
        pausePanel = panel.gameObject;

        CreateText(panel, "Paused", 36f, new Vector2(0f, 100f), new Vector2(500f, 60f));
        pauseStats = CreateText(panel, "", 28f, new Vector2(0f, 30f), new Vector2(500f, 50f));

        CreateButton(panel, "Resume", new Vector2(-125f, -80f), new Vector2(220f, 80f), GoColor, ResumeWalk);
        CreateButton(panel, "Finish", new Vector2(125f, -80f), new Vector2(220f, 80f), StopColor, EndWalkEarly);
    }

    private void BuildSummaryPanel()
    {
        RectTransform panel = CreatePanel("Summary Panel", new Vector2(560f, 360f));
        summaryPanel = panel.gameObject;

        summaryTitle = CreateText(panel, "", 36f, new Vector2(0f, 120f), new Vector2(520f, 60f));
        summaryStats = CreateText(panel, "", 30f, new Vector2(0f, 20f), new Vector2(520f, 120f));

        CreateButton(panel, "New walk", new Vector2(0f, -110f), new Vector2(300f, 80f), GoColor, ShowStart);
    }

    /// <summary>Shows 100 as "100 m" and 1000 as "1 km".</summary>
    private static string FormatDistance(float metres)
    {
        if (metres < 1000f)
        {
            return $"{metres:0.#} m";
        }

        return $"{metres / 1000f:0.#} km";
    }

    private RectTransform CreatePanel(string panelName, Vector2 size)
    {
        GameObject panel = new GameObject(panelName, typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        panel.AddComponent<Image>().color = PanelColor;
        return rect;
    }

    private TMP_Text CreateText(Transform parent, string text, float fontSize, Vector2 position, Vector2 size)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;

        // Text must not block taps meant for the button underneath it.
        label.raycastTarget = false;

        if (hudLabel != null)
        {
            label.font = hudLabel.font;
        }

        return label;
    }

    private Button CreateButton(Transform parent, string text, Vector2 position, Vector2 size, Color color, UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(text + " Button", typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.AddComponent<Image>();
        image.color = color;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        CreateText(buttonObject.transform, text, 30f, Vector2.zero, size);
        return button;
    }
}
