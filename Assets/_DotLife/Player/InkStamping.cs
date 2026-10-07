using System;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(BasicBehaviourScript), typeof(PlayerInk))]
public class InkStamping : MonoBehaviour
{
    [SerializeField] private InkStampSettings settings;
    [SerializeField] private TriggerBus trigger;
    private BasicBehaviourScript motor;
    private PlayerInk ink;
    private InkSurfaceSampler sampler;
    private InkMark[] marks;
    private GameObject poolRoot;
    private int nextMark;
    private int sequence;
    public InkStampSettings Settings => settings;
    public event Action OnPlacementRejected;
    public int Capacity => marks != null ? marks.Length : 0;
    public int ActiveMarkCount
    {
        get
        {
            int count = 0;
            if (marks != null) foreach (InkMark mark in marks)
                if (mark != null && mark.gameObject.activeInHierarchy) count++;
            return count;
        }
    }

    void Awake()
    {
        motor = GetComponent<BasicBehaviourScript>();
        ink = GetComponent<PlayerInk>();
        if (trigger == null) trigger = FindObjectOfType<TriggerBus>();
        if (settings == null || settings.material == null || trigger == null)
        { Debug.LogError("InkStamping requires settings, ink material and TriggerBus.", this); return; }
        sampler = new InkSurfaceSampler(motor, trigger, settings);
        marks = new InkMark[settings.Capacity];
        poolRoot = new GameObject("_Runtime_InkMarks");
    }

    private bool ContextValid => isActiveAndEnabled && sampler != null && ink != null && ink.isActiveAndEnabled
        && ink.Runtime != null && ink.Runtime.isActiveAndEnabled && ink.Runtime.Phase == GameSessionPhase.Playing
        && ink.Runtime.IsTaskComplete(DemoTaskId.Diary) && settings != null && settings.material != null;

    public bool CanOfferStamp => ContextValid && motor.CanInteract && sampler.TryCenter(out _);

    public bool TryStamp(InteractingHandler interactor)
    {
        if (interactor == null || !interactor.isActiveAndEnabled || interactor.Movement != motor || !CanOfferStamp) return false;
        interactor.RefreshSelection();
        if (interactor.CurrentTarget != null) return false;
        if (!ink.CanSpend) { ink.ReportInsufficientInk(); return false; }
        if (!sampler.TryBuild(out InkStampPlacement preview)) return RejectPlacement();
        Collider receiver = preview.receiver;
        Vector3 start = motor.transform.position;
        return interactor.TryPerformAction(0,
            () => ContextValid && ink.CanSpend && Vector3.Distance(start, motor.transform.position)
                <= Mathf.Clamp(settings.maximumActionDrift, 0.01f, 0.5f)
                && sampler.TryCenter(out RaycastHit hit) && hit.collider == receiver,
            () => CommitPlacement(receiver));
    }

    private bool CommitPlacement(Collider receiver)
    {
        if (!ContextValid) return false;
        if (!sampler.TryBuild(out InkStampPlacement placement) || placement.receiver != receiver) return RejectPlacement();
        return ink.TryCommitStamp(() => CreateMark(placement));
    }

    private bool RejectPlacement()
    {
        if (OnPlacementRejected != null) foreach (Action callback in OnPlacementRejected.GetInvocationList())
            try { callback(); } catch (Exception exception) { Debug.LogException(exception, this); }
        return false;
    }

    private bool CreateMark(InkStampPlacement placement)
    {
        if (!ContextValid || placement.receiver == null || marks == null || poolRoot == null) return false;
        InkMark mark = marks[nextMark];
        if (mark == null)
        {
            var root = new GameObject("InkMark_" + nextMark, typeof(MeshFilter), typeof(MeshRenderer), typeof(InkMark));
            root.transform.SetParent(poolRoot.transform, false);
            mark = root.GetComponent<InkMark>();
            marks[nextMark] = mark;
        }
        mark.Apply(placement, settings.material, sequence++ % 30000);
        nextMark = (nextMark + 1) % marks.Length;
        return true;
    }

    public bool TrySamplePlacement(out InkStampPlacement placement)
    {
        placement = default;
        return ContextValid && sampler.TryBuild(out placement);
    }

    public void ClearMarks()
    {
        if (marks == null) return;
        for (int i = 0; i < marks.Length; i++)
        {
            if (marks[i] != null) Destroy(marks[i].gameObject);
            marks[i] = null;
        }
        nextMark = sequence = 0;
    }

    void OnDestroy() { ClearMarks(); if (poolRoot != null) Destroy(poolRoot); }
}
