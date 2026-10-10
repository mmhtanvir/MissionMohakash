using System.Collections.Generic;
using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// TEMPORARY PLACEHOLDER helpers: primitive shapes + simple URP materials.
    /// Everything built with this is a stand-in for real 3D assets (Milestone 4).
    /// </summary>
    public static class Placeholder
    {
        static readonly Dictionary<int, Material> litCache = new Dictionary<int, Material>();
        static readonly Dictionary<int, Material> unlitCache = new Dictionary<int, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { litCache.Clear(); unlitCache.Clear(); }

        static int Key(Color c)
        {
            Color32 k = c;
            return (k.r << 24) | (k.g << 16) | (k.b << 8) | k.a;
        }

        static Shader FindShader(params string[] names)
        {
            foreach (var n in names)
            {
                var s = Shader.Find(n);
                if (s != null) return s;
            }
            return null;
        }

        public static Material Mat(Color c)
        {
            Material m;
            if (litCache.TryGetValue(Key(c), out m) && m != null) return m;
            var sh = FindShader("Universal Render Pipeline/Lit", "Standard");
            m = new Material(sh);
            m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.08f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.08f);
            litCache[Key(c)] = m;
            return m;
        }

        public static Material Unlit(Color c)
        {
            Material m;
            if (unlitCache.TryGetValue(Key(c), out m) && m != null) return m;
            var sh = FindShader("Universal Render Pipeline/Unlit", "Unlit/Color", "Universal Render Pipeline/Lit", "Standard");
            m = new Material(sh);
            m.color = c;
            unlitCache[Key(c)] = m;
            return m;
        }

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos,
                                      Vector3 localScale, Material mat, bool collider = true, Vector3 euler = default(Vector3))
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }
            return go;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }
    }
}
