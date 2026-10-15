using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Makes the imported Quaternius space-suit character read as a proper astronaut: white pressure suit with NASA-orange
    /// trim, mirrored gold visor, and a life-support backpack. The original materials are near-black teal, which is why
    /// the figure looked like a dark silhouette. Safe to call on any model: it only touches materials it recognises.
    /// </summary>
    public static class AstronautSuit
    {
        static readonly Color White = new Color(0.93f, 0.93f, 0.91f);
        static readonly Color OffWhite = new Color(0.80f, 0.80f, 0.78f);
        static readonly Color Grey = new Color(0.40f, 0.41f, 0.43f);
        static readonly Color Orange = new Color(0.96f, 0.40f, 0.08f);
        static readonly Color Gold = new Color(0.86f, 0.62f, 0.16f);

        static void Paint(Material m, Color c, float smooth, float metal)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("baseColorFactor")) m.SetColor("baseColorFactor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        }

        /// <param name="model">The spawned (and scaled) character.</param>
        /// <param name="root">The astronaut root; the backpack is parented here in world-scale metres.</param>
        public static void Dress(GameObject model, Transform root, float height)
        {
            if (model == null) return;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;                       // instances, so other users of the asset are untouched
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i]; if (m == null) continue;
                    string n = m.name;
                    if (n.Contains("SciFi_Light_Accent")) Paint(m, Orange, 0.35f, 0f);
                    else if (n.Contains("SciFi_Light")) Paint(m, White, 0.45f, 0f);
                    else if (n.Contains("SciFi_MainDark")) Paint(m, Grey, 0.30f, 0.1f);
                    else if (n.Contains("SciFi_Main")) Paint(m, OffWhite, 0.40f, 0f);
                    else if (n.StartsWith("Grey")) Paint(m, Gold, 0.95f, 0.85f);      // the visor glass
                }
                r.materials = mats;
            }
            AddBackpack(root, height);
        }

        static void AddBackpack(Transform root, float height)
        {
            if (root == null || root.Find("Life-support pack") != null) return;
            var lit = Resources.Load<Material>("Materials/MoonRock");
            Shader sh = lit != null ? lit.shader : Shader.Find("Universal Render Pipeline/Lit");
            float k = height / 1.8f;

            var pack = new GameObject("Life-support pack");
            pack.transform.SetParent(root, false);
            pack.layer = 2;

            Part(pack.transform, PrimitiveType.Cube, "Shell", new Vector3(0f, 1.20f, -0.27f) * k, new Vector3(0.46f, 0.58f, 0.22f) * k, White, sh);
            Part(pack.transform, PrimitiveType.Cube, "Top", new Vector3(0f, 1.52f, -0.27f) * k, new Vector3(0.40f, 0.07f, 0.20f) * k, Orange, sh);
            Part(pack.transform, PrimitiveType.Cylinder, "Tank L", new Vector3(-0.15f, 1.18f, -0.41f) * k, new Vector3(0.13f, 0.26f, 0.13f) * k, OffWhite, sh);
            Part(pack.transform, PrimitiveType.Cylinder, "Tank R", new Vector3(0.15f, 1.18f, -0.41f) * k, new Vector3(0.13f, 0.26f, 0.13f) * k, OffWhite, sh);
            Part(pack.transform, PrimitiveType.Cube, "Stripe", new Vector3(0f, 1.20f, -0.385f) * k, new Vector3(0.40f, 0.06f, 0.02f) * k, Orange, sh);
            // antenna + blinking status LED
            Part(pack.transform, PrimitiveType.Cylinder, "Antenna", new Vector3(0.14f, 1.72f, -0.27f) * k, new Vector3(0.012f, 0.16f, 0.012f) * k, Grey, sh);
            var led = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(led.GetComponent<Collider>());
            led.name = "Status LED"; led.layer = 2; led.transform.SetParent(pack.transform, false);
            led.transform.localPosition = new Vector3(0.14f, 1.89f, -0.27f) * k; led.transform.localScale = Vector3.one * 0.035f * k;
            var lm = new Material(sh); lm.EnableKeyword("_EMISSION"); led.GetComponent<Renderer>().sharedMaterial = lm;
            var bl = led.AddComponent<Blink>(); bl.Color = new Color(0.1f, 1f, 0.3f); bl.Rate = 0.7f;
            // helmet lamp, switches on when the sun is low
            var lampGo = new GameObject("Helmet lamp"); lampGo.transform.SetParent(root, false);
            lampGo.transform.localPosition = new Vector3(0f, 1.62f, 0.2f) * k; lampGo.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            var lamp = lampGo.AddComponent<Light>(); lamp.type = LightType.Spot; lamp.range = 18f; lamp.spotAngle = 70f; lamp.intensity = 0f; lamp.shadows = LightShadows.None; lamp.color = new Color(1f, 0.97f, 0.9f);
            Light sun = null; foreach (var l in Object.FindObjectsByType<Light>()) if (l.type == LightType.Directional) sun = l;
            var nl = lampGo.AddComponent<NightLight>(); nl.Lamp = lamp; nl.Sun = sun; nl.Intensity = 12f;
        }

        static void Part(Transform parent, PrimitiveType t, string name, Vector3 pos, Vector3 scale, Color c, Shader sh)
        {
            var go = GameObject.CreatePrimitive(t);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = name; go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            var m = new Material(sh);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.45f); m.SetFloat("_Metallic", 0.05f);
            go.GetComponent<Renderer>().sharedMaterial = m;
        }
    }
}
