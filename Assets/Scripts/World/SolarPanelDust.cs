using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Attaches a translucent orange-brown dust layer quad to a solar panel.
    /// Call <see cref="SetDust"/> every frame with a value 0–0.5 to fade the overlay in/out.
    /// The overlay sits 5 cm in front of the solar-blanket face so it is always visible.
    /// </summary>
    public class SolarPanelDust : MonoBehaviour
    {
        MeshRenderer dustQuad;
        Material mat;
        static readonly Color DustTint = new Color(0.72f, 0.46f, 0.22f, 1f);

        public static SolarPanelDust Attach(Transform panel)
        {
            var c = panel.gameObject.AddComponent<SolarPanelDust>();
            c.Build(panel);
            return c;
        }

        void Build(Transform panel)
        {
            // Find the "Tracker" child (the solar blanket that rotates to face the sun)
            Transform face = null;
            foreach (var t in panel.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Tracker") { face = t; break; }
            }
            if (face == null) face = panel;   // fallback: attach to root

            // Build a quad that covers the blanket face.
            // SolarArrayVSAT blanket is roughly 4 m wide × 2.6 m tall; quad is slightly smaller so it sits inside.
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "DustOverlay";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(face, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.05f);   // 5 cm in front of the blanket
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(3.6f, 2.3f, 1f);

            // Material: use the same transparent lit shader the dust particles use.
            var line = Resources.Load<Material>("Materials/Line");
            Shader sh = (line != null) ? line.shader : Shader.Find("Universal Render Pipeline/Lit");

            mat = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Particles/Unlit"))
            {
                name = "PanelDust_" + panel.name
            };

            // Build a simple 32×32 filled texture with slightly noisy alpha so it looks like settled dust
            const int n = 32;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // Slightly uneven coverage — denser near edges (dust catches in the frame)
                float fx = (float)x / n, fy = (float)y / n;
                float edge = 1f - Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy)) * 4f;
                float noise = Mathf.PerlinNoise(x * 0.4f, y * 0.4f);
                float a = Mathf.Clamp01(0.60f + 0.25f * edge + 0.15f * noise);
                px[y * n + x] = new Color32(
                    (byte)(DustTint.r * 255),
                    (byte)(DustTint.g * 255),
                    (byte)(DustTint.b * 255),
                    (byte)(a * 255));
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px); tex.Apply(false);

            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", DustTint);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3001;

            dustQuad = go.GetComponent<MeshRenderer>();
            dustQuad.sharedMaterial = mat;
            dustQuad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dustQuad.receiveShadows = false;
            SetDust(0f);
        }

        /// <summary>Update the dust overlay. d is the panel's dust fraction (0 = clean, 0.5 = max buildup).</summary>
        public void SetDust(float d)
        {
            if (mat == null) return;
            // Map 0–0.5 to alpha 0–0.75 so even max dust doesn't fully hide the panel
            float alpha = Mathf.Clamp01(d / 0.5f) * 0.75f;
            var c = DustTint;
            c.a = alpha;
            mat.SetColor("_BaseColor", c);
            if (dustQuad != null) dustQuad.enabled = alpha > 0.01f;
        }
    }
}
