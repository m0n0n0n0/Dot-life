using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UITaskTextDissolve : BaseMeshEffect
{
    private float progress;
    private readonly List<UIVertex> stream = new List<UIVertex>();

    public void SetProgress(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(value, progress)) return;
        progress = value;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || progress <= 0f) return;
        stream.Clear();
        vertices.GetUIVertexStream(stream);
        // Triangle streams also handle the duplicated geometry produced by the existing Outline effect.
        for (int quad = 0; quad + 5 < stream.Count; quad += 6)
        {
            UIVertex first = stream[quad];
            UIVertex opposite = stream[quad + 2];
            Vector3 center = (first.position + opposite.position) * 0.5f;
            float seed = Mathf.Repeat(Mathf.Sin(center.x * 0.013f + center.y * 0.007f) * 437.58f, 1f);
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - seed * 0.3f) / (1f - seed * 0.3f)));
            Vector3 drift = new Vector3((seed - 0.5f) * 18f, 12f + seed * 18f, 0f) * fade;
            for (int corner = 0; corner < 6; corner++)
            {
                UIVertex vertex = stream[quad + corner];
                vertex.position += drift;
                Color32 color = vertex.color;
                color.a = (byte)Mathf.RoundToInt(color.a * (1f - fade));
                vertex.color = color;
                stream[quad + corner] = vertex;
            }
        }
        vertices.Clear();
        vertices.AddUIVertexTriangleStream(stream);
    }
}
