using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class DotLifeCoreRegression
{
    private const string ActiveKey = "DotLife.CoreRegression.Active";
    private const string ResultKey = "DotLife.CoreRegression.Result";
    private const string MessageKey = "DotLife.CoreRegression.Message";
    private const string SceneKey = "DotLife.CoreRegression.Scene";
    private static IEnumerator cases;
    private static int frame = -1;
    private static int assertions;
    private static bool finishing;
    private static double started;
    private static BasicBehaviourScript motor;
    private static InteractingHandler interactor;
    private static CharacterController body;
    private static Collider playerCollider;
    private static GameState game;
    private const string ExpectedCallbackFailure = "DotLife expected callback failure.";
    private static int expectedExceptions;

    static DotLifeCoreRegression()
    {
        if (SessionState.GetBool(ActiveKey, false)) Attach();
    }

    [MenuItem("Tools/Dot Life/Run Core Regression")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("Stop Play mode and wait for compilation before testing.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SessionState.SetString(SceneKey, EditorSceneManager.GetActiveScene().path);
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetInt(ResultKey, 1);
        SessionState.SetString(MessageKey, "Run did not finish.");
        Attach();
        EditorSceneManager.OpenScene("Assets/_DotLife/Scenes/desk.unity");
        EditorApplication.isPlaying = true;
    }

    private static void Attach()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    public static void BuildSmoke()
    {
        string project = System.IO.Directory.GetParent(Application.dataPath).FullName;
        string destination = System.IO.Path.Combine(project, "Logs", "CoreSmokeBuild", "DotLife.exe");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/_DotLife/Scenes/MainMenu.unity", "Assets/_DotLife/Scenes/desk.unity" },
            locationPathName = destination,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        bool success = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        Debug.Log("CORE_BUILD " + (success ? "PASS " : "FAIL ") + report.summary.totalErrors + " errors.");
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            assertions = 0;
            expectedExceptions = 0;
            finishing = false;
            frame = -1;
            started = EditorApplication.timeSinceStartup;
            Application.logMessageReceived += OnLog;
            cases = RunCases();
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            int result = SessionState.GetInt(ResultKey, 1);
            string message = SessionState.GetString(MessageKey, "Missing result.");
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Debug.Log("CORE_REGRESSION " + (result == 0 ? "PASS " : "FAIL ") + message);
            if (Application.isBatchMode) EditorApplication.Exit(result);
            else
            {
                string previous = SessionState.GetString(SceneKey, "");
                if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
            }
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || finishing || cases == null) return;
        if (EditorApplication.timeSinceStartup - started > 150d)
        { Finish(false, "Timed out."); return; }
        if (frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (!cases.MoveNext()) Finish(true, assertions + " checks; physics, camera, ink budget/refill/meter, actions, tasks, edges, HUD, lifecycle, ending.");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Exception && expectedExceptions > 0
            && message.Contains("InvalidOperationException") && message.Contains(ExpectedCallbackFailure))
        { expectedExceptions--; return; }
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            Finish(false, message + "\n" + stack);
    }

    private static IEnumerator RunCases()
    {
        yield return null;
        game = UnityEngine.Object.FindObjectOfType<GameState>();
        motor = UnityEngine.Object.FindObjectOfType<BasicBehaviourScript>();
        interactor = motor.GetComponent<InteractingHandler>();
        body = motor.GetComponent<CharacterController>();
        playerCollider = motor.GetComponent<Collider>();
        TriggerBus bus = UnityEngine.Object.FindObjectOfType<TriggerBus>();
        FallingHandler falling = motor.GetComponent<FallingHandler>();
        CameraFollowingScript camera = motor.GetComponent<CameraFollowingScript>();
        ValidateCamera(camera);
        TaskStatusBar hud = UnityEngine.Object.FindObjectOfType<TaskStatusBar>();
        ValidateLayout(hud.GetComponentInParent<Canvas>());
        Check(bus.taskTriggers.Count == 4 && bus.dotTriggers.Count == 21 && bus.edgeTriggers.Count == 3
            && bus.inkWells.Count == 1, "Scene baseline including one ink well.");
        foreach (DotDrawingHandler task in bus.taskTriggers) Check(task.IsConfigured, "Configured task " + task.TaskId);
        Check(game.Phase == GameSessionPhase.Waiting && !motor.CanMove && !interactor.TryFall(Vector3.zero), "Waiting permission.");
        int starts = 0;
        int endings = 0;
        game.OnGameStart += () => starts++;
        game.OnGameOver += () => endings++;
        Check(game.TryStartGame() && !game.TryStartGame() && starts == 1, "Start is idempotent.");
        game.UpdateTasks(-1);
        game.UpdateTasks(4);
        Check(!game.isWin && !game.taskCompleted[0], "Invalid task IDs rejected.");
        ValidateInkResource(bus);
        ValidatePersistentTaskText(hud);
        interactor.enabled = false;

        Check(body != null && body.enabled && motor.GetComponent<Rigidbody>() == null
            && motor.GetComponents<Collider>().Length == 1, "One CharacterController without legacy physics body.");
        Check(Mathf.Abs(body.height - 1f) < 0.001f && Mathf.Abs(body.center.y + 0.15f) < 0.001f
            && body.minMoveDistance == 0f, "Capsule preserves body bottom and small movements.");
        ValidateVisualGrounding();
        for (int direction = 0; direction < 4; direction++)
        {
            Vector2 input = direction == 0 ? Vector2.up : direction == 1 ? Vector2.right
                : direction == 2 ? Vector2.down : Vector2.left;
            Vector3 start = motor.transform.position;
            float until = Time.time + 0.6f;
            while (Time.time < until) { motor.SetMoveInput(input); yield return null; }
            Debug.Log("CORE_SPAWN direction=" + direction + " displacement=" + (motor.transform.position - start)
                + " velocity=" + motor.Velocity + " grounded=" + motor.IsGrounded);
            Check(Vector3.ProjectOnPlane(motor.transform.position - start, Vector3.up).magnitude
                > motor.walkSpeed * 0.6f * 0.3f, "Native spawn movement direction " + direction);
        }

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "RegressionGround";
        floor.transform.position = new Vector3(200f, 19.5f, 200f);
        floor.transform.localScale = new Vector3(20f, 1f, 20f);
        ParkOnFloor();
        Check(motor.IsGrounded && motor.GroundContact.collider == floor.GetComponent<Collider>(), "Foot-based ground contact.");
        Transform originalCamera = motor.movementCamera;
        motor.movementCamera = null;
        Vector3 flatStart = motor.transform.position;
        float flatUntil = Time.time + 0.8f;
        while (Time.time < flatUntil) { motor.SetMoveInput(Vector2.up); yield return null; }
        Debug.Log("CORE_FLAT displacement=" + (motor.transform.position - flatStart) + " velocity=" + motor.Velocity);
        Check(motor.transform.position.z - flatStart.z > motor.walkSpeed * 0.8f * 0.65f, "Native continuous movement.");
        motor.SetMoveInput(Vector2.zero);
        float stopUntil = Time.time + 0.3f;
        while (Time.time < stopUntil) yield return null;
        Check(Vector3.ProjectOnPlane(motor.Velocity, Vector3.up).magnitude < 0.05f, "Release brakes movement.");

        float[] distances = new float[2];
        for (int rate = 0; rate < 2; rate++)
        {
            ParkOnFloor();
            Vector3 start = motor.transform.position;
            MoveTicks(Vector2.up, 1f, rate == 0 ? 1f / 30f : 1f / 60f);
            distances[rate] = motor.transform.position.z - start.z;
        }
        Check(distances[0] > 2.5f && distances[1] > 2.5f && Mathf.Abs(distances[0] - distances[1]) < 0.08f,
            "Move uses delta time consistently at 30 and 60 Hz.");
        motor.movementCamera = originalCamera;
        ParkOnFloor();
        Check(motor.movementCamera != null, "Scene movement has a camera reference.");
        Vector3 screenForward = Vector3.ProjectOnPlane(motor.movementCamera.forward, Vector3.up).normalized;
        motor.SetMoveInput(Vector2.up);
        motor.TickMovement(0.02f);
        Check(Vector3.Dot(motor.Velocity, screenForward) > 0f, "Up input follows screen direction.");
        ParkOnFloor();
        camera.ApplyRotationInput(30f);
        for (int i = 0; i < 150; i++) camera.TickCamera(0.02f);
        motor.SetMoveInput(Vector2.up);
        motor.TickMovement(0.02f);
        screenForward = Vector3.ProjectOnPlane(motor.movementCamera.forward, Vector3.up).normalized;
        Check(Vector3.Dot(motor.Velocity, screenForward) > 0f, "Up input follows the rotated camera heading.");
        camera.ApplyRotationInput(-camera.TargetYawOffset / camera.rotationSensitivity);
        for (int i = 0; i < 150; i++) camera.TickCamera(0.02f);
        ParkOnFloor();

        GameObject actualZone = new GameObject("RegressionTrigger", typeof(BoxCollider), typeof(DotDrawing));
        actualZone.transform.position = playerCollider.bounds.center;
        actualZone.GetComponent<BoxCollider>().size = playerCollider.bounds.size + Vector3.one;
        actualZone.GetComponent<BoxCollider>().isTrigger = true;
        Physics.SyncTransforms();
        float triggerDeadline = Time.time + 2f;
        while (!actualZone.GetComponent<DotDrawing>().IsPlayerInRange && Time.time < triggerDeadline) yield return null;
        Check(actualZone.GetComponent<DotDrawing>().IsPlayerInRange, "Controller delivers real inherited trigger callbacks.");
        motor.Teleport(motor.transform.position + Vector3.right * 3f);
        triggerDeadline = Time.time + 2f;
        while (actualZone.GetComponent<DotDrawing>().IsPlayerInRange && Time.time < triggerDeadline) yield return null;
        Check(!actualZone.GetComponent<DotDrawing>().IsPlayerInRange, "Controller leaves real trigger after teleport.");
        UnityEngine.Object.DestroyImmediate(actualZone);
        ParkOnFloor();
        motor.Teleport(motor.transform.position + Vector3.up * 2f);
        Check(!motor.IsGrounded && !motor.CanInteract, "Distant ground is not grounded.");
        float airStart = motor.transform.position.y;
        MoveTicks(Vector2.zero, 0.2f);
        Check(motor.transform.position.y < airStart - 0.2f, "Motor explicitly applies gravity without Rigidbody.");
        ParkOnFloor();
        motor.movementCamera = null;
        MoveTicks(Vector2.right, 0.4f);
        motor.SetMoveInput(Vector2.left);
        motor.TickMovement(0.02f);
        Check(motor.Velocity.x > 0f && motor.Velocity.x < motor.walkSpeed, "Reversal is accelerated, not instantaneous.");
        float speed = motor.Velocity.x;
        motor.SetMoveInput(Vector2.zero);
        motor.TickMovement(0.02f);
        Check(motor.Velocity.x < speed, "Deceleration.");
        ParkOnFloor();
        MoveTicks(Vector2.one, 0.4f);
        Check(Vector3.ProjectOnPlane(motor.Velocity, Vector3.up).magnitude <= motor.walkSpeed + 0.02f, "Diagonal speed normalized.");
        ParkOnFloor();

        float radius = body.radius;
        GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
        step.transform.localScale = new Vector3(3f, 0.2f, 3f);
        step.transform.position = new Vector3(200f, 20.1f, 200f + radius + 0.08f + 1.5f);
        Physics.SyncTransforms();
        float beforeStep = motor.transform.position.y;
        MoveTicks(Vector2.up, 0.8f);
        Debug.Log("CORE_STEP position=" + motor.transform.position + " initialHeight=" + beforeStep
            + " grounded=" + motor.IsGrounded + " nativeGrounded=" + body.isGrounded + " step=" + body.stepOffset);
        Check(motor.transform.position.y > beforeStep + 0.12f && motor.transform.position.y < beforeStep + 0.35f,
            "Native Step Offset traverses a low step.");
        UnityEngine.Object.DestroyImmediate(step);
        ParkOnFloor();
        step = GameObject.CreatePrimitive(PrimitiveType.Cube);
        step.transform.localScale = new Vector3(3f, 0.2f, 3f);
        step.transform.position = new Vector3(200f, 20.1f, 200f + radius + 0.08f + 1.5f);
        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.transform.localScale = new Vector3(6f, 0.1f, 6f);
        ceiling.transform.position = new Vector3(200f, playerCollider.bounds.max.y + 0.1f, 202f);
        Physics.SyncTransforms();
        beforeStep = motor.transform.position.y;
        MoveTicks(Vector2.up, 0.8f);
        Check(motor.transform.position.y < beforeStep + 0.1f && motor.transform.position.z < 200.8f,
            "Native capsule refuses a step without headroom.");
        UnityEngine.Object.DestroyImmediate(ceiling);
        UnityEngine.Object.DestroyImmediate(step);
        ParkOnFloor();
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.localScale = new Vector3(8f, 4f, 1f);
        wall.transform.position = new Vector3(200f, 22f, 200f + radius + 0.08f + 0.5f);
        Physics.SyncTransforms();
        float beforeWall = motor.transform.position.y;
        MoveTicks(Vector2.up, 0.8f);
        Check(motor.transform.position.y < beforeWall + 0.1f && motor.transform.position.z < 200.2f,
            "High wall blocks movement without levitation.");
        Vector3 slideStart = motor.transform.position;
        MoveTicks(Vector2.one, 0.6f);
        Check(motor.transform.position.x > slideStart.x + 0.8f && motor.transform.position.z < 200.2f,
            "Capsule slides along a wall.");
        UnityEngine.Object.DestroyImmediate(wall);
        ParkOnFloor();

        GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.transform.position = new Vector3(200f, 22f, 200f);
        ramp.transform.localScale = new Vector3(8f, 0.3f, 8f);
        foreach (float slope in new[] { 20f, 60f })
        {
            ramp.transform.rotation = Quaternion.Euler(-slope, 0f, 0f);
            Physics.SyncTransforms();
            Collider rampCollider = ramp.GetComponent<Collider>();
            Ray ray = new Ray(new Vector3(200f, rampCollider.bounds.max.y + 3f, 200f), Vector3.down);
            Check(rampCollider.Raycast(ray, out RaycastHit rampHit, 20f), "Test slope has a top surface.");
            float rootToFoot = motor.transform.position.y - playerCollider.bounds.min.y;
            motor.Teleport(new Vector3(200f, rampHit.point.y + rootToFoot + 0.02f, 200f));
            float slopeStart = motor.transform.position.y;
            MoveTicks(Vector2.up, 0.6f);
            Debug.Log("CORE_SLOPE angle=" + slope + " rise=" + (motor.transform.position.y - slopeStart)
                + " grounded=" + motor.IsGrounded);
            if (slope < motor.maxSlopeAngle)
                Check(motor.IsGrounded && motor.GroundContact.collider == rampCollider
                    && motor.transform.position.y > slopeStart + 0.2f, "Native capsule walks a valid slope.");
            else
                Check(!motor.IsGrounded && motor.transform.position.y < slopeStart + 0.1f,
                    "Slope limit rejects climbing and interacting on a steep surface.");
        }
        UnityEngine.Object.DestroyImmediate(ramp);
        ParkOnFloor();

        DotDrawingHandler diary = bus.taskTriggers.Find(t => t.TaskId == DemoTaskId.Diary);
        DotDrawingHandler math = bus.taskTriggers.Find(t => t.TaskId == DemoTaskId.MathBook);
        DotDrawing early = FindDot(math, math.StepCount - 1);
        Enter(early, playerCollider);
        Check(!early.CanInteract(motor) && !early.TryInteract(interactor), "Out-of-order interaction rejected before animation.");
        Check(motor.ActionState == PlayerActionState.Locomotion, "Rejected action does not lock movement.");
        Exit(early, playerCollider);

        DotDrawing first = FindDot(math, 0);
        int enters = 0;
        int exits = 0;
        first.OnEnteringAndExiting += entered => { if (entered) enters++; else exits++; };
        Enter(first, floor.GetComponent<Collider>());
        Check(!first.IsPlayerInRange, "Non-player ignored.");
        GameObject extra = new GameObject("RegressionExtraCollider");
        extra.transform.SetParent(motor.transform, false);
        BoxCollider extraCollider = extra.AddComponent<BoxCollider>();
        extraCollider.size = Vector3.one * 0.01f;
        Enter(first, playerCollider);
        Enter(first, extraCollider);
        Exit(first, playerCollider);
        Check(first.IsInRange(motor) && enters == 1 && exits == 0, "Compound player remains in range.");
        Exit(first, extraCollider);
        Check(!first.IsPlayerInRange && exits == 1, "Final collider exits once.");
        UnityEngine.Object.DestroyImmediate(extra);

        interactor.enabled = true;
        interactor.SendMessage("OnApplicationFocus", true);
        Enter(first, playerCollider);
        motor.RefreshGroundContact();
        int immediateCommits = 0;
        Check(!first.CanInteract(motor) && !first.TryInteract(interactor), "Other tasks cannot consume the diary budget.");
        bool nestedAction = true;
        Check(interactor.TryPerformAction(-1, () => true, () =>
        {
            immediateCommits++;
            nestedAction = interactor.TryPerformAction(-1, () => true, () => { immediateCommits++; return true; });
            return true;
        }), "Immediate action succeeds through the shared gate.");
        Check(immediateCommits == 1 && !nestedAction && motor.CanMove, "Immediate commit rejects synchronous re-entry and restores control.");
        Check(!interactor.TryPerformAction(-1, () => true, () => false) && motor.CanMove,
            "Rejected immediate result restores control.");
        ExpectCallbackFailure(() => interactor.TryPerformAction(0,
            () => throw new InvalidOperationException(ExpectedCallbackFailure), () => true),
            "Initial validity exception rejects action safely.");
        ExpectCallbackFailure(() => interactor.TryPerformAction(-1, () => true,
            () => throw new InvalidOperationException(ExpectedCallbackFailure)),
            "Immediate commit exception releases control.");
        bool throwValidity = false;
        Check(interactor.TryPerformAction(0, () => throwValidity
            ? throw new InvalidOperationException(ExpectedCallbackFailure) : true, () => true),
            "Begin validity failure recovery.");
        throwValidity = true;
        ExpectCallbackFailure(() => { Invoke(interactor, "AdvanceAction"); return false; },
            "Pending validity exception cancels instead of leaving movement locked.");
        Check(interactor.TryPerformAction(0, () => true,
            () => throw new InvalidOperationException(ExpectedCallbackFailure)), "Begin animated commit failure.");
        ExpectCallbackFailure(() => { interactor.CommitAction(); return false; },
            "Animated commit exception releases control.");
        Check(interactor.TryPerformAction(0, () => true, () => false), "Begin rejected animated result.");
        interactor.CommitAction();
        Check(motor.CanMove && math.AcceptedSteps == 0, "Rejected animated result restores control without task progress.");
        Check(interactor.TryPerformAction(0, () => true, () => true), "Begin controller-disable cancellation.");
        body.enabled = false;
        Invoke(interactor, "AdvanceAction");
        Check(motor.ActionState == PlayerActionState.Locomotion, "Disabled controller cancels pending action.");
        body.enabled = true;
        motor.RefreshGroundContact();
        Check(!interactor.TryPerformAction(2, () => true, () => true) && motor.CanMove,
            "Unknown action does not lock movement.");
        float normalTimeout = interactor.actionTimeout;
        interactor.actionTimeout = 0.1f;
        Check(interactor.TryPerformAction(0, () => true, () => { immediateCommits++; return true; }), "Begin contact timeout.");
        float timeoutDeadline = Time.time + 2f;
        while (!motor.CanMove && Time.time < timeoutDeadline) yield return null;
        Check(motor.CanMove && immediateCommits == 1, "Timeout recovers without silently committing.");
        interactor.actionTimeout = normalTimeout;
        bool actionValid = true;
        int genericCommits = 0;
        Check(interactor.TryPerformAction(0, () => actionValid, () => { genericCommits++; return true; }), "Generic contact action starts.");
        actionValid = false;
        yield return null;
        Check(genericCommits == 0 && motor.CanMove, "Invalidated action cancels before contact.");
        Check(interactor.TryPerformAction(0, () => true, () =>
        {
            genericCommits++;
            interactor.CommitAction();
            return true;
        }), "Generic contact action retries.");
        float genericDeadline = Time.time + 5f;
        while (motor.ActionState == PlayerActionState.Interacting && Time.time < genericDeadline) yield return null;
        Check(genericCommits == 1 && motor.CanMove, "Reentrant commit remains one-shot.");
        Check(interactor.TryPerformAction(0, () => true, () => { genericCommits++; return true; }), "Begin focus cancellation.");
        interactor.SendMessage("OnApplicationFocus", false);
        Check(genericCommits == 1 && motor.CanMove, "Loss of focus cancels pending action.");
        Check(!interactor.TryPerformAction(-1, () => true, () => true)
            && !interactor.TryFall(Vector3.zero), "Unfocused action requests are rejected.");
        interactor.SendMessage("OnApplicationFocus", true);
        Time.timeScale = 0f;
        try
        {
            Check(!interactor.TryPerformAction(-1, () => true, () => true)
                && !interactor.TryFall(Vector3.zero), "Paused action requests are rejected.");
        }
        finally { Time.timeScale = 1f; }
        foreach (DotDrawing dot in diary.Dots) Enter(dot, playerCollider);
        diary.frameDuration = 0.01f;
        float deadline = Time.time + 8f;
        while (!diary.IsComplete && Time.time < deadline) yield return null;
        Check(diary.IsComplete && game.IsTaskComplete(DemoTaskId.Diary), "Automatic ordered points retry without re-entry. Steps="
            + diary.AcceptedSteps + "; grounded=" + motor.IsGrounded + "; action=" + motor.ActionState);
        foreach (DotDrawing dot in diary.Dots) Exit(dot, playerCollider);
        IEnumerator refillChecks = ValidateInkAfterDiary(first, bus.inkWells[0], hud, camera);
        while (refillChecks.MoveNext()) yield return null;
        Enter(first, playerCollider);
        Check(first.TryInteract(interactor) && !motor.CanMove, "Manual action owns control.");
        Check(math.AcceptedSteps == 0 && !first.TryInteract(interactor), "No early or repeated commit.");
        interactor.CancelAction();
        Check(math.AcceptedSteps == 0 && motor.CanMove && interactor.Ink.CurrentInk == interactor.Ink.MaxInk,
            "Cancel before contact preserves progress and ink.");
        Check(first.TryInteract(interactor), "Begin motor-disable cancellation.");
        motor.enabled = false;
        motor.enabled = true;
        motor.RefreshGroundContact();
        Check(math.AcceptedSteps == 0 && motor.CanMove, "Motor disable cancels pending action.");
        Check(first.TryInteract(interactor), "Can retry cancelled action.");
        deadline = Time.time + 8f;
        while (math.AcceptedSteps == 0 && Time.time < deadline) yield return null;
        Check(math.AcceptedSteps == 1, "Animation contact commits once.");
        float inkAfterContact = interactor.Ink.CurrentInk;
        Check(Mathf.Abs(inkAfterContact - (interactor.Ink.MaxInk - interactor.Ink.StampCost)) < 0.0001f,
            "Accepted animation contact consumes one stamp cost.");
        interactor.CommitAction();
        Check(math.AcceptedSteps == 1 && interactor.Ink.CurrentInk == inkAfterContact, "Contact and ink charge are idempotent.");
        while (!motor.CanMove && Time.time < deadline) yield return null;
        Check(motor.CanMove, "Action recovers control.");
        Exit(first, playerCollider);

        foreach (EdgeFalling edge in bus.edgeTriggers) Check(edge.CanFall(), "Diary still unlocks each edge.");
        int completedCount = 0;
        game.OnTaskStateChanged += ignored => completedCount++;
        game.UpdateTasks(0);
        Check(completedCount == 0 && !game.isWin, "Duplicate task does not count.");

        foreach (EdgeFalling edge in bus.edgeTriggers)
        {
            ParkOnFloor();
            motor.RefreshGroundContact();
            Enter(edge, playerCollider);
            interactor.RefreshSelection();
            Invoke(hud, "LateUpdate");
            Check(interactor.CurrentTarget == edge, "Every edge is selectable.");
            Text prompt = (Text)Field(hud, "interactionPromptText");
            Check(prompt.text.Contains("E") && prompt.text.Length > 0, "Every edge has a valid HUD prompt.");
            falling.fallDuration = 0.12f;
            Check(edge.TryInteract(interactor) && motor.IsScriptedMovement && !body.enabled, "Edge owns scripted movement.");
            Check(!interactor.TryFall(edge.endPoint + Vector3.one), "Repeated fall rejected.");
            Vector3 locked = motor.transform.position;
            motor.SetMoveInput(Vector2.one);
            motor.TickMovement(0.02f);
            Check(motor.transform.position == locked, "Ordinary motor cannot write scripted position.");
            deadline = Time.time + 3f;
            while (motor.IsScriptedMovement && Time.time < deadline) yield return null;
            Check(!motor.IsScriptedMovement && body.enabled, "Fall restores CharacterController.");
            Check(Vector3.Distance(motor.transform.position, edge.endPoint) < 0.15f, "Original landing endpoint.");
            Exit(edge, playerCollider);
        }
        ParkOnFloor();
        motor.RefreshGroundContact();
        falling.fallDuration = 2f;
        Check(interactor.TryFall(Vector3.zero), "Begin interrupted fall.");
        falling.enabled = false;
        Check(!motor.IsScriptedMovement && body.enabled, "Disabled fall releases ownership.");
        falling.enabled = true;

        foreach (DotDrawingHandler task in bus.taskTriggers)
        {
            if (task.IsComplete) continue;
            ParkOnFloor();
            task.frameDuration = 0.01f;
            TransitionHandler transition = task.GetComponent<TransitionHandler>();
            if (transition != null) transition.frameRate = task.TaskId == DemoTaskId.Photo ? 0.05f : 0.01f;
            int logicalComplete = 0;
            task.OnTaskCompleted += ignored => logicalComplete++;
            for (int index = task.AcceptedSteps; index < task.StepCount; index++)
            {
                DotDrawing dot = FindDot(task, index);
                Enter(dot, playerCollider);
                motor.RefreshGroundContact();
                Check(dot.TryInteract(interactor), "Accept current step " + task.TaskId + ":" + index);
                deadline = Time.time + 8f;
                while ((task.AcceptedSteps <= index || motor.ActionState == PlayerActionState.Interacting) && Time.time < deadline)
                    yield return null;
                Check(task.AcceptedSteps == index + 1, "Exactly one step accepted.");
                Exit(dot, playerCollider);
            }
            if (transition != null && !transition.IsComplete)
                Check(!game.IsTaskComplete(task.TaskId), "Outro precedes logical completion.");
            if (task.TaskId == DemoTaskId.Photo)
            {
                falling.fallDuration = 4f;
                ParkOnFloor();
                motor.RefreshGroundContact();
                Check(interactor.TryFall(motor.transform.position), "Begin fall before final outro completes.");
            }
            deadline = Time.time + 8f;
            while (!task.IsComplete && Time.time < deadline) yield return null;
            Check(task.IsComplete && logicalComplete == 1 && game.IsTaskComplete(task.TaskId), "Task completed once after presentation.");
            if (transition != null) Check(transition.IsComplete, "Outro finished.");
            task.enabled = false;
            task.enabled = true;
            bus.enabled = false;
            bus.enabled = true;
            Check(logicalComplete == 1, "Re-enable does not duplicate completion.");
        }
        yield return null;
        Check(game.isWin && !game.isGameRunning && endings == 1 && !game.TryStartGame(), "Final state cannot restart.");
        Invoke(hud, "LateUpdate");
        Check(!camera.HasReachedFinalPose && !interactor.CanReturnToMenu
            && ((Text)Field(hud, "interactionPromptText")).text == "", "Camera transition cannot show or enable menu return.");
        Check(motor.IsScriptedMovement && !motor.CanMove, "Victory during falling preserves scripted ownership.");
        deadline = Time.time + 6f;
        while (motor.IsScriptedMovement && Time.time < deadline) yield return null;
        Check(!motor.IsScriptedMovement && body.enabled && !motor.CanMove,
            "Winning fall restores controller without restoring input.");
        Check(!motor.CanMove && !interactor.TryFall(Vector3.zero), "Finished controls stay locked.");
        Check(!interactor.Ink.TryRefill() && !interactor.Ink.TryCommitStamp(() => true), "Finished session rejects ink operations.");
        UIPlayerInk endingMeter = UnityEngine.Object.FindObjectOfType<UIPlayerInk>();
        endingMeter.TickDisplay(0f);
        Check(endingMeter.GetComponent<CanvasGroup>().alpha == 0f, "Ending hides the ink meter.");
        foreach (EdgeFalling edge in bus.edgeTriggers) Check(!edge.CanFall(), "Finished edges locked.");
        motor.SetMoveInput(Vector2.one);
        motor.TickMovement(0.02f);
        Check(new Vector2(motor.Velocity.x, motor.Velocity.z).sqrMagnitude < 0.001f, "No residual ending movement.");
        camera.finalMoveDuration = 0.05f;
        camera.enabled = false;
        Check(!camera.HasReachedFinalPose && !camera.IsEndingPromptReady, "Disabling ending camera clears its arrival state.");
        camera.enabled = true;
        Check(!interactor.CanReturnToMenu, "Restarted ending camera cannot reuse an old countdown.");
        deadline = Time.time + 2f;
        while (!camera.HasReachedFinalPose && Time.time < deadline) yield return null;
        Check(camera.HasReachedFinalPose && !camera.IsEndingPromptReady && Mathf.Approximately(camera.finalHoldDuration, 5f),
            "Actual final-pose arrival starts the default five-second hold.");
        Invoke(hud, "LateUpdate");
        Check(((Text)Field(hud, "interactionPromptText")).text == "", "Final pose alone does not show the return prompt.");
        Check(!interactor.TryReturnToMenu(), "Menu return is rejected before the five-second hold ends.");
        Check(Vector3.Distance(camera.followCamera.transform.position, camera.finalPos) < 0.001f, "Final camera respects authored position.");
        Check(Quaternion.Angle(camera.followCamera.transform.rotation, camera.finalRotation) < 0.01f, "Final camera respects authored rotation.");
        float endingZoom = camera.TargetZoomOffset;
        float endingYaw = camera.TargetYawOffset;
        camera.ApplyZoomInput(100f);
        camera.ApplyRotationInput(20f);
        camera.TickCamera(0.02f);
        Check(camera.TargetZoomOffset == endingZoom && Vector3.Distance(camera.followCamera.transform.position, camera.finalPos) < 0.001f,
            "Wheel zoom cannot override the ending camera.");
        Check(camera.TargetYawOffset == endingYaw && Quaternion.Angle(camera.followCamera.transform.rotation, camera.finalRotation) < 0.001f,
            "Mouse orbit cannot override the ending camera.");
        Check(UnityEngine.Object.FindObjectsOfType<TextureSurface>().Length == 4, "One texture owner per task renderer.");
        float arrivedAt = Time.unscaledTime;
        double holdDeadline = EditorApplication.timeSinceStartup + 7d;
        bool hiddenDuringHold = true;
        while (!camera.IsEndingPromptReady && EditorApplication.timeSinceStartup < holdDeadline)
        {
            Invoke(hud, "LateUpdate");
            hiddenDuringHold &= ((Text)Field(hud, "interactionPromptText")).text == "";
            yield return null;
        }
        Check(hiddenDuringHold, "Prompt remains hidden throughout the final hold.");
        Check(camera.IsEndingPromptReady && Time.unscaledTime - arrivedAt >= 4.95f, "Five real seconds elapse after camera arrival.");
        Invoke(hud, "LateUpdate");
        Check(((Text)Field(hud, "interactionPromptText")).text == "按ESC回到开始页面" && interactor.CanReturnToMenu,
            "Cached HUD refreshes into the exact bottom return prompt without another state change.");
        interactor.enabled = false;
        Check(!interactor.TryReturnToMenu(), "Disabled input handler cannot load the menu.");
        interactor.enabled = true;
        Time.timeScale = 0f;
        Check(!interactor.TryReturnToMenu(), "Paused input cannot return to the menu.");
        Time.timeScale = 1f;
        interactor.SendMessage("OnApplicationFocus", false);
        Check(!interactor.TryReturnToMenu(), "Unfocused input cannot return to the menu.");
        interactor.SendMessage("OnApplicationFocus", true);
        UnityEngine.Object.Destroy(floor);
        Check(interactor.TryReturnToMenu() && !interactor.TryReturnToMenu(), "Ending return loads MainMenu exactly once.");
        yield return null;
        MainMenu menu = UnityEngine.Object.FindObjectOfType<MainMenu>();
        Check(menu != null, "Main menu reloads.");
        ValidateLayout(UnityEngine.Object.FindObjectOfType<Canvas>());
        menu.StartGame();
        yield return null;
        yield return null;
        GameState reloaded = UnityEngine.Object.FindObjectOfType<GameState>();
        Check(reloaded != null && reloaded.Phase == GameSessionPhase.Waiting, "Menu enters a fresh waiting session.");
        PlayerInk freshInk = UnityEngine.Object.FindObjectOfType<PlayerInk>();
        Check(freshInk.CurrentInk == freshInk.InitialInk && !freshInk.HasDipped, "Reload resets ink to the tutorial budget.");
        Check(UnityEngine.Object.FindObjectsOfType<UIPlayerInk>().Length == 1, "Reload does not duplicate the ink meter.");
        Check(UnityEngine.Object.FindObjectsOfType<UIInkWellPrompt>().Length == 1, "Reload does not duplicate the ink well prompt.");
        Check(UnityEngine.Object.FindObjectsOfType<TextureSurface>().Length == 4, "Texture owners do not accumulate on reload.");
        int runtimeMaterials = 0;
        foreach (Material material in Resources.FindObjectsOfTypeAll<Material>())
            if (material.name.EndsWith(" (DotLifeRuntime)", StringComparison.Ordinal)) runtimeMaterials++;
        Check(runtimeMaterials == 4, "Owned material instances are released on scene unload.");
    }

    private static void ValidateInkResource(TriggerBus bus)
    {
        PlayerInk ink = interactor.Ink;
        UIPlayerInk meter = UnityEngine.Object.FindObjectOfType<UIPlayerInk>();
        Check(ink != null && meter != null && meter.Source == ink, "Player resource and meter are connected.");
        Check(ink.CurrentInk == ink.InitialInk && Mathf.Abs(ink.InitialInk - 9f * ink.StampCost) < 0.0001f
            && !ink.HasDipped, "Initial ink exactly covers nine diary stamps.");
        Check(meter.GetComponent<CanvasGroup>().alpha == 0f, "Initial partial ink is hidden.");
        InkWell well = bus.inkWells[0];
        Check(well.name == "ink_low" && well.GetComponent<BoxCollider>().isTrigger
            && well.GetComponentsInChildren<Collider>().Length >= 2, "Ink well retains its physical model and adds a separate trigger.");
        Enter(well, playerCollider);
        Check(!well.CanInteract(motor) && !well.TryInteract(interactor) && !ink.TryRefill(),
            "Refill cannot bypass the diary tutorial budget.");
        Exit(well, playerCollider);
        DotDrawingHandler diary = bus.taskTriggers.Find(task => task.TaskId == DemoTaskId.Diary);
        foreach (float cost in new[] { 0.01f, 0.17f, 1.3f })
        {
            GameObject root = new GameObject("RegressionInkResource");
            root.SetActive(false);
            PlayerInk sample = root.AddComponent<PlayerInk>();
            sample.stampCost = cost;
            sample.maximumInk = cost;
            typeof(PlayerInk).GetField("gameState", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sample, game);
            typeof(PlayerInk).GetField("diary", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sample, diary);
            root.SetActive(true);
            try
            {
                float initial = sample.CurrentInk;
                Check(Mathf.Abs(initial - 9f * cost) < 0.0001f && sample.MaxInk >= initial,
                    "Capacity cannot undercut the tutorial budget: " + cost);
                Check(!sample.TryCommitStamp(() => false) && sample.CurrentInk == initial,
                    "Rejected stamp restores reserved ink.");
                bool caught = false;
                try { sample.TryCommitStamp(() => throw new InvalidOperationException("Transaction rollback probe")); }
                catch (InvalidOperationException) { caught = true; }
                Check(caught && sample.CurrentInk == initial && sample.CanSpend, "Throwing stamp restores ink and transaction gate.");
                int accepted = 0;
                int empty = 0;
                sample.InsufficientInk += () => empty++;
                for (int i = 0; i < diary.StepCount; i++)
                {
                    bool nested = true;
                    Check(sample.TryCommitStamp(() =>
                    {
                        accepted++;
                        nested = sample.TryCommitStamp(() => true);
                        return true;
                    }) && !nested, "Stamp reservation blocks reentry: " + i);
                }
                Check(accepted == diary.StepCount && sample.CurrentInk == 0f && !sample.CanSpend,
                    "Fractional tutorial budget is exactly empty after nine stamps.");
                Check(!sample.TryCommitStamp(() => { accepted++; return true; }) && empty == 1
                    && accepted == diary.StepCount && sample.CurrentInk == 0f, "Empty stamp never commits or goes negative.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        InkMeterGraphic graphic = meter.GetComponent<InkMeterGraphic>();
        Check(Mathf.Abs(((Color)Field(graphic, "trackColor")).a - 0.45f) < 0.001f,
            "Ink meter prefab uses a translucent white track.");
        using (var vertices = new VertexHelper())
        {
            MethodInfo populate = typeof(InkMeterGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(VertexHelper) }, null);
            foreach (float fill in new[] { 0f, 0.5f, 1f })
            {
                graphic.SetValues(fill, 1f, fill == 0f ? 1f : 0f);
                populate.Invoke(graphic, new object[] { vertices });
                Check(vertices.currentVertCount > 0 && vertices.currentIndexCount > 0, "Rounded ink bar has visible geometry: " + fill);
                UIVertex vertex = default;
                for (int i = 0; i < vertices.currentVertCount; i++)
                {
                    vertices.PopulateUIVertex(ref vertex, i);
                    Rect bounds = graphic.rectTransform.rect;
                    Check(vertex.position.x >= bounds.xMin - 0.001f && vertex.position.x <= bounds.xMax + 0.001f
                        && vertex.position.y >= bounds.yMin - 0.001f && vertex.position.y <= bounds.yMax + 0.001f,
                        "Ink geometry stays inside its rect.");
                }
            }
        }
        graphic.SetValues(ink.NormalizedInk, ink.NormalizedInk, 0f);
    }

    private static void ValidatePersistentTaskText(TaskStatusBar hud)
    {
        Text description = (Text)Field(hud, "taskDescriptionText");
        var messages = (System.Collections.Generic.List<string>)Field(hud, "taskDescriptions");
        var original = new System.Collections.Generic.List<bool>(game.taskCompleted);
        try
        {
            foreach (int completed in new[] { -1, 2, 0, 1 })
            {
                if (completed >= 0) game.taskCompleted[completed] = true;
                Invoke(hud, "LateUpdate");
                int expected = completed == -1 || completed == 2 ? 0 : completed == 0 ? 1 : 3;
                Check(description.text == messages[expected], "Persistent task text follows the first unfinished task: " + expected);
            }
        }
        finally
        {
            for (int i = 0; i < original.Count; i++) game.taskCompleted[i] = original[i];
            Invoke(hud, "LateUpdate");
        }
        Check(description.text == messages[0], "Task text restores after a temporary state probe.");
    }

    private static void ValidateWellPrompt(InkWell well, TaskStatusBar hud)
    {
        UIInkWellPrompt prompt = UnityEngine.Object.FindObjectOfType<UIInkWellPrompt>();
        Check(prompt != null && prompt.Source == well && prompt.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace,
            "Ink well owns a world-space key prompt.");
        Camera camera = Camera.main;
        Vector3 position = camera.transform.position;
        Quaternion rotation = camera.transform.rotation;
        Collider area = well.GetComponent<Collider>();
        bool enabled = area.enabled;
        KeyCode key = interactor.InteractionKey;
        try
        {
            Bounds bounds = area.bounds;
            camera.transform.position = bounds.center + Vector3.up * 5f + Vector3.back * 6f;
            camera.transform.LookAt(bounds.center);
            prompt.TickPrompt(0f);
            Check(prompt.IsVisible && prompt.GetComponentInChildren<Text>().text == "E", "Entering the refill trigger shows E above the well.");
            float restingY = prompt.transform.position.y;
            prompt.TickPrompt(0.45f);
            Check(Mathf.Abs(prompt.transform.position.y - restingY - 0.06f) < 0.001f, "The key bobs gently above the model.");
            Check(Quaternion.Angle(prompt.transform.rotation, camera.transform.rotation) < 0.001f,
                "World-space E faces the camera.");
            Check(Mathf.Abs(prompt.transform.lossyScale.x - 0.012f) < 0.001f
                && Mathf.Abs(prompt.transform.lossyScale.y - 0.012f) < 0.001f, "Model scale does not stretch the key prompt.");
            Check(((Text)Field(hud, "interactionPromptText")).text == "", "Refill no longer writes a screen-space text prompt.");
            typeof(InteractingHandler).GetField("interactionKey", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(interactor, KeyCode.F);
            prompt.TickPrompt(0f);
            Check(prompt.GetComponentInChildren<Text>().text == "F", "Well prompt follows the configured interaction key.");
            area.enabled = false;
            prompt.TickPrompt(0f);
            Check(!prompt.IsVisible, "Disabled refill trigger hides its key immediately.");
            area.enabled = true;
            hud.enabled = false;
            Check(!prompt.IsVisible && !prompt.enabled, "Disabled HUD hides the owned well prompt.");
            hud.enabled = true;
            prompt.TickPrompt(0f);
            Check(prompt.IsVisible && UnityEngine.Object.FindObjectsOfType<UIInkWellPrompt>().Length == 1,
                "Re-enabled HUD restores one prompt without duplicates.");
        }
        finally
        {
            area.enabled = enabled;
            hud.enabled = true;
            typeof(InteractingHandler).GetField("interactionKey", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(interactor, key);
            camera.transform.SetPositionAndRotation(position, rotation);
            prompt.TickPrompt(Time.time);
        }
    }

    private static IEnumerator ValidateInkAfterDiary(DotDrawing first, InkWell well, TaskStatusBar hud, CameraFollowingScript camera)
    {
        PlayerInk ink = interactor.Ink;
        UIPlayerInk meter = UnityEngine.Object.FindObjectOfType<UIPlayerInk>();
        CanvasGroup group = meter.GetComponent<CanvasGroup>();
        InkMeterGraphic graphic = meter.GetComponent<InkMeterGraphic>();
        Check(ink.CurrentInk == 0f && !ink.HasDipped, "Diary consumes the entire starter budget.");
        for (int i = 0; i < 100; i++) camera.TickCamera(0.02f);
        meter.TickDisplay(2f);
        Check(group.alpha == 0f, "Empty meter is hidden when idle.");
        Enter(first, playerCollider);
        Check(first.CanInteract(motor) && !first.TryInteract(interactor) && motor.CanMove && first.Owner.AcceptedSteps == 0,
            "Empty manual stamp gives feedback without animation, lock or progress.");
        Check(meter.IsFlashing && group.alpha == 1f && graphic.Fill == 0f, "Empty attempt begins first flash.");
        meter.TickDisplay(0.17f);
        Check(group.alpha == 0f, "First empty flash turns off.");
        Check(!first.TryInteract(interactor), "Repeated empty request still rejects.");
        meter.TickDisplay(0.16f);
        Check(meter.IsFlashing && group.alpha == 1f, "Second flash turns on without restart.");
        meter.TickDisplay(0.16f);
        Check(group.alpha == 0f, "Second flash turns off.");
        meter.TickDisplay(0.16f);
        Check(!meter.IsFlashing && group.alpha == 0f, "Exactly two flashes then hide.");
        Exit(first, playerCollider);
        Enter(well, playerCollider);
        interactor.RefreshSelection();
        Invoke(hud, "LateUpdate");
        Check(interactor.CurrentTarget == well, "Ink well retains highest target priority.");
        ValidateWellPrompt(well, hud);
        Check(well.TryInteract(interactor) && !motor.CanMove && ink.CurrentInk == 0f,
            "Dip starts an owned action without filling before contact.");
        Check(!well.TryInteract(interactor), "Repeated dip request is rejected.");
        interactor.CancelAction();
        Check(ink.CurrentInk == 0f && !ink.HasDipped && motor.CanMove, "Canceled dip does not fill ink.");
        Check(well.TryInteract(interactor), "Retry canceled dip.");
        Exit(well, playerCollider);
        Invoke(interactor, "AdvanceAction");
        Check(motor.CanMove && ink.CurrentInk == 0f, "Leaving range before contact cancels dip.");
        Enter(well, playerCollider);
        Check(well.TryInteract(interactor), "Dip can retry after range cancellation.");
        float deadline = Time.time + 6f;
        while ((!ink.HasDipped || !motor.CanMove) && Time.time < deadline) yield return null;
        Check(ink.HasDipped && ink.CurrentInk == ink.MaxInk && motor.CanMove, "Animated dip fills to capacity and returns control.");
        Check(ink.TryRefill() && ink.CurrentInk == ink.MaxInk, "Refilling a full resource cannot exceed capacity.");
        meter.TickDisplay(0f);
        Check(group.alpha == 1f && graphic.Fill == 1f && graphic.Trail == 1f, "Refill briefly displays a full bar.");
        meter.TickDisplay(2f);
        Check(group.alpha == 0f, "Full bar returns to hidden idle state.");
        Exit(well, playerCollider);
    }

    private static void ValidateVisualGrounding()
    {
        Transform visual = (Transform)Field(motor, "visualRoot");
        Check(visual != null && visual != motor.transform && visual.IsChildOf(motor.transform), "Visual grounding uses a separate skeleton branch.");
        Invoke(motor, "RestoreVisualOffset");
        Vector3 original = visual.localPosition;
        Bounds capsule = body.bounds;
        Vector3 root = motor.transform.position;
        float before = LowestCharacterVertex();
        Invoke(motor, "LateUpdate");
        float after = LowestCharacterVertex();
        Check(Mathf.Abs(before - after - motor.visualGroundOffset) < 0.001f, "Only the visible character moves closer to the floor.");
        Check(body.bounds == capsule && motor.transform.position == root, "Visual grounding does not move the controller or scripted trajectory root.");
        Vector3 adjusted = visual.localPosition;
        Invoke(motor, "LateUpdate");
        Check(Vector3.Distance(visual.localPosition, adjusted) < 0.0001f, "Repeated visual updates do not accumulate downward drift.");
        motor.enabled = false;
        Check(Vector3.Distance(visual.localPosition, original) < 0.0001f, "Disabling the motor restores the unmodified animation pose.");
        motor.enabled = true;
        Debug.Log("CORE_VISUAL_GROUND offset=" + motor.visualGroundOffset + " meshBottomBefore=" + before
            + " meshBottomAfter=" + after + " capsuleBottom=" + capsule.min.y);
    }

    private static float LowestCharacterVertex()
    {
        float lowest = float.PositiveInfinity;
        foreach (SkinnedMeshRenderer renderer in motor.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh = new Mesh();
            try
            {
                renderer.BakeMesh(mesh);
                foreach (Vector3 vertex in mesh.vertices)
                    lowest = Mathf.Min(lowest, renderer.transform.TransformPoint(vertex).y);
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
        return lowest;
    }

    private static void ValidateCamera(CameraFollowingScript camera)
    {
        Check(camera != null && camera.followCamera != null && camera.target != null, "Follow camera is configured.");
        Transform originalTarget = camera.target;
        GameObject anchor = new GameObject("RegressionCameraAnchor");
        GameObject obstacle = null;
        anchor.transform.position = new Vector3(400f, 40f, 400f);
        float fov = camera.followCamera.fieldOfView;
        try
        {
            camera.enabled = false;
            camera.target = anchor.transform;
            camera.enabled = true;
            Vector3 farOffset = new Vector3(camera.distance * 0.5f, camera.height, -camera.distance) - camera.aimOffset;
            Vector3 focus = anchor.transform.position + camera.aimOffset;
            Quaternion fixedAngle = camera.followCamera.transform.rotation;
            Check(Vector3.Distance(camera.followCamera.transform.position, focus + farOffset) < 0.001f,
                "Original framing remains the farthest zoom.");
            Check(Mathf.Abs(camera.MaxFollowDistance - camera.MinFollowDistance - 4f) < 0.001f,
                "Zoom range is exactly four world units.");
            camera.ApplyZoomInput(1f);
            camera.TickCamera(0.02f);
            Check(camera.TargetZoomOffset == 1f && camera.CurrentZoomOffset > 0f && camera.CurrentZoomOffset < 1f,
                "Positive wheel input smoothly zooms in.");
            camera.ApplyZoomInput(100f);
            for (int i = 0; i < 150; i++) camera.TickCamera(0.02f);
            Check(camera.TargetZoomOffset == 4f && Mathf.Abs(camera.CurrentZoomOffset - 4f) < 0.001f,
                "Zoom-in clamps at four units.");
            Check(Mathf.Abs(Vector3.Distance(camera.followCamera.transform.position, focus) - camera.MinFollowDistance) < 0.001f,
                "Camera dollies along the original viewing direction.");
            Check(Quaternion.Angle(fixedAngle, camera.followCamera.transform.rotation) < 0.001f
                && camera.followCamera.fieldOfView == fov, "Zoom changes neither camera angle nor field of view.");
            camera.ApplyZoomInput(-100f);
            for (int i = 0; i < 150; i++) camera.TickCamera(0.02f);
            Check(camera.TargetZoomOffset == 0f && Vector3.Distance(camera.followCamera.transform.position, focus + farOffset) < 0.001f,
                "Zoom-out stops at the original framing.");
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position = focus + farOffset * 0.5f;
            obstacle.transform.localScale = Vector3.one * 2f;
            Physics.SyncTransforms();
            camera.TickCamera(0.02f);
            Check(Vector3.Distance(camera.followCamera.transform.position, focus + farOffset) < 0.001f,
                "Walls no longer shorten the camera distance.");
            anchor.transform.position += new Vector3(6f, 4f, 3f);
            anchor.transform.rotation = Quaternion.Euler(0f, 130f, 0f);
            camera.TickCamera(0.02f);
            Check(Quaternion.Angle(fixedAngle, camera.followCamera.transform.rotation) < 0.001f,
                "Horizontal/vertical follow and character rotation retain the fixed top-down angle.");
            for (int i = 0; i < 200; i++) camera.TickCamera(0.02f);
            Check(Vector3.Distance(camera.followCamera.transform.position, anchor.transform.position + camera.aimOffset + farOffset) < 0.001f,
                "Follow settles at the original offset after height changes.");
            Vector3 pausedPose = camera.followCamera.transform.position;
            anchor.transform.position += Vector3.one;
            camera.TickCamera(0f);
            Check(camera.followCamera.transform.position == pausedPose, "Zero delta time keeps the camera pose.");
            Time.timeScale = 0f;
            camera.ApplyZoomInput(1f);
            Check(camera.TargetZoomOffset == 0f, "Paused wheel input is ignored.");
            Time.timeScale = 1f;
            camera.ApplyZoomInput(float.NaN);
            camera.ApplyZoomInput(float.PositiveInfinity);
            Check(camera.TargetZoomOffset == 0f, "Invalid wheel values cannot corrupt the camera.");
            camera.ApplyZoomInput(2f);
            camera.enabled = false;
            camera.enabled = true;
            Check(camera.TargetZoomOffset == 2f && camera.CurrentZoomOffset == 2f,
                "Component re-enable retains chosen zoom.");
            float pitch = Vector3.Dot(camera.followCamera.transform.forward, Vector3.up);
            float radius = camera.CurrentFollowDistance;
            camera.ApplyRotationInput(30f);
            camera.TickCamera(0.02f);
            Check(camera.TargetYawOffset == 90f && camera.CurrentYawOffset > 0f && camera.CurrentYawOffset < 90f,
                "Horizontal mouse input starts a smooth orbit.");
            for (int i = 0; i < 150; i++) camera.TickCamera(0.02f);
            Check(Mathf.Abs(Mathf.DeltaAngle(camera.CurrentYawOffset, 90f)) < 0.001f,
                "Mouse orbit settles at the chosen horizontal heading.");
            Check(Mathf.Abs(Vector3.Dot(camera.followCamera.transform.forward, Vector3.up) - pitch) < 0.0001f
                && Mathf.Abs(camera.CurrentFollowDistance - radius) < 0.001f && camera.followCamera.fieldOfView == fov,
                "Orbit retains the original top-down pitch, distance and field of view.");
            Vector3 rotatedOffset = Quaternion.AngleAxis(90f, Vector3.up) * farOffset.normalized * radius;
            Check(Vector3.Distance(camera.followCamera.transform.position, anchor.transform.position + camera.aimOffset + rotatedOffset) < 0.001f,
                "Camera orbits the followed anchor instead of turning in place.");
            camera.ApplyRotationInput(360f / camera.rotationSensitivity);
            Check(Mathf.Abs(Mathf.DeltaAngle(camera.TargetYawOffset, 90f)) < 0.001f, "Full rotation wraps without losing the heading.");
            float yaw = camera.TargetYawOffset;
            Time.timeScale = 0f;
            camera.ApplyRotationInput(10f);
            Check(camera.TargetYawOffset == yaw, "Paused mouse rotation is ignored.");
            Time.timeScale = 1f;
            camera.ApplyRotationInput(float.NaN);
            camera.ApplyRotationInput(float.PositiveInfinity);
            Check(camera.TargetYawOffset == yaw, "Invalid mouse values cannot corrupt the camera.");
            camera.enabled = false;
            camera.enabled = true;
            Check(camera.CurrentYawOffset == yaw && camera.TargetYawOffset == yaw, "Component re-enable retains chosen orbit.");
            camera.enabled = false;
            Vector3 disabledPose = camera.followCamera.transform.position;
            camera.ApplyRotationInput(10f);
            camera.TickCamera(0.02f);
            Check(camera.TargetYawOffset == yaw && camera.followCamera.transform.position == disabledPose,
                "Disabled camera ignores orbit requests.");
            camera.enabled = true;
        }
        finally
        {
            Time.timeScale = 1f;
            camera.enabled = true;
            camera.ApplyRotationInput(-camera.TargetYawOffset / camera.rotationSensitivity);
            camera.ApplyZoomInput(-100f);
            camera.enabled = false;
            camera.target = originalTarget;
            camera.enabled = true;
            if (obstacle != null) UnityEngine.Object.DestroyImmediate(obstacle);
            UnityEngine.Object.DestroyImmediate(anchor);
        }
    }

    private static void ExpectCallbackFailure(Func<bool> action, string message)
    {
        expectedExceptions = 1;
        try
        {
            Check(!action() && expectedExceptions == 0 && motor.CanMove, message);
        }
        finally { expectedExceptions = 0; }
    }

    private static void MoveTicks(Vector2 input, float duration, float step = 0.02f)
    {
        int ticks = Mathf.RoundToInt(duration / step);
        for (int tick = 0; tick < ticks; tick++)
        {
            motor.SetMoveInput(input);
            motor.TickMovement(step);
        }
    }

    private static void ParkOnFloor()
    {
        interactor.CancelAction();
        Bounds bounds = playerCollider.bounds;
        Vector3 targetCenter = new Vector3(200f, 20f + bounds.extents.y + 0.01f, 200f);
        motor.Teleport(motor.transform.position + targetCenter - bounds.center);
        motor.transform.rotation = Quaternion.identity;
        motor.SetMoveInput(Vector2.zero);
        Physics.SyncTransforms();
        motor.RefreshGroundContact();
    }

    private static DotDrawing FindDot(DotDrawingHandler task, int index)
    {
        foreach (DotDrawing dot in task.Dots) if (dot.dotIndex == index) return dot;
        throw new Exception("Missing dot " + index);
    }

    private static void ValidateLayout(Canvas canvas)
    {
        Check(canvas != null, "Scene has UI canvas.");
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        Check(scaler != null && scaler.referenceResolution == new Vector2(1920f, 1080f)
            && Mathf.Abs(scaler.matchWidthOrHeight - 0.5f) < 0.01f, "Shared UI scale policy.");
        RenderMode originalMode = canvas.renderMode;
        RectTransform root = canvas.GetComponent<RectTransform>();
        Vector2 originalSize = root.sizeDelta;
        Vector3 originalScale = root.localScale;
        scaler.enabled = false;
        canvas.renderMode = RenderMode.WorldSpace;
        root.localScale = Vector3.one;
        try
        {
            foreach (Vector2 resolution in new[] { new Vector2(1920f, 1080f), new Vector2(1600f, 1000f),
                new Vector2(800f, 600f), new Vector2(2560f, 1080f) })
            {
                float scale = Mathf.Sqrt(resolution.x / 1920f * resolution.y / 1080f);
                root.sizeDelta = resolution / scale;
                Canvas.ForceUpdateCanvases();
                foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>())
                {
                    Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, graphic.rectTransform);
                    Check(bounds.min.x >= root.rect.xMin - 1f && bounds.max.x <= root.rect.xMax + 1f
                        && bounds.min.y >= root.rect.yMin - 1f && bounds.max.y <= root.rect.yMax + 1f,
                        "UI within viewport: " + graphic.name + " at " + resolution);
                }
                TaskStatusBar hud = canvas.GetComponentInChildren<TaskStatusBar>();
                if (hud != null) ValidateHudReadability(hud, root);
            }
        }
        finally
        {
            root.sizeDelta = originalSize;
            root.localScale = originalScale;
            canvas.renderMode = originalMode;
            scaler.enabled = true;
            Canvas.ForceUpdateCanvases();
        }
    }

    private static void ValidateHudReadability(TaskStatusBar hud, RectTransform canvas)
    {
        Text description = (Text)Field(hud, "taskDescriptionText");
        Text prompt = (Text)Field(hud, "interactionPromptText");
        Text heading = (Text)Field(hud, "taskHeadingText");
        Check(description.fontSize >= 36 && description.resizeTextMinSize >= 28
            && description.resizeTextMaxSize >= 36, "Task text remains enlarged even with best-fit enabled.");
        Check(prompt.fontSize >= 28 && heading.fontSize >= 24, "Interaction prompt and heading are enlarged.");
        var messages = (System.Collections.Generic.List<string>)Field(hud, "taskDescriptions");
        foreach (string message in messages) CheckTextFits(description, message);
        CheckTextFits(description, (string)Field(hud, "gameStartMessage"));
        CheckTextFits(description, (string)Field(hud, "gameOverMessage"));
        CheckTextFits(prompt, (string)Field(hud, "interactionMessage"));
        CheckTextFits(prompt, (string)Field(hud, "edgeMessage"));
        CheckTextFits(prompt, (string)Field(hud, "freeStampMessage"));
        CheckTextFits(prompt, (string)Field(hud, "stampBlockedMessage"));
        CheckTextFits(prompt, (string)Field(hud, "returnToMenuMessage"));
        CheckTextFits(heading, heading.text);
        var images = (System.Collections.Generic.List<Image>)Field(hud, "taskStatusImages");
        Bounds headingBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, heading.rectTransform);
        Bounds descriptionBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, description.rectTransform);
        Bounds previous = default;
        for (int i = 0; i < images.Count; i++)
        {
            Image image = images[i];
            Check(image.rectTransform.rect.width >= 60f && image.rectTransform.rect.height >= 60f,
                "Task progress circle is enlarged: " + i);
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, image.rectTransform);
            Check(bounds.max.y <= headingBounds.min.y && bounds.min.y >= descriptionBounds.max.y,
                "Progress circles do not overlap heading or task text: " + i + " circles=" + bounds
                + " heading=" + headingBounds + " description=" + descriptionBounds);
            if (i > 0) Check(bounds.min.x - previous.max.x >= 11.9f, "Progress circles retain a visible gap: " + i);
            Text label = image.GetComponentInChildren<Text>();
            Check(label != null && label.fontSize >= 26, "Circle status label is enlarged: " + i);
            CheckTextFits(label, (i + 1).ToString());
            CheckTextFits(label, "OK");
            previous = bounds;
        }
    }

    private static void CheckTextFits(Text text, string message)
    {
        TextGenerationSettings settings = text.GetGenerationSettings(text.rectTransform.rect.size);
        settings.resizeTextForBestFit = false;
        using (var generator = new TextGenerator())
        {
            float height = generator.GetPreferredHeight(message, settings) / text.pixelsPerUnit;
            Check(height > 0f && height <= text.rectTransform.rect.height + 1f, "Enlarged text fits vertically: " + message);
            if (text.horizontalOverflow == HorizontalWrapMode.Overflow)
            {
                float width = generator.GetPreferredWidth(message, settings) / text.pixelsPerUnit;
                Check(width <= text.rectTransform.rect.width + 1f, "Enlarged text fits horizontally: " + message);
            }
        }
    }

    private static void Enter(InteractionTarget target, Collider collider) => InvokeBase(target, "OnTriggerEnter", collider);
    private static void Exit(InteractionTarget target, Collider collider) => InvokeBase(target, "OnTriggerExit", collider);
    private static void InvokeBase(object target, string method, object argument) => typeof(InteractionTarget)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, new[] { argument });
    private static void Invoke(object target, string method) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    private static object Field(object target, string name) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        assertions++;
    }
    private static void Finish(bool passed, string message)
    {
        if (finishing) return;
        finishing = true;
        Application.logMessageReceived -= OnLog;
        SessionState.SetInt(ResultKey, passed ? 0 : 1);
        SessionState.SetString(MessageKey, message);
        EditorApplication.isPlaying = false;
    }
}
