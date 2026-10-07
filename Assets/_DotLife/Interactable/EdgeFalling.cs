using UnityEngine;

public class EdgeFalling : InteractionTarget
{
    [SerializeField] private GameState gameState;
    [SerializeField] private int requiredTaskIndex;
    public Vector3 endPoint;
    public override bool RequiresButton => true;

    void Awake()
    {
        if (gameState == null) gameState = FindObjectOfType<GameState>();
    }
    public bool CanFall() => gameState != null && gameState.isActiveAndEnabled
        && gameState.Phase == GameSessionPhase.Playing
        && gameState.IsTaskComplete((DemoTaskId)requiredTaskIndex);
    public override bool CanInteract(BasicBehaviourScript player) => IsInRange(player)
        && player.CanInteract && CanFall();
    public override bool TryInteract(InteractingHandler interactor) => interactor != null
        && CanInteract(interactor.Movement) && interactor.TryFall(endPoint);
}
