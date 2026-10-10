using UnityEngine;

namespace Mohakash
{
    /// <summary>Third-person orbit camera with a spring arm (sphere cast) and a terrain floor so it never dips underground.</summary>
    public class OrbitCamera : MonoBehaviour
    {
        public Transform Target;
        public float Distance = 4.2f, Height = 1.6f;
        public bool Input = true;       // mouse look enabled
        public bool AutoOrbit;          // slow cinematic orbit (briefing / habitat view)
        public MoonWorld World;

        float yaw = 30f, pitch = 14f, zoom = 1f, currentDist;
        float sYaw = 30f, sPitch = 14f, sZoom = 1f;      // smoothed copies actually used for the camera
        Vector3 pivotSmooth, pivotVel; Transform lastTarget;

        public void Snap(float newYaw, float newPitch) { yaw = newYaw; pitch = newPitch; sYaw = newYaw; sPitch = newPitch; }

        void LateUpdate()
        {
            if (Target == null) return;
            if (AutoOrbit) yaw += 4f * Time.unscaledDeltaTime;
            else if (Input && Cursor.lockState == CursorLockMode.Locked)
            {
                var d = GameInput.Look * 0.09f * Game.MouseSensitivity;
                yaw += d.x;
                pitch += (Game.InvertY ? d.y : -d.y);
                zoom = Mathf.Clamp(zoom - GameInput.Zoom * 0.0015f, 0.5f, 2.2f);
            }
            pitch = Mathf.Clamp(pitch, -20f, 70f);

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
            if (World != null) pos.y = Mathf.Max(pos.y, World.HeightAt(pos) + 0.5f);
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(pivot - pos));
        }
    }
}
