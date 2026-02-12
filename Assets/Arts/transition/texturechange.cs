using UnityEngine;

public class TextureTransition : MonoBehaviour
{
    public MeshRenderer blackWhiteRenderer;  // 黑白模型的Renderer
    public MeshRenderer colorRenderer;      // 彩色模型的Renderer
    public float duration = 5f;       // 过渡时间

    private float progress = 0f;
    private bool isTransitioning = false;

    void Start()
    {
        Debug.Log("Start函数执行了！");

        // 黑白：完全不透明
        // 彩色：完全透明
        Color c = colorRenderer.material.color;
        c.a = 0f;
        colorRenderer.material.color = c;

        // 5秒后开始渐变
        Invoke("StartTransition", 5f);
    }

    void Update()
    {
        if (isTransitioning)
        {
            progress += Time.deltaTime / duration;

            // 只改彩色材质的透明度：从0到1
            Color c = colorRenderer.material.color;
            c.a = Mathf.Lerp(0f, 1f, progress);
            colorRenderer.material.color = c;

            if (progress >= 1f)
            {
                isTransitioning = false;
                Debug.Log("渐变完成！");
            }
        }
    }

    public void StartTransition()
    {
        Debug.Log("5秒到了！开始渐变...");
        progress = 0f;
        isTransitioning = true;
    }
}