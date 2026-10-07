using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
public class InkWell : InteractionTarget
{
    public override bool RequiresButton => true;
    public override int Priority => 20;

    public bool CanRefill(BasicBehaviourScript player)
    {
        PlayerInk ink = player != null ? player.GetComponent<PlayerInk>() : null;
        return IsInRange(player) && ink != null && ink.CanRefill;
    }

    public override bool CanInteract(BasicBehaviourScript player) => CanRefill(player) && player.CanInteract;
    public override bool TryInteract(InteractingHandler interactor) => interactor != null
        && CanInteract(interactor.Movement) && interactor.TryRefillInk(this);
}
