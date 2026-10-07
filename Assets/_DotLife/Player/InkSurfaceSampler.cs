using System;
using System.Collections.Generic;
using UnityEngine;

public struct InkStampPlacement
{
    public Collider receiver;
    public Vector3 point;
    public Vector3 normal;
    public Vector3[] vertices;
    public Vector3[] normals;
    public Vector2[] uv;
    public int[] triangles;
}

public sealed class InkSurfaceSampler
{
    private readonly BasicBehaviourScript motor;
    private readonly TriggerBus bus;
    private readonly InkStampSettings settings;
    private RaycastHit[] hits = new RaycastHit[16];
    private Vector3[] vertices;
    private Vector3[] normals;
    private Vector2[] uv;
    private int[] triangles;
    private Collider visualReceiver;
    private Renderer[] visualRenderers;

    public InkSurfaceSampler(BasicBehaviourScript player, TriggerBus triggers, InkStampSettings configuration)
    { motor = player; bus = triggers; settings = configuration; }

    public bool TryCenter(out RaycastHit contact)
    {
        contact = default;
        if (motor == null || settings == null || motor.Controller == null || !motor.Controller.enabled
            || !motor.IsGrounded || !motor.isActiveAndEnabled) return false;
        Bounds body = motor.Controller.bounds;
        Vector3 foot = new Vector3(body.center.x, body.min.y, body.center.z);
        float offset = Mathf.Max(0.01f, motor.groundSnapOffset + motor.Controller.skinWidth);
        return CastNearest(foot + Vector3.up * offset, Vector3.down, offset + motor.groundRayLength, out contact)
            && IsAllowed(contact);
    }

    public bool TryBuild(out InkStampPlacement placement)
    {
        placement = default;
        if (!TryCenter(out RaycastHit center)) return false;
        int resolution = settings.Resolution;
        EnsureBuffers(resolution);
        float diameter = Mathf.Clamp(settings.diameter, 0.02f, 1f);
        float depth = Mathf.Clamp(settings.sampleDepth, 0.005f, 0.15f);
        Vector3 forward = Vector3.ProjectOnPlane(motor.transform.forward, center.normal).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(Vector3.forward, center.normal).normalized;
        Vector3 right = Vector3.Cross(center.normal, forward).normalized;
        Transform receiver = center.collider.transform;
        InkStampSurface rule = center.collider.GetComponentInParent<InkStampSurface>();
        float offset = Mathf.Clamp(settings.surfaceOffset + (rule != null ? rule.additionalOffset : 0f), 0.0001f, 0.04f);
        Matrix4x4 toLocal = receiver.worldToLocalMatrix;
        Matrix4x4 normalToLocal = receiver.localToWorldMatrix.transpose;
        for (int row = 0; row <= resolution; row++)
            for (int column = 0; column <= resolution; column++)
            {
                int index = row * (resolution + 1) + column;
                Vector2 coordinate = new Vector2((float)column / resolution, (float)row / resolution);
                Vector3 plane = center.point + right * ((coordinate.x - 0.5f) * diameter)
                    + forward * ((coordinate.y - 0.5f) * diameter);
                if (!Sample(plane, center, depth, out RaycastHit hit)) return false;
                vertices[index] = toLocal.MultiplyPoint3x4(hit.point + hit.normal * offset);
                normals[index] = normalToLocal.MultiplyVector(hit.normal).normalized;
                uv[index] = coordinate;
            }
        // Check triangle centers as well as vertices to catch more gaps and upper-surface boundaries.
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 centroid = (receiver.TransformPoint(vertices[triangles[i]])
                + receiver.TransformPoint(vertices[triangles[i + 1]]) + receiver.TransformPoint(vertices[triangles[i + 2]])) / 3f;
            if (!Sample(centroid, center, depth, out _)) return false;
        }
        placement = new InkStampPlacement { receiver = center.collider, point = center.point, normal = center.normal,
            vertices = vertices, normals = normals, uv = uv, triangles = triangles };
        return true;
    }

    private bool Sample(Vector3 plane, RaycastHit center, float depth, out RaycastHit hit) =>
        CastNearest(plane + center.normal * depth, -center.normal, depth * 2f, out hit)
        && hit.collider == center.collider && IsAllowed(hit)
        && Vector3.Angle(hit.normal, center.normal) <= Mathf.Clamp(settings.normalTolerance, 1f, 60f);

    private bool IsAllowed(RaycastHit hit)
    {
        if (hit.collider == null || (settings.surfaceLayers.value & (1 << hit.collider.gameObject.layer)) == 0
            || Vector3.Angle(hit.normal, Vector3.up) > motor.maxSlopeAngle) return false;
        InkStampSurface rule = hit.collider.GetComponentInParent<InkStampSurface>();
        if (rule != null && (!rule.isActiveAndEnabled || !rule.allowStamping)) return false;
        if (!HasVisualSurface(hit)) return false;
        if (bus == null) return false;
        foreach (DotDrawing dot in bus.dotTriggers)
            if (dot != null && dot.Owner != null && !dot.Owner.IsComplete && dot.IsInRange(motor)) return false;
        foreach (DotDrawingHandler task in bus.taskTriggers)
        {
            if (task == null || task.IsComplete) continue;
            if (hit.collider.transform.IsChildOf(task.transform)) return false;
            Renderer target = task.GetComponent<Renderer>();
            if (target == null) continue;
            Bounds bounds = target.bounds;
            bounds.Expand(0.06f);
            if (bounds.Contains(hit.point)) return false;
        }
        return true;
    }

    private bool HasVisualSurface(RaycastHit hit)
    {
        if (visualReceiver != hit.collider || visualRenderers == null)
        {
            visualReceiver = hit.collider;
            var originals = new List<Renderer>();
            foreach (Renderer candidate in hit.collider.GetComponentsInChildren<Renderer>(true))
                if (candidate.GetComponent<InkMark>() == null) originals.Add(candidate);
            visualRenderers = originals.ToArray();
        }
        foreach (Renderer renderer in visualRenderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            Bounds bounds = renderer.bounds;
            bounds.Expand(0.08f);
            if (bounds.Contains(hit.point)) return true;
        }
        return false;
    }

    private bool CastNearest(Vector3 origin, Vector3 direction, float length, out RaycastHit closest)
    {
        closest = default;
        int count;
        do
        {
            count = Physics.RaycastNonAlloc(origin, direction, hits, length,
                motor.groundLayer.value, QueryTriggerInteraction.Ignore);
            if (count < hits.Length) break;
            if (hits.Length >= 256) return false;
            Array.Resize(ref hits, hits.Length * 2);
        } while (true);
        float distance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider == null || hits[i].collider.transform.IsChildOf(motor.transform)
                || hits[i].distance >= distance) continue;
            closest = hits[i];
            distance = hits[i].distance;
        }
        return distance < float.PositiveInfinity;
    }

    private void EnsureBuffers(int resolution)
    {
        int count = (resolution + 1) * (resolution + 1);
        if (vertices != null && vertices.Length == count) return;
        vertices = new Vector3[count];
        normals = new Vector3[count];
        uv = new Vector2[count];
        triangles = new int[resolution * resolution * 6];
        int index = 0;
        for (int row = 0; row < resolution; row++)
            for (int column = 0; column < resolution; column++)
            {
                int a = row * (resolution + 1) + column;
                int b = a + 1;
                int c = a + resolution + 1;
                int d = c + 1;
                triangles[index++] = a; triangles[index++] = c; triangles[index++] = b;
                triangles[index++] = b; triangles[index++] = c; triangles[index++] = d;
            }
    }
}
