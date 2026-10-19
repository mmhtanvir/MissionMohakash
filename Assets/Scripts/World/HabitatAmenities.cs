using System;
using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Galley (east wall) and wash corner (north-west corner) for the habitat interior. They give the food and water
    /// stores a player-facing use (eat a meal, wash up) and are built from primitives like the rest of the room.
    /// Called once from Mission1.Launch right after HabitatInterior.Build.
    /// </summary>
    public static class HabitatAmenities
    {
        static Shader sh;

        static Material Mat(Color c, float smooth = 0.35f, float metal = 0.1f, float emit = 0f)
        {
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(sh);
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Smoothness", smooth); mat.SetFloat("_Metallic", metal);
            if (emit > 0f) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", c * emit); }
            return mat;
        }

        static GameObject Prim(Transform parent, PrimitiveType type, string name, Vector3 local, Vector3 size, Material mat, bool solid)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name; g.transform.SetParent(parent, false);
            g.transform.localPosition = local; g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            if (!solid) UnityEngine.Object.Destroy(g.GetComponent<Collider>());
            return g;
        }

        static void Station(Transform parent, string name, Vector3 local, float radius, Color marker, Func<string> prompt, Action use)
        {
            var it = Interactable.Create(name, HabitatInterior.Origin + local, radius, parent);
            it.Indoor = true; it.MarkerColor = marker; it.Prompt = prompt; it.Use = use;
        }

        public static void Build(Mission1 m, Transform interior)
        {
            if (m == null || interior == null) return;
            var white = Mat(new Color(0.88f, 0.9f, 0.92f), 0.4f, 0.1f);
            var steel = Mat(new Color(0.62f, 0.65f, 0.7f), 0.55f, 0.6f);
            var dark = Mat(new Color(0.16f, 0.18f, 0.22f), 0.3f, 0.3f);
            var amber = Mat(new Color(1f, 0.62f, 0.15f), 0.2f, 0f, 1.4f);
            var blue = Mat(new Color(0.35f, 0.7f, 1f), 0.3f, 0f, 0.8f);
            var mirror = Mat(new Color(0.75f, 0.85f, 0.92f), 0.95f, 0.9f);

            // ── galley (east wall, between the health scanner and the storage crates) ──
            Prim(interior, PrimitiveType.Cube, "Galley counter", new Vector3(5.45f, 0.45f, -2.1f), new Vector3(0.7f, 0.9f, 1.3f), white, true);
            Prim(interior, PrimitiveType.Cube, "Galley top", new Vector3(5.45f, 0.92f, -2.1f), new Vector3(0.74f, 0.04f, 1.34f), steel, false);
            Prim(interior, PrimitiveType.Cube, "Ration heater", new Vector3(5.4f, 1.0f, -1.8f), new Vector3(0.38f, 0.12f, 0.42f), dark, false);
            Prim(interior, PrimitiveType.Cube, "Heater glow", new Vector3(5.4f, 1.065f, -1.8f), new Vector3(0.3f, 0.01f, 0.34f), amber, false);
            Prim(interior, PrimitiveType.Cylinder, "Water dispenser", new Vector3(5.6f, 1.15f, -2.5f), new Vector3(0.16f, 0.22f, 0.16f), steel, false);
            Prim(interior, PrimitiveType.Cube, "Dispenser light", new Vector3(5.5f, 1.25f, -2.5f), new Vector3(0.02f, 0.05f, 0.1f), blue, false);
            Prim(interior, PrimitiveType.Cube, "Food locker", new Vector3(5.7f, 1.95f, -2.1f), new Vector3(0.4f, 0.7f, 1.3f), steel, false);
            Prim(interior, PrimitiveType.Cube, "Food locker stripe", new Vector3(5.49f, 1.95f, -2.1f), new Vector3(0.02f, 0.08f, 1.1f), amber, false);
            Station(interior, "Galley", new Vector3(4.5f, 0f, -2.1f), 1.5f, new Color(1f, 0.75f, 0.35f),
                () =>
                {
                    var mo = m.Model;
                    float wait = MissionModel.MealGapH - (mo.Hour - mo.LastMealHour);
                    return wait <= 0f
                        ? $"Eat a meal (food {mo.FoodDaysLeft:0.0} days left, fatigue {mo.Fatigue:0}%)"
                        : $"Galley: food {mo.FoodDaysLeft:0.0} days, water {mo.Water:0} L (next meal in {wait:0.0} h)";
                },
                m.Galley);
            Station(interior, "Water dispenser", new Vector3(4.7f, 0f, -3.1f), 0.8f, new Color(0.35f, 0.7f, 1f),
                () => $"Drink water (hydration {m.Model.Hydration:0}%, tank {m.Model.Water:0} L)", m.Drink);

            // ── wash corner (north-west, beside the bunk) ──
            Prim(interior, PrimitiveType.Cube, "Wash counter", new Vector3(-5.45f, 0.45f, 3.95f), new Vector3(0.7f, 0.9f, 1.0f), white, true);
            Prim(interior, PrimitiveType.Cube, "Wash top", new Vector3(-5.45f, 0.92f, 3.95f), new Vector3(0.74f, 0.04f, 1.04f), steel, false);
            Prim(interior, PrimitiveType.Cube, "Basin", new Vector3(-5.4f, 0.935f, 3.95f), new Vector3(0.46f, 0.02f, 0.56f), dark, false);
            Prim(interior, PrimitiveType.Cylinder, "Tap", new Vector3(-5.75f, 1.02f, 3.95f), new Vector3(0.04f, 0.1f, 0.04f), steel, false);
            Prim(interior, PrimitiveType.Cube, "Mirror", new Vector3(-5.96f, 1.65f, 3.95f), new Vector3(0.03f, 0.7f, 0.8f), mirror, false);
            Prim(interior, PrimitiveType.Cube, "Mirror light", new Vector3(-5.93f, 2.08f, 3.95f), new Vector3(0.03f, 0.04f, 0.8f), blue, false);
            Station(interior, "Wash basin", new Vector3(-4.6f, 0f, 3.7f), 1.5f, new Color(0.5f, 0.85f, 1f),
                () =>
                {
                    var mo = m.Model;
                    float wait = MissionModel.WashGapH - (mo.Hour - mo.LastWashHour);
                    return wait <= 0f
                        ? $"Wash up (uses 1 L of water, {mo.Water:0} L left)"
                        : $"Wash basin (next wash in {wait:0.0} h)";
                },
                m.WashUp);
        }
    }
}
