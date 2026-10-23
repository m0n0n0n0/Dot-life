using System;
using UnityEngine;

// GPU-only composition; source textures remain read-only and the authored material stays unchanged.
public sealed class TaskInkComposite : IDisposable
{
    private Material material;
    private RenderTexture current;
    private RenderTexture scratch;
    public Texture Texture => current;
    public bool IsReady => material != null && current != null && scratch != null
        && current.IsCreated() && scratch.IsCreated();

    public TaskInkComposite(Texture baseline, Shader shader)
    {
        if (baseline == null || shader == null || !shader.isSupported) return;
        material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
            ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
        current = CreateTarget(baseline, format, "Task Ink A");
        scratch = CreateTarget(baseline, format, "Task Ink B");
        if (!IsReady) { Dispose(); return; }
        RenderTexture previous = RenderTexture.active;
        try { Graphics.Blit(baseline, current); }
        finally { RenderTexture.active = previous; }
    }

    private static RenderTexture CreateTarget(Texture baseline, RenderTextureFormat format, string name)
    {
        var result = new RenderTexture(baseline.width, baseline.height, 0, format, RenderTextureReadWrite.Linear)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = baseline.filterMode,
            wrapMode = baseline.wrapMode,
            anisoLevel = baseline.anisoLevel,
            useMipMap = true,
            autoGenerateMips = true
        };
        result.Create();
        return result;
    }

    public void Apply(Texture before, Texture after)
    {
        material.SetTexture("_BeforeTex", before);
        material.SetTexture("_AfterTex", after);
        RenderTexture previous = RenderTexture.active;
        try { Graphics.Blit(current, scratch, material); }
        finally { RenderTexture.active = previous; }
        RenderTexture swap = current;
        current = scratch;
        scratch = swap;
    }

    public void Dispose()
    {
        if (current != null) { current.Release(); UnityEngine.Object.Destroy(current); }
        if (scratch != null) { scratch.Release(); UnityEngine.Object.Destroy(scratch); }
        if (material != null) UnityEngine.Object.Destroy(material);
        current = scratch = null;
        material = null;
    }
}
