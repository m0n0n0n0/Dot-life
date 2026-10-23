using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BasicBehaviourScript))]
[DefaultExecutionOrder(-100)]
public class InteractingHandler : MonoBehaviour
{
    [SerializeField] private TriggerBus trigger;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [Range(0f, 1f)] public float treadContactTime = 0.4f;
    [Range(0f, 1f)] public float hitContactTime = 0.45f;
    [Min(0.1f)] public float actionTimeout = 5f;
    public BasicBehaviourScript Movement { get; private set; }
    public InteractionTarget CurrentTarget { get; private set; }
    public DotDrawingHandler CurrentTask { get; private set; }
    public KeyCode InteractionKey => interactionKey;
    public PlayerInk Ink { get; private set; }
    public InkStamping Stamping { get; private set; }
    public bool CanReturnToMenu => !returningToMenu && !completionFeedbackPending && Movement != null && Movement.runtime != null
        && Movement.runtime.Phase == GameSessionPhase.Finished && cameraFollowing != null
        && cameraFollowing.IsEndingPromptReady;

    private Animator animator;
    private FallingHandler falling;
    private CameraFollowingScript cameraFollowing;
    private Func<bool> pendingValidity;
    private Func<bool> pendingCommit;
    private int actionHash;
    private float contactTime;
    private float elapsed;
    private bool sawAction;
    private bool committed;
    private bool focused = true;
    private bool dispatchingCallback;
    private bool returningToMenu;
    private bool completionFeedbackPending;
    public void SetCompletionFeedbackPending(bool pending) => completionFeedbackPending = pending;
    private bool CanReadInput => focused && Time.timeScale > 0f && !IsTyping();
    private bool CanContinueAction => Movement != null && Movement.isActiveAndEnabled
        && Movement.Controller != null && Movement.Controller.enabled && Movement.IsGrounded
        && Movement.runtime != null && Movement.runtime.isActiveAndEnabled
        && Movement.runtime.Phase == GameSessionPhase.Playing;

    void Awake()
    {
        Movement = GetComponent<BasicBehaviourScript>();
        Ink = GetComponent<PlayerInk>();
        Stamping = GetComponent<InkStamping>();
        animator = GetComponent<Animator>();
        falling = GetComponent<FallingHandler>();
        cameraFollowing = GetComponent<CameraFollowingScript>();
        if (trigger == null) trigger = FindObjectOfType<TriggerBus>();
    }

    void Update()
    {
        if (Movement == null || Movement.runtime == null) return;
        AdvanceAction();
        RefreshSelection();
        if (!CanReadInput)
        {
            Movement.SetMoveInput(Vector2.zero);
            return;
        }
        if (Movement.runtime.Phase == GameSessionPhase.Finished)
        {
            Movement.SetMoveInput(Vector2.zero);
            if (Input.GetKeyDown(KeyCode.Escape)) TryReturnToMenu();
            return;
        }
        if (Movement.IsAppearing)
        {
            Movement.SetMoveInput(Vector2.zero);
            return;
        }
        if (cameraFollowing != null)
        {
            cameraFollowing.ApplyZoomInput(Input.mouseScrollDelta.y);
            if (Input.GetMouseButton(1)) cameraFollowing.ApplyRotationInput(Input.GetAxisRaw("Mouse X"));
        }
        if (Movement.runtime.Phase == GameSessionPhase.Waiting && Input.anyKeyDown)
        {
            Movement.runtime.TryStartGame();
            Movement.SetMoveInput(Vector2.zero);
            return;
        }
        Vector2 input = new Vector2(
            (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
            (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));
        Movement.SetMoveInput(input);
        if (Input.GetKeyDown(interactionKey)) TryInteract();
        if (Movement.CanInteract) TryAutomaticDrawing();
    }

    void OnEnable()
    {
        if (Movement != null) Movement.OnActionStateChanged += HandleMovementState;
    }

    private void HandleMovementState(PlayerActionState state)
    {
        if (state == PlayerActionState.Interacting || pendingCommit == null) return;
        ClearPendingAction();
        if (state == PlayerActionState.Locomotion && animator != null && animator.isActiveAndEnabled)
            animator.CrossFadeInFixedTime("Base Layer.Idle", 0.08f);
    }

    private static bool IsTyping()
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null) return false;
        return EventSystem.current.currentSelectedGameObject.GetComponent<UnityEngine.UI.InputField>() != null;
    }

    public void RefreshSelection()
    {
        CurrentTarget = null;
        CurrentTask = null;
        if (Movement == null || trigger == null || Movement.runtime == null
            || Movement.runtime.Phase != GameSessionPhase.Playing) return;
        float taskDistance = float.PositiveInfinity;
        foreach (DotDrawing dot in trigger.dotTriggers)
        {
            if (dot == null) continue;
            float distance = dot.DistanceSquared(Movement.transform.position);
            if (dot.IsInRange(Movement) && dot.Owner != null && distance < taskDistance)
            { CurrentTask = dot.Owner; taskDistance = distance; }
            Consider(dot);
        }
        foreach (EdgeFalling edge in trigger.edgeTriggers) Consider(edge);
        foreach (InkWell well in trigger.inkWells) Consider(well);
        if (CurrentTarget is DotDrawing selected) CurrentTask = selected.Owner;
    }

    private void Consider(InteractionTarget candidate)
    {
        if (candidate == null || !candidate.RequiresButton || !candidate.CanInteract(Movement)) return;
        if (CurrentTarget == null || candidate.Priority > CurrentTarget.Priority
            || (candidate.Priority == CurrentTarget.Priority
                && candidate.DistanceSquared(Movement.transform.position) < CurrentTarget.DistanceSquared(Movement.transform.position)))
            CurrentTarget = candidate;
    }

    private void TryAutomaticDrawing()
    {
        if (trigger == null) return;
        DotDrawing best = null;
        foreach (DotDrawing dot in trigger.dotTriggers)
        {
            if (dot == null || dot.RequiresButton || !dot.CanInteract(Movement)) continue;
            if (best == null || dot.DistanceSquared(Movement.transform.position) < best.DistanceSquared(Movement.transform.position))
                best = dot;
        }
        if (best != null) best.TryInteract(this);
    }

    public bool TryPerformDrawing(DotDrawing dot)
    {
        if (dot == null || !dot.CanInteract(Movement) || !CanReadInput || Ink == null || !Ink.isActiveAndEnabled) return false;
        if (!Ink.CanSpend)
        {
            if (dot.RequiresButton) Ink.ReportInsufficientInk();
            return false;
        }
        return TryPerformAction(dot.characterAction,
            () => dot != null && dot.IsInRange(Movement) && dot.Owner != null && dot.Owner.CanAccept(dot)
                && Ink != null && Ink.CanSpend,
            () => dot != null && dot.Owner != null && Ink.TryCommitStamp(() => dot.Owner.TryCommit(dot)));
    }

    public bool TryRefillInk(InkWell well) => well != null && Ink != null && well.CanInteract(Movement)
        && TryPerformAction(0, () => well != null && well.CanRefill(Movement), () => Ink != null && Ink.TryRefill());

    public bool TryInteract()
    {
        if (!isActiveAndEnabled || !CanReadInput || Movement == null || !Movement.CanInteract) return false;
        RefreshSelection();
        return CurrentTarget != null ? CurrentTarget.TryInteract(this) : Stamping != null && Stamping.TryStamp(this);
    }

    public bool TryReturnToMenu()
    {
        if (!isActiveAndEnabled || !CanReadInput || !CanReturnToMenu
            || !Application.CanStreamedLevelBeLoaded("MainMenu")) return false;
        returningToMenu = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
        return true;
    }

    public bool TryPerformAction(int characterAction, Func<bool> canCommit, Func<bool> commit)
    {
        if (!isActiveAndEnabled || !CanReadInput || dispatchingCallback
            || Movement == null || !Movement.CanInteract || pendingCommit != null
            || canCommit == null || commit == null || !TryInvokeCallback(canCommit)) return false;
        if (characterAction < 0)
        {
            if (!Movement.TryBeginInteraction()) return false;
            bool accepted = TryInvokeCallback(commit);
            Movement.EndInteraction();
            return accepted;
        }
        actionHash = Animator.StringToHash(characterAction == 0 ? "Base Layer.Tread" : "Base Layer.Hit");
        if (characterAction > 1 || animator == null || !animator.isActiveAndEnabled
            || !animator.HasState(0, actionHash) || !Movement.TryBeginInteraction()) return false;
        pendingValidity = canCommit;
        pendingCommit = commit;
        committed = sawAction = false;
        elapsed = 0f;
        contactTime = Mathf.Clamp01(characterAction == 0 ? treadContactTime : hitContactTime);
        animator.CrossFadeInFixedTime(actionHash, 0.08f, 0, 0f);
        CurrentTarget = null;
        return true;
    }

    private void AdvanceAction()
    {
        if (Movement.ActionState != PlayerActionState.Interacting) return;
        if (pendingCommit == null || !CanContinueAction || (!committed && !TryInvokeCallback(pendingValidity))
            || animator == null || !animator.isActiveAndEnabled)
        { CancelAction(); return; }
        elapsed += Time.deltaTime;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash != actionHash && animator.IsInTransition(0))
            state = animator.GetNextAnimatorStateInfo(0);
        if (state.fullPathHash == actionHash)
        {
            sawAction = true;
            if (state.normalizedTime >= contactTime) CommitAction();
            if (pendingCommit == null || Movement.ActionState != PlayerActionState.Interacting) return;
            if (state.normalizedTime >= 1f) FinishAction();
        }
        else if (sawAction) FinishAction();
        if (elapsed >= Mathf.Max(0.1f, actionTimeout)) CancelAction();
    }

    // May also be called by an Animation Event at the actual contact frame.
    public void CommitAction()
    {
        if (dispatchingCallback || committed || pendingCommit == null
            || Movement.ActionState != PlayerActionState.Interacting) return;
        if (!CanContinueAction || !TryInvokeCallback(pendingValidity))
        { CancelAction(); return; }
        if (pendingCommit == null || Movement.ActionState != PlayerActionState.Interacting || !CanContinueAction)
        { CancelAction(); return; }
        // Set the guard before dispatch so synchronous callbacks cannot commit twice.
        committed = true;
        if (!TryInvokeCallback(pendingCommit)) CancelAction();
    }

    private bool TryInvokeCallback(Func<bool> callback)
    {
        if (callback == null) return false;
        dispatchingCallback = true;
        try
        {
            return callback();
        }
        catch (Exception exception)
        {
            CancelAction();
            Debug.LogException(exception, this);
            return false;
        }
        finally { dispatchingCallback = false; }
    }

    private void FinishAction()
    {
        if (!committed) { CancelAction(); return; }
        ClearPendingAction();
        Movement.EndInteraction();
    }

    public void CancelAction()
    {
        ClearPendingAction();
        if (Movement == null || Movement.ActionState != PlayerActionState.Interacting) return;
        Movement.EndInteraction();
        if (animator != null && animator.isActiveAndEnabled
            && animator.HasState(0, Animator.StringToHash("Base Layer.Idle")))
            animator.CrossFadeInFixedTime("Base Layer.Idle", 0.08f);
    }

    public bool TryFall(Vector3 destination) => isActiveAndEnabled && CanReadInput
        && !dispatchingCallback && falling != null && falling.TryFall(destination);

    private void ClearPendingAction()
    {
        pendingValidity = null;
        pendingCommit = null;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        focused = hasFocus;
        if (!hasFocus) { Movement?.SetMoveInput(Vector2.zero); CancelAction(); }
    }

    void OnDisable()
    {
        if (Movement != null) Movement.OnActionStateChanged -= HandleMovementState;
        CancelAction();
        Movement?.SetMoveInput(Vector2.zero);
        CurrentTarget = null;
        CurrentTask = null;
    }
}
