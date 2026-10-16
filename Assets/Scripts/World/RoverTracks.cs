using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Tyre tracks behind the scout rover: one tread strip under the left and right wheels every <see cref="Spacing"/> metres.
    /// Added by Rover.Create. Track width is measured from the rover model's bounds unless set.
    /// Ground height/normal come from MoonWorld (a downward raycast would hit the rover's own collider).
    /// </summary>
    [RequireComponent(typeof(Rover))]
    public class RoverTracks : MonoBehaviour
    {
        public int   PoolSize  = 800;      // 2 strips per Spacing -> ~400 m of track
        public float Spacing   = 1.0f;     // metres between strips; strips are slightly longer so they join up
        public float HalfTrack = 0f;       // wheel centre distance from the rover centre line (0 = measure)
        public float TyreWidth = 0f;       // (0 = measure)
        public Color DustColor = new Color(0.10f, 0.10f, 0.11f, 0.5f);

        Rover rover; GroundPrints prints; Vector3 last; float travelled;
        static Texture2D tex;

        void Start()
        {
            rover = GetComponent<Rover>();
            MeasureWheels();
            if (HalfTrack <= 0f) HalfTrack = 1.1f;
            if (TyreWidth <= 0f) TyreWidth = 0.3f;
            if (tex == null) tex = GroundPrints.MakeTexture("TyreTread", 32, 64, Tread);
            prints = new GroundPrints("TyreTracks", PoolSize, tex, DustColor);
            last = transform.position;
        }

        float centreX, rearZ;   // rover-local: midpoint between the wheel lines, rear axle

        // Track geometry from the actual wheel meshes (Rover.glb: Wheel_* nodes), in rover-local space.
        // Only meshes named "wheel"/"tyre"/"tire": the dust trail and lights must not count.
        void MeasureWheels()
        {
            float lx = 0, rx = 0, w = 0; int nl = 0, nr = 0; rearZ = float.MaxValue;
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                string n = r.name.ToLowerInvariant();
                if (!(n.Contains("wheel") || n.Contains("tyre") || n.Contains("tire"))) continue;
                Vector3 c = transform.InverseTransformPoint(r.bounds.center);
                if (c.x < 0) { lx += c.x; nl++; } else { rx += c.x; nr++; }
                rearZ = Mathf.Min(rearZ, c.z);
                w += Mathf.Abs(transform.InverseTransformVector(r.bounds.size).x);
            }
            if (nl == 0 || nr == 0) { rearZ = 0; return; }
            lx /= nl; rx /= nr;
            centreX = (lx + rx) * 0.5f;
            if (HalfTrack <= 0f) HalfTrack = (rx - lx) * 0.5f;
            if (TyreWidth <= 0f) TyreWidth = Mathf.Clamp(w / (nl + nr), 0.1f, 0.8f);
        }

        void OnDestroy() => prints?.Dispose();

        void Update()
        {
            if (rover.World == null) return;
            Vector3 d = transform.position - last; d.y = 0; last = transform.position;
            travelled += d.magnitude;
            if (travelled >= Spacing)
            {
                travelled = 0f;
                float z = rearZ - Spacing * 0.5f;   // strip centred on the ground the rear wheels just covered
                Strip(transform.TransformPoint(new Vector3(centreX - HalfTrack, 0f, z)));
                Strip(transform.TransformPoint(new Vector3(centreX + HalfTrack, 0f, z)));
            }
            prints.Tick();
        }

        void Strip(Vector3 p)
        {
            p.y = rover.World.HeightAt(p);
            prints.Stamp(p, rover.World.NormalAt(p), transform.forward, TyreWidth, Spacing * 1.04f, DustColor.a);
        }

        // tread: soft side edges, chevron bars repeating along the strip (6 per strip, so strips tile seamlessly)
        static float Tread(float u, float v)
        {
            float edge = Mathf.Clamp01((1f - Mathf.Abs(u)) * 5f);
            float bar = Mathf.Repeat(v * 6f + Mathf.Abs(u) * 0.35f, 1f) < 0.5f ? 1f : 0.45f;
            return edge * bar;
        }
    }
}
