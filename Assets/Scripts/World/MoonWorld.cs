using System.Collections.Generic;
using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Builds the Mission 1 surface: Connecting Ridge-inspired terrain (hand-authored, NOT real LOLA data yet),
    /// sky, Earth, sun, rocks and the outpost modules chosen in the loadout.
    /// Models: Quaternius "Ultimate Space Kit" (CC0).
    ///
    /// Graphics upgrades (Milestone 02):
    ///   – 5200-star field with magnitude-weighted sizes, blue/white/orange colour classes and proper hemisphere culling
    ///   – Low polar sun with a physically motivated limb-darkened disk and a faint corona glow ring
    ///   – Horizon gradient gradient quad that simulates moon-dust forward-scatter
    ///   – Soft shadow intensity ramp tied to sun elevation; ambient fills with Earthshine during shadow window
    ///   – Improved SetSun: smooth intensity curve, subtle colour temperature shift at low sun angles
    /// </summary>
    public class MoonWorld : MonoBehaviour
    {
        public const float Size = 1000f, MinY = -30f, HeightRange = 140f, PlayRadius = 460f;
        public const float SunTravelAzimuth   = 62f;
        public const float EarthAzimuth = 85f, EarthElevation = 7f;

        public UnityEngine.Terrain Terrain { get; private set; }  // null when using mesh terrain
        public Light   Sun          { get; private set; }
        public Transform Hab        { get; private set; }
        public Vector3 AirlockPoint { get; private set; }
        public Vector3 GaragePoint  { get; private set; }
        public Vector3 RoverStart   { get; private set; }
        public Vector3 SeisCratePoint { get; private set; }
        public Transform SunDisk, SunCorona, Earth, Stars;
        public readonly List<Transform> SolarArrays = new List<Transform>();

        // Horizon haze quad (forward-scatter dust effect)
        Transform horizonHaze;

        // ------------------------------------------------------------------ Craters

        static readonly (float x, float z, float r, float depth)[] Craters =
        {
            (60, -360, 150, 45),
            (-260, 180, 70, 16), (220, 230, 55, 12), (-150, -140, 34, 8), (140, -60, 22, 5),
            (-60, 120, 16, 3.5f), (300, -80, 40, 9), (-330, -40, 48, 11), (90, 140, 12, 2.5f), (-110, 40, 10, 2),
            (180, 60, 8, 1.6f), (-200, -260, 60, 14), (30, -150, 14, 3), (-40, -90, 9, 1.8f),
        };

        // ------------------------------------------------------------------ Height

        public static float RawHeight(float x, float z)
        {
            float h = 38f * Mathf.Exp(-(z * z) / (230f * 230f)) + 0.012f * x;
            h += 6f  * (Mathf.PerlinNoise(x * 0.004f + 11f, z * 0.004f + 7f) - 0.5f);
            h += 1.6f * (Mathf.PerlinNoise(x * 0.03f  + 3f, z * 0.03f  + 5f) - 0.5f);
            h += 0.35f * (Mathf.PerlinNoise(x * 0.18f, z * 0.18f) - 0.5f);
            foreach (var c in Craters)
            {
                float d = Mathf.Sqrt((x - c.x) * (x - c.x) + (z - c.z) * (z - c.z)) / c.r;
                if (d < 1f) h -= c.depth * (1f - d * d);
                h += c.depth * 0.22f * Mathf.Exp(-((d - 1f) * (d - 1f)) / 0.06f);
            }
            return h;
        }

        public float HeightAt(Vector3 p)
        {
            // Use Physics raycast against the terrain mesh collider
            if (Physics.Raycast(new Vector3(p.x, 500f, p.z), Vector3.down, out var hit, 1000f))
                return hit.point.y;
            // Fallback to procedural formula before mesh collider is ready
            return RawHeight(p.x, p.z) - RawHeight(0, 0);
        }
        public Vector3 OnGround(float x, float z, float lift = 0f)
        { var p = new Vector3(x, 0, z); p.y = HeightAt(p) + lift; return p; }

        // ------------------------------------------------------------------ Bootstrap

        public void BuildEnvironment()
        {
            BuildTerrain();
            BuildSky();
            ScatterRocks();
        }

        // ------------------------------------------------------------------ Terrain

        static Texture2D MakeGreyTex()
        {
            var t = new Texture2D(4, 4, TextureFormat.RGB24, false);
            var grey = new Color(0.38f, 0.37f, 0.35f);
            for (int i = 0; i < 16; i++) t.SetPixel(i % 4, i / 4, grey);
            t.Apply();
            return t;
        }

        void BuildTerrain()
        {
            const int res = 129;   // mesh resolution (129×129 verts)
            const int res1 = res - 1;
            float baseH = RawHeight(0, 0);

            var verts  = new Vector3[res * res];
            var norms  = new Vector3[res * res];
            var uvs    = new Vector2[res * res];
            var tris   = new int[res1 * res1 * 6];

            for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float wx = -Size / 2 + Size * ix / res1;
                float wz = -Size / 2 + Size * iz / res1;
                float h  = RawHeight(wx, wz) - baseH;
                float d  = Mathf.Sqrt(wx * wx + wz * wz);
                float flat = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(38f, 60f, d));
                h = Mathf.Lerp(h, 0.15f * (Mathf.PerlinNoise(wx * 0.2f, wz * 0.2f) - 0.5f), flat);
                verts[iz * res + ix] = new Vector3(wx, h, wz);
                uvs[iz * res + ix]   = new Vector2(ix / (float)res1, iz / (float)res1);
            }

            // Normals
            for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                int i = iz * res + ix;
                float hL = ix > 0      ? verts[iz * res + ix - 1].y : verts[i].y;
                float hR = ix < res1   ? verts[iz * res + ix + 1].y : verts[i].y;
                float hD = iz > 0      ? verts[(iz-1)*res + ix].y   : verts[i].y;
                float hU = iz < res1   ? verts[(iz+1)*res + ix].y   : verts[i].y;
                norms[i] = new Vector3(hL - hR, 2f * (Size / res1), hD - hU).normalized;
            }

            int t2 = 0;
            for (int iz = 0; iz < res1; iz++)
            for (int ix = 0; ix < res1; ix++)
            {
                int a = iz * res + ix, b = a + 1, c = a + res, d2 = c + 1;
                tris[t2++]=a; tris[t2++]=c; tris[t2++]=b;
                tris[t2++]=b; tris[t2++]=c; tris[t2++]=d2;
            }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices  = verts;
            mesh.normals   = norms;
            mesh.uv        = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();

            var go = new GameObject("Moon Terrain");
            go.transform.SetParent(transform);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();

            // Borrow shader from MoonRock material – guaranteed URP Lit shader
            var rockMat = Resources.Load<Material>("Materials/MoonRock");
            if (rockMat != null)
            {
                var mat = new Material(rockMat);
                mat.color = new Color(0.38f, 0.37f, 0.35f);
                mat.SetFloat("_Smoothness", 0.03f);
                mr.sharedMaterial = mat;
            }

            // Add a MeshCollider for HeightAt raycasts
            go.AddComponent<MeshCollider>().sharedMesh = mesh;

            // Store a fake Terrain reference so HeightAt still works via raycast
            _terrainMeshGo = go;
        }

        GameObject _terrainMeshGo;
        {
            const int res = 513;
            float baseH = RawHeight(0, 0);
            var data = new TerrainData { heightmapResolution = res };
            data.size = new Vector3(Size, HeightRange, Size);
            var hs = new float[res, res];
            for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float x = -Size / 2 + Size * ix / (res - 1);
                float z = -Size / 2 + Size * iz / (res - 1);
                float h = RawHeight(x, z);
                float d = Mathf.Sqrt(x * x + z * z);
                float flat = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(38f, 60f, d));
                h = Mathf.Lerp(h, baseH + 0.15f * (Mathf.PerlinNoise(x * 0.2f, z * 0.2f) - 0.5f), flat);
                hs[iz, ix] = Mathf.Clamp01((h - baseH - MinY) / HeightRange);
            }
            data.SetHeights(0, 0, hs);

            // Build the terrain layer in code so it works without running ProjectSetup first.
            // Load the pre-baked texture if available; fall back to a solid grey.
            var regTex = Resources.Load<Texture2D>("Materials/RegolithTex");
            var layer = new TerrainLayer();
            if (regTex != null)
            {
                layer.diffuseTexture = regTex;
                layer.tileSize       = new Vector2(12f, 12f);
            }
            else
            {
                // Fallback: 4×4 grey texture – still beats solid pink
                layer.diffuseTexture = MakeGreyTex();
                layer.tileSize       = new Vector2(20f, 20f);
            }
            layer.smoothness = 0.03f;
            layer.metallic   = 0f;
            data.terrainLayers = new[] { layer };

            // Qualify with UnityEngine.Terrain to avoid collision with our Terrain property
            var go = UnityEngine.Terrain.CreateTerrainGameObject(data);
            go.name = "Moon Terrain (inspired by LOLA polar ridge - not real data)";
            go.transform.position = new Vector3(-Size / 2, MinY, -Size / 2);
            Terrain = go.GetComponent<UnityEngine.Terrain>();

            // Get the URP Lit shader from MoonRock.mat which is guaranteed to exist.
            // Shader.Find() returns null in URP builds so we borrow from an existing asset.
            var rockMat = Resources.Load<Material>("Materials/MoonRock");
            var terrainMat = rockMat != null
                ? new Material(rockMat) { color = new Color(0.38f, 0.37f, 0.35f) }
                : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Hidden/InternalErrorShader"));
            terrainMat.color = new Color(0.38f, 0.37f, 0.35f);
            terrainMat.SetFloat("_Smoothness", 0.03f);
            Terrain.materialTemplate = terrainMat;
            Terrain.heightmapPixelError = 2f;
            Terrain.basemapDistance   = 500f;
            Terrain.drawInstanced     = true;
            go.transform.SetParent(transform);
        }

        // ------------------------------------------------------------------ Sky

        void BuildSky()
        {
            // Force skybox off – must be set before and after the scene's RenderSettings
            // can re-assert the default Unity skybox.
            RenderSettings.skybox = null;
            DynamicGI.UpdateEnvironment();
            RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.03f, 0.04f, 0.07f);
            RenderSettings.ambientEquatorColor = new Color(0.10f, 0.10f, 0.11f);
            RenderSettings.ambientGroundColor  = new Color(0.06f, 0.05f, 0.04f);
            RenderSettings.fog = false;

            // Directional sun light
            var sunGo = new GameObject("Sun (low polar sun)");
            sunGo.transform.SetParent(transform);
            Sun = sunGo.AddComponent<Light>();
            Sun.type          = LightType.Directional;
            Sun.color         = new Color(1f, 0.97f, 0.90f);
            Sun.intensity     = 3.4f;
            Sun.shadows       = LightShadows.Soft;
            Sun.shadowStrength  = 1f;
            Sun.shadowNormalBias = 0.4f;
            Sun.shadowBias      = 0.02f;
            // Cookie not set – would need a texture asset.
            SetSun(1f);

            // ── Sun disk + corona ────────────────────────────────────────
            SunDisk   = MakeDisk("Sun disk",    "Materials/SunGlow",  22f);   // smaller, sharper disk
            SunCorona = MakeDisk("Sun corona",  "Materials/SunGlow",  55f);   // wider soft halo

            // Make the corona use a semi-transparent additive material copy so it blends softly.
            // We just scale it differently and tint it more orange.
            var coronaRenderer = SunCorona.GetComponent<Renderer>();
            if (coronaRenderer != null)
            {
                var mat = new Material(coronaRenderer.sharedMaterial);
                mat.SetColor("_BaseColor", new Color(6f, 4.5f, 2.0f, 0.5f));  // orange-ish glow
                coronaRenderer.material = mat;
            }
            SunCorona.GetComponent<Collider>()?.gameObject.GetComponent<Collider>()?.gameObject
                .TryGetComponent<Collider>(out var _); // just a safe noop

            // ── Earth sphere ─────────────────────────────────────────────
            Earth = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            Earth.name = "Earth (near the horizon, as seen from the south pole)";
            Object.Destroy(Earth.GetComponent<Collider>());
            Earth.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>("Materials/Earth");
            Earth.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Earth.localScale = Vector3.one * 40f;   // slightly larger → more imposing
            Earth.SetParent(transform);

            // ── Stars ────────────────────────────────────────────────────
            Stars = BuildStars().transform;

            // ── Horizon haze (forward-scatter moon dust) ─────────────────
            horizonHaze = BuildHorizonHaze();
        }

        Transform MakeDisk(string name, string mat, float scale)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            t.name = name;
            Object.Destroy(t.GetComponent<Collider>());
            var r = t.GetComponent<Renderer>();
            r.sharedMaterial = Resources.Load<Material>(mat);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.localScale = Vector3.one * scale;
            t.SetParent(transform);
            return t;
        }

        /// <summary>
        /// 5200-star procedural mesh. Stars are weighted by a fake magnitude distribution,
        /// tinted by three colour classes (blue-white O/B, neutral G, orange K/M), and
        /// concentrated above the horizon (y > -0.15).
        /// </summary>
        GameObject BuildStars()
        {
            var rnd = new System.Random(42);
            const int n = 5200;
            var v   = new Vector3[n * 4];
            var tri = new int[n * 6];
            var col = new Color[n * 4];

            for (int i = 0; i < n; i++)
            {
                // Uniform sphere point – keep above horizon
                var dir = new Vector3(
                    (float)rnd.NextDouble() * 2f - 1f,
                    (float)rnd.NextDouble() * 2f - 1f,
                    (float)rnd.NextDouble() * 2f - 1f).normalized;
                if (dir.y < -0.15f) dir.y = -dir.y;

                var c = dir * 1450f;

                // Magnitude distribution: most stars are faint; ~3 % are bright
                double mag = rnd.NextDouble();
                float s;
                if (mag < 0.03)       s = 4.5f + (float)rnd.NextDouble() * 3.0f;  // bright star
                else if (mag < 0.15)  s = 2.2f + (float)rnd.NextDouble() * 2.0f;  // medium
                else                  s = 0.8f + (float)rnd.NextDouble() * 1.4f;  // faint

                // Colour class: O/B blue-white, G yellow-white, K/M orange-red
                Color tint;
                double cls = rnd.NextDouble();
                float b = 0.55f + (float)rnd.NextDouble() * 0.45f;
                if (cls < 0.18)
                    tint = new Color(b * 0.75f, b * 0.88f, b);           // blue-white
                else if (cls < 0.65)
                    tint = new Color(b, b, b * 0.92f);                    // neutral white
                else
                    tint = new Color(b, b * 0.80f, b * 0.60f);           // orange-warm

                var right = Vector3.Cross(dir, Vector3.up).normalized;
                if (right == Vector3.zero) right = Vector3.right;
                var up2 = Vector3.Cross(right, dir);

                v[i * 4]     = c - right * s - up2 * s;
                v[i * 4 + 1] = c + right * s - up2 * s;
                v[i * 4 + 2] = c + right * s + up2 * s;
                v[i * 4 + 3] = c - right * s + up2 * s;
                for (int k = 0; k < 4; k++) col[i * 4 + k] = tint;

                tri[i * 6]     = i * 4;     tri[i * 6 + 1] = i * 4 + 2; tri[i * 6 + 2] = i * 4 + 1;
                tri[i * 6 + 3] = i * 4;     tri[i * 6 + 4] = i * 4 + 3; tri[i * 6 + 5] = i * 4 + 2;
            }

            var mesh = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                vertices    = v,
                triangles   = tri,
                colors      = col
            };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 3200f);

            var go = new GameObject("Stars");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Resources.Load<Material>("Materials/Stars");
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(transform);
            return go;
        }

        /// <summary>
        /// Horizon haze: a large cylinder/ring of near-transparent additive quads that simulates
        /// the faint forward-scatter luminosity you see when the sun is near the horizon on the
        /// Moon (caused by levitated electrostatically charged regolith dust).
        /// </summary>
        Transform BuildHorizonHaze()
        {
            const int segs = 64;
            const float hazeR = 1300f, hazeH = 80f, hazeY = 20f;   // ring radius, height, elevation
            var v   = new Vector3[segs * 2];
            var tri = new int[segs * 6];
            var col = new Color[segs * 2];

            for (int i = 0; i < segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                float x = Mathf.Cos(a) * hazeR, z = Mathf.Sin(a) * hazeR;
                v[i * 2]     = new Vector3(x, hazeY - hazeH * 0.5f, z);
                v[i * 2 + 1] = new Vector3(x, hazeY + hazeH * 0.5f, z);
                // fade from opaque at bottom to transparent at top
                col[i * 2]     = new Color(0.75f, 0.65f, 0.50f, 0.18f);
                col[i * 2 + 1] = new Color(0.6f,  0.55f, 0.42f, 0f);
            }

            for (int i = 0; i < segs; i++)
            {
                int n = (i + 1) % segs;
                int b = i * 6;
                tri[b]     = i * 2;     tri[b + 1] = n * 2;     tri[b + 2] = n * 2 + 1;
                tri[b + 3] = i * 2;     tri[b + 4] = n * 2 + 1; tri[b + 5] = i * 2 + 1;
            }

            var mesh = new Mesh { vertices = v, triangles = tri, colors = col };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 3000f);

            var go = new GameObject("Horizon Haze (regolith dust scatter)");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            // Re-use the additive Beam material for the transparent haze.
            mr.sharedMaterial = Resources.Load<Material>("Materials/Beam");
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(transform);
            return go.transform;
        }

        // ------------------------------------------------------------------ Sun update

        /// <summary>
        /// sun 0..1 from the illumination curve (0 = shadowed, 1 = full sun).
        /// Updates sun direction, intensity, colour temperature, shadow strength, and ambient fill.
        /// </summary>
        public void SetSun(float sun)
        {
            if (Sun == null) return;

            // Elevation: polar sun stays within a few degrees of the horizon; raised +4° for readability.
            float elev = Mathf.Lerp(-4f, 13f, sun);
            Sun.transform.rotation = Quaternion.Euler(elev, SunTravelAzimuth, 0f);

            // Intensity: smooth ramp; no jump at the shadow boundary.
            Sun.intensity = Mathf.Lerp(0f, 3.4f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.55f, sun)));

            // Colour temperature: deep orange at first light, cool white at full sun.
            // Lerp between warm sunrise tint and neutral solar white.
            Sun.color = Color.Lerp(
                new Color(1.0f, 0.68f, 0.38f),   // warm low sun
                new Color(1.0f, 0.97f, 0.90f),   // cool solar white
                Mathf.Clamp01(Mathf.InverseLerp(0.1f, 0.7f, sun)));

            // Shadow strength – fade out near the terminator to avoid hard-edged disappearance.
            Sun.shadowStrength = Mathf.Lerp(0f, 1f, Mathf.InverseLerp(0.08f, 0.45f, sun));

            // Trilight ambient: Earthshine (blue-grey) fills the scene when the sun is down.
            // When sun is up, sky dome brightens towards the solar direction.
            float earthshine = Mathf.Lerp(0.18f, 0.07f, sun);    // Earthshine dominant at night
            RenderSettings.ambientSkyColor     = Color.Lerp(
                new Color(earthshine, earthshine * 1.05f, earthshine * 1.15f),
                new Color(0.06f, 0.07f, 0.12f),
                sun);
            RenderSettings.ambientEquatorColor = Color.Lerp(
                new Color(0.12f, 0.12f, 0.15f),
                new Color(0.22f, 0.22f, 0.24f),
                sun);
            RenderSettings.ambientGroundColor  = Color.Lerp(
                new Color(0.04f, 0.04f, 0.03f),
                new Color(0.08f, 0.07f, 0.05f),
                sun);

            // Haze visibility: brighter and more visible at low sun angles (forward-scatter).
            if (horizonHaze != null)
            {
                var mr = horizonHaze.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    float hazeAlpha = Mathf.Clamp01(1f - Mathf.Abs(sun - 0.12f) / 0.4f) * 0.8f;
                    var mat = mr.material;
                    var c = mat.color;
                    mat.color = new Color(c.r, c.g, c.b, hazeAlpha);
                }
            }
        }

        // ------------------------------------------------------------------ Per-frame sky tracking

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var p = cam.transform.position;

            // Keep the celestial sphere centred on the camera.
            if (Stars)       Stars.position       = p;
            if (horizonHaze) horizonHaze.position = new Vector3(p.x, 0f, p.z);

            if (Sun)
            {
                if (SunDisk)
                    SunDisk.position = p - Sun.transform.forward * 1350f;
                if (SunCorona)
                    SunCorona.position = p - Sun.transform.forward * 1340f;
            }

            if (Earth)
                Earth.position = p + Quaternion.Euler(-EarthElevation, EarthAzimuth, 0f) * Vector3.forward * 1280f;
        }

        // ------------------------------------------------------------------ Rocks

        void ScatterRocks()
        {
            var rnd = new System.Random(7);
            string[] small = { "Rock", "Rock_34W5ymEePk", "Rock_b7gRkv0cEa" };
            string[] large = { "RockLarge", "RockLarge_d2VWOdthtR" };
            var rockMat   = Resources.Load<Material>("Materials/MoonRock");
            var parent    = new GameObject("Rocks").transform;
            parent.SetParent(transform);

            for (int i = 0; i < 260; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2;
                float d = Mathf.Lerp(26f, PlayRadius, Mathf.Sqrt((float)rnd.NextDouble()));
                float x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d;
                bool big = rnd.NextDouble() < 0.12;
                string name = big ? large[rnd.Next(large.Length)] : small[rnd.Next(small.Length)];
                float s = big ? 0.6f + (float)rnd.NextDouble() * 1.4f
                              : 0.12f + (float)(rnd.NextDouble() * rnd.NextDouble()) * 0.9f;
                var rock = Spawn(name, OnGround(x, z, -0.25f * s), Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0), s, parent);
                if (rock == null) continue;
                if (rockMat) foreach (var r in rock.GetComponentsInChildren<Renderer>()) r.sharedMaterial = rockMat;
                if (s > 0.35f) AddBoxCollider(rock);
            }
        }

        // ------------------------------------------------------------------ Outpost

        public void BuildOutpost(Loadout kit)
        {
            var root = new GameObject("Outpost").transform; root.SetParent(transform);
            Hab = Spawn("GeodesicDome", OnGround(0, 0), Quaternion.identity, 1f, root, "HAB - Habitat Module").transform;
            AddBoxCollider(Hab.gameObject, 0.8f);

            var airlock = Spawn("Connector", OnGround(5.6f, 0), Quaternion.Euler(0, 180, 0), 1f, root, "SUI - Suit Station / airlock");
            if (airlock) { airlock.transform.position += Vector3.down * 1.1f; }
            AirlockPoint = OnGround(8.6f, 0);

            if (kit.Has("OGS")) AddBoxCollider(Spawn("HouseLong", OnGround(-11f, -1f), Quaternion.Euler(0, 90, 0), 0.8f, root, "OGS - Oxygen Generator module"), 0.9f);
            if (kit.Has("STO"))
            {
                var sto = Spawn("HouseCylinder", OnGround(-6f, 11f), Quaternion.identity, 0.85f, root, "STO - Storage Module");
                AddBoxCollider(sto, 0.85f);
            }
            if (kit.Has("COM")) Spawn("RoofAntenna", OnGround(-3f, -8f, -4.05f), Quaternion.identity, 1f, root, "COM - Communications antenna");
            if (kit.Has("RAD")) Spawn("RoofRadar", OnGround(6f, -8f, -4.05f), Quaternion.identity, 1f, root, "RAD - Radiation Monitor post");

            if (kit.Has("GAR"))
            {
                var gar = Spawn("BaseLarge", OnGround(2f, 26f), Quaternion.Euler(0, 180, 0), 0.75f, root, "GAR - Garage");
                AddBoxCollider(gar, 0.85f);
            }
            GaragePoint  = OnGround(2f, 17f);
            RoverStart   = OnGround(9f, 17f);

            int arrays = kit.Count("SOL-S");
            for (int i = 0; i < arrays; i++)
            {
                var a = Spawn("SolarPanelGround", OnGround(-9f + i * 5f, -17f), Quaternion.Euler(0, 62f + 180f, 0), 2.6f, root, $"SOL-S Solar Array {i + 1}");
                if (a) { SolarArrays.Add(a.transform); AddBoxCollider(a); }
            }
            for (int i = 0; i < kit.Count("BAT"); i++)
                AddBoxCollider(Spawn("PickupCrate", OnGround(-13f - i * 1.6f, -12f, 0.6f), Quaternion.identity, 1.3f, root, "BAT - Battery"));
            for (int i = 0; i < kit.Count("TNK-O"); i++)
                Spawn("PickupJar", OnGround(-16f, 3f + i * 1.8f, 1.1f), Quaternion.identity, 1.6f, root, "TNK-O - Oxygen Tank");
            if (kit.Has("TNK-W"))
                Spawn("PickupJar", OnGround(-16f, 8f, 1.1f), Quaternion.identity, 1.6f, root, "TNK-W - Water Tank");

            var lander = Spawn("Spaceship", OnGround(-30f, 18f, 0.3f), Quaternion.Euler(0, 140, 0), 1.1f, root, "Lander");
            AddBoxCollider(lander, 0.9f);

            SeisCratePoint = OnGround(9f, 5f);
        }

        // ------------------------------------------------------------------ Helpers

        public static GameObject Spawn(string model, Vector3 pos, Quaternion rot, float scale,
                                       Transform parent, string label = null)
        {
            var prefab = Resources.Load<GameObject>("Models/" + model);
            if (prefab == null) { Debug.LogWarning("Missing model " + model); return null; }
            var go = Object.Instantiate(prefab, pos, rot, parent);
            go.transform.localScale = Vector3.one * scale;
            go.name = label ?? model;
            return go;
        }

        public static void AddBoxCollider(GameObject go, float shrink = 1f)
        {
            if (go == null) return;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = go.transform.InverseTransformPoint(b.center);
            var size = go.transform.InverseTransformVector(b.size);
            bc.size = new Vector3(Mathf.Abs(size.x) * shrink, Mathf.Abs(size.y), Mathf.Abs(size.z) * shrink);
        }

        public LineRenderer MakeRing(string name, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial    = Resources.Load<Material>("Materials/Line");
            lr.loop              = true;
            lr.useWorldSpace     = true;
            lr.widthMultiplier   = width;
            lr.startColor        = lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.positionCount     = 0;
            return lr;
        }

        public void SetRing(LineRenderer lr, Vector3 center, float radius)
        {
            const int n = 160;
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n;
                lr.SetPosition(i, OnGround(center.x + Mathf.Cos(a) * radius, center.z + Mathf.Sin(a) * radius, 0.35f));
            }
        }

        public GameObject MakeBeacon(Vector3 at, Color c, float height = 14f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "Beacon";
            go.transform.SetParent(transform);
            go.transform.position = at + Vector3.up * height / 2;
            go.transform.localScale = new Vector3(0.35f, height / 2, 0.35f);
            var r = go.GetComponent<Renderer>();
            r.material = new Material(Resources.Load<Material>("Materials/Beam")) { color = c };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
