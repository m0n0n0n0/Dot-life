using UnityEngine;

public class ModelTransition : MonoBehaviour
{
    public MeshRenderer oldModelRenderer;  // 旧模型的Renderer（黑白/旧贴图）
    public MeshRenderer newModelRenderer;  // 新模型的Renderer（彩色/新贴图）
    public float duration = 5f;       // 过渡时间

    private float progress = 0f;
    private bool isTransitioning = false;

    void Start()
    {
        Debug.Log("Start函数执行了！");

        // 旧模型：完全不透明
        // 新模型：完全透明
        Color oldColor = oldModelRenderer.material.color;
        oldColor.a = 1f;
        oldModelRenderer.material.color = oldColor;

        Color newColor = newModelRenderer.material.color;
        newColor.a = 0f;
        newModelRenderer.material.color = newColor;

        // 5秒后开始渐变
        Invoke("StartTransition", 5f);
    }

    void Update()
    {
        if (isTransitioning)
        {
            progress += Time.deltaTime / duration;
            float t = Mathf.Clamp01(progress);

            // 旧模型透明度：1 → 0（慢慢消失）
            Color oldC = oldModelRenderer.material.color;
            oldC.a = Mathf.Lerp(1f, 0f, t);
            oldModelRenderer.material.color = oldC;

            // 新模型透明度：0 → 1（慢慢出现）
            Color newC = newModelRenderer.material.color;
            newC.a = Mathf.Lerp(0f, 1f, t);
            newModelRenderer.material.color = newC;

            if (progress >= 1f)
            {
                isTransitioning = false;
                Debug.Log("模型渐变完成！");
            }
        }
    }

    public void StartTransition()
    {
        Debug.Log("5秒到了！开始模型渐变...");
        progress = 0f;
        isTransitioning = true;
    }
}