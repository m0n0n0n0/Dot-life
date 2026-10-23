using UnityEngine;
using UnityEngine.Audio;

public enum FootstepPlaybackMode { Distance, AnimationEvents }

[CreateAssetMenu(menuName = "Dot Life/Player SFX", fileName = "SFX_Player")]
public class PlayerSfxSettings : ScriptableObject
{
    [Header("Routing")]
    public AudioMixerGroup output;

    [Header("Footsteps")]
    public AudioClip[] footstepClips = new AudioClip[0];
    [Range(0f, 1f)] public float footstepVolume = 0.5f;
    public Vector2 footstepPitch = new Vector2(0.95f, 1.05f);
    public FootstepPlaybackMode footstepMode = FootstepPlaybackMode.Distance;
    [Min(0.05f)] public float stepDistance = 1f;
    [Min(0.05f)] public float minimumStepInterval = 0.12f;
    [Min(0f)] public float minimumMoveSpeed = 0.1f;

    [Header("Stamp Contact")]
    public AudioClip[] stampClips = new AudioClip[0];
    [Range(0f, 1f)] public float stampVolume = 0.7f;
    public Vector2 stampPitch = new Vector2(0.97f, 1.03f);

    [Header("Ink Refill")]
    public AudioClip[] refillClips = new AudioClip[0];
    [Range(0f, 1f)] public float refillVolume = 0.7f;
    public Vector2 refillPitch = new Vector2(0.97f, 1.03f);
}
