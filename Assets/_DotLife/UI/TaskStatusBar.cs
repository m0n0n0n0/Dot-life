using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TaskStatusBar : MonoBehaviour
{
    [SerializeField] private GameState gameState;
    [SerializeField] private InteractingHandler interactor;
    [SerializeField] private List<Image> taskStatusImages;
    [SerializeField] private Color completedColor = Color.green;
    [SerializeField] private Color incompleteColor = Color.white;
    [SerializeField] private Text taskDescriptionText;
    [SerializeField] private List<string> taskDescriptions = new List<string> { "日记", "语文书", "数学书", "相册" };
    [Header("Interaction Prompt")]
    [SerializeField] private Text interactionPromptText;
    [SerializeField] private string interactionMessage = "按 E 打点";
    [SerializeField] private string edgeMessage = "按 E 跳下";
    [SerializeField] private string freeStampMessage = "按 E 盖印";
    [SerializeField] private string stampBlockedMessage = "此处无法盖印";
    [SerializeField] private UIPlayerInk inkMeterPrefab;
    [SerializeField] private string gameStartMessage = "按任意键开始游玩";
    [SerializeField] private string gameOverMessage = "我们的游戏就到这里！感谢你的游玩^^";
    [SerializeField] private string returnToMenuMessage = "按ESC回到开始页面";
    [Header("Readability")]
    [SerializeField] private Text taskHeadingText;
    [SerializeField, Min(1)] private int taskTextSize = 36;
    [SerializeField, Min(1)] private int taskTextMinSize = 28;
    [SerializeField, Min(1)] private int promptTextSize = 28;
    [SerializeField, Min(1)] private int headingTextSize = 24;
    [SerializeField, Min(1)] private int statusTextSize = 26;
    [SerializeField, Min(1f)] private float statusCircleSize = 60f;
    [SerializeField, Min(0f)] private float statusCircleGap = 12f;
    private GameSessionPhase lastPhase = (GameSessionPhase)(-1);
    private InteractionTarget lastTarget;
    private int lastTaskIndex = -2;
    private KeyCode lastKey;
    private bool lastFreeStamp;
    private bool lastStampBlocked;
    private bool lastEndingReady;
    private float stampBlockedUntil;
    private readonly List<Text> statusLabels = new List<Text>();
    private readonly List<UIInkWellPrompt> inkWellPrompts = new List<UIInkWellPrompt>();

    void Awake()
    {
        if (gameState == null) gameState = FindObjectOfType<GameState>();
        if (interactor == null) interactor = FindObjectOfType<InteractingHandler>();
        ApplyReadability();
        CreateInkWellPrompts();
        if (inkMeterPrefab != null && interactor != null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                UIPlayerInk meter = Instantiate(inkMeterPrefab, canvas.transform, false);
                meter.name = "PlayerInkMeter";
                meter.Bind(interactor.Ink);
            }
        }
        if (taskDescriptionText != null)
        {
            taskDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            taskDescriptionText.verticalOverflow = VerticalWrapMode.Truncate;
            taskDescriptionText.resizeTextForBestFit = true;
            taskDescriptionText.resizeTextMinSize = Mathf.Clamp(taskTextMinSize, 1, taskDescriptionText.fontSize);
            taskDescriptionText.resizeTextMaxSize = taskDescriptionText.fontSize;
        }
        if (taskStatusImages == null) return;
        foreach (Image image in taskStatusImages)
        {
            if (image == null) { statusLabels.Add(null); continue; }
            var label = new GameObject("StatusLabel", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(image.transform, false);
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Text text = label.GetComponent<Text>();
            text.font = taskDescriptionText != null ? taskDescriptionText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.Max(1, statusTextSize);
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            text.raycastTarget = false;
            statusLabels.Add(text);
        }
    }

    private void CreateInkWellPrompts()
    {
        if (gameState == null || interactor == null) return;
        TriggerBus bus = gameState.GetComponent<TriggerBus>();
        if (bus == null) return;
        foreach (InkWell well in bus.inkWells)
        {
            if (well == null) continue;
            var root = new GameObject("InkWellPrompt", typeof(RectTransform), typeof(Canvas), typeof(UIInkWellPrompt));
            root.transform.SetParent(well.transform, false);
            UIInkWellPrompt prompt = root.GetComponent<UIInkWellPrompt>();
            prompt.Bind(well, interactor, taskDescriptionText != null ? taskDescriptionText.font : null);
            inkWellPrompts.Add(prompt);
        }
    }

    private void ApplyReadability()
    {
        if (taskHeadingText == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform heading = canvas != null ? canvas.transform.Find("TaskHeading") : null;
            if (heading != null) taskHeadingText = heading.GetComponent<Text>();
        }
        if (taskHeadingText != null)
        {
            taskHeadingText.fontSize = Mathf.Max(taskHeadingText.fontSize, headingTextSize);
            GrowFromTopLeft(taskHeadingText.rectTransform, new Vector2(260f, 44f));
        }
        if (taskDescriptionText != null)
        {
            taskDescriptionText.fontSize = Mathf.Max(taskDescriptionText.fontSize, taskTextSize);
            GrowFromTopLeft(taskDescriptionText.rectTransform, new Vector2(640f, 180f));
        }
        if (interactionPromptText != null)
        {
            interactionPromptText.fontSize = Mathf.Max(interactionPromptText.fontSize, promptTextSize);
            RectTransform rect = interactionPromptText.rectTransform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(rect.rect.width, 480f));
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(rect.rect.height, 64f));
        }
        if (taskStatusImages == null) return;
        RectTransform previous = null;
        foreach (Image image in taskStatusImages)
        {
            if (image == null) continue;
            RectTransform rect = image.rectTransform;
            float size = Mathf.Max(1f, statusCircleSize, rect.rect.width, rect.rect.height);
            GrowFromTopLeft(rect, new Vector2(size, size));
            if (previous != null)
            {
                Vector2 position = rect.anchoredPosition;
                position.x = previous.anchoredPosition.x + (1f - previous.pivot.x) * previous.rect.width
                    + Mathf.Max(0f, statusCircleGap) + rect.pivot.x * size;
                rect.anchoredPosition = position;
            }
            if (taskHeadingText != null)
            {
                var corners = new Vector3[4];
                taskHeadingText.rectTransform.GetWorldCorners(corners);
                float headingBottom = corners[0].y;
                rect.GetWorldCorners(corners);
                float overlap = corners[1].y - headingBottom + 8f * rect.lossyScale.y;
                if (overlap > 0f) rect.position += Vector3.down * overlap;
            }
            previous = rect;
        }
    }

    // Enlarge UI at runtime without rewriting the scene or shifting the authored top-left edge.
    private static void GrowFromTopLeft(RectTransform rect, Vector2 minimumSize)
    {
        Vector2 oldSize = rect.rect.size;
        Vector2 newSize = Vector2.Max(oldSize, minimumSize);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newSize.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newSize.y);
        rect.anchoredPosition += Vector2.Scale(newSize - oldSize, new Vector2(rect.pivot.x, rect.pivot.y - 1f));
    }

    void OnEnable()
    {
        if (gameState != null) gameState.OnTaskStateChanged += RefreshStatus;
        if (interactor != null && interactor.Stamping != null) interactor.Stamping.OnPlacementRejected += HandleStampRejected;
        RefreshStatus(-1);
        lastPhase = (GameSessionPhase)(-1);
        foreach (UIInkWellPrompt prompt in inkWellPrompts)
            if (prompt != null) prompt.enabled = true;
    }

    private void HandleStampRejected() { stampBlockedUntil = Time.unscaledTime + 1.1f; }

    private void RefreshStatus(int ignored)
    {
        if (gameState == null || taskStatusImages == null) return;
        for (int i = 0; i < taskStatusImages.Count; i++)
        {
            bool complete = i < gameState.taskCompleted.Count && gameState.taskCompleted[i];
            if (taskStatusImages[i] != null) taskStatusImages[i].color = complete ? completedColor : incompleteColor;
            if (i < statusLabels.Count && statusLabels[i] != null) statusLabels[i].text = complete ? "OK" : (i + 1).ToString();
        }
    }

    void LateUpdate()
    {
        if (gameState == null) return;
        GameSessionPhase phase = gameState.Phase;
        InteractionTarget target = interactor != null ? interactor.CurrentTarget : null;
        if (target != null && !target.CanInteract(interactor.Movement)) target = null;
        int taskIndex = CurrentTaskIndex();
        KeyCode key = interactor != null ? interactor.InteractionKey : KeyCode.E;
        bool freeStamp = target == null && interactor != null && interactor.Stamping != null && interactor.Stamping.CanOfferStamp;
        bool stampBlocked = freeStamp && Time.unscaledTime < stampBlockedUntil;
        bool endingReady = phase == GameSessionPhase.Finished && interactor != null && interactor.CanReturnToMenu;
        if (phase == lastPhase && target == lastTarget && taskIndex == lastTaskIndex && key == lastKey && freeStamp == lastFreeStamp
            && stampBlocked == lastStampBlocked && endingReady == lastEndingReady) return;
        lastPhase = phase;
        lastTarget = target;
        lastTaskIndex = taskIndex;
        lastKey = key;
        lastFreeStamp = freeStamp;
        lastStampBlocked = stampBlocked;
        lastEndingReady = endingReady;
        string description = "";
        string prompt = "";
        if (phase == GameSessionPhase.Waiting) description = gameStartMessage;
        else if (phase == GameSessionPhase.Finished)
        {
            description = gameOverMessage;
            if (endingReady) prompt = returnToMenuMessage;
        }
        else
        {
            if (taskIndex >= 0 && taskIndex < taskDescriptions.Count)
                description = taskDescriptions[taskIndex];
            if (target != null && !(target is InkWell))
                prompt = (target is EdgeFalling ? edgeMessage : interactionMessage)
                    .Replace(" E ", " " + key + " ");
            else if (freeStamp) prompt = stampBlocked ? stampBlockedMessage : freeStampMessage.Replace(" E ", " " + key + " ");
        }
        if (taskDescriptionText != null) taskDescriptionText.text = description;
        if (interactionPromptText != null) interactionPromptText.text = prompt;
    }

    private int CurrentTaskIndex()
    {
        for (int i = 0; i < gameState.taskCompleted.Count; i++)
            if (!gameState.taskCompleted[i]) return i;
        return -1;
    }

    void OnDisable()
    {
        if (gameState != null) gameState.OnTaskStateChanged -= RefreshStatus;
        if (interactor != null && interactor.Stamping != null) interactor.Stamping.OnPlacementRejected -= HandleStampRejected;
        stampBlockedUntil = 0f;
        foreach (UIInkWellPrompt prompt in inkWellPrompts)
            if (prompt != null) prompt.enabled = false;
    }

    void OnDestroy()
    {
        foreach (UIInkWellPrompt prompt in inkWellPrompts)
            if (prompt != null) Destroy(prompt.gameObject);
        inkWellPrompts.Clear();
    }
}
