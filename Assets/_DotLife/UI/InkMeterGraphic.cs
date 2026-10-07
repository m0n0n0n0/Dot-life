using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class InkMeterGraphic : MaskableGraphic
{
    [SerializeField] private Color inkColor = new Color(0.1f, 0.15f, 0.19f, 1f);
    [SerializeField] private Color trackColor = new Color(0.95f, 0.96f, 0.98f, 0.95f);
    [SerializeField] private Color spentColor = new Color(0.48f, 0.64f, 0.69f, 1f);
    [SerializeField] private Color outlineColor = new Color(0.08f, 0.1f, 0.12f, 0.95f);
    [SerializeField] private Color emptyColor = new Color(0.95f, 0.25f, 0.23f, 1f);
    [SerializeField, Min(0f)] private float borderWidth = 1.5f;
    public float Fill { get; private set; }
    public float Trail { get; private set; }
    public float Pulse { get; private set; }

    public void SetValues(float fill, float trail, float pulse)
    {
        fill = Mathf.Clamp01(fill);
        trail = Mathf.Clamp(trail, fill, 1f);
        pulse = Mathf.Clamp01(pulse);
        if (Fill == fill && Trail == trail && Pulse == pulse) return;
        Fill = fill; Trail = trail; Pulse = pulse;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect outer = rectTransform.rect;
        DrawCapsule(vh, outer, Color.Lerp(outlineColor, emptyColor, Pulse));
        float inset = Mathf.Clamp(borderWidth, 0f, outer.width * 0.4f);
        Rect inner = Rect.MinMaxRect(outer.xMin + inset, outer.yMin + inset, outer.xMax - inset, outer.yMax - inset);
        DrawCapsule(vh, inner, trackColor);
        DrawCapsule(vh, new Rect(inner.x, inner.y, inner.width, inner.height * Trail), spentColor);
        DrawCapsule(vh, new Rect(inner.x, inner.y, inner.width, inner.height * Fill), inkColor);
    }

    private void DrawCapsule(VertexHelper vh, Rect rect, Color tint)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        int start = vh.currentVertCount;
        Color32 vertexColor = tint * color;
        vh.AddVert(rect.center, vertexColor, Vector2.zero);
        const int halfSegments = 12;
        for (int cap = 0; cap < 2; cap++)
        {
            Vector2 center = new Vector2(rect.center.x, cap == 0 ? rect.yMax - radius : rect.yMin + radius);
            for (int i = 0; i <= halfSegments; i++)
            {
                float angle = (cap + (float)i / halfSegments) * Mathf.PI;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, vertexColor, Vector2.zero);
            }
        }
        int count = (halfSegments + 1) * 2;
        for (int i = 0; i < count; i++) vh.AddTriangle(start, start + 1 + (i + 1) % count, start + 1 + i);
    }
}
