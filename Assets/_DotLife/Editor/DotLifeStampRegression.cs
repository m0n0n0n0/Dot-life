using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class DotLifeStampRegression
{
    private const string Key = "DotLife.StampRegression";
    private static IEnumerator cases;
    private static int frame = -1, checks;
    private static double started;
    private static bool finishing, passed;
    private static string result;
    private static BasicBehaviourScript motor;
    private static InteractingHandler interactor;
    private static PlayerInk ink;
    private static InkStamping stamping;
    private static float footOffset;
    private static readonly MethodInfo CreateMark = typeof(InkStamping).GetMethod("CreateMark", BindingFlags.NonPublic | BindingFlags.Instance);

    static DotLifeStampRegression() { if (SessionState.GetBool(Key, false)) Attach(); }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project for this probe.");
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
            checks = 0; frame = -1; finishing = false;
            started = EditorApplication.timeSinceStartup;
            Application.logMessageReceived += OnLog;
            cases = Cases();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlay;
            Debug.Log("STAMP_REGRESSION " + (passed ? "PASS " : "FAIL ") + result);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || finishing || cases == null || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 240d) throw new TimeoutException("Stamp regression timeout.");
            if (!cases.MoveNext()) Finish(true, checks + " checks; native contact, surface geometry, task priority, cancellation, pool and URP render.");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack);
    }
    private static void Finish(bool success, string message)
    {
        if (finishing) return;
        passed = success; result = message; finishing = true;
        Application.logMessageReceived -= OnLog;
        EditorApplication.isPlaying = false;
    }
    private static IEnumerator Cases()
    {
        yield return null;
        var game = UnityEngine.Object.FindObjectOfType<GameState>();
        var bus = UnityEngine.Object.FindObjectOfType<TriggerBus>();
        motor = UnityEngine.Object.FindObjectOfType<BasicBehaviourScript>();
        interactor = motor.GetComponent<InteractingHandler>();
        interactor.SendMessage("OnApplicationFocus", true);
        ink = interactor.Ink; stamping = interactor.Stamping;
        Check(stamping != null && stamping.Settings != null && stamping.Settings.material != null, "Scene references present.");
        Check(!stamping.CanOfferStamp && !interactor.TryInteract(), "Waiting cannot stamp.");
        Check(game.TryStartGame(), "Start game.");
        footOffset = motor.transform.position.y - motor.Controller.bounds.min.y;
        GameObject floor = Pad("StampTest_Floor", new Vector3(200f, 20f, 200f), new Vector3(12f, 1f, 12f));
        Stand(floor.GetComponent<Collider>(), 200f, 200f);
        Check(!stamping.CanOfferStamp, "Tutorial has no free stamp fallback.");
        // Geometry tests use resource-only setup; CoreRegression independently plays the full nine-point diary.
        while (ink.CanSpend) Check(ink.TryCommitStamp(() => true), "Drain initial tutorial budget.");
        game.UpdateTasks((int)DemoTaskId.Diary);
        Check(stamping.CanOfferStamp && !interactor.TryInteract() && stamping.ActiveMarkCount == 0 && ink.CurrentInk == 0f,
            "Empty free stamp gives feedback but does not create a mark.");
        Check(ink.TryRefill(), "Resource setup refill.");
        Check(stamping.TrySamplePlacement(out InkStampPlacement flat) && flat.receiver == floor.GetComponent<Collider>(), "Flat receiver selected.");
        ValidatePlacement(flat);
        float before = ink.CurrentInk;
        motor.SetMoveInput(Vector2.right);
        for (int i = 0; i < 8; i++) motor.TickMovement(0.02f);
        motor.SetMoveInput(Vector2.zero);
        Check(stamping.ActiveMarkCount == 0 && ink.CurrentInk == before, "Walking makes no marks and spends no ink.");
        Stand(floor.GetComponent<Collider>(), 200f, 200f);
        Check(interactor.TryInteract(), "Unified E dispatch starts free Tread action.");
        Check(stamping.ActiveMarkCount == 0 && ink.CurrentInk == before && !interactor.TryInteract(), "Contact deferred; repeated input rejected.");
        double deadline = EditorApplication.timeSinceStartup + 6d;
        while (motor.ActionState == PlayerActionState.Interacting && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(motor.CanInteract && stamping.ActiveMarkCount == 1 && Mathf.Approximately(ink.CurrentInk, before - ink.StampCost),
            "Real Animator contact commits exactly once and restores control.");
        Time.timeScale = 0f;
        Check(!interactor.TryInteract(), "Pause rejects new stamp input.");
        Time.timeScale = 1f;
        interactor.SendMessage("OnApplicationFocus", false);
        Check(!interactor.TryInteract(), "Lost focus rejects new stamp input.");
        interactor.SendMessage("OnApplicationFocus", true);
        interactor.CommitAction();
        Check(stamping.ActiveMarkCount == 1, "Late contact does not duplicate mark.");
        InkMark first = UnityEngine.Object.FindObjectOfType<InkMark>();
        Check(first.Geometry.vertexCount == 49 && first.Geometry.triangles.Length == 216
            && first.GetComponent<Collider>() == null && first.GetComponent<Renderer>().sharedMaterial == stamping.Settings.material,
            "Small shared-material mesh; no mark collider.");
        before = ink.CurrentInk;
        Check(interactor.TryInteract(), "Cancellation action starts.");
        interactor.CancelAction();
        Check(ink.CurrentInk == before && stamping.ActiveMarkCount == 1 && motor.CanInteract, "Cancel before contact does not spend.");
        Check(interactor.TryInteract(), "Disable case starts.");
        stamping.enabled = false;
        interactor.CommitAction();
        Check(motor.CanInteract && ink.CurrentInk == before && stamping.ActiveMarkCount == 1, "Disabled stamp component cancels only its pending commit.");
        stamping.enabled = true;
        Check(interactor.TryInteract(), "Drift case starts.");
        motor.Teleport(motor.transform.position + Vector3.right * 0.3f);
        interactor.CommitAction();
        Check(ink.CurrentInk == before && stamping.ActiveMarkCount == 1 && motor.CanInteract, "Displacement cancels pre-contact stamp.");
        Stand(floor.GetComponent<Collider>(), 200f, 200f);
        Check(interactor.TryInteract(), "Invalidated footprint case starts.");
        var blocker = Pad("StampTest_Blocker", new Vector3(200.12f, 20.525f, 200f), new Vector3(0.04f, 0.05f, 0.4f));
        Physics.SyncTransforms();
        interactor.CommitAction();
        Check(ink.CurrentInk == before && stamping.ActiveMarkCount == 1 && motor.CanInteract, "Whole footprint rechecked at commit; new upper object costs nothing.");
        UnityEngine.Object.Destroy(blocker);
        yield return null;

        var paper = Pad("StampTest_Paper", new Vector3(202f, 20.525f, 200f), new Vector3(1.2f, 0.05f, 1.2f));
        Stand(paper.GetComponent<Collider>(), 202f, 200f);
        Check(stamping.TrySamplePlacement(out InkStampPlacement upper) && upper.receiver == paper.GetComponent<Collider>(), "Thin upper sheet wins over table.");
        ValidatePlacement(upper);
        Stand(paper.GetComponent<Collider>(), 202.52f, 200f);
        int rejected = 0;
        stamping.OnPlacementRejected += () => rejected++;
        Check(!stamping.TrySamplePlacement(out _) && !interactor.TryInteract() && ink.CurrentInk == before,
            "Footprint crossing paper edge rejected without painting lower table.");
        Check(rejected == 1, "Invalid footprint publishes one presentation event.");
        var hud = UnityEngine.Object.FindObjectOfType<TaskStatusBar>();
        hud.SendMessage("LateUpdate");
        var prompt = (UnityEngine.UI.Text)typeof(TaskStatusBar).GetField("interactionPromptText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hud);
        Check(prompt.text.Contains("无法"), "Invalid footprint has visible feedback.");
        typeof(TaskStatusBar).GetField("stampBlockedUntil", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hud, -1f);
        hud.SendMessage("LateUpdate");
        Check(prompt.text.Contains("E") && !prompt.text.Contains("无法"), "Feedback expires back to normal prompt.");
        Stand(paper.GetComponent<Collider>(), 202f, 200f);
        var rule = paper.AddComponent<InkStampSurface>();
        rule.allowStamping = false;
        Check(!stamping.TrySamplePlacement(out _) && !interactor.TryInteract(), "Forbidden upper receiver blocks lower receiver.");
        rule.allowStamping = true;
        paper.layer = 2;
        LayerMask originalLayers = stamping.Settings.surfaceLayers;
        stamping.Settings.surfaceLayers = ~(1 << 2);
        Check(!stamping.TrySamplePlacement(out _), "Excluded upper layer cannot be painted through.");
        stamping.Settings.surfaceLayers = originalLayers;
        paper.layer = 0;
        var gap = Pad("StampTest_Narrow", new Vector3(204f, 21f, 200f), new Vector3(0.15f, 0.2f, 1f));
        Stand(gap.GetComponent<Collider>(), 204f, 200f);
        Check(motor.IsGrounded && !stamping.TrySamplePlacement(out _), "Narrow support cannot hold a complete stamp.");
        motor.Teleport(new Vector3(210f, 24f, 200f));
        Check(!stamping.CanOfferStamp && !stamping.TrySamplePlacement(out _) && !interactor.TryInteract(), "No stamp in midair.");

        var slope = Pad("StampTest_Slope", new Vector3(220f, 20f, 200f), new Vector3(4f, 1f, 4f));
        slope.transform.rotation = Quaternion.Euler(0f, 15f, 20f);
        Stand(slope.GetComponent<Collider>(), 220f, 200f);
        Check(stamping.TrySamplePlacement(out InkStampPlacement angled), "Sloping standable face can be sampled.");
        Check(Vector3.Angle(angled.normal, Vector3.up) > 15f, "Slope uses real face normal.");
        ValidatePlacement(angled);
        CommitGeometry(angled);
        var movingParent = new GameObject("StampTest_MovingParent");
        movingParent.transform.position = new Vector3(230f, 20f, 200f);
        movingParent.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
        movingParent.transform.localScale = new Vector3(1.7f, 0.8f, 1.2f);
        var moving = Pad("StampTest_Moving", Vector3.zero, new Vector3(3f, 1f, 3f));
        moving.transform.SetParent(movingParent.transform, false);
        Stand(moving.GetComponent<Collider>(), 230f, 200f);
        Check(stamping.TrySamplePlacement(out InkStampPlacement scaled), "Nonuniform-scale hierarchy sampled.");
        ValidatePlacement(scaled);
        CommitGeometry(scaled);
        InkMark attached = Array.Find(UnityEngine.Object.FindObjectsOfType<InkMark>(), m => m.transform.parent == moving.transform);
        Vector3 local = attached.Geometry.vertices[24];
        movingParent.transform.position += new Vector3(1f, 2f, 0f);
        movingParent.transform.rotation *= Quaternion.Euler(0f, 30f, 0f);
        movingParent.transform.localScale = new Vector3(1.1f, 1.3f, 0.9f);
        Check(Vector3.Distance(attached.transform.TransformPoint(local), moving.transform.TransformPoint(local)) < 0.0001f,
            "Mark follows translating, rotating, rescaling receiver without world-size parenting error.");
        movingParent.transform.localScale = Vector3.one;

        // Real task surface and range still belong to the original ordered task logic.
        DotDrawingHandler math = bus.taskTriggers.Find(t => t.TaskId == DemoTaskId.MathBook);
        DotDrawing mathFirst = bus.dotTriggers.Find(d => d.Owner == math && d.dotIndex == 0);
        if (mathFirst == null) throw new InvalidOperationException("Missing first math dot.");
        var taskCollider = math.GetComponent<Collider>();
        if (taskCollider == null) taskCollider = math.GetComponentInChildren<Collider>();
        Bounds taskBounds = math.GetComponent<Renderer>().bounds;
        bool protectedSurface = false;
        for (int x = -2; x <= 2 && !protectedSurface; x++) for (int z = -2; z <= 2 && !protectedSurface; z++)
        {
            Vector3 p = taskBounds.center + new Vector3(taskBounds.extents.x * x / 3f, 0f, taskBounds.extents.z * z / 3f);
            if (!Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out RaycastHit taskHit, 5f, motor.groundLayer, QueryTriggerInteraction.Ignore)) continue;
            Stand(taskHit.collider, p.x, p.z);
            if (!motor.CanInteract) continue;
            protectedSurface = !stamping.TrySamplePlacement(out _);
        }
        Check(protectedSurface && !math.IsComplete, "Unfinished authored task surface protected against free ink.");
        var protectedPad = Pad("StampTest_TaskReceiver", new Vector3(240f, 20f, 200f), new Vector3(4f, 1f, 4f));
        protectedPad.transform.SetParent(math.transform, true);
        Stand(protectedPad.GetComponent<Collider>(), 240f, 200f);
        Check(!stamping.CanOfferStamp, "Unfinished receiver hierarchy remains reserved for its task.");
        FieldInfo completedField = typeof(DotDrawingHandler).GetField("<IsComplete>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        completedField.SetValue(math, true);
        Check(stamping.TrySamplePlacement(out InkStampPlacement completed), "Completed task receiver permits free ink.");
        CommitGeometry(completed);
        Check(math.AcceptedSteps == 0 && !game.IsTaskComplete(DemoTaskId.MathBook), "Free mark does not notify or increment task progress.");
        completedField.SetValue(math, false);
        Stand(floor.GetComponent<Collider>(), 200f, 200f);
        mathFirst.SendMessage("OnTriggerEnter", motor.Controller);
        interactor.RefreshSelection();
        Check(interactor.CurrentTarget == mathFirst && !stamping.TrySamplePlacement(out _), "Valid task target overrides free stamp.");
        Check(interactor.TryInteract(), "Unified input dispatches task action.");
        interactor.CancelAction();
        Check(stamping.ActiveMarkCount == 4 && ink.CurrentInk == before - 3f * ink.StampCost, "Task cancellation never creates a free mark.");
        motor.Teleport(motor.transform.position);
        var well = bus.inkWells[0];
        well.SendMessage("OnTriggerEnter", motor.Controller);
        Check(interactor.TryInteract(), "Ink well priority starts refill rather than free stamp.");
        interactor.CancelAction();
        motor.Teleport(motor.transform.position);
        EdgeFalling edge = bus.edgeTriggers[0];
        edge.SendMessage("OnTriggerEnter", motor.Controller);
        interactor.RefreshSelection();
        Check(interactor.CurrentTarget == edge, "Jump target still selected ahead of free stamp.");
        motor.Teleport(motor.transform.position);

        var curved = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        curved.name = "StampTest_Curved";
        curved.transform.position = new Vector3(250f, 20f, 200f);
        curved.transform.localScale = Vector3.one * 2f;
        curved.GetComponent<Renderer>().sharedMaterial = floor.GetComponent<Renderer>().sharedMaterial;
        Stand(curved.GetComponent<Collider>(), 250f, 200f);
        Check(stamping.TrySamplePlacement(out InkStampPlacement curve), "Gentle curve accepts a conforming footprint.");
        ValidatePlacement(curve);
        float tolerance = stamping.Settings.normalTolerance;
        stamping.Settings.normalTolerance = 1f;
        Check(!stamping.TrySamplePlacement(out _), "Excessive curvature relative to configured tolerance rejected.");
        stamping.Settings.normalTolerance = tolerance;
        var underGap = Pad("StampTest_UnderGap", new Vector3(260f, 20f, 200f), new Vector3(5f, 1f, 5f));
        var split = new GameObject("StampTest_Split", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        split.transform.position = new Vector3(260f, 20.7f, 200f);
        var splitMesh = new Mesh();
        splitMesh.vertices = new[] { new Vector3(-2f, 0f, -2f), new Vector3(-0.04f, 0f, -2f), new Vector3(-2f, 0f, 2f), new Vector3(-0.04f, 0f, 2f),
            new Vector3(0.04f, 0f, -2f), new Vector3(2f, 0f, -2f), new Vector3(0.04f, 0f, 2f), new Vector3(2f, 0f, 2f) };
        splitMesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7 };
        splitMesh.RecalculateNormals(); splitMesh.RecalculateBounds();
        split.GetComponent<MeshFilter>().sharedMesh = splitMesh;
        split.GetComponent<MeshCollider>().sharedMesh = splitMesh;
        split.GetComponent<Renderer>().sharedMaterial = floor.GetComponent<Renderer>().sharedMaterial;
        motor.Teleport(new Vector3(260f, 20.7f + footOffset + 0.02f, 200f));
        for (int i = 0; i < 5; i++) motor.TickMovement(0.02f);
        Check(motor.IsGrounded && !stamping.CanOfferStamp, "Side support rays cannot stamp across a true center gap onto the lower floor.");
        var overwide = Pad("StampTest_OverwideCollider", new Vector3(280f, 20f, 200f), new Vector3(4f, 1f, 4f));
        overwide.GetComponent<BoxCollider>().size = new Vector3(2f, 1f, 1f);
        Stand(overwide.GetComponent<Collider>(), 283f, 200f);
        Check(motor.IsGrounded && !stamping.CanOfferStamp && !interactor.TryInteract(),
            "Oversized invisible collision margin is not a visual ink surface.");
        var vanishing = Pad("StampTest_Vanishing", new Vector3(270f, 20f, 200f), new Vector3(4f, 1f, 4f));
        Stand(vanishing.GetComponent<Collider>(), 270f, 200f);
        int countBeforeDestroy = stamping.ActiveMarkCount;
        Check(stamping.TrySamplePlacement(out InkStampPlacement temporary), "Temporary receiver placement.");
        CommitGeometry(temporary);
        float inkAfterCreation = ink.CurrentInk;
        Check(interactor.TryInteract(), "Receiver-destruction cancellation starts.");
        UnityEngine.Object.Destroy(vanishing);
        yield return null;
        yield return null;
        Check(stamping.ActiveMarkCount == countBeforeDestroy && ink.CurrentInk == inkAfterCreation && motor.CanMove,
            "Destroyed receiver removes its mark and cancels uncommitted action without extra ink loss.");

        stamping.ClearMarks();
        yield return null;
        Check(stamping.ActiveMarkCount == 0 && UnityEngine.Object.FindObjectsOfType<InkMark>().Length == 0, "Clear destroys mark objects.");
        Stand(floor.GetComponent<Collider>(), 200f, 200f);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < stamping.Capacity + 40; i++)
        {
            if (!ink.CanSpend) Check(ink.TryRefill(), "Stress refill.");
            Check(stamping.TrySamplePlacement(out InkStampPlacement placement), "Stress placement.");
            CommitGeometry(placement);
            Check(stamping.ActiveMarkCount == Mathf.Min(i + 1, stamping.Capacity), "Bounded oldest-mark recycling.");
        }
        timer.Stop();
        Debug.Log("STAMP_POOL capacity=" + stamping.Capacity + "; objects=" + UnityEngine.Object.FindObjectsOfType<InkMark>().Length
            + "; sampled/created=" + (stamping.Capacity + 40) + "; totalMs=" + timer.Elapsed.TotalMilliseconds);
        int meshCount = 0;
        foreach (Mesh mesh in Resources.FindObjectsOfTypeAll<Mesh>()) if (mesh.name == "DotLifeInkMark") meshCount++;
        Check(meshCount == stamping.Capacity, "No mesh leaked during recycling.");
        foreach (InkMark mark in UnityEngine.Object.FindObjectsOfType<InkMark>())
            Check(mark.GetComponent<Renderer>().sharedMaterial == stamping.Settings.material, "Pool shares one material.");
        stamping.ClearMarks();
        yield return null;
        meshCount = 0;
        foreach (Mesh mesh in Resources.FindObjectsOfTypeAll<Mesh>()) if (mesh.name == "DotLifeInkMark") meshCount++;
        Check(meshCount == 0, "Clear releases owned meshes.");

        // Native URP render differences prove the material really draws, not just that a mesh exists.
        Camera camera = motor.GetComponent<CameraFollowingScript>().followCamera;
        if (camera == null) camera = Camera.main;
        var following = motor.GetComponent<CameraFollowingScript>();
        following.enabled = false;
        Stand(floor.GetComponent<Collider>(), 200f, 200f);
        camera.transform.SetPositionAndRotation(new Vector3(200f, 26f, 195f), Quaternion.LookRotation(new Vector3(200f, 20.5f, 200f) - new Vector3(200f, 26f, 195f)));
        camera.aspect = 16f / 9f;
        Check(stamping.TrySamplePlacement(out flat), "Render placement.");
        motor.Teleport(motor.transform.position + new Vector3(0.8f, 0f, 0.8f));
        Texture2D blank = Capture(camera, null);
        CommitGeometry(flat);
        Texture2D drawn = Capture(camera, "B2StampNear.png");
        Color32[] a = blank.GetPixels32(), b = drawn.GetPixels32();
        int changed = 0, pink = 0;
        for (int i = 0; i < a.Length; i++)
        {
            if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 20) changed++;
            if (b[i].r > 220 && b[i].b > 220 && b[i].g < 30) pink++;
        }
        Check(changed > 12 && changed < a.Length / 10, "Stamp visible in native URP pixel difference.");
        Check(pink == 0, "No pink error shader.");
        UnityEngine.Object.Destroy(blank); UnityEngine.Object.Destroy(drawn);
        Stand(slope.GetComponent<Collider>(), 220f, 200f);
        Check(stamping.TrySamplePlacement(out angled), "Render slope placement.");
        CommitGeometry(angled);
        motor.Teleport(motor.transform.position + new Vector3(0.8f, 0f, 0.8f));
        camera.transform.SetPositionAndRotation(new Vector3(220f, 26f, 195f), Quaternion.LookRotation(new Vector3(220f, 20.5f, 200f) - new Vector3(220f, 26f, 195f)));
        UnityEngine.Object.Destroy(Capture(camera, "B2StampSlope.png"));
        Stand(paper.GetComponent<Collider>(), 202f, 200f);
        Check(stamping.TrySamplePlacement(out upper), "Render upper paper placement.");
        CommitGeometry(upper);
        motor.Teleport(motor.transform.position + new Vector3(-0.8f, 0f, -0.8f));
        camera.transform.SetPositionAndRotation(new Vector3(202f, 25f, 196f), Quaternion.LookRotation(new Vector3(202f, 20.5f, 200f) - new Vector3(202f, 25f, 196f)));
        UnityEngine.Object.Destroy(Capture(camera, "B2StampPaper.png"));
        // Scan the current authored scene, without moving or changing any of its objects.
        int authored = 0;
        Collider[] originals = UnityEngine.Object.FindObjectsOfType<Collider>();
        foreach (string family in new[] { "book", "desk", "paper" })
        {
          bool found = false;
          foreach (Collider receiver in originals)
          {
            if (receiver.isTrigger || receiver.transform.IsChildOf(motor.transform) || receiver.name.StartsWith("StampTest_")) continue;
            if (!InFamily(receiver.transform, family)) continue;
            Bounds bounds = receiver.bounds;
            if (bounds.size.x < 2f || bounds.size.z < 2f || bounds.center.x > 100f) continue;
            for (int x = -4; x <= 4 && !found; x++) for (int z = -4; z <= 4 && !found; z++)
            {
                float px = bounds.center.x + bounds.extents.x * x / 5f, pz = bounds.center.z + bounds.extents.z * z / 5f;
                if (!TryStand(receiver, px, pz)) continue;
                if (!stamping.TrySamplePlacement(out InkStampPlacement real) || real.receiver != receiver) continue;
                if (!ink.CanSpend) Check(ink.TryRefill(), "Authored render refill.");
                Vector3 center = real.point;
                motor.Teleport(motor.transform.position + new Vector3(-0.8f, 0f, -0.8f));
                camera.transform.SetPositionAndRotation(center + new Vector3(0f, 5f, -4f), Quaternion.LookRotation(new Vector3(0f, -5f, 4f)));
                Texture2D sceneBefore = Capture(camera, null);
                CommitGeometry(real);
                Texture2D sceneAfter = Capture(camera, "B2Stamp_" + family + ".png");
                Color32[] beforePixels = sceneBefore.GetPixels32(), afterPixels = sceneAfter.GetPixels32();
                int sceneChanged = 0;
                for (int pixel = 0; pixel < beforePixels.Length; pixel++)
                    if (Mathf.Abs(beforePixels[pixel].r - afterPixels[pixel].r) + Mathf.Abs(beforePixels[pixel].g - afterPixels[pixel].g)
                        + Mathf.Abs(beforePixels[pixel].b - afterPixels[pixel].b) > 20) sceneChanged++;
                Debug.Log("STAMP_AUTHORED_PIXELS changed=" + sceneChanged + "; collider=" + real.receiver.bounds + "; renderer="
                    + real.receiver.GetComponentInChildren<Renderer>().bounds);
                Check(sceneChanged > 12, "Ink really appears above the current authored model, not just its collider.");
                UnityEngine.Object.Destroy(sceneBefore); UnityEngine.Object.Destroy(sceneAfter);
                camera.transform.SetPositionAndRotation(center + new Vector3(5f, 9f, -7f), Quaternion.LookRotation(new Vector3(-5f, -9f, 7f)));
                UnityEngine.Object.Destroy(Capture(camera, "B2Stamp_" + family + "_Far.png"));
                Debug.Log("STAMP_AUTHORED receiver=" + real.receiver.name + "; point=" + real.point + "; normal=" + real.normal);
                found = true; authored++;
            }
            if (found) break;
          }
          Check(found, "Current authored " + family + " has a visible free-stamp receiver.");
        }
        Check(authored == 3, "Authored book, tabletop and paper checked separately.");
        var meter = UnityEngine.Object.FindObjectOfType<UIPlayerInk>();
        Check(meter != null, "B1 meter remains single and connected.");
        Check(stamping.ActiveMarkCount > 0, "Scene reload starts with live ink marks.");
        UnityEngine.SceneManagement.SceneManager.LoadScene("desk");
        yield return null;
        yield return null;
        InkStamping reloaded = UnityEngine.Object.FindObjectOfType<InkStamping>();
        PlayerInk reloadedInk = UnityEngine.Object.FindObjectOfType<PlayerInk>();
        Check(reloaded != null && reloaded.ActiveMarkCount == 0 && UnityEngine.Object.FindObjectsOfType<InkMark>().Length == 0,
            "Scene reload removes every receiver-parented mark.");
        Check(reloadedInk != null && Mathf.Approximately(reloadedInk.CurrentInk, reloadedInk.InitialInk)
            && !reloadedInk.HasDipped, "Reload restores tutorial ink budget and dipping state.");
        meshCount = 0;
        foreach (Mesh mesh in Resources.FindObjectsOfTypeAll<Mesh>()) if (mesh.name == "DotLifeInkMark") meshCount++;
        Check(meshCount == 0, "Scene unload releases all owned ink meshes.");
    }

    private static GameObject Pad(string name, Vector3 position, Vector3 scale)
    {
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = name; pad.transform.position = position; pad.transform.localScale = scale;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.white;
        pad.GetComponent<Renderer>().sharedMaterial = mat;
        return pad;
    }
    private static bool InFamily(Transform node, string family)
    {
        for (Transform t = node; t != null; t = t.parent)
        {
            string name = t.name.ToLowerInvariant();
            if (family == "desk" ? name == "desk" || name.Contains("desk2") : name.Contains(family)) return true;
        }
        return false;
    }
    private static void Stand(Collider receiver, float x, float z)
    {
        if (!TryStand(receiver, x, z)) throw new InvalidOperationException("No floor near " + receiver.name + " at " + x + "," + z);
    }
    private static bool TryStand(Collider receiver, float x, float z)
    {
        Physics.SyncTransforms();
        Vector3 origin = new Vector3(x, receiver.bounds.max.y + 2f, z);
        RaycastHit hit = default;
        float distance = float.PositiveInfinity;
        foreach (RaycastHit candidate in Physics.RaycastAll(origin, Vector3.down, 8f, motor.groundLayer, QueryTriggerInteraction.Ignore))
        {
            if (candidate.collider.transform.IsChildOf(motor.transform) || candidate.distance >= distance) continue;
            hit = candidate; distance = candidate.distance;
        }
        if (hit.collider == null) return false;
        motor.Teleport(hit.point + Vector3.up * (footOffset + 0.02f));
        motor.SetMoveInput(Vector2.zero);
        for (int i = 0; i < 5; i++) motor.TickMovement(0.02f);
        Physics.SyncTransforms();
        return true;
    }
    private static void CommitGeometry(InkStampPlacement placement)
    {
        Check(ink.TryCommitStamp(() => (bool)CreateMark.Invoke(stamping, new object[] { placement })), "Geometry creates before resource commit.");
    }
    private static void ValidatePlacement(InkStampPlacement placement)
    {
        Check(placement.vertices.Length == 49 && placement.triangles.Length == 216, "Fixed geometry size.");
        Matrix4x4 normals = placement.receiver.transform.worldToLocalMatrix.transpose;
        for (int i = 0; i < placement.vertices.Length; i++)
        {
            Vector3 p = placement.receiver.transform.TransformPoint(placement.vertices[i]);
            Vector3 n = normals.MultiplyVector(placement.normals[i]).normalized;
            Check(Physics.Raycast(p + n * 0.05f, -n, out RaycastHit hit, 0.1f, motor.groundLayer, QueryTriggerInteraction.Ignore)
                && hit.collider == placement.receiver && Vector3.Distance(p, hit.point) < 0.01f, "Vertex attached to nearest receiver face.");
            Check(Vector3.Angle(n, placement.normal) < 31f, "Correct normals under scaled transforms.");
        }
        for (int i = 0; i < placement.triangles.Length; i += 3)
        {
            Vector3 a = placement.receiver.transform.TransformPoint(placement.vertices[placement.triangles[i]]);
            Vector3 b = placement.receiver.transform.TransformPoint(placement.vertices[placement.triangles[i + 1]]);
            Vector3 c = placement.receiver.transform.TransformPoint(placement.vertices[placement.triangles[i + 2]]);
            Check(Vector3.Dot(Vector3.Cross(b - a, c - a), placement.normal) > 0f, "Triangle faces upwards.");
        }
    }
    private static Texture2D Capture(Camera camera, string name)
    {
        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
        target.Create();
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0); image.Apply();
            if (name != null)
            {
                string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "..", "B2Visuals");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name), image.EncodeToPNG());
            }
        }
        finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; target.Release(); UnityEngine.Object.Destroy(target); }
        return image;
    }
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException("Check " + checks + ": " + message);
    }
}
