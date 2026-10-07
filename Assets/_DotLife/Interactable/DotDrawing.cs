using UnityEngine;

public class DotDrawing : InteractionTarget
{
    public bool isPressInteract;
    public int dotIndex;
    public int texIndex;
    public int characterAction = -1;
    public DotDrawingHandler Owner { get; private set; }
    public override bool RequiresButton => isPressInteract;
    public override int Priority => 10;

    public void Bind(DotDrawingHandler owner) => Owner = owner;
    public override bool CanInteract(BasicBehaviourScript player) => IsInRange(player)
        && player.CanInteract && Owner != null && Owner.CanAccept(this)
        && (Owner.TaskId == DemoTaskId.Diary || player.runtime.IsTaskComplete(DemoTaskId.Diary));
    public override bool TryInteract(InteractingHandler interactor)
    {
        if (interactor == null || !interactor.isActiveAndEnabled || !CanInteract(interactor.Movement)) return false;
        return interactor.TryPerformDrawing(this);
    }
}
