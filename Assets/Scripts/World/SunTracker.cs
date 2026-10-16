using UnityEngine;

namespace Mohakash
{
    /// <summary>Turns a VSAT solar blanket (child named "Tracker") about the mast so it faces the Sun, as the real
    /// vertical arrays do at the lunar south pole where the Sun circles just above the horizon.</summary>
    public class SunTracker : MonoBehaviour
    {
        public Light Sun;
        Transform tracker;

        void Start()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == "Tracker") { tracker = t; break; }
        }

        void LateUpdate()
        {
            if (tracker == null || Sun == null) return;
            Vector3 d = -Sun.transform.forward; d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) return;
            tracker.rotation = Quaternion.Slerp(tracker.rotation, Quaternion.LookRotation(d.normalized, Vector3.up), 1f - Mathf.Exp(-0.8f * Time.deltaTime));
        }
    }
}
