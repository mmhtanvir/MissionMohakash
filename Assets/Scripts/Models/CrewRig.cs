using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Plays the clips baked into AstronautEVA.glb / CrewCasual.glb.
    /// glTFast imports glTF animations as legacy clips on an Animation component (Animation Method = Legacy, the default).
    /// If no Animation component exists the character simply stays in its bind pose (standing, arms down).
    /// </summary>
    public class CrewRig : MonoBehaviour
    {
        Animation anim;
        string current;
        public bool Ready => anim != null;
        public string Current => current;

        public bool Init()
        {
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            anim = GetComponentInChildren<Animation>(true);
            if (anim == null)
            {
                Debug.LogWarning("CrewRig: no Animation component on " + name + ". Set the .glb importer Animation Method to Legacy.");
                return false;
            }
            anim.playAutomatically = false;
            anim.cullingType = AnimationCullingType.AlwaysAnimate;
            foreach (AnimationState s in anim)
            {
                string n = s.name.ToLowerInvariant();
                bool loop = n.Contains("idle") || n.Contains("walk") || n.Contains("run") || n.Contains("sleep");
                s.wrapMode = (n.Contains("death") || n == "jump") ? WrapMode.ClampForever : loop ? WrapMode.Loop : WrapMode.Once;
            }
            anim.Stop();
            return true;
        }

        AnimationState Find(string clip)
        {
            if (anim == null) return null;
            foreach (AnimationState s in anim) if (string.Equals(s.name, clip, System.StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        public bool Has(string clip) => Find(clip) != null;

        readonly System.Collections.Generic.Dictionary<string, Transform> sockets = new System.Collections.Generic.Dictionary<string, Transform>();

        /// <summary>Finds an empty bone-parented transform baked into the GLB (Socket_Hold, Socket_HandL, Socket_HandR, Socket_Back, Socket_Helmet).</summary>
        public Transform Socket(string socketName)
        {
            if (sockets.TryGetValue(socketName, out var t) && t != null) return t;
            foreach (var c in GetComponentsInChildren<Transform>(true))
                if (c.name == socketName) { sockets[socketName] = c; return c; }
            return null;
        }

        public float Length(string clip) { var s = Find(clip); return s != null ? s.length : 0f; }

        public void Play(string clip, float fade = 0.15f, float speed = 1f, bool restart = false)
        {
            var s = Find(clip); if (s == null) return;
            s.speed = speed;
            if (current == s.name && !restart) return;
            if (restart || current != s.name) s.time = 0f;
            current = s.name;
            anim.CrossFade(s.name, fade);
        }

        public void SetSpeed(float speed)
        {
            var s = current != null ? Find(current) : null;
            if (s != null) s.speed = speed;
        }
    }
}
