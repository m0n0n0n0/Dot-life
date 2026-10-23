using UnityEngine;
using UnityEngine.UI;

// Runtime UI graphic shared by all four completion effects.
// Gathering fragments, light and trail use one mesh without allocating particle materials or textures.
[RequireComponent(typeof(CanvasRenderer))]
public class UITaskCompletionGlow : MaskableGraphic
{
    private readonly Vector2[] positions = new Vector2[16];
    private readonly float[] radii = new float[16];
    private readonly Color[] colors = new Color[16];
    private int count;
    private const int Segments = 24;

    public void Clear() => count = 0;

    public void AddLight(Vector2 position, float radius, Color tint, float alpha)
    {
        if (count >= positions.Length || alpha <= 0f) return;
        positions[count] = position;
        radii[count] = radius;
        tint.a *= Mathf.Clamp01(alpha);
        colors[count++] = tint;
    }

    public void RefreshMesh() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        for (int light = 0; light < count; light++)
        {
            int start = vertices.currentVertCount;
            Vector2 center = positions[light];
            Color tint = colors[light];
            Color core = Color.Lerp(tint, Color.white, 0.8f);
            core.a = tint.a;
            vertices.AddVert(center, core, Vector2.zero);
            for (int ring = 0; ring < 3; ring++)
            {
                float fraction = ring == 0 ? 0.22f : ring == 1 ? 0.55f : 1f;
                Color color = ring == 0 ? core : tint;
                color.a *= ring == 0 ? 1f : ring == 1 ? 0.25f : 0f;
                for (int segment = 0; segment < Segments; segment++)
                {
                    float angle = segment * Mathf.PI * 2f / Segments;
                    Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radii[light] * fraction;
                    vertices.AddVert(point, color, Vector2.zero);
                    int next = (segment + 1) % Segments;
                    int vertex = start + 1 + ring * Segments + segment;
                    int nextVertex = start + 1 + ring * Segments + next;
                    if (ring == 0) vertices.AddTriangle(start, vertex, nextVertex);
                    else
                    {
                        vertices.AddTriangle(vertex - Segments, vertex, nextVertex);
                        vertices.AddTriangle(vertex - Segments, nextVertex, nextVertex - Segments);
                    }
                }
            }
        }
    }
}
