using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

[InitializeOnLoad]
public static class DotLifeInkSceneProbe
{
    private const string Key = "DotLife.InkSceneProbe";
    private static IEnumerator cases;
    private static int frame = -1;
    private static double started;
    private static bool finishing;
    private static bool passed;
    private static string result;

    static DotLifeInkSceneProbe() { if (SessionState.GetBool(Key, false)) Attach(); }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this probe in an isolated batch project.");
        SessionState.SetBool(Key, true);
        Attach();
        EditorSceneManager.OpenScene("Assets/_DotLife/Scenes/desk.unity");
        EditorApplication.isPlaying = true;
    }

    private static void Attach()
    {
        EditorApplication.playModeStateChanged -= OnPlay;
        EditorApplication.playModeStateChanged += OnPlay;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void OnPlay(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            finishing = false;
            started = EditorApplication.timeSinceStartup;
            cases = Probe();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlay;
            Debug.Log("INK_SCENE_PROBE " + (passed ? "PASS " : "FAIL ") + result);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || finishing || cases == null || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 240d) throw new TimeoutException("Ink scene probe timed out.");
            if (!cases.MoveNext()) Finish(true, "Real collider contact, animated refill, UI projection and diagnostic routes checked.");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Finish(bool success, string message)
    {
        passed = success;
        result = message;
        finishing = true;
        EditorApplication.isPlaying = false;
    }

    private static IEnumerator Probe()
    {
        yield return null;
        GameState game = UnityEngine.Object.FindObjectOfType<GameState>();
        BasicBehaviourScript motor = UnityEngine.Object.FindObjectOfType<BasicBehaviourScript>();
        InteractingHandler interactor = motor.GetComponent<InteractingHandler>();
        PlayerInk ink = interactor.Ink;
        CharacterController body = motor.Controller;
        InkWell well = UnityEngine.Object.FindObjectOfType<InkWell>();
        TriggerBus bus = UnityEngine.Object.FindObjectOfType<TriggerBus>();
        CameraFollowingScript camera = motor.GetComponent<CameraFollowingScript>();
        UIPlayerInk meter = UnityEngine.Object.FindObjectOfType<UIPlayerInk>();
        game.TryStartGame();
        interactor.enabled = false;
        // Resource-only setup; the full regression separately completes the actual diary points.
        while (ink.CanSpend) Require(ink.TryCommitStamp(() => true), "Drain tutorial budget for physical scene probe.");
        game.UpdateTasks((int)DemoTaskId.Diary);
        BoxCollider zone = well.GetComponent<BoxCollider>();
        Physics.SyncTransforms();
        Debug.Log("INK_ZONE world=" + zone.bounds + "; model=" + well.GetComponentInChildren<Renderer>().bounds);
        Bounds bounds = new Bounds(zone.bounds.center, Vector3.one * 200f);
        var sources = new List<NavMeshBuildSource>();
        UnityEngine.AI.NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
            new List<NavMeshBuildMarkup>(), sources);
        sources.RemoveAll(source => source.component != null && source.component.transform.IsChildOf(motor.transform));
        NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
        settings.agentRadius = body.radius + body.skinWidth;
        settings.agentHeight = body.height;
        settings.agentClimb = motor.maxStepHeight;
        settings.agentSlope = motor.maxSlopeAngle;
        settings.overrideVoxelSize = true;
        settings.voxelSize = 0.05f;
        NavMeshData data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        Require(data != null, "Diagnostic navigation data exists.");
        NavMeshDataInstance instance = NavMesh.AddNavMeshData(data);
        var destinations = new List<Vector3>();
        float footOffset = motor.transform.position.y - body.bounds.min.y;
        try
        {
            for (int x = -8; x <= 8; x++)
                for (int z = -8; z <= 8; z++)
                {
                    Vector3 origin = zone.bounds.center + new Vector3(x * 0.5f, 6f, z * 0.5f);
                    if (!Physics.Raycast(origin, Vector3.down, out RaycastHit floor, 12f, motor.groundLayer, QueryTriggerInteraction.Ignore)) continue;
                    if (!NavMesh.SamplePosition(floor.point, out NavMeshHit nav, 0.3f, NavMesh.AllAreas)) continue;
                    Vector3 center = nav.position + Vector3.up * (body.height * 0.5f);
                    if (Vector3.Distance(zone.ClosestPoint(center), center) > 0.001f) continue;
                    Vector3 local = zone.transform.InverseTransformPoint(center) - zone.center;
                    Vector3 scale = zone.transform.lossyScale;
                    float edgeMargin = Mathf.Min((zone.size.x * 0.5f - Mathf.Abs(local.x)) * Mathf.Abs(scale.x),
                        (zone.size.z * 0.5f - Mathf.Abs(local.z)) * Mathf.Abs(scale.z));
                    if (edgeMargin < 0.4f) continue;
                    destinations.Add(nav.position);
                }
            Require(destinations.Count > 0, "A standable point overlaps the real ink-well trigger.");
            var actual = new List<Vector3>();
            foreach (Vector3 candidate in destinations)
            {
                motor.Teleport(candidate + Vector3.up * (footOffset + 0.02f));
                for (int i = 0; i < 12; i++) { motor.TickMovement(0.02f); if (i % 4 == 0) yield return null; }
                float contactTime = Time.fixedTime + Time.fixedDeltaTime * 2f;
                while (Time.fixedTime < contactTime) yield return null;
                if (well.IsInRange(motor) && motor.CanInteract)
                    actual.Add(new Vector3(body.bounds.center.x, body.bounds.min.y, body.bounds.center.z));
            }
            Debug.Log("INK_CONTACT_CANDIDATES actual=" + actual.Count + "/" + destinations.Count + "; lastBody=" + body.bounds);
            Require(actual.Count > 0, "At least one candidate has real native trigger contact after settling.");
            destinations = actual;
            Vector3 refillFeet = destinations[0];
            int routes = 0;
            foreach (EdgeFalling edge in bus.edgeTriggers)
            {
                motor.Teleport(edge.endPoint);
                motor.SetMoveInput(Vector2.zero);
                for (int i = 0; i < 120; i++)
                {
                    motor.TickMovement(0.02f);
                    if (i % 4 == 0) yield return null;
                }
                Vector3 feet = new Vector3(body.bounds.center.x, body.bounds.min.y, body.bounds.center.z);
                Debug.Log("INK_LANDING " + edge.name + " authored=" + edge.endPoint + " settled=" + motor.transform.position
                    + " ground=" + (motor.GroundContact.collider != null ? motor.GroundContact.collider.name : "none"));
                if (!NavMesh.SamplePosition(feet, out NavMeshHit start, 1f, NavMesh.AllAreas))
                { Debug.LogWarning("INK_ROUTE UNVERIFIED " + edge.name + " no navigation surface near authored landing."); continue; }
                NavMeshPath best = null;
                float length = float.PositiveInfinity;
                foreach (Vector3 destination in destinations)
                {
                    var path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(start.position, destination, NavMesh.AllAreas, path)
                        || path.status != NavMeshPathStatus.PathComplete) continue;
                    float distance = 0f;
                    for (int i = 1; i < path.corners.Length; i++) distance += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                    if (distance >= length) continue;
                    best = path;
                    length = distance;
                }
                if (best == null)
                {
                    // Navigation does not represent walking off a low book edge; test that drop with the real motor.
                    foreach (float radius in new[] { 1f, 2f, 3f, 4f })
                        for (int direction = 0; direction < 16; direction++)
                        {
                            float angle = direction * Mathf.PI / 8f;
                            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                            if (!NavMesh.SamplePosition(feet + offset, out NavMeshHit escape, 0.5f, NavMesh.AllAreas)
                                || escape.position.y > feet.y + 0.05f || escape.position.y < feet.y - 1f) continue;
                            foreach (Vector3 destination in destinations)
                            {
                                var path = new NavMeshPath();
                                if (!NavMesh.CalculatePath(escape.position, destination, NavMesh.AllAreas, path)
                                    || path.status != NavMeshPathStatus.PathComplete) continue;
                                float distance = Vector3.Distance(feet, escape.position);
                                for (int i = 1; i < path.corners.Length; i++) distance += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                                if (distance >= length) continue;
                                best = path;
                                length = distance;
                            }
                        }
                    if (best == null)
                    { Debug.LogWarning("INK_ROUTE UNVERIFIED " + edge.name + " has no complete diagnostic path, including a low-edge exit."); continue; }
                }
                Debug.Log("INK_PATH " + edge.name + " corners=" + string.Join(";", Array.ConvertAll(best.corners, corner => corner.ToString())));
                bool walked = true;
                foreach (Vector3 corner in best.corners)
                {
                    if (well.IsInRange(motor) && motor.CanInteract) break;
                    int ticks = 0;
                    while (!(well.IsInRange(motor) && motor.CanInteract)
                        && Vector3.ProjectOnPlane(corner - motor.transform.position, Vector3.up).magnitude > 0.15f && ticks++ < 600)
                    {
                        Vector3 direction = Vector3.ProjectOnPlane(corner - motor.transform.position, Vector3.up).normalized;
                        Vector3 forward = Vector3.ProjectOnPlane(motor.movementCamera.forward, Vector3.up).normalized;
                        Vector3 right = Vector3.Cross(Vector3.up, forward);
                        float arrival = Mathf.Clamp01(Vector3.ProjectOnPlane(corner - motor.transform.position, Vector3.up).magnitude / 0.75f);
                        motor.SetMoveInput(new Vector2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward)) * arrival);
                        motor.TickMovement(0.02f);
                        if (ticks % 4 == 0) yield return null;
                    }
                    if (ticks >= 600) { walked = false; break; }
                }
                motor.SetMoveInput(Vector2.zero);
                for (int i = 0; i < 12; i++) { motor.TickMovement(0.02f); yield return null; }
                float routeContactTime = Time.fixedTime + Time.fixedDeltaTime * 2f;
                while (Time.fixedTime < routeContactTime) yield return null;
                if (walked && well.IsInRange(motor) && motor.CanInteract)
                {
                    routes++;
                    refillFeet = new Vector3(body.bounds.center.x, body.bounds.min.y, body.bounds.center.z);
                    Debug.Log("INK_ROUTE WALK_PASS " + edge.name + " length=" + length + " corners=" + best.corners.Length);
                }
                else Debug.LogWarning("INK_ROUTE UNVERIFIED " + edge.name + " diagnostic path exists but native controller did not finish; position="
                    + motor.transform.position + "; range=" + well.IsInRange(motor) + "; grounded=" + motor.IsGrounded);
            }
            Debug.Log("INK_ROUTE_SUMMARY native walks=" + routes + "/" + bus.edgeTriggers.Count
                + "; standable refill points=" + destinations.Count + "; no scene geometry modified.");
            Require(routes == bus.edgeTriggers.Count, "Every authored landing has a native walk into a stable refill stance.");
            motor.Teleport(refillFeet + Vector3.up * (footOffset + 0.02f));
            for (int i = 0; i < 12; i++) { motor.TickMovement(0.02f); yield return null; }
            float refillContactTime = Time.fixedTime + Time.fixedDeltaTime * 2f;
            while (Time.fixedTime < refillContactTime) yield return null;
            Require(well.IsInRange(motor) && motor.CanInteract, "Real physics contact allows refill at the ink well.");
            interactor.enabled = true;
            interactor.SendMessage("OnApplicationFocus", true);
            interactor.RefreshSelection();
            Require(interactor.CurrentTarget == well, "Physical overlap selects ink well.");
            Require(well.TryInteract(interactor), "Real ink-well contact starts animated refill.");
            float deadline = Time.time + 6f;
            while ((!ink.HasDipped || !motor.CanMove) && Time.time < deadline) yield return null;
            Require(ink.HasDipped && ink.CurrentInk == ink.MaxInk && motor.CanMove, "Real contact refill completes.");
            for (int i = 0; i < 100; i++) camera.TickCamera(0.02f);
            Require(ink.TryCommitStamp(() => true), "Resource-only spend for UI visual probe.");
            meter.TickDisplay(0f);
            Require(meter.GetComponent<CanvasGroup>().alpha > 0f, "Spent meter is visible.");
            InkMeterGraphic graphic = meter.GetComponent<InkMeterGraphic>();
            Require(graphic.Trail > graphic.Fill && Mathf.Abs(graphic.Fill - ink.NormalizedInk) < 0.0001f,
                "Spent meter displays a reduction trail.");
            RectTransform rect = meter.GetComponent<RectTransform>();
            Vector2 size = rect.sizeDelta;
            Vector2 position = rect.anchoredPosition;
            camera.followCamera.transform.position += camera.followCamera.transform.right * 0.5f;
            meter.TickDisplay(0f);
            Require(Vector2.Distance(position, rect.anchoredPosition) > 0.1f && Mathf.Abs(rect.sizeDelta.x - size.x) < 0.001f,
                "Meter follows projection without changing its width.");
            camera.TickCamera(1f);
            meter.TickDisplay(0.4f);
            Require(graphic.Trail < 1f && graphic.Trail >= graphic.Fill, "Reduction trail catches up after hold.");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                ink.TryRefill();
                ink.TryCommitStamp(() => true);
                meter.TickDisplay(0f);
                Canvas.ForceUpdateCanvases();
                Directory.CreateDirectory("Logs");
                CaptureView(camera.followCamera, meter);
                Debug.Log("INK_SCREENSHOT Logs/B1InkMeter.png screen=" + Screen.width + "x" + Screen.height
                    + " meterPosition=" + rect.anchoredPosition + " meterSize=" + rect.sizeDelta);
            }
            meter.TickDisplay(2f);
            Require(meter.GetComponent<CanvasGroup>().alpha == 0f && ink.CurrentInk < ink.MaxInk,
                "A partial resource returns to hidden idle state.");
        }
        finally { instance.Remove(); UnityEngine.Object.DestroyImmediate(data); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CaptureView(Camera camera, UIPlayerInk meter)
    {
        Canvas canvas = meter.GetComponentInParent<Canvas>();
        RenderMode mode = canvas.renderMode;
        Camera uiCamera = canvas.worldCamera;
        float distance = canvas.planeDistance;
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        var target = new RenderTexture(Screen.width, Screen.height, 24);
        var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 0.1f;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            meter.TickDisplay(0f);
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            texture.Apply();
            Color32[] pixels = texture.GetPixels32();
            int nonblank = 0;
            foreach (Color32 pixel in pixels) if (pixel.r > 20 || pixel.g > 20 || pixel.b > 20) nonblank++;
            Require(nonblank > pixels.Length / 10, "Rendered probe is not blank.");
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "B1InkMeter.png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Debug.Log("INK_RENDER nonblankPixels=" + nonblank + "/" + pixels.Length + " path=" + path);
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            canvas.renderMode = mode;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = distance;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(texture);
            Canvas.ForceUpdateCanvases();
        }
    }
}
