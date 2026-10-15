using System.Collections.Generic;
using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Procedural PBR surface library (all generated once, tileable): painted hull panels with seams and rivets,
    /// steel floor grating, suit fabric weave and a grime/dust detail layer. Lets the primitives and the low-poly GLBs
    /// read as real hard-surface materials instead of flat colour.
    /// </summary>
    public static class SurfaceKit
    {
        static Shader lit;
        public static Shader Lit
        {
            get
            {
                if (lit != null) return lit;
                var rock = Resources.Load<Material>("Materials/MoonRock");
                lit = rock != null ? rock.shader : Shader.Find("Universal Render Pipeline/Lit");
                return lit;
            }
        }

        // ── noise / helpers ───────────────────────────────────────────────────────
        static float Periodic(int x, int y, int n, float freq)
        {
            float f = freq / n; float u = x / (float)n, w = y / (float)n;
            float a = Mathf.PerlinNoise(x * f, y * f), b = Mathf.PerlinNoise((x - n) * f, y * f);
            float c = Mathf.PerlinNoise(x * f, (y - n) * f), d = Mathf.PerlinNoise((x - n) * f, (y - n) * f);
            return a * (1 - u) * (1 - w) + b * u * (1 - w) + c * (1 - u) * w + d * u * w;
        }

        static float Hash(int a, int b)
        {
            unchecked
            {
                uint n = (uint)(a * 374761393 + b * 668265263);
                n = (n ^ (n >> 13)) * 1274126177u; n ^= n >> 16;
                return (n & 0xFFFFFF) / 16777216f;
            }
        }

        static Texture2D ToTex(Color32[] px, int n, bool linear)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true, linear) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            t.SetPixels32(px); t.Apply(true);
            return t;
        }

        static Color32[] NormalFromHeight(float[] h, int n, float strength)
        {
            var np = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = h[y * n + (x + 1) % n] - h[y * n + (x + n - 1) % n];
                float dy = h[((y + 1) % n) * n + x] - h[((y + n - 1) % n) * n + x];
                var v = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                byte nx = (byte)Mathf.RoundToInt((v.x * 0.5f + 0.5f) * 255f), ny = (byte)Mathf.RoundToInt((v.y * 0.5f + 0.5f) * 255f);
                np[y * n + x] = new Color32(255, ny, ny, nx);        // DXT5nm layout (x in alpha, y in green)
            }
            return np;
        }

        // ── texture sets ─────────────────────────────────────────────────────────
        static Texture2D hullA, hullN, floorA, floorN, fabricN, grimeA, grimeN;

        /// <summary>Painted hull plating: 4x4 panels per tile, recessed seams, rivets, scratches, edge grime.</summary>
        public static void Hull(out Texture2D albedo, out Texture2D normal)
        {
            if (hullA == null)
            {
                const int n = 512, cells = 4, cs = n / cells;
                var h = new float[n * n]; var a = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int lx = x % cs, ly = y % cs, cx = x / cs, cy = y / cs;
                    int e = Mathf.Min(Mathf.Min(lx, ly), Mathf.Min(cs - 1 - lx, cs - 1 - ly));
                    float panel = 0.80f + (Hash(cx, cy) - 0.5f) * 0.10f;                 // per-panel paint variation
                    float grime = Periodic(x, y, n, 7f) * 0.6f + Periodic(x, y, n, 31f) * 0.4f;
                    float v = panel * (0.86f + grime * 0.18f);
                    float ht = 0.6f + (Periodic(x, y, n, 90f) - 0.5f) * 0.05f;
                    if (e < 3) { v *= 0.30f; ht = 0.05f; }                                // seam groove
                    else if (e < 7) { v *= 0.88f + 0.12f * (e - 3) / 4f; ht = 0.05f + 0.55f * (e - 3) / 4f; }   // bevel
                    // seam grime: darker toward edges
                    if (e < 22) v *= 0.82f + 0.18f * e / 22f;
                    // rivets
                    foreach (var o in new[] { new Vector2(14, 14), new Vector2(cs - 15, 14), new Vector2(14, cs - 15), new Vector2(cs - 15, cs - 15) })
                    {
                        float d = Vector2.Distance(new Vector2(lx, ly), o);
                        if (d < 3.4f) { ht += 0.35f * (1f - d / 3.4f); v *= 0.93f; }
                    }
                    h[y * n + x] = ht;
                    byte g = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
                    a[y * n + x] = new Color32(g, g, (byte)Mathf.Min(255, g + 2), 255);
                }
                // scratches + paint chips
                var rnd = new System.Random(5);
                for (int i = 0; i < 70; i++)
                {
                    int sx = rnd.Next(n), sy = rnd.Next(n); float ang = (float)rnd.NextDouble() * Mathf.PI, len = 10 + rnd.Next(50);
                    for (float s = 0; s < len; s += 0.7f)
                    {
                        int px = ((int)(sx + Mathf.Cos(ang) * s) % n + n) % n, py = ((int)(sy + Mathf.Sin(ang) * s) % n + n) % n;
                        var c = a[py * n + px]; byte g = (byte)Mathf.Min(255, c.r + 40);
                        a[py * n + px] = new Color32(g, g, g, 255);
                    }
                }
                hullA = ToTex(a, n, false); hullN = ToTex(NormalFromHeight(h, n, 5.5f), n, true);
            }
            albedo = hullA; normal = hullN;
        }

        /// <summary>Steel grating: slotted holes between bright bars, with a worn centre.</summary>
        public static void Floor(out Texture2D albedo, out Texture2D normal)
        {
            if (floorA == null)
            {
                const int n = 512, cells = 8, cs = n / cells;
                var h = new float[n * n]; var a = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int lx = x % cs, ly = y % cs;
                    bool hole = lx > 8 && lx < cs - 8 && ly > 14 && ly < cs - 14;
                    float wear = Periodic(x, y, n, 12f);
                    float v = hole ? 0.04f : 0.42f + wear * 0.16f;
                    float ht = hole ? 0f : 0.7f + (wear - 0.5f) * 0.1f;
                    h[y * n + x] = ht;
                    byte g = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
                    a[y * n + x] = new Color32(g, g, (byte)Mathf.Min(255, g + 4), 255);
                }
                floorA = ToTex(a, n, false); floorN = ToTex(NormalFromHeight(h, n, 4f), n, true);
            }
            albedo = floorA; normal = floorN;
        }

        /// <summary>Woven fabric normal for the pressure suit.</summary>
        public static Texture2D FabricNormal()
        {
            if (fabricN == null)
            {
                const int n = 256; var h = new float[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float wx = Mathf.Sin(x * Mathf.PI * 2f / 8f), wy = Mathf.Sin(y * Mathf.PI * 2f / 8f);
                    h[y * n + x] = 0.5f + 0.18f * wx * wy + 0.12f * (Periodic(x, y, n, 14f) - 0.5f);
                }
                fabricN = ToTex(NormalFromHeight(h, n, 3.4f), n, true);
            }
            return fabricN;
        }

        /// <summary>Neutral (0.5) grey grime/dust detail for the x2 detail-albedo blend, with matching normal.</summary>
        public static void Grime(out Texture2D albedo, out Texture2D normal)
        {
            if (grimeA == null)
            {
                const int n = 512; var h = new float[n * n]; var a = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = 0.45f * Periodic(x, y, n, 5f) + 0.30f * Periodic(x, y, n, 18f) + 0.25f * Periodic(x, y, n, 70f);
                    float streak = Periodic(x, y / 6, n, 10f);                     // vertical streaks, like dust running down panels
                    float g0 = Mathf.Clamp01(0.5f + (v - 0.5f) * 0.55f - (streak - 0.5f) * 0.18f);
                    h[y * n + x] = v;
                    byte g = (byte)Mathf.RoundToInt(g0 * 255f);
                    a[y * n + x] = new Color32(g, g, g, 255);
                }
                grimeA = ToTex(a, n, false); grimeN = ToTex(NormalFromHeight(h, n, 2.2f), n, true);
            }
            albedo = grimeA; normal = grimeN;
        }

        // ── materials ────────────────────────────────────────────────────────────
        public static Material Make(Color tint, float smooth, float metal, Texture albedo = null, Texture normal = null, Vector2? tile = null, float bump = 1f, float emission = 0f)
        {
            var m = new Material(Lit);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            if (albedo != null) m.SetTexture("_BaseMap", albedo);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); }
            if (tile.HasValue) m.SetTextureScale("_BaseMap", tile.Value);
            if (emission > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", tint * emission); }
            return m;
        }

        static readonly Dictionary<string, Material> tiledCache = new Dictionary<string, Material>();

        /// <summary>A clone of a textured material tiled for the largest faces of a box of this size (about one tile per 2.4 m).</summary>
        public static Material TiledFor(Material src, Vector3 size, float metersPerTile = 2.4f)
        {
            float sx = Mathf.Abs(size.x), sy = Mathf.Abs(size.y), sz = Mathf.Abs(size.z);
            Vector2 t;
            if (sy <= sx && sy <= sz) t = new Vector2(sx, sz);            // floors / ceilings: top face is X by Z
            else if (sz <= sx) t = new Vector2(sx, sy);                   // front/back walls: X by Y
            else t = new Vector2(sz, sy);                                 // side walls: Z by Y
            t /= metersPerTile;
            t = new Vector2(Mathf.Max(0.25f, Mathf.Round(t.x * 4f) / 4f), Mathf.Max(0.25f, Mathf.Round(t.y * 4f) / 4f));
            string key = src.name + "_" + src.GetHashCode() + "_" + t.x + "_" + t.y;
            if (!tiledCache.TryGetValue(key, out var m) || m == null)
            {
                m = new Material(src);
                m.SetTextureScale("_BaseMap", t);
                tiledCache[key] = m;
            }
            return m;
        }

        static Material dust;
        /// <summary>Soft round alpha-blended dust puff (the old Beam material was additive cyan, so dust looked blue).</summary>
        public static Material DustMaterial()
        {
            if (dust != null) return dust;
            var line = Resources.Load<Material>("Materials/Line");
            Shader sh = line != null ? line.shader : Shader.Find("Universal Render Pipeline/Particles/Unlit");
            const int n = 64; var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px); tex.Apply(true);
            dust = new Material(sh) { name = "Dust puff" };
            dust.SetTexture("_BaseMap", tex);
            dust.SetColor("_BaseColor", Color.white);
            dust.SetFloat("_Surface", 1f); dust.SetFloat("_Blend", 0f);
            dust.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            dust.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            dust.SetFloat("_ZWrite", 0f);
            dust.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            dust.SetOverrideTag("RenderType", "Transparent");
            dust.renderQueue = 3000;
            return dust;
        }
    }
}
