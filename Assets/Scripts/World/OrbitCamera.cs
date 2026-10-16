using UnityEngine;

namespace Mohakash
{
    /// <summary>Third-person orbit camera with a spring arm (sphere cast) and a terrain floor so it never dips underground.</summary>
    public class OrbitCamera : MonoBehaviour
    {
        public Transform Target;
        public float Distance = 4.2f, Height = 1.6f;
        public bool Input = true;       // mouse look enabled
        public bool AutoOrbit;
        public bool ClampToTerrain = true;   // off inside the habitat interior (it is not on the terrain)
        public float MaxPitch = 70f;          // slow cinematic orbit (briefing / habitat view)
        public MoonWorld World;

        float yaw = 30f, pitch = 14f, zoom = 1f, currentDist;
        float sYaw = 30f, sPitch = 14f, sZoom = 1f;      // smoothed copies actually used for the camera
        Vector3 pivotSmooth, pivotVel; Transform lastTarget;

        public void Snap(float newYaw, float newPitch) { yaw = newYaw; pitch = newPitch; sYaw = newYaw; sPitch = newPitch; }

        // ── follow shot (scripted sequences: airlock, hatch) ──────────────────────────────────────────
        // The camera stays at the figure's own eye height, at a fixed world-space offset from it (chosen per shot so it is
        // always on the open side of a wall), and looks at the figure's chest/head. No orbit, no spring arm, no wall clipping.
        bool shot; Vector3 shotOffset; float shotEye = 1.65f, shotLook = 1.35f;
        public bool InShot => shot;

        /// <param name="offset">horizontal offset from the figure (world XZ; y ignored)</param>
        /// <param name="snap">jump there instantly (after a teleport) instead of gliding</param>
        public void FollowShot(Vector3 offset, bool snap = false, float eye = 1.65f, float look = 1.35f)
        {
            bool first = !shot;
            shot = true; shotOffset = new Vector3(offset.x, 0f, offset.z); shotEye = eye; shotLook = look;
            if ((snap || first) && Target != null)
            {
                Vector3 p = Target.position + shotOffset + Vector3.up * shotEye, l = Target.position + Vector3.up * shotLook;
                if (snap) transform.SetPositionAndRotation(p, Quaternion.LookRotation(l - p));
            }
        }

        /// <summary>Back to the normal orbit camera, continuing from where the shot left the camera (no jump).</summary>
        public void EndShot()
        {
            if (!shot) return;
            shot = false;
            if (Target == null) return;
            Vector3 d = transform.position - (Target.position + Vector3.up * Height);
            float flat = new Vector2(d.x, d.z).magnitude;
            Snap(Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg, Mathf.Clamp(Mathf.Atan2(d.y, flat) * Mathf.Rad2Deg, -20f, MaxPitch));
            currentDist = Mathf.Max(0.8f, d.magnitude); pivotSmooth = Target.position + Vector3.up * Height; pivotVel = Vector3.zero;
        }

        void LateUpdate()
        {
            if (Target == null) return;
            if (shot)
            {
                float k = 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime);
                Vector3 shotPos = Target.position + shotOffset + Vector3.up * shotEye;
                Vector3 look = Target.position + Vector3.up * shotLook;
                Vector3 p = Vector3.Lerp(transform.position, shotPos, k);
                transform.SetPositionAndRotation(p, Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look - p), k * 1.4f));
                return;
            }
            if (AutoOrbit) yaw += 4f * Time.unscaledDeltaTime;
            else if (Input && Cursor.lockState == CursorLockMode.Locked)
            {
                var d = GameInput.Look * 0.09f * Game.MouseSensitivity;
                yaw += d.x;
                pitch += (Game.InvertY ? d.y : -d.y);
                zoom = Mathf.Clamp(zoom - GameInput.Zoom * 0.0015f, 0.5f, 2.2f);
            }
            pitch = Mathf.Clamp(pitch, -20f, MaxPitch);

            float dtc = Time.unscaledDeltaTime;
            float lk = 1f - Mathf.Exp(-16f * dtc);
            sYaw = Mathf.LerpAngle(sYaw, yaw, lk);
            sPitch = Mathf.Lerp(sPitch, pitch, lk);
            sZoom = Mathf.Lerp(sZoom, zoom, 1f - Mathf.Exp(-8f * dtc));

            var rot = Quaternion.Euler(sPitch, sYaw, 0);
            Vector3 rawPivot = Target.position + Vector3.up * Height;
            if (Target != lastTarget) { pivotSmooth = rawPivot; pivotVel = Vector3.zero; lastTarget = Target; }
            // critically damped follow removes the jitter of the character stepping / bobbing
            pivotSmooth = Vector3.SmoothDamp(pivotSmooth, rawPivot, ref pivotVel, 0.07f, Mathf.Infinity, dtc);
            Vector3 pivot = pivotSmooth;
            float want = Distance * sZoom;
            if (Physics.SphereCast(pivot, 0.3f, rot * Vector3.back, out var hit, want, ~(1 << 2), QueryTriggerInteraction.Ignore))
                want = Mathf.Max(0.8f, hit.distance - 0.1f);
            currentDist = want < currentDist ? want : Mathf.Lerp(currentDist, want, 1f - Mathf.Exp(-6f * dtc));
            Vector3 pos = pivot + rot * Vector3.back * currentDist;
            if (World != null && ClampToTerrain) pos.y = Mathf.Max(pos.y, World.HeightAt(pos) + 0.5f);
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(pivot - pos));
        }
    }
}
