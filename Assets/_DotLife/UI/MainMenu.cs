using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Scene Management")]
    [Tooltip("要加载的游戏场景名称（例如：desk）")]
    public string gameSceneName = "desk";

    /// <summary>
    /// 开始游戏按钮点击时调用
    /// </summary>
    public void StartGame()
    {
        // 确保游戏时间比例是正常的（防止在其他地方修改过Time.timeScale导致游戏卡住）
        Time.timeScale = 1f;
        
        if (!string.IsNullOrEmpty(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
        else
        {
            Debug.LogWarning("未设置游戏场景名称 (Game Scene Name)！请在 Inspector 中设置。");
        }
    }

    /// <summary>
    /// 退出游戏按钮点击时调用
    /// </summary>
    public void ExitGame()
    {
        Debug.Log("正在退出游戏...");
        
        // 在打包后的游戏中退出
        Application.Quit();

#if UNITY_EDITOR
        // 如果在 Unity 编辑器中运行，则停止播放模式
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
