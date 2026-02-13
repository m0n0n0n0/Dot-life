using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TaskStatusBar : MonoBehaviour
{
    [SerializeField]
    private GameState gameState;
    [SerializeField]
    private TriggerBus triggerBus;

    [SerializeField]
    private List<Image> taskStatusImages;

    [SerializeField]
    private Color completedColor = Color.green;
    [SerializeField]
    private Color incompleteColor = Color.white;

    [SerializeField]
    private Text taskDescriptionText;
    
    [SerializeField]
    private List<string> taskDescriptions = new List<string> { "日记", "语文书", "数学书", "相册" };

    [Header("Interaction Prompt")]
    [SerializeField]
    private Text interactionPromptText;
    [SerializeField]
    private string interactionMessage = "按 F 键交互";

    void Start()
    {
        if (gameState == null)
            gameState = FindObjectOfType<GameState>();
        if (triggerBus == null)
            triggerBus = FindObjectOfType<TriggerBus>();

        if (triggerBus != null)
        {
            triggerBus.onTaskAreaEnterExit += HandleTaskAreaEvent;
        }

        if (taskDescriptionText != null)
        {
            taskDescriptionText.text = "";
        }
        if (interactionPromptText != null)
        {
            interactionPromptText.text = "";
        }
    }

    void Update()
    {
        if (gameState != null && taskStatusImages != null)
        {
            for (int i = 0; i < taskStatusImages.Count; i++)
            {
                if (i < gameState.taskCompleted.Count)
                {
                    taskStatusImages[i].color = gameState.taskCompleted[i] ? completedColor : incompleteColor;
                }
            }
        }
    }

    void HandleTaskAreaEvent(int index, bool isEnter)
    {
        if (isEnter)
        {
            if (taskDescriptionText != null && index >= 0 && index < taskDescriptions.Count)
            {
                taskDescriptionText.text = taskDescriptions[index];
            }
            if (interactionPromptText != null)
            {
                interactionPromptText.text = interactionMessage;
            }
        }
        else
        {
            // Only clear if the text currently matches the task we are leaving
            if (index >= 0 && index < taskDescriptions.Count)
            {
                 if (taskDescriptionText != null && taskDescriptionText.text == taskDescriptions[index])
                 {
                     taskDescriptionText.text = "";
                 }
            }
            // For prompt, we just clear it when exiting. 
            // NOTE: If we want to support overlapping areas, we might need a counter, 
            // but given current logic (one active dot usually), this is acceptable.
            if (interactionPromptText != null)
            {
                interactionPromptText.text = "";
            }
        }
    }

    void OnDestroy()
    {
        if (triggerBus != null)
        {
            triggerBus.onTaskAreaEnterExit -= HandleTaskAreaEvent;
        }
    }
}
