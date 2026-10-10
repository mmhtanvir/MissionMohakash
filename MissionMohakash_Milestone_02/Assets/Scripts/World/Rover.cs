using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Scout Rover (ROV-SC): arcade driving that hugs the terrain.
    /// Battery cost per metre from GDD §19. Max climbable slope 20°.
    ///
    /// Upgraded (Milestone 02):
    ///   • <see cref="RoverAnimator"/> is added automatically in <see cref="Create"/>
    ///     and wired up, giving the rover wheel spin, body lean, dust trail and body bob.
    ///   • Speed value is exposed via <see cref="CurrentSpeed"/> so RoverAnimator can
    ///     read it without an extra per-frame calculation.
    ///   • Very minor: engine rumble placeholder comment; ExitPoint unchanged.
    /// </summary>
    public class Rover : MonoBehaviour
    {
        public const float MaxSpeed = 7f, MaxSlope = 20f;

        public bool Driving;
        public System.Func<float, bool> UseEnergy;   // metres -> false when battery empty
        public MoonWorld World;

        /// <summary>Signed forward speed in m/s, updated every frame. Used by RoverAnimator.</summary>
        public float CurrentSpeed { get; private set; }

        float speed, yaw;
        Transform model;

        // ── Factory ────────────────────────────────────────────────────────

        public static Rover Create(MoonWorld world, Vector3 pos)
        {
            var root = new GameObject("Scout Rover (ROV-SC)");
            root.transform.position = pos;

            var m = MoonWorld.Spawn("Rover", pos, Quaternion.identity, 0.62f,
                                    root.transform, "Rover model (Quaternius CC0)");
            if (m != null) MoonWorld.AddBoxCollider(m, 0.9f);

            var r          = root.AddComponent<Rover>();
            r.World        = world;
            r.model        = m ? m.transform : null;
            r.yaw          = 0f;

            // Attach the animator – it reads Rover.Driving and GameInput itself.
            root.AddComponent<RoverAnimator>();

            return r;
        }

        // ── Per-frame driving ──────────────────────────────────────────────

        void Update()
        {
            if (World == null) return;
            float dt = Time.deltaTime;

            Vector2 input   = Driving ? GameInput.Move : Vector2.zero;
            float   target  = input.y * MaxSpeed * (GameInput.Sprint ? 1f : 0.7f);

            // Faster acceleration than braking for a snappier feel
            float accelRate = (Mathf.Abs(target) > Mathf.Abs(speed)) ? 3f : 6f;
            speed = Mathf.MoveTowards(speed, target, accelRate * dt);

            if (Mathf.Abs(speed) > 0.2f)
                yaw += input.x * 55f * dt * Mathf.Sign(speed);

            Vector3 fwd  = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            Vector3 next = transform.position + fwd * speed * dt;

            // ── Boundary clamp ─────────────────────────────────────────────
            if (next.magnitude > MoonWorld.PlayRadius) { speed = 0; next = transform.position; }

            // ── Slope check ────────────────────────────────────────────────
            float h0   = World.HeightAt(transform.position);
            float h1   = World.HeightAt(next);
            float dist = Vector3.Distance(new Vector3(next.x, 0, next.z),
                                          new Vector3(transform.position.x, 0, transform.position.z));
            if (dist > 0.0001f && Mathf.Atan2(h1 - h0, dist) * Mathf.Rad2Deg > MaxSlope)
            { speed = 0; next = transform.position; }

            // ── Obstacle check ─────────────────────────────────────────────
            if (dist > 0.0001f &&
                Physics.SphereCast(transform.position + Vector3.up * 1.2f, 1.1f,
                    fwd * Mathf.Sign(speed), out var hit, dist + 1.3f,
                    ~0, QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(transform) &&
                !(hit.collider is TerrainCollider))
            { speed = 0; next = transform.position; dist = 0; }

            // ── Battery drain ──────────────────────────────────────────────
            if (dist > 0 && UseEnergy != null && !UseEnergy(dist))
            { speed = 0; next = transform.position; }

            // ── Move & terrain-align ───────────────────────────────────────
            next.y = World.HeightAt(next);
            transform.position = next;

            var n = World.Terrain.terrainData.GetInterpolatedNormal(
                (next.x - World.Terrain.transform.position.x) / MoonWorld.Size,
                (next.z - World.Terrain.transform.position.z) / MoonWorld.Size);
            var rot = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0, yaw, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 8f * dt);

            // Expose for RoverAnimator
            CurrentSpeed = speed;
        }

        // ── Helpers ────────────────────────────────────────────────────────

        public Vector3 ExitPoint =>
            transform.position + transform.right * 2.6f + Vector3.up * 0.5f;
    }
}
