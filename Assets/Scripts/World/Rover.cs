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
        float verticalVelocity;   // for smooth terrain following with moon gravity
        Transform model;

        // ── Factory ────────────────────────────────────────────────────────

        public static Rover Create(MoonWorld world, Vector3 pos)
        {
            var root = new GameObject("Scout Rover (ROV-SC)");
            root.transform.position = pos;

            var m = MoonWorld.Spawn("Rover", pos, Quaternion.identity, 0.62f,
                                    root.transform, "Rover model (Quaternius CC0)");
            if (m != null)
            {
                // Fix any glTF/broken materials so the rover is actually visible in URP
                ModelMaterialFixer.Fix(m);
                MoonWorld.AddBoxCollider(m, 0.9f);

                // Ensure the rover casts and receives shadows
                foreach (var r in m.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows    = true;
                }
            }

            var r2         = root.AddComponent<Rover>();
            r2.World       = world;
            r2.model       = m ? m.transform : null;
            r2.yaw         = 0f;

            // Attach the animator – it reads Rover.Driving and GameInput itself.
            root.AddComponent<RoverAnimator>();

            return r2;
        }

        // ── Per-frame driving ──────────────────────────────────────────────

        void Update()
        {
            if (World == null) return;
            float dt = Time.deltaTime;

            Vector2 input  = Driving ? GameInput.Move : Vector2.zero;
            float   target = input.y * MaxSpeed * (GameInput.Sprint ? 1f : 0.7f);

            // Faster acceleration than braking for a snappier feel
            float accelRate = (Mathf.Abs(target) > Mathf.Abs(speed)) ? 3f : 6f;
            speed = Mathf.MoveTowards(speed, target, accelRate * dt);

            if (Mathf.Abs(speed) > 0.2f)
                yaw += input.x * 55f * dt * Mathf.Sign(speed);

            Vector3 fwd  = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            Vector3 next = transform.position + fwd * speed * dt;

            // ── Boundary clamp (XZ only) ───────────────────────────────────
            float xzDist = new Vector3(next.x, 0, next.z).magnitude;
            if (xzDist > MoonWorld.PlayRadius) { speed = 0; next = transform.position; }

            // ── Slope check ────────────────────────────────────────────────
            float h0   = SampleTerrain(transform.position);
            float h1   = SampleTerrain(next);
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
                hit.collider.gameObject.name != "Moon Terrain")
            { speed = 0; next = transform.position; dist = 0; }

            // ── Battery drain ──────────────────────────────────────────────
            if (dist > 0 && UseEnergy != null && !UseEnergy(dist))
            { speed = 0; next = transform.position; }

            // ── Terrain follow with smooth vertical settling ───────────────
            // Use SampleTerrain (ignores own collider) to get true ground height.
            float targetY  = SampleTerrain(next);
            float currentY = transform.position.y;

            if (currentY > targetY + 0.05f)
            {
                // Above ground – fall with moon gravity
                verticalVelocity -= Astronaut.MoonGravity * dt;
                float newY = Mathf.Max(currentY + verticalVelocity * dt, targetY);
                next.y = newY;
                if (newY <= targetY + 0.05f) verticalVelocity = 0f;
            }
            else
            {
                // On ground – settle smoothly, kill vertical velocity
                next.y = Mathf.Lerp(currentY, targetY, Mathf.Min(1f, 18f * dt));
                verticalVelocity = 0f;
            }

            transform.position = next;

            // Align to terrain normal
            Vector3 terrainNormal = Vector3.up;
            if (Physics.Raycast(next + Vector3.up * 5f, Vector3.down, out var normalHit, 20f,
                                 ~0, QueryTriggerInteraction.Ignore) &&
                normalHit.collider.gameObject.name == "Moon Terrain")
                terrainNormal = normalHit.normal;

            var rot = Quaternion.FromToRotation(Vector3.up, terrainNormal) * Quaternion.Euler(0, yaw, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 6f * dt);

            CurrentSpeed = speed;
        }

        /// <summary>
        /// Samples the terrain height at a world position, ignoring the rover's own colliders.
        /// Uses the procedural formula as a reliable fallback when the raycast misses.
        /// </summary>
        float SampleTerrain(Vector3 pos)
        {
            // Raycast from high above, only hit the Moon Terrain mesh collider
            if (Physics.Raycast(new Vector3(pos.x, 500f, pos.z), Vector3.down, out var hit, 1000f,
                                 ~0, QueryTriggerInteraction.Ignore) &&
                hit.collider.gameObject.name == "Moon Terrain")
                return hit.point.y;
            // Fallback to the procedural formula
            return MoonWorld.RawHeight(pos.x, pos.z) - MoonWorld.RawHeight(0, 0);
        }

        // ── Helpers ────────────────────────────────────────────────────────

        public Vector3 ExitPoint =>
            transform.position + transform.right * 2.6f + Vector3.up * 0.5f;
    }
}
