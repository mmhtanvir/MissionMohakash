using System.Collections.Generic;
using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// glTFast imports GLB models with its own Shader Graph materials. If those were generated while no URP pipeline
    /// was active (or the shader fails to compile in this Unity/URP version) they render magenta. This swaps any
    /// glTF/unsupported/error material for a URP Lit copy that keeps the colour, textures, metal/rough and emission.
    /// Call Fix(gameObject) right after instantiating a model.
    /// </summary>
    public static class ModelMaterialFixer
    {
        static readonly Dictionary<Material, Material> cache = new Dictionary<Material, Material>();
        static Shader lit;

        static Shader LitShader()
        {
            if (lit != null) return lit;
            var rock = Resources.Load<Material>("Materials/MoonRock");
            if (rock != null && rock.shader != null && rock.shader.isSupported) lit = rock.shader;
            if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
            return lit;
        }

        static bool Broken(Material m)
        {
            if (m == null || m.shader == null) return true;
            string n = m.shader.name;
            if (n.Contains("InternalError") || !m.shader.isSupported) return true;
            return n.StartsWith("Shader Graphs/glTF") || n.StartsWith("glTF") || n.Contains("glTF-");
        }

        static bool TryColor(Material src, out Color c, params string[] names)
        {
            foreach (var n in names) if (src != null && src.HasProperty(n)) { c = src.GetColor(n); return true; }
            c = Color.white; return false;
        }

        static Texture TryTex(Material src, params string[] names)
        {
            foreach (var n in names)
                if (src != null && src.HasProperty(n)) { var t = src.GetTexture(n); if (t != null) return t; }
            return null;
        }

        static float TryFloat(Material src, float fallback, params string[] names)
        {
            foreach (var n in names) if (src != null && src.HasProperty(n)) return src.GetFloat(n);
            return fallback;
        }

        static Material Convert(Material src)
        {
            if (src != null && cache.TryGetValue(src, out var done) && done != null) return done;
            var shader = LitShader();
            if (shader == null) return src;
            var m = new Material(shader) { name = (src != null ? src.name : "model") + " (URP)" };

            TryColor(src, out var col, "baseColorFactor", "_BaseColor", "_Color");
            m.SetColor("_BaseColor", col);
            var tex = TryTex(src, "baseColorTexture", "_BaseMap", "_MainTex");
            if (tex != null) m.SetTexture("_BaseMap", tex);

            m.SetFloat("_Metallic", Mathf.Clamp01(TryFloat(src, 0f, "metallicFactor", "_Metallic")));
            float rough = TryFloat(src, -1f, "roughnessFactor");
            m.SetFloat("_Smoothness", rough >= 0f ? 1f - Mathf.Clamp01(rough) : Mathf.Clamp01(TryFloat(src, 0.3f, "_Smoothness", "_Glossiness")));

            var nrm = TryTex(src, "normalTexture", "_BumpMap");
            if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); }

            bool em = TryColor(src, out var ec, "emissiveFactor", "_EmissionColor") && (ec.r + ec.g + ec.b) > 0.001f;
            var emTex = TryTex(src, "emissiveTexture", "_EmissionMap");
            if (em || emTex != null)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", em ? ec : Color.white);
                if (emTex != null) m.SetTexture("_EmissionMap", emTex);
            }
            m.SetFloat("_Cull", 0f); // glTF double-sided meshes are common in these packs
            if (src != null) cache[src] = m;
            return m;
        }

        public static void Fix(GameObject root)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    if (Broken(mats[i])) { mats[i] = Convert(mats[i]); changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
        }
    }
}
