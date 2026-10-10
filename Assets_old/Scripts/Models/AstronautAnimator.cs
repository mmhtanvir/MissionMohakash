using UnityEngine;

/// Drives the clips baked into astronaut.glb (Idle, Walk, Run, Wave, Interact, Death).
/// glTFast imports glTF animations as legacy clips on an Animation component by default,
/// so this script uses the legacy Animation API. Put it on the imported astronaut root.
public enum AstronautState { Idle, Walk, Run, Wave, Interact, Death }

public class AstronautAnimator : MonoBehaviour
{
    [SerializeField] float fade = 0.15f;
    [SerializeField] float walkSpeed = 1.7f;   // m/s the Walk clip is authored for (no foot sliding)
    [SerializeField] float runSpeed = 4.2f;    // m/s the Run clip is authored for

    Animation anim;
    AstronautState state = (AstronautState)(-1);
    bool dead;

    void Awake()
    {
        anim = GetComponentInChildren<Animation>();
        if (anim == null) { Debug.LogError("AstronautAnimator: no Animation component found. Check glTFast import settings (animation = Legacy)."); enabled = false; return; }
        foreach (AnimationState s in anim)
        {
            string n = s.name.ToLowerInvariant();
            bool loop = n.Contains("idle") || n.Contains("walk") || n.Contains("run");
            s.wrapMode = n.Contains("death") ? WrapMode.ClampForever : loop ? WrapMode.Loop : WrapMode.Once;
        }
        anim.playAutomatically = false;
        Play(AstronautState.Idle);
    }

    void Update()
    {
        // one-shot clips return to Idle when they finish (Death stays)
        if (state == AstronautState.Wave || state == AstronautState.Interact)
            if (!anim.isPlaying) Play(AstronautState.Idle);
    }

    /// Call every frame from your controller with the current horizontal speed (m/s).
    public void SetMoveSpeed(float speed)
    {
        if (dead || state == AstronautState.Wave || state == AstronautState.Interact) return;
        if (speed < 0.1f) Play(AstronautState.Idle);
        else if (speed < (walkSpeed + runSpeed) * 0.5f) { Play(AstronautState.Walk); Speed(AstronautState.Walk, speed / walkSpeed); }
        else { Play(AstronautState.Run); Speed(AstronautState.Run, speed / runSpeed); }
    }

    public void Play(AstronautState s)
    {
        if (anim == null || dead || s == state) return;
        string clip = Find(s); if (clip == null) return;
        state = s; if (s == AstronautState.Death) dead = true;
        if (s == AstronautState.Wave || s == AstronautState.Interact || s == AstronautState.Death) anim[clip].time = 0f;
        anim.CrossFade(clip, fade);
    }

    void Speed(AstronautState s, float mult) { string c = Find(s); if (c != null) anim[c].speed = Mathf.Clamp(mult, 0.5f, 1.6f); }

    string Find(AstronautState s)
    {
        string key = s.ToString().ToLowerInvariant();
        foreach (AnimationState st in anim) if (st.name.ToLowerInvariant().Contains(key)) return st.name;
        return null;
    }
}
