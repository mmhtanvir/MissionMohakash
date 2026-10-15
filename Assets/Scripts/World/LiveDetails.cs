using UnityEngine;

namespace Mohakash
{
    /// <summary>Small animated touches: blinking status lights and scrolling telemetry screens.</summary>
    public class Blink : MonoBehaviour
    {
        public Color Color = Color.red; public float Rate = 1.2f, Strength = 3f, Phase;
        public Light Lamp; public float LampIntensity = 1.5f;
        Renderer r; MaterialPropertyBlock mpb;
        void Awake() { r = GetComponent<Renderer>(); mpb = new MaterialPropertyBlock(); }
        void Update()
        {
            float k = Mathf.Clamp01(Mathf.Sin((Time.time + Phase) * Rate * Mathf.PI * 2f) * 3f + 0.5f);   // quick on/off pulse
            if (r != null) { r.GetPropertyBlock(mpb); mpb.SetColor("_EmissionColor", Color * (0.15f + k * Strength)); r.SetPropertyBlock(mpb); }
            if (Lamp != null) Lamp.intensity = LampIntensity * k;
        }
    }

    /// <summary>Turns a spotlight on when the sun is low (shadow window / night) so floodlights and helmet lamps matter.</summary>
    public class NightLight : MonoBehaviour
    {
        public Light Lamp; public Light Sun; public float Threshold = 2.2f, Intensity = 6f;
        void Update()
        {
            if (Lamp == null) return;
            bool dark = Sun == null || Sun.intensity < Threshold;
            Lamp.intensity = Mathf.MoveTowards(Lamp.intensity, dark ? Intensity : 0f, Time.deltaTime * 8f);
            Lamp.enabled = Lamp.intensity > 0.02f;
        }
    }

    /// <summary>Procedural telemetry display on a mesh: scrolling graph lines, bars and text rows, refreshed ~8 times/second.</summary>
    public class ScreenDisplay : MonoBehaviour
    {
        public Color Tint = new Color(0.2f, 0.9f, 1f); public int Style; public float Glow = 1.8f;
        const int W = 128, H = 64;
        Texture2D tex; Color32[] px; float timer, seed;

        void Start()
        {
            seed = Random.value * 100f;
            px = new Color32[W * H];
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var r = GetComponent<Renderer>();
            var m = new Material(SurfaceKit.Lit);
            m.SetColor("_BaseColor", new Color(0.02f, 0.03f, 0.04f)); m.SetFloat("_Smoothness", 0.8f);
            m.SetTexture("_EmissionMap", tex); m.SetColor("_EmissionColor", Color.white * Glow); m.EnableKeyword("_EMISSION");
            r.sharedMaterial = m;
            Redraw();
        }

        void Update() { timer -= Time.deltaTime; if (timer <= 0f) { timer = 0.12f; Redraw(); } }

        void Redraw()
        {
            if (tex == null) return;
            float t = Time.time + seed;
            var bg = new Color32(4, 10, 14, 255);
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            Color32 c = Tint, dim = new Color32((byte)(Tint.r * 70), (byte)(Tint.g * 70), (byte)(Tint.b * 70), 255);
            for (int x = 0; x < W; x += 16) for (int y = 0; y < H; y++) px[y * W + x] = dim;       // grid
            for (int y = 0; y < H; y += 16) for (int x = 0; x < W; x++) px[y * W + x] = dim;
            if (Style == 0)
            {
                for (int x = 0; x < W; x++)
                {
                    int y = (int)(H * 0.55f + Mathf.Sin(x * 0.14f + t * 2f) * 11f + (Mathf.PerlinNoise(x * 0.1f, t) - 0.5f) * 14f);
                    Put(x, y, c); Put(x, y + 1, c);
                }
                for (int b = 0; b < 12; b++)
                {
                    int bh = 3 + (int)(Mathf.PerlinNoise(b * 0.7f, t * 0.8f) * 14f);
                    for (int x = b * 10 + 4; x < b * 10 + 11; x++) for (int y = 0; y < bh; y++) Put(x, y, c);
                }
            }
            else
            {
                for (int row = 0; row < 8; row++)
                {
                    int y = H - 6 - row * 7; int len = 20 + (int)(Mathf.PerlinNoise(row * 1.3f, Mathf.Floor(t * 0.5f)) * 90f);
                    for (int x = 4; x < Mathf.Min(W - 4, 4 + len); x++) if (((x / 3) + row) % 5 != 0) { Put(x, y, c); Put(x, y - 1, c); }
                }
                int sweep = (int)(Mathf.Repeat(t * 40f, W));
                for (int y = 0; y < H; y++) Put(sweep, y, new Color32(255, 255, 255, 255));
            }
            tex.SetPixels32(px); tex.Apply(false);
        }

        void Put(int x, int y, Color32 c) { if (x >= 0 && x < W && y >= 0 && y < H) px[y * W + x] = c; }
    }

    public class Spin : MonoBehaviour
    {
        public float Speed = 20f;
        void Update() { transform.Rotate(0f, Speed * Time.deltaTime, 0f, Space.Self); }
    }
}
