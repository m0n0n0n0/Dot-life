using UnityEngine;

[DisallowMultipleComponent]
public class PlayerSfxEvents : MonoBehaviour
{
    [SerializeField] private PlayerSfx output;

    // Animation Events arrive on the Animator's object, not the scene audio manager.
    public void OnFootstep()
    {
        if (isActiveAndEnabled && output != null) output.OnFootstep();
    }
}
