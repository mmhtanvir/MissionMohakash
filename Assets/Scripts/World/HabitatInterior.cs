using System.Collections;
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
        // ── blanket: a cloth grid draped over the mattress, and over the sleeper's real (baked) pose while asleep ──
        const int BX = 26, BZ = 34;
        const float BedX = -5.3f, MattressTop = 0.60f, BlX0 = -5.84f, BlX1 = -4.60f, BlZ0 = 0.80f, BlZ1 = 2.34f;
        Mesh blanketMesh, sheetMesh; float[] blH, blTarget; bool draped;

        /// <summary>Call every frame. Flat on the mattress when the bunk is empty; tucked over the body once the sleeper is asleep.</summary>
        public void UpdateBlanket(Astronaut who)
        {
            if (blanketMesh == null) return;
            bool want = who != null && who.Asleep;
            if (want && !draped) { BlanketTarget(who); draped = true; }
            else if (!want && draped) { BlanketTarget(null); draped = false; }
            float k = 1f - Mathf.Exp(-(draped ? 2.5f : 6f) * Time.deltaTime);
            bool moved = false;
            for (int i = 0; i < blH.Length; i++) { float h = Mathf.Lerp(blH[i], blTarget[i], k); moved |= Mathf.Abs(h - blH[i]) > 1e-4f; blH[i] = h; }
            if (moved) RebuildBlanket();
        }

        static float BedProfile(float x, float z)
        {
            float h = MattressTop + 0.025f;
            float dx = Mathf.Abs(x - BedX) - 0.5f; if (dx > 0f) h -= dx * 2.4f;          // hangs over the side
            if (z < 0.90f) h -= (0.90f - z) * 2.4f;                                        // ...and over the foot end
            float qx = Mathf.Repeat((x - BlX0) / 0.16f, 1f), qz = Mathf.Repeat((z - BlZ0) / 0.16f, 1f);
            h -= 0.005f * (Mathf.Exp(-qx * qx * 60f) + Mathf.Exp(-qz * qz * 60f));         // quilting seams
            return Mathf.Max(h, 0.28f);
        }

        void BlanketTarget(Astronaut who)
        {
            float dx = (BlX1 - BlX0) / (BX - 1), dz = (BlZ1 - BlZ0) / (BZ - 1);
            var body = new float[BX * BZ];
            for (int i = 0; i < body.Length; i++) body[i] = -1f;
            if (who != null && who.ActiveModel != null)
            {
                var baked = new Mesh();
                foreach (var smr in who.ActiveModel.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    smr.BakeMesh(baked, true);
                    var M = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                    foreach (var v in baked.vertices)
                    {
                        Vector3 p = M.MultiplyPoint3x4(v) - Origin;
                        int ix = Mathf.RoundToInt((p.x - BlX0) / dx), iz = Mathf.RoundToInt((p.z - BlZ0) / dz);
                        if (ix < 0 || ix >= BX || iz < 0 || iz >= BZ) continue;
                        body[iz * BX + ix] = Mathf.Max(body[iz * BX + ix], p.y + 0.035f);
                    }
                }
                Destroy(baked);
                // cloth tension: the blanket spans from the body's high points and falls off at ~40 degrees
                float fall = 0.85f * Mathf.Max(dx, dz);
                for (int it = 0; it < 10; it++)
                    for (int z = 0; z < BZ; z++)
                        for (int x = 0; x < BX; x++)
                        {
                            float h = body[z * BX + x];
                            if (x > 0) h = Mathf.Max(h, body[z * BX + x - 1] - fall);
                            if (x < BX - 1) h = Mathf.Max(h, body[z * BX + x + 1] - fall);
                            if (z > 0) h = Mathf.Max(h, body[(z - 1) * BX + x] - fall);
                            if (z < BZ - 1) h = Mathf.Max(h, body[(z + 1) * BX + x] - fall);
                            body[z * BX + x] = h;
                        }
            }
            for (int z = 0; z < BZ; z++)
                for (int x = 0; x < BX; x++)
                {
                    float px = BlX0 + x * dx, pz = BlZ0 + z * dz;
                    float b = body[z * BX + x];
                    if (Mathf.Abs(px - BedX) > 0.52f) b -= (Mathf.Abs(px - BedX) - 0.52f) * 1.5f;   // drop down past the mattress edge
                    blTarget[z * BX + x] = Mathf.Max(BedProfile(px, pz), b);
                }
            // soften (cloth never has sharp creases at this scale)
            var tmp = (float[])blTarget.Clone();
            for (int z = 1; z < BZ - 1; z++)
                for (int x = 1; x < BX - 1; x++)
                    blTarget[z * BX + x] = Mathf.Max(BedProfile(BlX0 + x * dx, BlZ0 + z * dz),
                        (tmp[z * BX + x] * 4 + tmp[z * BX + x - 1] + tmp[z * BX + x + 1] + tmp[(z - 1) * BX + x] + tmp[(z + 1) * BX + x]) / 8f);
        }

        void RebuildBlanket()
        {
            float dx = (BlX1 - BlX0) / (BX - 1), dz = (BlZ1 - BlZ0) / (BZ - 1);
            var v = new Vector3[BX * BZ];
            for (int z = 0; z < BZ; z++)
                for (int x = 0; x < BX; x++)
                    v[z * BX + x] = new Vector3(BlX0 + x * dx, blH[z * BX + x], BlZ0 + z * dz);
            blanketMesh.vertices = v; blanketMesh.RecalculateNormals(); blanketMesh.RecalculateBounds();
            // white turned-down sheet along the top edge
            const int rows = 3; var sv = new Vector3[BX * rows];
            for (int r = 0; r < rows; r++)
                for (int x = 0; x < BX; x++) { var p = v[(BZ - rows + r) * BX + x]; sv[r * BX + x] = p + Vector3.up * 0.006f; }
            sheetMesh.vertices = sv; sheetMesh.RecalculateNormals(); sheetMesh.RecalculateBounds();
        }

        static Mesh GridMesh(string name, int nx, int nz)
        {
            var m = new Mesh { name = name };
            var v = new Vector3[nx * nz]; var uv = new Vector2[nx * nz]; var t = new int[(nx - 1) * (nz - 1) * 6];
            for (int z = 0; z < nz; z++) for (int x = 0; x < nx; x++) uv[z * nx + x] = new Vector2(x / (nx - 1f), z / (nz - 1f));
            int k = 0;
            for (int z = 0; z < nz - 1; z++)
                for (int x = 0; x < nx - 1; x++)
                {
                    int a = z * nx + x;
                    t[k++] = a; t[k++] = a + nx; t[k++] = a + 1; t[k++] = a + 1; t[k++] = a + nx; t[k++] = a + nx + 1;
                }
            m.vertices = v; m.uv = uv; m.triangles = t; m.MarkDynamic();
            return m;
        }

        void BuildBlanket(Material orange, Material white)
        {
            Material Cloth(Material src, string n)
            {
                var c = new Material(src) { name = n };
                c.SetFloat("_Smoothness", 0.12f); c.SetFloat("_Cull", 0f);   // fabric, visible from both sides
                return c;
            }
            blanketMesh = GridMesh("Blanket", BX, BZ); sheetMesh = GridMesh("Sheet fold", BX, 3);
            foreach (var (mesh, mat, n) in new[] { (blanketMesh, Cloth(orange, "Blanket"), "Blanket"), (sheetMesh, Cloth(white, "Sheet"), "Sheet fold") })
            {
                var g = new GameObject(n); g.transform.SetParent(transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            }
            blH = new float[BX * BZ]; blTarget = new float[BX * BZ];
            BlanketTarget(null);
            System.Array.Copy(blTarget, blH, blH.Length);
            RebuildBlanket();
        }

        public static Vector3 SpawnPoint => Origin + new Vector3(0f, 0.15f, -3.0f);
        // airlock + suit dock choreography points (feet on the floor, y = 0)
        public static Vector3 VestibulePoint => Origin + new Vector3(0f, 0f, -6.3f);     // inside the airlock chamber, behind the inner door
        public static Vector3 InsideDoorPoint => Origin + new Vector3(0f, 0f, -3.4f);    // just inside the room, in front of the door
        public static Vector3 DoorwayPoint => Origin + new Vector3(0f, 0f, -4.45f);      // standing in the doorway (fade point to / from the surface airlock)
        public static Vector3 DockStand => Origin + new Vector3(-3.9f, 0f, -3.05f);      // in front of the open suit alcove
        public static float DockYaw => 180f;                                              // facing the alcove (-Z)
        public static Vector3 SuitHang => Origin + new Vector3(-3.9f, 0f, -4.12f);       // where the empty suit stands in the alcove
        public static Vector3 HelmetShelf => Origin + new Vector3(-3.9f, 1.98f, -4.18f); // helmet pivot on the alcove shelf
        public SlidingDoor AirlockDoor { get; private set; }
        static readonly float[] PortholeZ = { 3.5f, 2.6f };   // same places as the HAB's windows outside
        const float PortholeY = 1.65f;
        const float VestibuleZ = -D / 2 - 0.3f - 1.4f;

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
            // south wall with the airlock doorway (x -1..1, y 0..2.5) cut out
            float sz = -D / 2 - 0.15f, side = (W + 0.6f) / 2 - 1.0f;
            Box("Wall S left", new Vector3(-1.0f - side / 2, H / 2, sz), new Vector3(side, H, 0.3f), wall);
            Box("Wall S right", new Vector3(1.0f + side / 2, H / 2, sz), new Vector3(side, H, 0.3f), wall);
            Box("Wall S over door", new Vector3(0, (2.5f + H) / 2, sz), new Vector3(2.0f, H - 2.5f, 0.3f), wall);
            Box("Wall N", new Vector3(0, H / 2, D / 2 + 0.15f), new Vector3(W + 0.6f, H, 0.3f), wall);
            Box("Wall W", new Vector3(-W / 2 - 0.15f, H / 2, 0), new Vector3(0.3f, H, D), wall);
            // east wall with a big window (z -1.8..2.2, y 0.9..2.5)
            float ex = W / 2 + 0.15f;
            Box("Wall E", new Vector3(ex, H / 2, 0), new Vector3(0.3f, H, D), wall);
            // the opening shows the real lunar surface outside the HAB (portal camera, see HabitatWindow)
            // two round portholes, matching the HAB's two windows outside (+X side, z = +3.5 and +2.6, 2.55 m above the regolith;
            // cabin floor ~0.9 m up, so 1.65 m here). Dark frame ring like the outside, glass = live portal view from that window.
            var frame = Mat(new Color(0.12f, 0.13f, 0.16f), 0.4f, 0.4f);
            foreach (float wz in PortholeZ)
            {
                var f = Prim(PrimitiveType.Cylinder, "Porthole frame", new Vector3(W / 2 - 0.02f, PortholeY, wz), new Vector3(0.6f, 0.03f, 0.6f), frame, false);
                f.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                var r = Prim(PrimitiveType.Cylinder, "Porthole rim", new Vector3(W / 2 - 0.035f, PortholeY, wz), new Vector3(0.5f, 0.012f, 0.5f), steel, false);
                r.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                HabitatWindow.Create(transform, Origin + new Vector3(W / 2 - 0.05f, PortholeY, wz), 0.44f, 0.44f, m.World, () => m.Model != null && m.Model.Inside,
                                     new Material(sh), 2.55f, wz, round: true);
            }

            // wall ribs + dark skirting for depth
            for (int i = -1; i <= 1; i++)
            {
                Box("Rib S", new Vector3(i * 4f, H / 2, -D / 2 + 0.05f), new Vector3(0.14f, H, 0.12f), wallDark, false);
                Box("Rib N", new Vector3(i * 4f + 2f, H / 2, D / 2 - 0.05f), new Vector3(0.14f, H, 0.12f), wallDark, false);
            }
            Box("Skirt S", new Vector3(0, 0.1f, -D / 2 + 0.03f), new Vector3(W, 0.2f, 0.06f), wallDark, false);
            Box("Skirt N", new Vector3(0, 0.1f, D / 2 - 0.03f), new Vector3(W, 0.2f, 0.06f), wallDark, false);
            Box("Skirt W", new Vector3(-W / 2 + 0.03f, 0.1f, 0), new Vector3(0.06f, 0.2f, D), wallDark, false);

            // NASA module feel: angled panels where walls meet the ceiling (pressure-vessel cross-section),
            // ISS-style overhead handrails, and soft cargo transfer bags (CTBs) stowed by the south wall
            foreach (var (pos, size, rotX, rotZ) in new[] {
                (new Vector3(0, H - 0.25f, D / 2 - 0.25f), new Vector3(W, 0.08f, 0.75f), 45f, 0f),
                (new Vector3(0, H - 0.25f, -D / 2 + 0.25f), new Vector3(W, 0.08f, 0.75f), -45f, 0f),
                (new Vector3(-W / 2 + 0.25f, H - 0.25f, 0), new Vector3(0.75f, 0.08f, D), 0f, 45f),
                (new Vector3(W / 2 - 0.25f, H - 0.25f, 0), new Vector3(0.75f, 0.08f, D), 0f, -45f) })
            {
                var c = Box("Ceiling chamfer", pos, size, wall, false, 2);
                c.transform.localRotation = Quaternion.Euler(rotX, 0f, rotZ);
            }
            var rail = Mat(new Color(0.92f, 0.66f, 0.12f), 0.45f, 0.3f);
            foreach (float zr in new[] { D / 2 - 0.14f, -D / 2 + 0.14f })
            {
                Pipe("Handrail", new Vector3(-W / 2 + 0.6f, 2.62f, zr), new Vector3(W / 2 - 0.6f, 2.62f, zr), 0.022f, rail);
                for (float xr = -W / 2 + 0.8f; xr < W / 2 - 0.6f; xr += 1.6f)
                    Pipe("Handrail standoff", new Vector3(xr, 2.62f, zr), new Vector3(xr, 2.62f, zr + Mathf.Sign(zr) * 0.14f), 0.016f, rail);
            }
            var ctb = Mat(new Color(0.83f, 0.79f, 0.67f), 0.15f, 0f);
            var strap = Mat(new Color(0.20f, 0.30f, 0.55f), 0.2f, 0f);
            for (int i = 0; i < 5; i++)
            {
                var p = new Vector3(2.3f + (i % 3) * 0.55f, 0.2f + (i / 3) * 0.4f, -D / 2 + 0.38f);
                Box("Cargo transfer bag", p, new Vector3(0.5f, 0.38f, 0.42f), ctb, i < 3);
                Box("CTB strap", p + new Vector3(0, 0.0f, 0.215f), new Vector3(0.06f, 0.39f, 0.01f), strap, false);
                Box("CTB label", p + new Vector3(0.12f, 0.08f, 0.215f), new Vector3(0.14f, 0.08f, 0.012f), white, false);
            }

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
            // inner airlock door: two leaves sliding into the wall
            AirlockDoor = SlidingDoor.Create(transform, "Airlock inner door", Origin + new Vector3(0, 1.25f, -D / 2 - 0.12f), Vector3.right, Vector3.forward,
                                             2.0f, 2.5f, wallDark, steel, Mat(new Color(1f, 0.15f, 0.1f), 0.2f, 0f, 2.0f), greenGlow);
            // airlock chamber (vestibule) behind it, with the outer hatch at the far end
            // airlock chamber (vestibule) behind it: 2.8 m deep, outer hatch at the far end, vents + air shower for the cycle
            float vz = VestibuleZ, vd = 2.8f;
            Box("Airlock floor", new Vector3(0, -0.15f, vz), new Vector3(2.9f, 0.3f, vd), floor);
            Box("Airlock ceiling", new Vector3(0, 2.85f, vz), new Vector3(2.9f, 0.3f, vd), wall, true, 2);
            Box("Airlock wall W", new Vector3(-1.45f, 1.35f, vz), new Vector3(0.3f, 2.7f, vd), wall);
            Box("Airlock wall E", new Vector3(1.45f, 1.35f, vz), new Vector3(0.3f, 2.7f, vd), wall);
            Box("Airlock outer wall", new Vector3(0, 1.35f, vz - vd / 2 - 0.15f), new Vector3(2.9f, 2.7f, 0.3f), wall);
            Box("Outer hatch", new Vector3(0, 1.1f, vz - vd / 2 + 0.01f), new Vector3(1.1f, 1.9f, 0.06f), wallDark, false);
            Box("Outer hatch wheel", new Vector3(0, 1.15f, vz - vd / 2 + 0.05f), new Vector3(0.35f, 0.35f, 0.04f), steel, false);
            Box("Airlock light", new Vector3(0, 2.68f, vz), new Vector3(0.9f, 0.04f, 0.25f), lamp, false);
            Box("Airlock floor grille", new Vector3(0, 0.012f, vz), new Vector3(1.6f, 0.02f, 2.2f), steel, false);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Box("Air vent", new Vector3(sd * 1.29f, 1.2f, vz), new Vector3(0.03f, 0.5f, 1.2f), steel, false);
                for (int k = 0; k < 5; k++) Box("Vent slat", new Vector3(sd * 1.27f, 1.0f + k * 0.1f, vz), new Vector3(0.02f, 0.02f, 1.15f), wallDark, false);
            }
            Box("Air shower", new Vector3(0, 2.66f, vz + 0.6f), new Vector3(0.7f, 0.05f, 0.7f), steel, false);
            var vl = new GameObject("Airlock lamp").AddComponent<Light>();
            vl.transform.SetParent(transform, false); vl.transform.localPosition = new Vector3(0, 2.4f, vz);
            vl.type = LightType.Point; vl.range = 4.0f; vl.intensity = 2.0f; vl.color = new Color(0.9f, 0.95f, 1f); vl.shadows = LightShadows.None;
            Box("Hazard strip", new Vector3(0, 0.012f, -D / 2 + 0.55f), new Vector3(2.4f, 0.02f, 0.35f), amberGlow, false);
            Station("Airlock door", new Vector3(0, 0, -3.5f), 2.2f, new Color(0.3f, 1f, 0.5f),
                () => m.Player != null && !m.Player.SuitedIndoors ? "Airlock: put on your EVA suit first (suit alcove, left)"
                    : m.Model.EvaLeft < 0.6f ? $"Airlock: suit still refilling ({m.Model.EvaLeft:0.0} h) - wait or rest" : $"Go outside (EVA suit ready: {m.Model.EvaLeft:0.0} h)",
                m.ExitHabitat);

            // ── suit dock (south wall, west) ─────────────────────────────────────────
            // open suit alcove (the suit is stored here while you are inside; helmet on the shelf) + one closed locker
            {
                float x = -3.9f, zc = -D / 2 + 0.4f;
                Box("Suit alcove back", new Vector3(x, 1.15f, -D / 2 + 0.02f), new Vector3(1.14f, 2.3f, 0.04f), wallDark, false);
                Box("Suit alcove side L", new Vector3(x - 0.57f, 1.15f, zc), new Vector3(0.04f, 2.3f, 0.8f), white, true);
                Box("Suit alcove side R", new Vector3(x + 0.57f, 1.15f, zc), new Vector3(0.04f, 2.3f, 0.8f), white, true);
                Box("Suit alcove roof", new Vector3(x, 2.32f, zc), new Vector3(1.18f, 0.04f, 0.8f), white, false);
                Box("Helmet shelf", new Vector3(x, 1.96f, zc - 0.1f), new Vector3(1.1f, 0.03f, 0.55f), steel, false);
                Box("Alcove stripe", new Vector3(x, 0.02f, zc + 0.38f), new Vector3(1.1f, 0.02f, 0.06f), orange, false);
                Box("Alcove light", new Vector3(x, 2.28f, zc), new Vector3(0.7f, 0.03f, 0.12f), greenGlow, false);
            }
            {
                float x = -2.75f;
                Box("Suit locker", new Vector3(x, 1.1f, -D / 2 + 0.3f), new Vector3(1.0f, 2.2f, 0.5f), white, true);
                Box("Locker stripe", new Vector3(x, 1.5f, -D / 2 + 0.56f), new Vector3(0.9f, 0.1f, 0.02f), orange, false);
                Box("Locker light", new Vector3(x, 2.0f, -D / 2 + 0.57f), new Vector3(0.3f, 0.08f, 0.02f), greenGlow, false);
            }
            Station("EVA suit", new Vector3(-3.9f, 0, -3.2f), 1.25f, new Color(1f, 0.55f, 0.15f),
                () => m.Player != null && m.Player.SuitedIndoors ? "Take off EVA suit (stow it in the alcove)" : "Put on EVA suit (needed to go outside)",
                m.SuitUp);
            Station("Suit dock", new Vector3(-2.75f, 0, -3.4f), 1.1f, new Color(1f, 0.7f, 0.3f),
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
            BuildBlanket(orange, Mat(new Color(0.95f, 0.95f, 0.93f), 0.2f, 0f));
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
            Box("Window seat", new Vector3(5.45f, 0.25f, 3.05f), new Vector3(0.7f, 0.5f, 2.0f), wallDark, false);
            Box("Seat cushion", new Vector3(5.2f, 0.56f, 0.2f), new Vector3(0.7f, 0.12f, 2.9f), Mat(new Color(0.30f, 0.40f, 0.55f), 0.2f, 0f), false);
            Station("Window", new Vector3(4.6f, 0, 3.05f), 1.8f, new Color(0.7f, 0.85f, 1f), () => "Look out the window", m.WindowLook);

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
                MoonWorld.Spawn("PickupCrate", Origin + new Vector3(5.2f - (i % 2) * 0.9f, 0.4f + (i / 2) * 0.8f, -0.6f - (i % 2) * 0.2f), Quaternion.Euler(0, i * 15f, 0), 0.8f, transform, "Crate");
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
