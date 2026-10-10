using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Mohakash
{
    /// <summary>
    /// Smooth character animation without Animator state transitions. All clips (Idle, Walk, Run, Air, Land, Interact,
    /// Wave, Death) sit in one mixer and their weights ease toward targets every frame, so Idle -> Walk -> Run blends
    /// continuously with the real movement speed and one-shots (wave, interact, land) fade in and out instead of popping.
    /// Needs no Animator Controller setup; it reads the clips Rebuild Project Setup already generated in
    /// Resources/Animation/Clips. If anything is missing, Init returns false and the caller keeps the old Animator.
    /// </summary>
    public class LocomotionBlender : MonoBehaviour
    {
        public enum Slot { Idle, Walk, Run, Air, Land, Interact, Wave, Death }

        public float WalkSpeed = 2.4f, RunSpeed = 4.6f;
        public float BlendRate = 9f;                 // higher = snappier blends

        // Set every frame by the owner
        public float MoveSpeed;                      // actual horizontal speed, m/s
        public bool Airborne, Landing, Dead;

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] play;
        AnimationClip[] clip;
        float[] weight, time;
        const int N = 8;
        Slot oneShot; float oneShotLeft; bool hasOneShot;
        bool ready;
        readonly float[] tgt = new float[8];

        static readonly string[] Names = { "Idle", "Walk", "Run", "Jump_Idle", "Jump_Land", "Interact", "Wave", "Death" };

        public bool Init(Animator animator)
        {
            if (animator == null) return false;
            clip = new AnimationClip[N];
            for (int i = 0; i < N; i++) clip[i] = Resources.Load<AnimationClip>("Animation/Clips/" + Names[i]);
            if (clip[(int)Slot.Idle] == null || clip[(int)Slot.Walk] == null || clip[(int)Slot.Run] == null) return false;
            for (int i = 0; i < N; i++) if (clip[i] == null) clip[i] = clip[(int)Slot.Idle];

            animator.runtimeAnimatorController = null;       // the playable graph drives this Animator now
            graph = PlayableGraph.Create("MohakashLocomotion");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Anim", animator);
            mixer = AnimationMixerPlayable.Create(graph, N);
            output.SetSourcePlayable(mixer);

            play = new AnimationClipPlayable[N];
            weight = new float[N]; time = new float[N];
            for (int i = 0; i < N; i++)
            {
                play[i] = AnimationClipPlayable.Create(graph, clip[i]);
                play[i].SetApplyFootIK(false);
                play[i].Pause();                              // we scrub time ourselves
                graph.Connect(play[i], 0, mixer, i);
                mixer.SetInputWeight(i, 0f);
            }
            weight[(int)Slot.Idle] = 1f;
            mixer.SetInputWeight((int)Slot.Idle, 1f);
            graph.Play();
            ready = true;
            return true;
        }

        public void Trigger(Slot s, float duration)
        {
            oneShot = s; oneShotLeft = duration; hasOneShot = true;
            time[(int)s] = 0f;
        }

        public void ClearOneShot() { hasOneShot = false; }

        static bool Loops(int i) => i <= (int)Slot.Air;

        void Update()
        {
            if (!ready) return;
            float dt = Time.deltaTime;
            if (hasOneShot) { oneShotLeft -= dt; if (oneShotLeft <= 0f) hasOneShot = false; }

            // ── target weights ──────────────────────────────────────────
            var t = tgt; System.Array.Clear(t, 0, N);
            if (Dead) t[(int)Slot.Death] = 1f;
            else if (hasOneShot) t[(int)oneShot] = 1f;
            else if (Airborne) t[(int)Slot.Air] = 1f;
            else if (Landing) t[(int)Slot.Land] = 1f;
            else
            {
                float s = MoveSpeed;
                if (s <= WalkSpeed)
                {
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s / WalkSpeed));
                    t[(int)Slot.Idle] = 1f - a; t[(int)Slot.Walk] = a;
                }
                else
                {
                    float b = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s - WalkSpeed) / Mathf.Max(0.1f, RunSpeed - WalkSpeed)));
                    t[(int)Slot.Walk] = 1f - b; t[(int)Slot.Run] = b;
                }
            }

            // ── ease weights, normalise ─────────────────────────────────
            float k = 1f - Mathf.Exp(-BlendRate * dt);
            float sum = 0f;
            for (int i = 0; i < N; i++) { weight[i] = Mathf.Lerp(weight[i], t[i], k); if (weight[i] < 0.001f) weight[i] = 0f; sum += weight[i]; }
            if (sum < 0.0001f) { weight[(int)Slot.Idle] = 1f; sum = 1f; }

            // ── advance clip times (walk/run scaled so feet match speed) ──
            for (int i = 0; i < N; i++)
            {
                weight[i] /= sum;
                mixer.SetInputWeight(i, weight[i]);
                if (weight[i] <= 0f && !(i == (int)Slot.Idle)) continue;
                float len = Mathf.Max(0.05f, clip[i].length);
                float rate = 1f;
                if (i == (int)Slot.Walk) rate = Mathf.Clamp(MoveSpeed / WalkSpeed, 0.7f, 1.3f);
                else if (i == (int)Slot.Run) rate = Mathf.Clamp(MoveSpeed / RunSpeed, 0.8f, 1.4f);
                time[i] += dt * rate;
                if (Loops(i)) time[i] = Mathf.Repeat(time[i], len);
                else time[i] = Mathf.Min(time[i], len);
                play[i].SetTime(time[i]);
            }
        }

        void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }
    }
}
