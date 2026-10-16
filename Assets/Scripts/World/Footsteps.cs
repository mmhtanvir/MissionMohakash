using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Boot prints in the lunar dust. Added to the player root by Astronaut.Create.
    /// Prints alternate left/right every stride and follow the terrain slope (pool + fading in <see cref="GroundPrints"/>).
    /// Jump landings leave a deeper two-foot print. No prints indoors. The sole texture is generated at runtime.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Footsteps : MonoBehaviour
    {
        public int   PoolSize      = 220;
        public float WalkStride    = 0.75f;   // metres between prints while walking
        public float RunStride     = 1.15f;   // ...while running
        public float FootOffset    = 0.13f;   // sideways distance from the body centre
        public float PrintLength   = 0.34f;
        public float PrintWidth    = 0.15f;
        public float MinAirTime    = 0.3f;    // shorter air time (slope bumps, teleports) is not a jump landing
        public Color DustColor     = new Color(0.12f, 0.12f, 0.13f, 0.55f);

        CharacterController cc; Astronaut player; GroundPrints prints;
        Vector3 last; float travelled, airTime; bool leftFoot, wasGrounded;
        static Texture2D tex;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            player = GetComponent<Astronaut>();
            if (tex == null) tex = GroundPrints.MakeTexture("FootprintSole", 64, 128, Sole);
            prints = new GroundPrints("Footprints", PoolSize, tex, DustColor);
        }

        // player is deactivated in the rover / teleported in and out of the habitat: restart tracking from here
        void OnEnable() { last = transform.position; wasGrounded = true; airTime = 0f; travelled = 0f; }

        void OnDestroy() => prints?.Dispose();

        void Update()
        {
            bool grounded = !cc.enabled || cc.isGrounded;   // controller off = a scripted walk on the ground (airlock sequence)
            Vector3 d = transform.position - last; d.y = 0; last = transform.position;

            if (player != null && player.Indoors) { wasGrounded = true; airTime = 0f; }
            else
            {
                if (!grounded) airTime += Time.deltaTime;
                if (grounded && !wasGrounded && airTime >= MinAirTime) { Place(-1, 1.35f); Place(1, 1.35f); travelled = 0; }   // landing: both feet, bigger and darker
                if (grounded) airTime = 0f;
                wasGrounded = grounded;

                if (grounded && d.magnitude < 3f)   // ignore teleports
                {
                    travelled += d.magnitude;
                    float stride = cc.velocity.magnitude > 3.2f ? RunStride : WalkStride;
                    if (travelled >= stride) { travelled = 0; leftFoot = !leftFoot; Place(leftFoot ? -1 : 1, 1f); }
                }
            }
            prints.Tick();
        }

        void Place(int side, float scale)
        {
            Vector3 origin = transform.position + transform.right * (side * FootOffset) + Vector3.up * 0.5f;
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 1.5f, ~(1 << 2), QueryTriggerInteraction.Ignore)) return;
            prints.Stamp(hit.point, hit.normal, transform.forward,
                         PrintWidth * scale * (side > 0 ? -1f : 1f), PrintLength * scale,   // mirror the right boot
                         DustColor.a * Mathf.Min(1f, scale));
        }

        // boot sole: oval toe + narrower heel, separated by an arch gap, with tread bars cut out (v: 0 = heel, 1 = toe)
        static float Sole(float u, float v)
        {
            float a = 0f;
            if (v > 0.42f) a = Mathf.Max(a, 1f - Mathf.Pow(u / 0.95f, 2) - Mathf.Pow((v - 0.72f) / 0.30f, 2));            // toe / ball
            if (v < 0.30f) a = Mathf.Max(a, 1f - Mathf.Pow(u / 0.70f, 2) - Mathf.Pow((v - 0.15f) / 0.17f, 2));            // heel
            if (v >= 0.30f && v <= 0.46f) a = Mathf.Max(a, 0.9f - Mathf.Pow(u / 0.55f, 2));                                // arch bridge
            a = Mathf.Clamp01(a * 6f);
            if (a > 0f && v > 0.45f && Mathf.Repeat(v * 14f, 1f) < 0.22f) a *= 0.35f;                                      // tread grooves
            return a;
        }
    }
}
