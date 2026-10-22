using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// The walkable habitat interior, built from primitives at runtime and placed far from the surface (so it never clashes
    /// with the moon terrain). Same cross shape as the habitat outside: a command hub (8 x 8 m) with a 2 m doorway on each
    /// side into four rooms (4.6 m wide, 10 m long, 3.4 m high): airlock room +X, crew quarters -X, galley +Z, lab -Z.
    /// When the player goes through the airlock the astronaut is teleported here and the camera follows.
    /// Stations (press E): airlock door, suit alcove + dock, EVA bag, bunk, power and life support, comms, plan board,
    /// stores log, sample lab, health scanner, radiation shelter, galley, water, wash basin, window.
    /// </summary>
    public class HabitatInterior : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(2600f, 0f, 2600f);

        // room geometry (local coords, floor at y = 0): arm half-width, hub half-size, arm end, ceiling height
        public const float HW = 2.3f, Hub = 4f, End = 14f, H = 3.4f, W = 2f * End + 1f, D = W;

        // Station groups keep the build code written for the original 12 x 9 m room and are placed in the cross as a whole:
        // group -> (where it goes, extra yaw, the old-room point that lands there). L() maps an old-room point.
        static readonly Dictionary<string, (Vector3 at, float yaw, Vector3 old)> Layout = new Dictionary<string, (Vector3, float, Vector3)>
        {
            ["airlock"] = (new Vector3(End + 0.12f, 0f, 0f), -90f, new Vector3(0f, 0f, -4.62f)),   // inner airlock door, end of the airlock room
            ["suit"] = (new Vector3(10.6f, 0f, -0.85f), 0f, new Vector3(-3.9f, 0f, -3.05f)),     // suit alcove + locker, airlock room south wall
            ["bag"] = (new Vector3(7.5f, 0f, 2.07f), 180f, new Vector3(2.85f, 0f, -4.12f)),      // cargo bags + EVA bag, airlock room north wall
            ["tanks"] = (new Vector3(11.0f, 0f, 2.3f), 180f, new Vector3(-5.03f, 0f, -4.4f)),    // O2 bottles, airlock room north wall
            ["comms"] = (new Vector3(-Hub, 0f, -2.5f), 0f, new Vector3(-6.0f, 0f, -2.3f)),       // Mission Control desk, hub west wall
            ["stores"] = (new Vector3(-Hub, 0f, 2.5f), 0f, new Vector3(-6.0f, 0f, -0.2f)),       // stores log board, hub west wall
            ["plan"] = (new Vector3(2.5f, 0f, Hub), 0f, new Vector3(1.45f, 0f, 4.5f)),           // plan board, hub north wall
            ["bunk"] = (new Vector3(-9.0f, 0f, -1.73f), -90f, new Vector3(-5.3f, 0f, 1.9f)),     // bunk, crew quarters south wall
            ["bunk2"] = (new Vector3(-6.0f, 0f, -1.73f), -90f, new Vector3(-5.3f, 0f, 1.9f)),    // second bunk
            ["crates"] = (new Vector3(-6.0f, 0f, 1.55f), 0f, new Vector3(4.75f, 0f, -0.7f)),     // stowage, crew quarters north wall
            ["wash"] = (new Vector3(-12.5f, 0f, 1.77f), 90f, new Vector3(-5.45f, 0f, 3.95f)),    // wash basin (HabitatAmenities), crew quarters north wall
            ["galley"] = (new Vector3(HW, 0f, 8.0f), 0f, new Vector3(6.0f, 0f, -2.1f)),          // galley counter (HabitatAmenities), galley east wall
            ["table"] = (new Vector3(0f, 0f, 10.5f), 90f, new Vector3(0.3f, 0f, -0.4f)),         // crew table + stools + moon globe
            ["planter"] = (new Vector3(0f, 0f, End), 180f, new Vector3(2.0f, 0f, -4.5f)),        // greenhouse shelf, galley end wall
            ["lab"] = (new Vector3(-HW, 0f, -6.5f), -90f, new Vector3(3.4f, 0f, 4.5f)),          // lab bench, lab west wall
            ["scanner"] = (new Vector3(1.93f, 0f, -10.0f), 0f, new Vector3(5.4f, 0f, -3.5f)),    // health scanner, lab east wall
        };
        public static Vector3 L(string group, Vector3 old) { var p = Layout[group]; return p.at + Quaternion.Euler(0f, p.yaw, 0f) * (old - p.old); }
        /// <summary>Yaw of the airlock door frame: the old room's "door is at -Z" directions turned to the door at +X.</summary>
        public static float AirlockYaw => Layout["airlock"].yaw;

        // bunk pose for the sleeping crew member (head on the pillow)
        public static Vector3 BedPosition => Origin + L("bunk", new Vector3(-5.3f, 0.60f, 1.95f));
        public static float BedYaw => 180f + Layout["bunk"].yaw;
        public static Vector3 BedExit => Origin + L("bunk", new Vector3(-4.3f, 0.15f, 1.9f));
        Transform bunkFrame;   // old-room coordinates of the bunk group (the blanket grid lives in them)
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
                        Vector3 p = bunkFrame.InverseTransformPoint(M.MultiplyPoint3x4(v));
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
                var g = new GameObject(n); g.transform.SetParent(bunkFrame, false);
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            }
            blH = new float[BX * BZ]; blTarget = new float[BX * BZ];
            BlanketTarget(null);
            System.Array.Copy(blTarget, blH, blH.Length);
            RebuildBlanket();
        }

        public static Vector3 SpawnPoint => Origin + new Vector3(5.5f, 0.15f, 0f);   // airlock room, by the hub doorway
        // airlock + suit dock choreography points (feet on the floor, y = 0)
        public static Vector3 VestibulePoint => Origin + L("airlock", new Vector3(0f, 0f, -6.3f));     // behind the inner door
        public static Vector3 InsideDoorPoint => Origin + L("airlock", new Vector3(0f, 0f, -3.4f));    // just inside the room, in front of the door
        public static Vector3 DoorwayPoint => Origin + L("airlock", new Vector3(0f, 0f, -4.45f));      // standing in the doorway (fade point to / from the surface airlock)
        public static Vector3 DockStand => Origin + L("suit", new Vector3(-3.9f, 0f, -3.05f));        // in front of the open suit alcove
        public static float DockYaw => 180f + Layout["suit"].yaw;                                       // facing the alcove
        public static Vector3 SuitHang => Origin + L("suit", new Vector3(-3.9f, 0f, -4.12f));         // where the empty suit stands in the alcove
        public static Vector3 HelmetShelf => Origin + L("suit", new Vector3(-3.9f, 1.98f, -4.18f));   // helmet pivot on the alcove shelf
        public SlidingDoor AirlockDoor { get; private set; }
        public Interactable ShelterSpot { get; private set; }

        const float OldD = 9f;                                // the old room's depth, still used by the moved station groups
        Transform grp;                                        // current station group (its local coords are the old room's)

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
            g.transform.SetParent(parent != null ? parent : grp != null ? grp : transform, false);
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
            g.name = name; g.transform.SetParent(grp != null ? grp : transform, false);
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
            var it = Interactable.Create(name, grp != null ? grp.TransformPoint(local) : Origin + local, radius, transform);
            it.Indoor = true; it.MarkerColor = marker; it.Prompt = prompt; it.Use = use;
            return it;
        }

        /// <summary>Start a station group: everything built until the next Group() call uses the old room's coordinates.</summary>
        public Transform Group(string name)
        {
            var p = Layout[name];
            var a = new GameObject(name).transform; a.SetParent(transform, false);
            a.localPosition = p.at; a.localRotation = Quaternion.Euler(0f, p.yaw, 0f);
            grp = new GameObject(name + " (old room frame)").transform; grp.SetParent(a, false); grp.localPosition = -p.old;
            return grp;
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

            // ── shell: floor + ceiling over the whole cross, four rooms, the hub ─────
            Box("Floor E-W", new Vector3(0, -0.15f, 0), new Vector3(2f * End + 0.6f, 0.3f, 2f * HW + 0.6f), floor);
            Box("Floor N-S", new Vector3(0, -0.15f, 0), new Vector3(2f * HW + 0.6f, 0.3f, 2f * End + 0.6f), floor);
            Box("Floor hub", new Vector3(0, -0.15f, 0), new Vector3(2f * Hub + 0.6f, 0.3f, 2f * Hub + 0.6f), floor);
            Box("Ceiling E-W", new Vector3(0, H + 0.15f, 0), new Vector3(2f * End + 0.6f, 0.3f, 2f * HW + 0.6f), wall, true, 2);   // layer 2: the camera ignores it
            Box("Ceiling N-S", new Vector3(0, H + 0.15f, 0), new Vector3(2f * HW + 0.6f, 0.3f, 2f * End + 0.6f), wall, true, 2);
            Box("Ceiling hub", new Vector3(0, H + 0.15f, 0), new Vector3(2f * Hub + 0.6f, 0.3f, 2f * Hub + 0.6f), wall, true, 2);
            var rail = Mat(new Color(0.92f, 0.66f, 0.12f), 0.45f, 0.3f);
            Arm(Quaternion.Euler(0f, 90f, 0f), wall, wallDark, lamp, rail, true);    // airlock room
            Arm(Quaternion.Euler(0f, -90f, 0f), wall, wallDark, lamp, rail, false);  // crew quarters
            Arm(Quaternion.identity, wall, wallDark, lamp, rail, false);              // galley
            Arm(Quaternion.Euler(0f, 180f, 0f), wall, wallDark, lamp, rail, false);  // lab
            // hub: walls with a 2 m doorway on each side (framed, green strips), angled corner panels
            for (int i = 0; i < 4; i++)
            {
                var q = Quaternion.Euler(0f, i * 90f, 0f);
                foreach (float sd in new[] { -1f, 1f })
                {
                    Rot(Box("Hub wall", q * new Vector3(sd * (1f + (Hub - 1f) / 2f), H / 2, Hub + 0.15f), new Vector3(Hub - 1f, H, 0.3f), wall), q);
                    Rot(Box("Doorway post", q * new Vector3(sd * 1.1f, 1.3f, Hub), new Vector3(0.2f, 2.6f, 0.36f), steel, false), q);
                    Rot(Box("Door strip", q * new Vector3(sd * 1.0f, 1.3f, Hub - 0.19f), new Vector3(0.04f, 2.3f, 0.02f), greenGlow, false), q);
                }
                Rot(Box("Doorway lintel", q * new Vector3(0f, (2.6f + H) / 2, Hub + 0.15f), new Vector3(2f, H - 2.6f, 0.3f), wall), q);
                Rot(Box("Doorway sill", q * new Vector3(0f, 0.012f, Hub), new Vector3(2f, 0.02f, 0.3f), amberGlow, false), q);
                var qd = Quaternion.Euler(0f, 45f + i * 90f, 0f);
                Rot(Box("Hub corner panel", qd * new Vector3(0f, H / 2, Hub * 1.414f - 0.75f), new Vector3(2.1f, H, 0.1f), wallDark, false), qd);
            }
            // command console in the middle of the hub: round desk, mast, four screens facing out
            Prim(PrimitiveType.Cylinder, "Command console", new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.45f, 1.8f), steel, true);
            Prim(PrimitiveType.Cylinder, "Console top", new Vector3(0, 0.915f, 0), new Vector3(1.9f, 0.015f, 1.9f), wallDark, false);
            Prim(PrimitiveType.Cylinder, "Console mast", new Vector3(0, 1.8f, 0), new Vector3(0.3f, 0.9f, 0.3f), steel, false);
            for (int i = 0; i < 4; i++)
            {
                var q = Quaternion.Euler(0f, i * 90f, 0f);
                Rot(Screen("Console screen", q * new Vector3(0f, 1.5f, 0.19f), new Vector3(0.75f, 0.5f, 0.04f), i % 2 == 0 ? new Color(0.3f, 1f, 0.5f) : new Color(0.2f, 0.85f, 1f), i % 2), q);
            }
            Led(new Vector3(0.6f, 0.95f, 0.6f), new Color(1f, 0.15f, 0.1f), 0.8f, 0.0f);
            Led(new Vector3(-0.6f, 0.95f, 0.6f), new Color(0.25f, 1f, 0.4f), 1.4f, 0.3f);
            Led(new Vector3(0.6f, 0.95f, -0.6f), new Color(1f, 0.7f, 0.1f), 0.5f, 0.6f);
            Box("Ceiling light", new Vector3(0, H - 0.04f, 0), new Vector3(2.4f, 0.05f, 2.4f), lamp, false);
            var hl = new GameObject("Light hub").AddComponent<Light>();
            hl.transform.SetParent(transform, false); hl.transform.localPosition = new Vector3(0, H - 0.5f, 0);
            hl.type = LightType.Point; hl.range = 9f; hl.intensity = 3.8f; hl.color = new Color(1f, 0.96f, 0.90f); hl.shadows = LightShadows.None;
            Station("Power & life support", new Vector3(0f, 0, -1.6f), 1.3f, new Color(1f, 0.85f, 0.3f),
                () => $"Open power & life support (battery {m.Model.Battery:0}, cabin O2 {m.Model.CabinO2:0.0})", m.ToggleSystems);
            // floor guide strip from the hub to the airlock door
            Box("Guide strip", new Vector3((Hub + End) / 2f, 0.012f, 0f), new Vector3(End - Hub - 1.4f, 0.02f, 0.14f), cyanGlow, false);

            // ── airlock door (end of the airlock room) ───────────────────────────────
            Group("airlock");
            Box("Airlock post L", new Vector3(-1.15f, 1.3f, -OldD / 2 + 0.15f), new Vector3(0.22f, 2.6f, 0.3f), steel, false);
            Box("Airlock post R", new Vector3(1.15f, 1.3f, -OldD / 2 + 0.15f), new Vector3(0.22f, 2.6f, 0.3f), steel, false);
            Box("Airlock lintel", new Vector3(0, 2.7f, -OldD / 2 + 0.15f), new Vector3(2.5f, 0.22f, 0.3f), steel, false);
            // inner airlock door: two leaves sliding into the wall; a dark vestibule behind it
            AirlockDoor = SlidingDoor.Create(transform, "Airlock inner door", grp.TransformPoint(new Vector3(0, 1.25f, -OldD / 2 - 0.12f)), grp.TransformDirection(Vector3.right), grp.TransformDirection(Vector3.forward),
                                             2.0f, 2.5f, wallDark, steel, Mat(new Color(1f, 0.15f, 0.1f), 0.2f, 0f, 2.0f), greenGlow);
            Box("Vestibule back", new Vector3(0, 1.3f, -OldD / 2 - 1.6f), new Vector3(2.4f, 2.6f, 0.1f), wallDark, false);
            Box("Door strip L", new Vector3(-1.04f, 1.3f, -OldD / 2 + 0.3f), new Vector3(0.04f, 2.3f, 0.02f), greenGlow, false);
            Box("Door strip R", new Vector3(1.04f, 1.3f, -OldD / 2 + 0.3f), new Vector3(0.04f, 2.3f, 0.02f), greenGlow, false);
            Box("Hazard strip", new Vector3(0, 0.012f, -OldD / 2 + 0.55f), new Vector3(2.4f, 0.02f, 0.35f), amberGlow, false);
            Station("Airlock door", new Vector3(0, 0, -3.5f), 1.4f, new Color(0.3f, 1f, 0.5f),
                () => m.Player != null && !m.Player.SuitedIndoors ? "Airlock: put on your EVA suit first (suit alcove, this room)"
                    : m.Model.EvaLeft < 0.6f ? $"Airlock: suit still refilling ({m.Model.EvaLeft:0.0} h) - wait or rest" : $"Go outside (EVA suit ready: {m.Model.EvaLeft:0.0} h)",
                m.ExitHabitat);

            // ── suit alcove + suit locker (airlock room, south wall) ─────────────────
            Group("suit");
            {
                float x = -3.9f, zc = -OldD / 2 + 0.4f;
                Box("Suit alcove back", new Vector3(x, 1.15f, -OldD / 2 + 0.02f), new Vector3(1.14f, 2.3f, 0.04f), wallDark, false);
                Box("Suit alcove side L", new Vector3(x - 0.57f, 1.15f, zc), new Vector3(0.04f, 2.3f, 0.8f), white, true);
                Box("Suit alcove side R", new Vector3(x + 0.57f, 1.15f, zc), new Vector3(0.04f, 2.3f, 0.8f), white, true);
                Box("Suit alcove roof", new Vector3(x, 2.32f, zc), new Vector3(1.18f, 0.04f, 0.8f), white, false);
                Box("Helmet shelf", new Vector3(x, 1.96f, zc - 0.1f), new Vector3(1.1f, 0.03f, 0.55f), steel, false);
                Box("Alcove stripe", new Vector3(x, 0.02f, zc + 0.38f), new Vector3(1.1f, 0.02f, 0.06f), orange, false);
                Box("Alcove light", new Vector3(x, 2.28f, zc), new Vector3(0.7f, 0.03f, 0.12f), greenGlow, false);
            }
            {
                float x = -2.75f;
                Box("Suit locker", new Vector3(x, 1.1f, -OldD / 2 + 0.3f), new Vector3(1.0f, 2.2f, 0.5f), white, true);
                Box("Locker stripe", new Vector3(x, 1.5f, -OldD / 2 + 0.56f), new Vector3(0.9f, 0.1f, 0.02f), orange, false);
                Box("Locker light", new Vector3(x, 2.0f, -OldD / 2 + 0.57f), new Vector3(0.3f, 0.08f, 0.02f), greenGlow, false);
            }
            Station("EVA suit", new Vector3(-3.9f, 0, -3.2f), 1.25f, new Color(1f, 0.55f, 0.15f),
                () => m.Player != null && m.Player.SuitedIndoors ? "Take off EVA suit (stow it in the alcove)" : "Put on EVA suit (needed to go outside)",
                m.SuitUp);
            Station("Suit dock", new Vector3(-2.75f, 0, -3.4f), 1.0f, new Color(1f, 0.7f, 0.3f),
                () => m.Model.Spares > 0 && m.Model.SuitIntegrity < 100f ? $"Repair suit with a spare kit (integrity {m.Model.SuitIntegrity:0}%, {m.Model.Spares} spare)"
                                                                         : $"Suit status: {m.Model.EvaLeft:0.0} h EVA, integrity {m.Model.SuitIntegrity:0}%",
                m.SuitDockUse);

            // ── cargo transfer bags + EVA bag, O2 bottles (airlock room, north wall) ──
            Group("bag");
            var ctb = Mat(new Color(0.83f, 0.79f, 0.67f), 0.15f, 0f);
            var strap = Mat(new Color(0.20f, 0.30f, 0.55f), 0.2f, 0f);
            for (int i = 0; i < 5; i++)
            {
                var p = new Vector3(2.3f + (i % 3) * 0.55f, 0.2f + (i / 3) * 0.4f, -OldD / 2 + 0.38f);
                Box("Cargo transfer bag", p, new Vector3(0.5f, 0.38f, 0.42f), ctb, i < 3);
                Box("CTB strap", p + new Vector3(0, 0.0f, 0.215f), new Vector3(0.06f, 0.39f, 0.01f), strap, false);
                Box("CTB label", p + new Vector3(0.12f, 0.08f, 0.215f), new Vector3(0.14f, 0.08f, 0.012f), white, false);
            }
            Station("EVA bag", new Vector3(2.85f, 0, -OldD / 2 + 1.0f), 1.2f, new Color(0.85f, 0.8f, 0.65f),
                () => m.Model.Inv.HasBag ? "Leave the EVA bag here (back to 4 pocket slots)" : "Take the EVA bag (20 slots, 20 kg)", m.BagUse);
            Group("tanks");
            for (int i = 0; i < 3; i++)
                Cyl("O2 tank", new Vector3(-5.45f + i * 0.42f, 0.6f, -4.1f), new Vector3(0.3f, 0.6f, 0.3f), Mat(new Color(0.85f, 0.88f, 0.9f), 0.5f, 0.4f), true);

            // ── hub walls: Mission Control desk, stores log, plan board ──────────────
            Group("comms");
            Box("Comms desk", new Vector3(-5.45f, 0.45f, -2.3f), new Vector3(0.9f, 0.9f, 2.0f), steel);
            Screen("Comms screen", new Vector3(-5.9f, 1.45f, -2.3f), new Vector3(0.08f, 0.6f, 1.2f), new Color(0.2f, 0.85f, 1f), 0);
            Screen("Comms screen 2", new Vector3(-5.9f, 1.45f, -1.55f), new Vector3(0.08f, 0.45f, 0.5f), new Color(1f, 0.7f, 0.2f), 1);
            Cyl("Antenna mast", new Vector3(-5.45f, 1.1f, -3.0f), new Vector3(0.05f, 0.22f, 0.05f), steel);
            Prim(PrimitiveType.Sphere, "Dish", new Vector3(-5.45f, 1.35f, -3.0f), new Vector3(0.35f, 0.12f, 0.35f), white, false);
            Station("Mission Control link", new Vector3(-4.6f, 0, -2.3f), 1.2f, new Color(0.4f, 0.8f, 1f),
                () => (m.Model.Load("COM")?.Powered ?? false) ? "Open the comms workstation" : "Comms link offline (COM has no power)", m.CommsCall);
            Group("stores");
            Screen("Stores log board", new Vector3(-5.93f, 1.5f, -0.2f), new Vector3(0.05f, 0.9f, 1.4f), new Color(1f, 0.7f, 0.2f), 1);
            Box("Board frame", new Vector3(-5.96f, 1.5f, -0.2f), new Vector3(0.04f, 1.0f, 1.5f), wallDark, false);
            Station("Stores log", new Vector3(-5.0f, 0, -0.2f), 1.1f, new Color(1f, 0.75f, 0.3f), () => "Read the stores log (what we have in stock)", m.ToggleStores);
            Group("plan");
            Screen("Plan board", new Vector3(1.45f, 1.4f, 4.3f), new Vector3(0.9f, 0.6f, 0.05f), new Color(0.3f, 1f, 0.5f), 0);
            Station("Plan board", new Vector3(1.45f, 0, 3.5f), 0.9f, new Color(0.3f, 1f, 0.5f), () => "Open the mission plan and task list (or press J anywhere)", m.TogglePlan);

            // ── crew quarters: two bunks (south wall), stowage (north wall) ──────────
            bunkFrame = Group("bunk");
            var mattress = Mat(new Color(0.30f, 0.40f, 0.55f), 0.2f, 0f);
            Box("Bunk frame", new Vector3(-5.3f, 0.2f, 1.9f), new Vector3(1.1f, 0.4f, 2.1f), wallDark);
            Box("Mattress", new Vector3(-5.3f, 0.5f, 1.9f), new Vector3(1.0f, 0.2f, 2.0f), mattress, false);
            Box("Pillow", new Vector3(-5.3f, 0.66f, 2.7f), new Vector3(0.7f, 0.13f, 0.4f), white, false);
            BuildBlanket(orange, Mat(new Color(0.95f, 0.95f, 0.93f), 0.2f, 0f));
            Box("Bunk locker", new Vector3(-5.78f, 2.1f, 1.9f), new Vector3(0.4f, 0.7f, 2.0f), steel, false);
            Station("Bunk", new Vector3(-4.3f, 0, 1.9f), 1.5f, new Color(0.6f, 0.7f, 1f),
                () => m.Model.Resting ? "Wake up" : $"Sleep (fatigue {m.Model.Fatigue:0}%, time runs 4x)", m.ToggleRest);
            Group("bunk2");
            Box("Bunk frame", new Vector3(-5.3f, 0.2f, 1.9f), new Vector3(1.1f, 0.4f, 2.1f), wallDark);
            Box("Mattress", new Vector3(-5.3f, 0.5f, 1.9f), new Vector3(1.0f, 0.2f, 2.0f), mattress, false);
            Box("Pillow", new Vector3(-5.3f, 0.66f, 2.7f), new Vector3(0.7f, 0.13f, 0.4f), white, false);
            Box("Blanket (folded)", new Vector3(-5.3f, 0.64f, 1.2f), new Vector3(0.95f, 0.08f, 0.6f), orange, false);
            Box("Bunk locker", new Vector3(-5.78f, 2.1f, 1.9f), new Vector3(0.4f, 0.7f, 2.0f), steel, false);
            Group("crates");
            for (int i = 0; i < 3; i++)
                MoonWorld.Spawn("PickupCrate", grp.TransformPoint(new Vector3(5.2f - (i % 2) * 0.9f, 0.4f + (i / 2) * 0.8f, -0.6f - (i % 2) * 0.2f)), grp.rotation * Quaternion.Euler(0, i * 15f, 0), 0.8f, grp, "Crate");

            // ── lab: bench (west wall), health scanner (east wall), shielded shelter at the end ──
            Group("lab");
            Box("Lab bench", new Vector3(3.4f, 0.48f, 3.95f), new Vector3(2.6f, 0.95f, 0.8f), white);
            Box("Lab cabinet", new Vector3(3.4f, 2.1f, 4.25f), new Vector3(2.6f, 0.7f, 0.35f), steel, false);
            Box("Microscope base", new Vector3(2.8f, 1.0f, 3.9f), new Vector3(0.3f, 0.08f, 0.3f), wallDark, false);
            Cyl("Microscope tube", new Vector3(2.8f, 1.2f, 3.9f), new Vector3(0.1f, 0.18f, 0.1f), steel);
            Box("Sample tray", new Vector3(3.6f, 0.98f, 3.85f), new Vector3(0.6f, 0.04f, 0.4f), wallDark, false);
            for (int i = 0; i < 3; i++) Prim(PrimitiveType.Sphere, "Sample", new Vector3(3.4f + i * 0.17f, 1.04f, 3.85f), Vector3.one * 0.09f, Mat(new Color(0.55f, 0.50f, 0.45f), 0.1f, 0f), false);
            Screen("Lab screen", new Vector3(4.2f, 1.4f, 4.15f), new Vector3(0.5f, 0.4f, 0.04f), new Color(0.2f, 0.85f, 1f), 1);
            Station("Sample lab", new Vector3(3.4f, 0, 3.0f), 1.6f, new Color(0.3f, 0.9f, 1f),
                () => m.Model.SamplesDelivered > m.SamplesAnalyzed ? $"Analyse a regolith sample ({m.Model.SamplesDelivered - m.SamplesAnalyzed} waiting)"
                                                                  : "Sample lab (no samples waiting - collect some outside)", m.AnalyzeSample);
            Group("scanner");
            Box("Scanner body", new Vector3(5.4f, 0.85f, -3.5f), new Vector3(0.7f, 1.7f, 0.9f), white);
            Screen("Scanner screen", new Vector3(5.03f, 1.35f, -3.5f), new Vector3(0.05f, 0.4f, 0.6f), new Color(0.3f, 1f, 0.5f), 0);
            Box("Scanner stripe", new Vector3(5.03f, 0.6f, -3.5f), new Vector3(0.05f, 0.1f, 0.7f), orange, false);
            Led(new Vector3(5.03f, 1.75f, -3.5f), new Color(1f, 0.15f, 0.1f), 0.7f, 0.2f);
            Station("Health scan", new Vector3(4.3f, 0, -3.5f), 1.4f, new Color(0.4f, 1f, 0.6f),
                () => $"Health scan (health {m.Model.Health:0}%, fatigue {m.Model.Fatigue:0}%, dose {m.Model.Dose:0.0})", m.HealthScan);
            grp = null;
            var water = Mat(new Color(0.25f, 0.45f, 0.75f), 0.3f, 0f);
            foreach (float sd in new[] { -1f, 1f })   // water-wall bags around the shielded end of the lab
                Box("Water wall", new Vector3(sd * (HW - 0.3f), 1.15f, -End + 1.4f), new Vector3(0.5f, 2.3f, 2.6f), water);
            Box("Water wall", new Vector3(0f, 2.6f, -End + 0.4f), new Vector3(2f * HW - 1.2f, 0.5f, 0.6f), water, false);
            ShelterSpot = Station("Radiation shelter", new Vector3(0f, 0, -End + 1.2f), 1.5f, new Color(1f, 0.35f, 0.3f),
                () => m.Model.Tasks.Prompt(Spot.Shelter) ?? "Radiation shelter (water-walled end of the lab)", m.ShelterUse);

            // ── galley: table + stools + moon globe, greenhouse shelf and porthole at the end ──
            Group("table");
            Box("Table top", new Vector3(0.3f, 0.78f, -0.4f), new Vector3(2.2f, 0.06f, 1.1f), white);
            for (int ix = -1; ix <= 1; ix += 2)
            for (int iz = -1; iz <= 1; iz += 2)
                Cyl("Table leg", new Vector3(0.3f + ix * 0.95f, 0.38f, -0.4f + iz * 0.42f), new Vector3(0.07f, 0.38f, 0.07f), steel);
            Cyl("Stool A", new Vector3(-0.5f, 0.25f, 0.6f), new Vector3(0.42f, 0.25f, 0.42f), orange);
            Cyl("Stool B", new Vector3(1.1f, 0.25f, 0.6f), new Vector3(0.42f, 0.25f, 0.42f), orange);
            MoonWorld.Spawn("PickupJar", grp.TransformPoint(new Vector3(0.0f, 0.81f, -0.4f)), Quaternion.identity, 0.5f, grp, "Jar");
            Box("Tablet", new Vector3(0.9f, 0.82f, -0.35f), new Vector3(0.28f, 0.015f, 0.2f), cyanGlow, false);
            var globe = Prim(PrimitiveType.Sphere, "Moon globe", new Vector3(0.3f, 1.12f, -0.4f), Vector3.one * 0.38f, null, false);
            SurfaceKit.Grime(out var gA, out var gN);
            globe.GetComponent<Renderer>().sharedMaterial = SurfaceKit.Make(new Color(0.62f, 0.64f, 0.68f), 0.15f, 0f, gA, gN, new Vector2(3f, 2f), 1.2f);
            globe.AddComponent<Spin>().Speed = 14f;
            Prim(PrimitiveType.Cylinder, "Globe base", new Vector3(0.3f, 0.83f, -0.4f), new Vector3(0.22f, 0.02f, 0.22f), cyanGlow, false);
            Group("planter");
            Box("Planter shelf", new Vector3(2.0f, 0.5f, -4.1f), new Vector3(1.6f, 1.0f, 0.5f), steel);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Sphere, "Plant", new Vector3(1.5f + i * 0.33f, 1.1f, -4.1f), new Vector3(0.28f, 0.22f, 0.28f), Mat(new Color(0.25f, 0.65f, 0.25f), 0.2f, 0f), false);
            grp = null;
            var frame = Mat(new Color(0.12f, 0.13f, 0.16f), 0.4f, 0.4f);
            var port = Prim(PrimitiveType.Cylinder, "Porthole frame", new Vector3(-1.5f, 1.6f, End - 0.02f), new Vector3(0.75f, 0.03f, 0.75f), frame, false);
            port.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var glass = Prim(PrimitiveType.Cylinder, "Porthole (Earth view)", new Vector3(-1.5f, 1.6f, End - 0.04f), new Vector3(0.6f, 0.01f, 0.6f), Mat(new Color(0.05f, 0.08f, 0.15f), 0.95f, 0f, 0.4f), false);
            glass.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Station("Window", new Vector3(-1.5f, 0, End - 1.1f), 1.2f, new Color(0.7f, 0.85f, 1f), () => "Look out the window", m.WindowLook);
        }

        static GameObject Rot(GameObject g, Quaternion q) { g.transform.localRotation = q; return g; }

        /// <summary>One room of the cross along the local +Z of q: side walls, end wall (with the airlock doorway if asked),
        /// skirting, ceiling chamfers, ribs, handrails, a light strip and one point light.</summary>
        void Arm(Quaternion q, Material wall, Material wallDark, Material lamp, Material rail, bool endDoor)
        {
            float mid = (Hub + End) / 2f, len = End - Hub;
            GameObject B(string n, Vector3 p, Vector3 sz, Material mt, bool solid = true, int layer = 0) => Rot(Box(n, q * p, sz, mt, solid, layer), q);
            foreach (float sd in new[] { -1f, 1f })
            {
                B("Wall", new Vector3(sd * (HW + 0.15f), H / 2, mid), new Vector3(0.3f, H, len), wall);
                B("Skirt", new Vector3(sd * (HW - 0.03f), 0.1f, mid), new Vector3(0.06f, 0.2f, len), wallDark, false);
                Rot(Box("Ceiling chamfer", q * new Vector3(sd * (HW - 0.28f), H - 0.28f, mid), new Vector3(0.8f, 0.08f, len), wall, false, 2), q * Quaternion.Euler(0f, 0f, sd * 45f));
                Pipe("Handrail", q * new Vector3(sd * (HW - 0.14f), 2.5f, Hub + 0.5f), q * new Vector3(sd * (HW - 0.14f), 2.5f, End - 0.5f), 0.022f, rail);
                for (float z = Hub + 1.25f; z < End; z += 2.5f) B("Rib", new Vector3(sd * (HW - 0.05f), H / 2, z), new Vector3(0.12f, H, 0.14f), wallDark, false);
            }
            if (endDoor)   // doorway x -1..1, y 0..2.5 for the inner airlock door
            {
                float side = HW + 0.3f - 1.0f;
                B("End wall", new Vector3(-1f - side / 2, H / 2, End + 0.15f), new Vector3(side, H, 0.3f), wall);
                B("End wall", new Vector3(1f + side / 2, H / 2, End + 0.15f), new Vector3(side, H, 0.3f), wall);
                B("End wall over door", new Vector3(0, (2.5f + H) / 2, End + 0.15f), new Vector3(2f, H - 2.5f, 0.3f), wall);
            }
            else B("End wall", new Vector3(0, H / 2, End + 0.15f), new Vector3(2f * HW + 0.6f, H, 0.3f), wall);
            B("Ceiling light", new Vector3(0, H - 0.04f, mid), new Vector3(0.45f, 0.05f, len - 2f), lamp, false);
            var lg = new GameObject("Light").AddComponent<Light>();
            lg.transform.SetParent(transform, false); lg.transform.localPosition = q * new Vector3(0, H - 0.5f, mid);
            lg.type = LightType.Point; lg.range = 10f; lg.intensity = 3.8f; lg.color = new Color(1f, 0.96f, 0.90f); lg.shadows = LightShadows.None;
        }
    }
}
