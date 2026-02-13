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
    private EdgeFalling edge;

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
    [SerializeField]
    private string edgeMessage = "按 F 键交互";
    [SerializeField]
    private string gameStartMessage = "按任意键开始游玩";
    [SerializeField]
    private string gameOverMessage = "我们的游戏就到这里！感谢你的游玩^^";


    void Start()
    {
        if (gameState == null)
            gameState = FindObjectOfType<GameState>();
        if (triggerBus == null)
            triggerBus = FindObjectOfType<TriggerBus>();


        if (gameState != null)
        {
            gameState.OnGameOver += HandleGameOver;
        }
        if (triggerBus != null)
        {
            triggerBus.onTaskAreaEnterExit += HandleTaskAreaEvent;
        }

        edge.OnEnteringAndExiting += HandleEdgeEvent;

        if (taskDescriptionText != null)
        {
            taskDescriptionText.text = "";
        }
        if (interactionPromptText != null)
        {
            interactionPromptText.text = "";
        }

        taskDescriptionText.text = gameStartMessage;
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

    void HandleEdgeEvent(bool isEnter)
    {
        if (isEnter)
        {
            if (interactionPromptText != null)
            {
                interactionPromptText.text = edgeMessage;
            }
        }
        else
        {
            if (interactionPromptText != null)
            {
                interactionPromptText.text = "";
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
            if (interactionPromptText != null && index != 0)
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

    void HandleGameOver()
    {
        if (taskDescriptionText != null)
        {
            taskDescriptionText.text = gameOverMessage;
        }
        if (interactionPromptText != null)
        {
            interactionPromptText.text = "";
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
