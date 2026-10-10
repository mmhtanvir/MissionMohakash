using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Third-person astronaut: camera-relative WASD, sprint, lunar-gravity jump, Animator-driven animation.
    /// Animation clips from the CC0 Quaternius space-suit character (Idle / Walk / Run / Jump / Death).
    ///
    /// Upgraded (Milestone 02):
    ///   • Speed-based blend: Walk/Run clip speed is scaled to actual movement speed so feet never slide.
    ///   • Smooth blend threshold: cross-fade between Walk and Run is gated by a hysteresis band
    ///     so there is no flickering at the boundary.
    ///   • Footstep-style body bob: small procedural vertical oscillation driven by distance travelled,
    ///     which avoids the foot-sliding issue without needing IK.
    ///   • Landing recovery: a brief squat squash on landing using local scale (no extra clip needed).
    ///   • Suit helmet head sway: tiny XZ tilt proportional to turn rate for life.
    ///   • Jump apex hang: gravity is eased near zero velocity to give the low-gravity float feel.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Astronaut : MonoBehaviour
    {
        // ── Constants ──────────────────────────────────────────────────────
        public const float WalkSpeed  = 2.4f;
        public const float RunSpeed   = 4.6f;
        public const float MoonGravity = 1.62f;
        public const float Height      = 1.8f;

        // Walk→Run blend threshold with hysteresis
        const float WalkToRunSpeed   = 3.2f;    // m/s – switch Walk→Run above this
        const float RunToWalkSpeed   = 2.6f;    // m/s – switch Run→Walk below this

        // ── Public state ───────────────────────────────────────────────────
        public bool  Frozen;
        public float SpeedFactor = 1f;
        public bool  IsSprinting  { get; private set; }
        public bool  Dead         { get; private set; }

        // ── Components ─────────────────────────────────────────────────────
        CharacterController cc;
        Animator anim;
        Transform cam;
        Transform modelRoot;   // direct child holding the mesh (used for bob/squash)

        // ── Motion state ───────────────────────────────────────────────────
        Vector3  velocity;
        string   currentState;
        bool     isRunning;           // hysteresis flag
        bool     wasGrounded;
        float    landTimer;
        float    interactTimer;

        // ── Procedural animation state ─────────────────────────────────────
        float distanceTravelled;
        Vector3 prevPosition;
        float   bobPhase;
        Vector3 modelLocalOrigin;       // baked scale on Start
        float   landSquashTimer;        // seconds of landing squash remaining
        const float LandSquashDur = 0.22f;

        // Head sway (rotate the model root a tiny amount)
        float headSwayX, headSwayZ;

        // ── Factory ────────────────────────────────────────────────────────

        public static Astronaut Create(Vector3 pos)
        {
            var root = new GameObject("Astronaut (player)");
            root.transform.position = pos;
            root.layer = 2;   // Ignore Raycast

            var cc = root.AddComponent<CharacterController>();
            cc.height     = 1.8f;
            cc.radius     = 0.35f;
            cc.center     = new Vector3(0, 0.92f, 0);
            cc.slopeLimit = 40f;
            cc.stepOffset = 0.4f;

            var model = MoonWorld.Spawn("Astronaut", pos, Quaternion.identity, 1f,
                root.transform, "Astronaut model (Quaternius CC0 space suit)");

            if (model != null)
            {
                var rs = model.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    if (b.size.y > 0.01f)
                        model.transform.localScale = Vector3.one * (Height / b.size.y);
                }
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    // Cast shadows so the astronaut projects a long moon shadow on the terrain
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows    = true;
                }
                foreach (var t in model.GetComponentsInChildren<Transform>())
                    t.gameObject.layer = 2;

                var a = model.GetComponentInChildren<Animator>();
                if (a)
                {
                    a.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animation/Astronaut");
                    a.applyRootMotion = false;
                    a.cullingMode     = AnimatorCullingMode.AlwaysAnimate;
                }
            }

            return root.AddComponent<Astronaut>();
        }

        // ── Lifecycle ──────────────────────────────────────────────────────

        void Awake()
        {
            cc   = GetComponent<CharacterController>();
            anim = GetComponentInChildren<Animator>();
        }

        void Start()
        {
            if (transform.childCount > 0)
            {
                modelRoot       = transform.GetChild(0);
                modelLocalOrigin = modelRoot.localScale;
            }
            prevPosition = transform.position;
        }

        // ── State animation helpers ────────────────────────────────────────

        void Play(string state, float fade = 0.15f)
        {
            if (anim == null || currentState == state ||
                anim.runtimeAnimatorController == null) return;
            currentState = state;
            anim.CrossFadeInFixedTime(state, fade);
        }

        public void PlayWave()
        {
            currentState = null;
            Play("Wave", 0.2f);
        }

        public void PlayInteract()
        {
            currentState  = null;
            interactTimer = 0.9f;
            Play("Interact", 0.1f);
        }

        public void Die()
        {
            if (Dead) return;
            Dead = true;
            Play("Death", 0.2f);
        }

        public void Teleport(Vector3 p)
        {
            cc.enabled = false;
            transform.position = p;
            cc.enabled = true;
            velocity = Vector3.zero;
        }

        // ── Main update ────────────────────────────────────────────────────

        void Update()
        {
            if (cam == null && Camera.main) cam = Camera.main.transform;
            if (Dead) return;

            float    dt       = Time.deltaTime;
            Vector2  input    = Frozen ? Vector2.zero : GameInput.Move;
            Vector3  fwd      = cam ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            Vector3  right    = cam ? Vector3.ProjectOnPlane(cam.right,   Vector3.up).normalized : Vector3.right;
            Vector3  moveDir  = fwd * input.y + right * input.x;
            float    inputMag = Mathf.Clamp01(moveDir.magnitude);

            IsSprinting = !Frozen && GameInput.Sprint && inputMag > 0.01f;
            float topSpeed = (IsSprinting ? RunSpeed : WalkSpeed) * SpeedFactor;

            // ── Gravity + jump ─────────────────────────────────────────────
            bool grounded = cc.isGrounded;
            if (grounded && velocity.y < 0f) velocity.y = -1f;

            if (grounded && !Frozen && GameInput.Jump)
            {
                // v² = 2gh → v for ~1.8 m apex on the Moon (g = 1.62)
                velocity.y = Mathf.Sqrt(2f * MoonGravity * 1.8f);
                Play("Jump", 0.05f);
            }

            // Ease gravity near apex for the low-gravity float effect
            float gravScale = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(1f - Mathf.Abs(velocity.y) / 2f));
            velocity.y -= MoonGravity * gravScale * dt;

            // ── Horizontal movement ────────────────────────────────────────
            Vector3 horizontal = moveDir.normalized * (topSpeed * inputMag);
            cc.Move((horizontal + Vector3.up * velocity.y) * dt);

            // Face the camera-relative move direction – smooth turn speed
            if (moveDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(moveDir), 8f * dt);

            // ── State transitions ──────────────────────────────────────────
            float horizSpeed = horizontal.magnitude;
            distanceTravelled += Vector3.Distance(transform.position, prevPosition);
            prevPosition = transform.position;

            // Landing detection
            bool justLanded = !wasGrounded && grounded;
            if (justLanded) { landTimer = 0.28f; landSquashTimer = LandSquashDur; }
            wasGrounded = grounded;
            landTimer  -= dt;

            if (!grounded && velocity.y < 1.5f)
            {
                Play("Jump_Idle");
            }
            else if (!grounded)
            {
                // still rising – keep Jump clip
            }
            else if (landTimer > 0f)
            {
                Play("Jump_Land", 0.05f);
            }
            else if (inputMag < 0.05f)
            {
                interactTimer -= dt;
                if (currentState != "Wave" && interactTimer <= 0f)
                    Play("Idle");
            }
            else
            {
                // Hysteresis between Walk and Run
                if (!isRunning && horizSpeed >= WalkToRunSpeed) isRunning = true;
                if ( isRunning && horizSpeed <= RunToWalkSpeed) isRunning = false;

                if (isRunning || IsSprinting)
                {
                    Play("Run");
                    // Scale Run clip speed so feet match actual movement
                    if (anim) anim.speed = Mathf.Clamp(horizSpeed / RunSpeed, 0.6f, 1.5f);
                }
                else
                {
                    Play("Walk");
                    if (anim) anim.speed = Mathf.Clamp(horizSpeed / WalkSpeed, 0.5f, 1.3f);
                }
            }

            // Restore animator speed to 1 when not moving
            if (inputMag < 0.05f && anim) anim.speed = 1f;

            // ── Procedural bobbing ─────────────────────────────────────────
            ProceduralBob(dt, inputMag, grounded, horizSpeed);
        }

        // ── Procedural body bob + landing squash ──────────────────────────

        void ProceduralBob(float dt, float inputMag, bool grounded, float horizSpeed)
        {
            if (modelRoot == null) return;

            // Target scale: normally (1,1,1); on landing briefly squash Y/scale X.
            Vector3 targetScale = modelLocalOrigin;
            if (landSquashTimer > 0f)
            {
                landSquashTimer -= dt;
                float t = Mathf.Clamp01(landSquashTimer / LandSquashDur);
                // Squash on impact (t≈1), then spring back (t→0).
                float squashY = Mathf.Lerp(1f, 0.88f, Mathf.Sin(t * Mathf.PI));
                float stretchX = Mathf.Lerp(1f, 1.08f, Mathf.Sin(t * Mathf.PI));
                targetScale = new Vector3(
                    modelLocalOrigin.x * stretchX,
                    modelLocalOrigin.y * squashY,
                    modelLocalOrigin.z * stretchX);
            }
            modelRoot.localScale = Vector3.Lerp(modelRoot.localScale, targetScale, 12f * dt);

            // Vertical bob when moving on the ground
            if (grounded && inputMag > 0.05f && horizSpeed > 0.1f)
            {
                // Frequency proportional to speed for consistent footstep rhythm
                float freq = 2.2f * (horizSpeed / WalkSpeed);
                bobPhase += freq * Mathf.PI * 2f * dt;

                float bobY = Mathf.Sin(bobPhase) * 0.022f * Mathf.Clamp01(horizSpeed / WalkSpeed);
                var localPos = modelRoot.localPosition;
                modelRoot.localPosition = new Vector3(localPos.x, bobY, localPos.z);
            }
            else
            {
                // Return bob to rest
                var localPos = modelRoot.localPosition;
                modelRoot.localPosition = Vector3.Lerp(localPos, Vector3.zero, 8f * dt);
                if (!grounded) bobPhase = 0f;   // reset phase on airborne so landing feels fresh
            }

            // Gentle head sway when turning (gives a sense of weight in the suit)
            float turnRate = Frozen ? 0f : GameInput.Move.x * Mathf.Clamp01(horizSpeed / WalkSpeed);
            headSwayX = Mathf.Lerp(headSwayX,  turnRate * 2.0f,  5f * dt);   // tilt left/right
            headSwayZ = Mathf.Lerp(headSwayZ, -turnRate * 1.5f,  5f * dt);   // counter-lean

            // Apply the sway on top of any lean – only if the model isn't using root motion.
            // We rotate the model root by a tiny amount so it doesn't fight the Animator.
            var currentRot = modelRoot.localRotation;
            var targetRot  = Quaternion.Euler(headSwayX, 0f, headSwayZ);
            modelRoot.localRotation = Quaternion.Slerp(currentRot, targetRot, 6f * dt);
        }
    }
}
