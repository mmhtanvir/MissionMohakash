using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// A walkable 3D interior for the habitat, built entirely from primitives at runtime and placed far from the
    /// surface (so it never clashes with the moon terrain). When the player goes through the airlock the astronaut is
    /// teleported here and the third-person camera follows. Stations (press E): airlock door, suit dock, bunk,
    /// power and life-support console, sample lab, Mission Control comms, health scanner and a window onto Earth.
    /// </summary>
    public class HabitatInterior : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(2600f, 0f, 2600f);
        // bunk pose for the sleeping crew member (head on the pillow at +Z, avatar yaw 180)
        public static Vector3 BedPosition => Origin + new Vector3(-5.3f, 0.60f, 1.95f);
        public static float BedYaw => 180f;
        public static Vector3 BedExit => Origin + new Vector3(-4.3f, 0.15f, 1.9f);
        GameObject blanket;
        public void SetBedCovered(bool on)
        {
            if (blanket == null) return;
            var p = blanket.transform.localPosition; p.y = on ? 0.93f : 0.62f; blanket.transform.localPosition = p;
            var sc = blanket.transform.localScale; sc.y = on ? 0.10f : 0.06f; blanket.transform.localScale = sc;
        }

        public static Vector3 SpawnPoint => Origin + new Vector3(0f, 0.15f, -3.0f);
        public const float W = 12f, D = 9f, H = 3.4f;

        Mission1 m;
        Shader sh;
        readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        readonly HashSet<Material> textured = new HashSet<Material>();
        Material Tex(Material m) { textured.Add(m); return m; }

        public static HabitatInterior Build(Mission1 mission)
        {
            var go = new GameObject("Habitat Interior");
            go.transform.position = Origin;
            var hi = go.AddComponent<HabitatInterior>();
            hi.m = mission;
            hi.Construct();
            return hi;
        }

        // ── materials / primitives ────────────────────────────────────────────────
        Material Mat(Color c, float smooth = 0.35f, float metal = 0.1f, float emit = 0f)
        {
            string key = $"{c.r:0.00}{c.g:0.00}{c.b:0.00}{smooth:0.0}{metal:0.0}{emit:0.0}";
            if (mats.TryGetValue(key, out var mm)) return mm;
            var mat = new Material(sh);
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Smoothness", smooth); mat.SetFloat("_Metallic", metal);
            if (emit > 0f) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", c * emit); }
            mats[key] = mat;
            return mat;
        }

        GameObject Prim(PrimitiveType t, string name, Vector3 pos, Vector3 size, Material mat, bool solid = true, int layer = 0, Transform parent = null)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = name;
            g.transform.SetParent(parent != null ? parent : transform, false);
            g.transform.localPosition = pos; g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = textured.Contains(mat) ? SurfaceKit.TiledFor(mat, size) : mat;
            if (!solid) Destroy(g.GetComponent<Collider>());
            g.layer = layer;
            return g;
        }
        GameObject Box(string n, Vector3 pos, Vector3 size, Material mat, bool solid = true, int layer = 0) => Prim(PrimitiveType.Cube, n, pos, size, mat, solid, layer);
        GameObject Cyl(string n, Vector3 pos, Vector3 size, Material mat, bool solid = false) => Prim(PrimitiveType.Cylinder, n, pos, size, mat, solid);

        GameObject Screen(string name, Vector3 pos, Vector3 size, Color tint, int style)
        {
            var g = Box(name, pos, size, Mat(new Color(0.02f, 0.03f, 0.04f)), false);
            var sd = g.AddComponent<ScreenDisplay>(); sd.Tint = tint; sd.Style = style;
            return g;
        }

        GameObject Pipe(string name, Vector3 a, Vector3 b, float radius, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name; g.transform.SetParent(transform, false);
            Vector3 d = b - a;
            g.transform.localPosition = (a + b) * 0.5f;
            g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            g.transform.localScale = new Vector3(radius * 2f, d.magnitude * 0.5f, radius * 2f);
            g.GetComponent<Renderer>().sharedMaterial = mat;
            Destroy(g.GetComponent<Collider>());
            return g;
        }

        void Led(Vector3 pos, Color c, float rate, float phase, bool lamp = false)
        {
            var g = Prim(PrimitiveType.Sphere, "LED", pos, Vector3.one * 0.05f, Mat(c, 0.2f, 0f, 2f), false);
            var b = g.AddComponent<Blink>(); b.Color = c; b.Rate = rate; b.Phase = phase; b.Strength = 3.5f;
        }

        Interactable Station(string name, Vector3 local, float radius, Color marker, Func<string> prompt, Action use)
        {
            var it = Interactable.Create(name, Origin + local, radius, transform);
            it.Indoor = true; it.MarkerColor = marker; it.Prompt = prompt; it.Use = use;
            return it;
        }

        // ── build ─────────────────────────────────────────────────────────────────
        void Construct()
        {
            var rock = Resources.Load<Material>("Materials/MoonRock");
            sh = rock != null ? rock.shader : Shader.Find("Universal Render Pipeline/Lit");

            SurfaceKit.Hull(out var hullA, out var hullN);
            SurfaceKit.Floor(out var floorA, out var floorN);
            var wall = Tex(SurfaceKit.Make(new Color(0.86f, 0.88f, 0.91f), 0.34f, 0.12f, hullA, hullN));
            var wallDark = Tex(SurfaceKit.Make(new Color(0.34f, 0.38f, 0.45f), 0.42f, 0.35f, hullA, hullN));
            var floor = Tex(SurfaceKit.Make(new Color(0.95f, 0.95f, 0.97f), 0.55f, 0.75f, floorA, floorN, null, 1.3f));
            var steel = Tex(SurfaceKit.Make(new Color(0.78f, 0.80f, 0.84f), 0.62f, 0.75f, hullA, hullN));
            var white = Tex(SurfaceKit.Make(new Color(0.95f, 0.95f, 0.93f), 0.42f, 0.08f, hullA, hullN));
            var orange = Mat(new Color(0.95f, 0.42f, 0.08f), 0.35f, 0f);
            var cyanGlow = Mat(new Color(0.20f, 0.85f, 1.00f), 0.2f, 0f, 2.2f);
            var greenGlow = Mat(new Color(0.25f, 1.00f, 0.45f), 0.2f, 0f, 2.0f);
            var amberGlow = Mat(new Color(1.00f, 0.70f, 0.20f), 0.2f, 0f, 2.0f);
            var lamp = Mat(new Color(1f, 0.97f, 0.90f), 0.2f, 0f, 3.0f);

            // shell
            Box("Floor", new Vector3(0, -0.15f, 0), new Vector3(W + 0.6f, 0.3f, D + 0.6f), floor);
            Box("Ceiling", new Vector3(0, H + 0.15f, 0), new Vector3(W + 0.6f, 0.3f, D + 0.6f), wall, true, 2);   // layer 2: the camera ignores it
            Box("Wall S (airlock)", new Vector3(0, H / 2, -D / 2 - 0.15f), new Vector3(W + 0.6f, H, 0.3f), wall);
            Box("Wall N", new Vector3(0, H / 2, D / 2 + 0.15f), new Vector3(W + 0.6f, H, 0.3f), wall);
            Box("Wall W", new Vector3(-W / 2 - 0.15f, H / 2, 0), new Vector3(0.3f, H, D), wall);
            // east wall with a big window (z -1.8..2.2, y 0.9..2.5)
            float ex = W / 2 + 0.15f;
            Box("Wall E low", new Vector3(ex, 0.45f, 0), new Vector3(0.3f, 0.9f, D), wall);
            Box("Wall E high", new Vector3(ex, (2.5f + H) / 2, 0), new Vector3(0.3f, H - 2.5f, D), wall);
            Box("Wall E front", new Vector3(ex, 1.7f, -3.15f), new Vector3(0.3f, 1.6f, 2.7f), wall);
            Box("Wall E back", new Vector3(ex, 1.7f, 3.35f), new Vector3(0.3f, 1.6f, 2.3f), wall);
            Box("Window sill", new Vector3(W / 2 - 0.1f, 0.92f, 0.2f), new Vector3(0.5f, 0.06f, 4.2f), steel, false);
            Box("Frame top", new Vector3(W / 2, 2.52f, 0.2f), new Vector3(0.2f, 0.06f, 4.1f), steel, false);
            Box("Frame L", new Vector3(W / 2, 1.7f, -1.8f), new Vector3(0.2f, 1.6f, 0.06f), steel, false);
            Box("Frame R", new Vector3(W / 2, 1.7f, 2.2f), new Vector3(0.2f, 1.6f, 0.06f), steel, false);

            // wall ribs + dark skirting for depth
            for (int i = -1; i <= 1; i++)
            {
                Box("Rib S", new Vector3(i * 4f, H / 2, -D / 2 + 0.05f), new Vector3(0.14f, H, 0.12f), wallDark, false);
                Box("Rib N", new Vector3(i * 4f + 2f, H / 2, D / 2 - 0.05f), new Vector3(0.14f, H, 0.12f), wallDark, false);
            }
            Box("Skirt S", new Vector3(0, 0.1f, -D / 2 + 0.03f), new Vector3(W, 0.2f, 0.06f), wallDark, false);
            Box("Skirt N", new Vector3(0, 0.1f, D / 2 - 0.03f), new Vector3(W, 0.2f, 0.06f), wallDark, false);
            Box("Skirt W", new Vector3(-W / 2 + 0.03f, 0.1f, 0), new Vector3(0.06f, 0.2f, D), wallDark, false);

            // lighting: three ceiling strips + soft point lights (kept at 3 so URP's per-object light limit is never hit)
            for (int i = -1; i <= 1; i++)
            {
                Box("Ceiling light", new Vector3(0, H - 0.04f, i * 2.8f), new Vector3(3.2f, 0.05f, 0.4f), lamp, false);
                var lg = new GameObject("Light " + i).AddComponent<Light>();
                lg.transform.SetParent(transform, false); lg.transform.localPosition = new Vector3(0, H - 0.5f, i * 2.8f);
                lg.type = LightType.Point; lg.range = 10.5f; lg.intensity = 3.8f; lg.color = new Color(1f, 0.96f, 0.90f); lg.shadows = LightShadows.None;
            }
            // floor guide strip to the airlock
            Box("Guide strip", new Vector3(0, 0.012f, -2.4f), new Vector3(0.18f, 0.02f, 3.4f), cyanGlow, false);

            // ── airlock door (south wall, centre) ────────────────────────────────────
            Box("Airlock post L", new Vector3(-1.15f, 1.3f, -D / 2 + 0.15f), new Vector3(0.22f, 2.6f, 0.3f), steel, false);
            Box("Airlock post R", new Vector3(1.15f, 1.3f, -D / 2 + 0.15f), new Vector3(0.22f, 2.6f, 0.3f), steel, false);
            Box("Airlock lintel", new Vector3(0, 2.7f, -D / 2 + 0.15f), new Vector3(2.5f, 0.22f, 0.3f), steel, false);
            Box("Airlock door", new Vector3(0, 1.25f, -D / 2 + 0.06f), new Vector3(2.0f, 2.5f, 0.1f), wallDark, false);
            Box("Airlock status", new Vector3(0, 2.85f, -D / 2 + 0.05f), new Vector3(0.7f, 0.12f, 0.03f), greenGlow, false);
            Box("Hazard strip", new Vector3(0, 0.012f, -D / 2 + 0.55f), new Vector3(2.4f, 0.02f, 0.35f), amberGlow, false);
            Station("Airlock door", new Vector3(0, 0, -3.5f), 2.2f, new Color(0.3f, 1f, 0.5f),
                () => m.Model.EvaLeft < 0.6f ? $"Airlock: suit still refilling ({m.Model.EvaLeft:0.0} h) - wait or rest" : $"Go outside (EVA suit ready: {m.Model.EvaLeft:0.0} h)",
                m.ExitHabitat);

            // ── suit dock (south wall, west) ─────────────────────────────────────────
            for (int i = 0; i < 2; i++)
            {
                float x = -3.9f + i * 1.15f;
                Box("Suit locker", new Vector3(x, 1.1f, -D / 2 + 0.3f), new Vector3(1.0f, 2.2f, 0.5f), white, true);
                Box("Locker stripe", new Vector3(x, 1.5f, -D / 2 + 0.56f), new Vector3(0.9f, 0.1f, 0.02f), orange, false);
                Box("Locker light", new Vector3(x, 2.0f, -D / 2 + 0.57f), new Vector3(0.3f, 0.08f, 0.02f), greenGlow, false);
            }
            Station("Suit dock", new Vector3(-3.35f, 0, -3.3f), 2.1f, new Color(1f, 0.7f, 0.3f),
                () => m.Model.Spares > 0 && m.Model.SuitIntegrity < 100f ? $"Repair suit with a spare kit (integrity {m.Model.SuitIntegrity:0}%, {m.Model.Spares} spare)"
                                                                         : $"Suit status: {m.Model.EvaLeft:0.0} h EVA, integrity {m.Model.SuitIntegrity:0}%",
                m.SuitDockUse);

            // ── Mission Control comms (west wall, south) ─────────────────────────────
            Box("Comms desk", new Vector3(-5.45f, 0.45f, -2.3f), new Vector3(0.9f, 0.9f, 2.0f), steel);
            Screen("Comms screen", new Vector3(-5.9f, 1.45f, -2.3f), new Vector3(0.08f, 0.6f, 1.2f), new Color(0.2f, 0.85f, 1f), 0);
            Screen("Comms screen 2", new Vector3(-5.9f, 1.45f, -1.0f), new Vector3(0.08f, 0.45f, 0.6f), new Color(1f, 0.7f, 0.2f), 1);
            Cyl("Antenna mast", new Vector3(-5.45f, 1.1f, -3.0f), new Vector3(0.05f, 0.22f, 0.05f), steel);
            Prim(PrimitiveType.Sphere, "Dish", new Vector3(-5.45f, 1.35f, -3.0f), new Vector3(0.35f, 0.12f, 0.35f), white, false);
            Station("Mission Control link", new Vector3(-4.5f, 0, -2.3f), 2.1f, new Color(0.4f, 0.8f, 1f),
                () => (m.Model.Load("COM")?.Powered ?? false) ? "Call Mission Control for advice" : "Comms link offline (COM has no power)", m.CommsCall);

            // ── bunk (west wall, north) ──────────────────────────────────────────────
            Box("Bunk frame", new Vector3(-5.3f, 0.2f, 1.9f), new Vector3(1.1f, 0.4f, 2.1f), wallDark);
            Box("Mattress", new Vector3(-5.3f, 0.5f, 1.9f), new Vector3(1.0f, 0.2f, 2.0f), Mat(new Color(0.30f, 0.40f, 0.55f), 0.2f, 0f), false);
            Box("Pillow", new Vector3(-5.3f, 0.66f, 2.7f), new Vector3(0.7f, 0.13f, 0.4f), white, false);
            blanket = Box("Blanket", new Vector3(-5.3f, 0.62f, 1.5f), new Vector3(0.95f, 0.06f, 1.1f), orange, false);
            Box("Bunk locker", new Vector3(-5.78f, 2.1f, 1.9f), new Vector3(0.4f, 0.7f, 2.0f), steel, false);
            Station("Bunk", new Vector3(-4.3f, 0, 1.9f), 2.1f, new Color(0.6f, 0.7f, 1f),
                () => m.Model.Resting ? "Wake up" : $"Sleep (fatigue {m.Model.Fatigue:0}%, time runs 4x)", m.ToggleRest);

            // ── power & life-support console (north wall) ────────────────────────────
            Box("Console desk", new Vector3(-1.0f, 0.45f, 3.95f), new Vector3(3.2f, 0.9f, 0.75f), steel);
            Screen("Screen A", new Vector3(-2.3f, 1.4f, 4.3f), new Vector3(0.95f, 0.55f, 0.05f), new Color(0.3f, 1f, 0.5f), 0);
            Screen("Screen B", new Vector3(-1.0f, 1.4f, 4.3f), new Vector3(0.95f, 0.55f, 0.05f), new Color(1f, 0.7f, 0.2f), 1);
            Screen("Screen C", new Vector3(0.3f, 1.4f, 4.3f), new Vector3(0.95f, 0.55f, 0.05f), new Color(0.2f, 0.85f, 1f), 0);
            Station("Power & life support", new Vector3(-1.0f, 0, 3.0f), 2.5f, new Color(1f, 0.85f, 0.3f),
                () => $"Open power & life support (battery {m.Model.Battery:0}, cabin O2 {m.Model.CabinO2:0.0})", m.ToggleSystems);

            // ── sample lab (north wall, east) ────────────────────────────────────────
            Box("Lab bench", new Vector3(3.4f, 0.48f, 3.95f), new Vector3(2.6f, 0.95f, 0.8f), white);
            Box("Lab cabinet", new Vector3(3.4f, 2.1f, 4.25f), new Vector3(2.6f, 0.7f, 0.35f), steel, false);
            Box("Microscope base", new Vector3(2.8f, 1.0f, 3.9f), new Vector3(0.3f, 0.08f, 0.3f), wallDark, false);
            Cyl("Microscope tube", new Vector3(2.8f, 1.2f, 3.9f), new Vector3(0.1f, 0.18f, 0.1f), steel);
            Box("Sample tray", new Vector3(3.6f, 0.98f, 3.85f), new Vector3(0.6f, 0.04f, 0.4f), wallDark, false);
            for (int i = 0; i < 3; i++) Prim(PrimitiveType.Sphere, "Sample", new Vector3(3.4f + i * 0.17f, 1.04f, 3.85f), Vector3.one * 0.09f, Mat(new Color(0.55f, 0.50f, 0.45f), 0.1f, 0f), false);
            Screen("Lab screen", new Vector3(4.2f, 1.4f, 4.15f), new Vector3(0.5f, 0.4f, 0.04f), new Color(0.2f, 0.85f, 1f), 1);
            Station("Sample lab", new Vector3(3.4f, 0, 3.0f), 2.3f, new Color(0.3f, 0.9f, 1f),
                () => m.Model.SamplesDelivered > m.SamplesAnalyzed ? $"Analyse a regolith sample ({m.Model.SamplesDelivered - m.SamplesAnalyzed} waiting)"
                                                                  : "Sample lab (no samples waiting - collect some outside)", m.AnalyzeSample);

            // ── health scanner (east wall, south) ────────────────────────────────────
            Box("Scanner body", new Vector3(5.4f, 0.85f, -3.5f), new Vector3(0.7f, 1.7f, 0.9f), white);
            Screen("Scanner screen", new Vector3(5.03f, 1.35f, -3.5f), new Vector3(0.05f, 0.4f, 0.6f), new Color(0.3f, 1f, 0.5f), 0);
            Box("Scanner stripe", new Vector3(5.03f, 0.6f, -3.5f), new Vector3(0.05f, 0.1f, 0.7f), orange, false);
            Station("Health scan", new Vector3(4.3f, 0, -3.5f), 2.1f, new Color(0.4f, 1f, 0.6f),
                () => $"Health scan (health {m.Model.Health:0}%, fatigue {m.Model.Fatigue:0}%, dose {m.Model.Dose:0.0})", m.HealthScan);

            // ── window seat ───────────────────────────────────────────────────────────
            Box("Window seat", new Vector3(5.2f, 0.25f, 0.2f), new Vector3(0.8f, 0.5f, 3.0f), wallDark, false);
            Box("Seat cushion", new Vector3(5.2f, 0.56f, 0.2f), new Vector3(0.7f, 0.12f, 2.9f), Mat(new Color(0.30f, 0.40f, 0.55f), 0.2f, 0f), false);
            Station("Window", new Vector3(4.2f, 0, 0.2f), 2.4f, new Color(0.7f, 0.85f, 1f), () => "Look out the window", m.WindowLook);

            // ── table, stools, props ─────────────────────────────────────────────────
            Box("Table top", new Vector3(0.3f, 0.78f, -0.4f), new Vector3(2.2f, 0.06f, 1.1f), white);
            for (int ix = -1; ix <= 1; ix += 2)
            for (int iz = -1; iz <= 1; iz += 2)
                Cyl("Table leg", new Vector3(0.3f + ix * 0.95f, 0.38f, -0.4f + iz * 0.42f), new Vector3(0.07f, 0.38f, 0.07f), steel);
            Cyl("Stool A", new Vector3(-0.5f, 0.25f, 0.6f), new Vector3(0.42f, 0.25f, 0.42f), orange);
            Cyl("Stool B", new Vector3(1.1f, 0.25f, 0.6f), new Vector3(0.42f, 0.25f, 0.42f), orange);
            MoonWorld.Spawn("PickupJar", Origin + new Vector3(0.0f, 0.81f, -0.4f), Quaternion.identity, 0.5f, transform, "Jar");
            Box("Tablet", new Vector3(0.9f, 0.82f, -0.35f), new Vector3(0.28f, 0.015f, 0.2f), cyanGlow, false);

            // storage corner (north-east): crates + gas tanks
            for (int i = 0; i < 3; i++)
                MoonWorld.Spawn("PickupCrate", Origin + new Vector3(5.2f - (i % 2) * 0.9f, 0.4f + (i / 2) * 0.8f, 3.7f - (i % 2) * 0.2f), Quaternion.Euler(0, i * 15f, 0), 0.8f, transform, "Crate");
            for (int i = 0; i < 3; i++)
                Cyl("O2 tank", new Vector3(-5.45f + i * 0.42f, 0.6f, -4.1f), new Vector3(0.3f, 0.6f, 0.3f), Mat(new Color(0.85f, 0.88f, 0.9f), 0.5f, 0.4f), true);
            // greenhouse shelf (decor)
            Box("Planter shelf", new Vector3(2.0f, 0.5f, -4.1f), new Vector3(1.6f, 1.0f, 0.5f), steel);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Sphere, "Plant", new Vector3(1.5f + i * 0.33f, 1.1f, -4.1f), new Vector3(0.28f, 0.22f, 0.28f), Mat(new Color(0.25f, 0.65f, 0.25f), 0.2f, 0f), false);

            // ── detail pass: pipes, vents, conduits, status lights, door lights, moon globe ─────────────────────
            var pipeMat = Mat(new Color(0.72f, 0.74f, 0.78f), 0.6f, 0.8f);
            var pipeOrange = Mat(new Color(0.95f, 0.42f, 0.08f), 0.4f, 0.4f);
            Pipe("Ceiling pipe A", new Vector3(-W / 2 + 0.2f, H - 0.35f, -D / 2 + 0.3f), new Vector3(W / 2 - 0.2f, H - 0.35f, -D / 2 + 0.3f), 0.09f, pipeMat);
            Pipe("Ceiling pipe B", new Vector3(-W / 2 + 0.2f, H - 0.55f, -D / 2 + 0.3f), new Vector3(W / 2 - 0.2f, H - 0.55f, -D / 2 + 0.3f), 0.06f, pipeOrange);
            Pipe("Ceiling pipe C", new Vector3(-W / 2 + 0.2f, H - 0.4f, D / 2 - 0.3f), new Vector3(W / 2 - 0.2f, H - 0.4f, D / 2 - 0.3f), 0.08f, pipeMat);
            Pipe("Ceiling pipe D", new Vector3(-W / 2 + 0.3f, H - 0.4f, -D / 2 + 0.3f), new Vector3(-W / 2 + 0.3f, H - 0.4f, D / 2 - 0.3f), 0.07f, pipeMat);
            for (int i = -2; i <= 2; i++)
            {
                Pipe("Flange", new Vector3(i * 2.4f - 0.05f, H - 0.35f, -D / 2 + 0.3f), new Vector3(i * 2.4f + 0.05f, H - 0.35f, -D / 2 + 0.3f), 0.13f, steel);
                Pipe("Flange", new Vector3(i * 2.4f - 0.05f, H - 0.4f, D / 2 - 0.3f), new Vector3(i * 2.4f + 0.05f, H - 0.4f, D / 2 - 0.3f), 0.12f, steel);
            }
            Pipe("Riser SW", new Vector3(-W / 2 + 0.25f, 0.05f, -D / 2 + 0.3f), new Vector3(-W / 2 + 0.25f, H - 0.3f, -D / 2 + 0.3f), 0.07f, pipeOrange);
            Pipe("Riser NE", new Vector3(W / 2 - 0.25f, 0.05f, D / 2 - 0.3f), new Vector3(W / 2 - 0.25f, H - 0.3f, D / 2 - 0.3f), 0.07f, pipeMat);
            for (int i = 0; i < 4; i++)
                Box("Vent", new Vector3((i % 2 == 0 ? -3.5f : 3.5f), H - 0.03f, (i < 2 ? -3.3f : 3.3f)), new Vector3(0.9f, 0.04f, 0.5f), floor, false);
            for (int i = -2; i <= 2; i++)
                Box("Wall panel light", new Vector3(i * 2.4f, 2.2f, -D / 2 + 0.07f), new Vector3(0.5f, 0.05f, 0.02f), cyanGlow, false);

            // status LEDs that blink at different rates
            Led(new Vector3(-2.3f, 1.05f, 3.95f), new Color(1f, 0.15f, 0.1f), 0.8f, 0.0f);
            Led(new Vector3(-2.15f, 1.05f, 3.95f), new Color(0.25f, 1f, 0.4f), 1.4f, 0.3f);
            Led(new Vector3(0.7f, 1.05f, 3.95f), new Color(1f, 0.7f, 0.1f), 0.5f, 0.6f);
            Led(new Vector3(-1.2f, 2.2f, -D / 2 + 0.2f), new Color(0.25f, 1f, 0.4f), 0.4f, 0.1f);
            Led(new Vector3(1.2f, 2.2f, -D / 2 + 0.2f), new Color(0.25f, 1f, 0.4f), 0.4f, 0.1f);
            Led(new Vector3(5.03f, 1.75f, -3.5f), new Color(1f, 0.15f, 0.1f), 0.7f, 0.2f);

            // glowing door frame strips
            Box("Door strip L", new Vector3(-1.04f, 1.3f, -D / 2 + 0.3f), new Vector3(0.04f, 2.3f, 0.02f), greenGlow, false);
            Box("Door strip R", new Vector3(1.04f, 1.3f, -D / 2 + 0.3f), new Vector3(0.04f, 2.3f, 0.02f), greenGlow, false);

            // rotating holographic moon globe on the table
            var globe = Prim(PrimitiveType.Sphere, "Moon globe", new Vector3(0.3f, 1.12f, -0.4f), Vector3.one * 0.38f, null, false);
            SurfaceKit.Grime(out var gA, out var gN);
            var gm = SurfaceKit.Make(new Color(0.62f, 0.64f, 0.68f), 0.15f, 0f, gA, gN, new Vector2(3f, 2f), 1.2f);
            globe.GetComponent<Renderer>().sharedMaterial = gm;
            globe.AddComponent<Spin>().Speed = 14f;
            Prim(PrimitiveType.Cylinder, "Globe base", new Vector3(0.3f, 0.83f, -0.4f), new Vector3(0.22f, 0.02f, 0.22f), cyanGlow, false);
            var gl = new GameObject("Globe light").AddComponent<Light>();
            gl.transform.SetParent(transform, false); gl.transform.localPosition = new Vector3(0.3f, 1.3f, -0.4f);
            gl.type = LightType.Point; gl.range = 3.2f; gl.intensity = 1.6f; gl.color = new Color(0.4f, 0.8f, 1f); gl.shadows = LightShadows.None;
        }
    }
}
