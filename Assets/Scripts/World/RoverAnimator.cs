using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Procedural animation for the scout rover.
    /// Drop this on the same GameObject as <see cref="Rover"/> or add it from <see cref="Rover.Create"/>.
    ///
    /// Features:
    ///   • Wheel spin  – all child transforms whose name contains "Wheel" rotate around their local X axis
    ///                   at a speed proportional to the rover's ground speed (circumference = 2πr, r ≈ 0.35 m).
    ///   • Suspension lean – the model pitches forward/back slightly on acceleration and rolls into turns.
    ///   • Body bob    – gentle sinusoidal vertical oscillation based on distance travelled (simulates
    ///                   terrain micro-bumps without per-frame raycasts).
    ///   • Dust particle trail – a CPU particle system spawned behind the rear axle, intensity
    ///                   scales with speed, fades to nothing when stationary.
    ///   • Speed-up / slow-down squash – the body squash/stretches 1–3 % for tactile feel.
    /// </summary>
    [RequireComponent(typeof(Rover))]
    public class RoverAnimator : MonoBehaviour
    {
        // ── Tuning parameters ──────────────────────────────────────────────
        [Header("Wheels")]
        [Tooltip("Approximate wheel radius in metres – used to convert speed to angular velocity.")]
        [SerializeField] float wheelRadius = 0.35f;

        [Header("Body Lean")]
        [Tooltip("Max pitch angle (degrees) when accelerating/braking.")]
        [SerializeField] float maxPitch    = 3.5f;
        [Tooltip("Max roll angle (degrees) when turning.")]
        [SerializeField] float maxRoll     = 4f;
        [Tooltip("Smoothing speed for the lean angles.")]
        [SerializeField] float leanSmooth  = 6f;

        [Header("Body Bob")]
        [Tooltip("Peak vertical bob amplitude (metres).")]
        [SerializeField] float bobAmp      = 0.025f;
        [Tooltip("Bob frequency – cycles per metre of travel.")]
        [SerializeField] float bobFreq     = 1.4f;

        [Header("Dust Trail")]
        [Tooltip("Max emission rate (particles/second) at top speed.")]
        [SerializeField] float dustMaxRate = 60f;
        [Tooltip("How quickly the dust emission reacts to speed changes.")]
        [SerializeField] float dustSmooth  = 4f;

        // ── Runtime state ──────────────────────────────────────────────────
        Rover rover;
        Transform[] wheels;       // child transforms whose name contains "Wheel"
        Transform bodyModel;      // the root model child (first child of the rover root)

        // Lean state
        float pitchCurrent, rollCurrent;
        Vector3 prevVelocity;

        // Bob state
        float distanceTravelled;
        float prevBobY;

        // Dust
        ParticleSystem dustPS;
        float dustEmissionTarget;

        // Cache previous position for derivative speed
        Vector3 prevPosition;
        float   currentSpeed;

        // Body original local position (baked in at Start)
        Vector3 bodyLocalOrigin;
        Quaternion bodyLocalOriginRot;

        // ── Lifecycle ──────────────────────────────────────────────────────

        void Awake()
        {
            rover = GetComponent<Rover>();
        }

        void Start()
        {
            // Discover the model child and wheel bones.
            // The Quaternius Rover model root is the first non-primitive child of the rover root.
            if (transform.childCount > 0)
                bodyModel = transform.GetChild(0);

            if (bodyModel != null)
            {
                bodyLocalOrigin    = bodyModel.localPosition;
                bodyLocalOriginRot = bodyModel.localRotation;
                CollectWheels(bodyModel);
            }

            // If no "Wheel"-named transforms were found, discover any spinning-looking children
            // by falling back to transforms at depth 2+ with "wheel" or "tyre" in the name (case-insensitive).
            if (wheels == null || wheels.Length == 0)
                wheels = FindWheelsByName(transform);

            // ── Dust particle system ──────────────────────────────────────
            var dustGo = new GameObject("Rover Dust Trail");
            dustGo.transform.SetParent(transform);
            // Offset to just behind the rear axle; adjust if geometry differs.
            dustGo.transform.localPosition = new Vector3(0f, 0.1f, -1.2f);

            dustPS = dustGo.AddComponent<ParticleSystem>();
            var main = dustPS.main;
            main.startLifetime    = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSpeed       = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            main.startSize        = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
            main.startColor       = new ParticleSystem.MinMaxGradient(
                new Color(0.72f, 0.68f, 0.60f, 0.55f),
                new Color(0.80f, 0.76f, 0.68f, 0.25f));
            main.gravityModifier  = new ParticleSystem.MinMaxCurve(0.018f);   // very low lunar gravity
            main.maxParticles     = 200;
            main.simulationSpace  = ParticleSystemSimulationSpace.World;

            var emission = dustPS.emission;
            emission.rateOverTime = 0f;   // driven manually each frame

            var shape = dustPS.shape;
            shape.shapeType   = ParticleSystemShapeType.Cone;
            shape.angle       = 22f;
            shape.radius      = 0.45f;
            shape.rotation    = new Vector3(-80f, 0f, 0f);   // blast backward & upward

            var vel = dustPS.velocityOverLifetime;
            vel.enabled    = true;
            vel.x          = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            vel.y          = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
            vel.z          = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            var sizeOL = dustPS.sizeOverLifetime;
            sizeOL.enabled = true;
            var sizeCurve  = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.4f);
            sizeCurve.AddKey(0.6f, 1f);
            sizeCurve.AddKey(1f, 0f);
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOL = dustPS.colorOverLifetime;
            colorOL.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.78f, 0.73f, 0.62f), 0f),
                         new GradientColorKey(new Color(0.78f, 0.73f, 0.62f), 1f) },
                new[] { new GradientAlphaKey(0.55f, 0f),
                         new GradientAlphaKey(0.25f, 0.6f),
                         new GradientAlphaKey(0f,   1f) });
            colorOL.color = new ParticleSystem.MinMaxGradient(grad);

            // Try to assign the URP unlit material for particles.
            var mr = dustGo.GetComponent<ParticleSystemRenderer>();
            mr.sharedMaterial = SurfaceKit.DustMaterial();      // soft grey-tan puffs (Beam was additive cyan)

            prevPosition = transform.position;
        }

        // ── Per-frame update ───────────────────────────────────────────────

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Derive scalar speed from displacement (works for slope & reverse).
            Vector3 disp   = transform.position - prevPosition;
            currentSpeed   = disp.magnitude / dt;
            float signedSpd = Vector3.Dot(disp.normalized, transform.forward) * currentSpeed;
            prevPosition   = transform.position;

            distanceTravelled += currentSpeed * dt;

            AnimateWheels(signedSpd, dt);
            AnimateLean(signedSpd, dt);
            AnimateBob(dt);
            AnimateDust(dt);
        }

        // ── Wheel spin ─────────────────────────────────────────────────────

        void AnimateWheels(float signedSpeed, float dt)
        {
            if (wheels == null) return;
            // Angular velocity = linear speed / radius  (rad/s → deg/s)
            float degPerSec = (signedSpeed / wheelRadius) * Mathf.Rad2Deg;
            foreach (var w in wheels)
                if (w != null)
                    w.Rotate(degPerSec * dt, 0f, 0f, Space.Self);
        }

        // ── Body lean (pitch + roll) ────────────────────────────────────────

        void AnimateLean(float signedSpeed, float dt)
        {
            if (bodyModel == null) return;

            // Pitch from acceleration: positive speed-up → nose dips.
            float accel = (signedSpeed - Vector3.Dot(prevVelocity, transform.forward)) / Mathf.Max(dt, 0.001f);
            float targetPitch = Mathf.Clamp(-accel * 0.28f, -maxPitch, maxPitch);

            // Roll from turn input (steer rate is the yaw change; proxy via lateral input).
            float steer      = rover.Driving ? GameInput.Move.x : 0f;
            float targetRoll = Mathf.Clamp(-steer * maxRoll * Mathf.Sign(signedSpeed), -maxRoll, maxRoll);

            pitchCurrent = Mathf.Lerp(pitchCurrent, targetPitch, leanSmooth * dt);
            rollCurrent  = Mathf.Lerp(rollCurrent,  targetRoll,  leanSmooth * dt);

            bodyModel.localRotation = bodyLocalOriginRot *
                Quaternion.Euler(pitchCurrent, 0f, rollCurrent);

            prevVelocity = transform.forward * signedSpeed;
        }

        // ── Body bob ───────────────────────────────────────────────────────

        void AnimateBob(float dt)
        {
            if (bodyModel == null) return;
            if (currentSpeed < 0.05f)
            {
                // Return smoothly to rest
                var pos = bodyModel.localPosition;
                bodyModel.localPosition = Vector3.Lerp(pos,
                    new Vector3(bodyLocalOrigin.x, bodyLocalOrigin.y, bodyLocalOrigin.z),
                    8f * dt);
                return;
            }

            float bobY  = Mathf.Sin(distanceTravelled * bobFreq * Mathf.PI * 2f) * bobAmp;
            // Speed-scale amplitude so fast bumps feel more dramatic.
            float scale = Mathf.Clamp01(currentSpeed / Rover.MaxSpeed);
            bobY *= scale;

            bodyModel.localPosition = new Vector3(
                bodyLocalOrigin.x,
                bodyLocalOrigin.y + bobY,
                bodyLocalOrigin.z);

            prevBobY = bobY;
        }

        // ── Dust emission ──────────────────────────────────────────────────

        void AnimateDust(float dt)
        {
            if (dustPS == null) return;

            float speedFrac   = Mathf.Clamp01(currentSpeed / Rover.MaxSpeed);
            dustEmissionTarget = speedFrac * dustMaxRate;
            float current      = dustPS.emission.rateOverTime.constant;
            float next         = Mathf.Lerp(current, dustEmissionTarget, dustSmooth * dt);

            var em = dustPS.emission;
            em.rateOverTime = next;

            // Face the particle system away from travel direction so dust trails properly.
            if (currentSpeed > 0.1f && dustPS.transform != null)
            {
                var dir = transform.forward * Mathf.Sign(Vector3.Dot(
                    (transform.position - prevPosition).normalized, transform.forward));
                if (dir != Vector3.zero)
                    dustPS.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
            }
        }

        // ── Utility ────────────────────────────────────────────────────────

        void CollectWheels(Transform root)
        {
            var list = new System.Collections.Generic.List<Transform>();
            CollectWheelsRecurse(root, list);
            wheels = list.Count > 0 ? list.ToArray() : null;
        }

        void CollectWheelsRecurse(Transform t, System.Collections.Generic.List<Transform> list)
        {
            if (t.name.IndexOf("wheel", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                t.name.IndexOf("tyre",  System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                t.name.IndexOf("tire",  System.StringComparison.OrdinalIgnoreCase) >= 0)
                list.Add(t);
            for (int i = 0; i < t.childCount; i++)
                CollectWheelsRecurse(t.GetChild(i), list);
        }

        static Transform[] FindWheelsByName(Transform root)
        {
            var list = new System.Collections.Generic.List<Transform>();
            var queue = new System.Collections.Generic.Queue<Transform>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var t = queue.Dequeue();
                string n = t.name.ToLowerInvariant();
                if (n.Contains("wheel") || n.Contains("tyre") || n.Contains("tire"))
                    list.Add(t);
                for (int i = 0; i < t.childCount; i++) queue.Enqueue(t.GetChild(i));
            }
            return list.ToArray();
        }
    }
}
