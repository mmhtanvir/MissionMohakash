using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Fixed pool of flat textured quads lying on the ground (boot prints, tyre tracks). Oldest print is recycled first.
    /// Owner calls Stamp() to place one, Tick() every frame for fading, Dispose() in OnDestroy.
    /// </summary>
    public class GroundPrints
    {
        public Color Color;
        public float FadeAfter = 240f;   // seconds before a print starts fading (0 = never)
        public float FadeTime  = 30f;
        public float SurfaceLift = 0.015f;   // avoids z-fighting with the terrain

        readonly Transform holder; readonly Transform[] pool; readonly MeshRenderer[] rend; readonly float[] born; int next;
        static Mesh quad; static MaterialPropertyBlock mpb;

        public GroundPrints(string name, int size, Texture2D tex, Color color)
        {
            Color = color;
            if (quad == null) quad = MakeQuad();
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader) { name = name, mainTexture = tex };
            pool = new Transform[size]; rend = new MeshRenderer[size]; born = new float[size];
            holder = new GameObject(name).transform;   // world space, not parented to whatever moves
            for (int i = 0; i < size; i++)
            {
                var g = new GameObject("print"); g.layer = 2; g.transform.SetParent(holder, false);
                g.AddComponent<MeshFilter>().sharedMesh = quad;
                var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                g.SetActive(false); pool[i] = g.transform; rend[i] = r; born[i] = -1e9f;
            }
        }

        /// <summary>Lay one print on the ground at <paramref name="point"/>; texture V axis points along <paramref name="forward"/>. Negative width mirrors it.</summary>
        public void Stamp(Vector3 point, Vector3 normal, Vector3 forward, float width, float length, float alpha)
        {
            var t = pool[next]; var r = rend[next]; born[next] = Time.time; next = (next + 1) % pool.Length;
            Vector3 fwd = Vector3.ProjectOnPlane(forward, normal).normalized;
            t.position = point + normal * SurfaceLift;
            t.rotation = Quaternion.LookRotation(fwd, normal) * Quaternion.Euler(90f, 0f, 0f);   // quad faces up along the normal
            t.localScale = new Vector3(width, length, 1f);
            SetAlpha(r, alpha);
            t.gameObject.SetActive(true);
        }

        public void Tick()
        {
            if (FadeAfter <= 0f) return;
            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].gameObject.activeSelf) continue;
                float age = Time.time - born[i] - FadeAfter;
                if (age <= 0f) continue;
                float a = 1f - Mathf.Clamp01(age / FadeTime);
                if (a <= 0f) { pool[i].gameObject.SetActive(false); continue; }
                SetAlpha(rend[i], a * Color.a);
            }
        }

        public void Dispose() { if (holder != null) Object.Destroy(holder.gameObject); }

        void SetAlpha(MeshRenderer r, float a)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            var c = new Color(Color.r, Color.g, Color.b, a);
            r.GetPropertyBlock(mpb); mpb.SetColor("_Color", c); mpb.SetColor("_BaseColor", c); r.SetPropertyBlock(mpb);
        }

        // unit quad in the XY plane, facing -Z (rotated onto the ground in Stamp)
        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "GroundPrintQuad" };
            m.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
            return m;
        }

        /// <summary>White texture whose alpha comes from <paramref name="alpha"/>(u in -1..1, v in 0..1).</summary>
        public static Texture2D MakeTexture(string name, int w, int h, System.Func<float, float, float> alpha)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = name };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color(1, 1, 1, Mathf.Clamp01(alpha((x + .5f) / w * 2f - 1f, (y + .5f) / h)));
            t.SetPixels(px); t.Apply(true); return t;
        }
    }
}
