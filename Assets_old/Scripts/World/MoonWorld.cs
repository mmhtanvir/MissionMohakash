using System.Collections.Generic;
using UnityEngine;

namespace Mohakash
{
    public class MoonWorld : MonoBehaviour
    {
        public const float Size = 1000f, MinY = -30f, HeightRange = 140f, PlayRadius = 460f;
        public const float SunTravelAzimuth = 62f;
        public const float EarthAzimuth = 85f, EarthElevation = 7f;

        public Terrain Terrain { get; private set; }
        public Light Sun { get; private set; }
        public Transform Hab { get; private set; }
        public Vector3 AirlockPoint { get; private set; }
        public Vector3 GaragePoint { get; private set; }
        public Vector3 RoverStart { get; private set; }
        public Vector3 SeisCratePoint { get; private set; }
        public Transform SunDisk, SunCorona, Earth, Stars;
        public readonly List<Transform> SolarArrays = new List<Transform>();

        Transform horizonHaze;

        static readonly (float x, float z, float r, float depth)[] Craters =
        {
            (60, -360, 150, 45),
            (-260, 180, 70, 16), (220, 230, 55, 12), (-150, -140, 34, 8), (140, -60, 22, 5),
            (-60, 120, 16, 3.5f), (300, -80, 40, 9), (-330, -40, 48, 11), (90, 140, 12, 2.5f), (-110, 40, 10, 2),
            (180, 60, 8, 1.6f), (-200, -260, 60, 14), (30, -150, 14, 3), (-40, -90, 9, 1.8f),
        };

        public static float RawHeight(float x, float z)
        {
            float h = 38f * Mathf.Exp(-(z * z) / (230f * 230f)) + 0.012f * x;
            h += 6f * (Mathf.PerlinNoise(x * 0.004f + 11f, z * 0.004f + 7f) - 0.5f);
            h += 1.6f * (Mathf.PerlinNoise(x * 0.03f + 3f, z * 0.03f + 5f) - 0.5f);
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
            if (Physics.Raycast(new Vector3(p.x, 500f, p.z), Vector3.down, out var hit, 1000f))
                return hit.point.y;
            return RawHeight(p.x, p.z) - RawHeight(0, 0);
        }

        public Vector3 OnGround(float x, float z, float lift = 0f)
        {
            var p = new Vector3(x, 0f, z);
            p.y = HeightAt(p) + lift;
            return p;
        }

        public void BuildEnvironment()
        {
            BuildTerrain();
            BuildSky();
            ScatterRocks();
        }

        void BuildTerrain()
        {
            const int res = 257;   // finer mesh = smoother silhouette + shading
            const int res1 = res - 1;
            float baseH = RawHeight(0, 0);

            var verts = new Vector3[res * res];
            var norms = new Vector3[res * res];
            var uvs = new Vector2[res * res];
            var tris = new int[res1 * res1 * 6];

            for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float wx = -Size / 2 + Size * ix / res1;
                float wz = -Size / 2 + Size * iz / res1;
                float h = RawHeight(wx, wz) - baseH;
                float d = Mathf.Sqrt(wx * wx + wz * wz);
                float flat = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(38f, 60f, d));
                h = Mathf.Lerp(h, 0.15f * (Mathf.PerlinNoise(wx * 0.2f, wz * 0.2f) - 0.5f), flat);
                verts[iz * res + ix] = new Vector3(wx, h, wz);
                uvs[iz * res + ix] = new Vector2(wx / 9f, wz / 9f);   // tile the regolith every 9 m (was one stretched 512px image over 1 km)
            }

            for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                int i = iz * res + ix;
                float hL = ix > 0 ? verts[iz * res + ix - 1].y : verts[i].y;
                float hR = ix < res1 ? verts[iz * res + ix + 1].y : verts[i].y;
                float hD = iz > 0 ? verts[(iz - 1) * res + ix].y : verts[i].y;
                float hU = iz < res1 ? verts[(iz + 1) * res + ix].y : verts[i].y;
                norms[i] = new Vector3(hL - hR, 2f * (Size / res1), hD - hU).normalized;
            }

            int t2 = 0;
            for (int iz = 0; iz < res1; iz++)
            for (int ix = 0; ix < res1; ix++)
            {
                int a = iz * res + ix;
                int b = a + 1;
                int c = a + res;
                int d2 = c + 1;
                tris[t2++] = a; tris[t2++] = c; tris[t2++] = b;
                tris[t2++] = b; tris[t2++] = c; tris[t2++] = d2;
            }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();

            var go = new GameObject("Moon Terrain");
            go.transform.SetParent(transform);
            go.transform.position = new Vector3(0f, 0f, 0f);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            Material mat;
            var rockMat = Resources.Load<Material>("Materials/MoonRock");
            if (rockMat != null)
            {
                // Clone the MoonRock material (guaranteed URP Lit shader) and adjust for regolith.
                // URP Lit uses _BaseColor, not .color / _Color.
                mat = new Material(rockMat);
                var regolithColor = new Color(0.80f, 0.78f, 0.74f, 1f);
                mat.SetColor("_BaseColor", regolithColor);
                mat.SetFloat("_Smoothness", 0.02f);
                mat.SetFloat("_Metallic", 0f);
                // Apply regolith texture if available (URP Lit uses _BaseMap, not mainTexture)
                var regTex = Resources.Load<Texture2D>("Materials/RegolithTex");
                if (regTex != null) mat.SetTexture("_BaseMap", regTex);
                // Fine procedural relief so the ground has grain and the low sun rakes across it.
                mat.SetTexture("_BumpMap", MakeRegolithNormalMap());
                mat.SetFloat("_BumpScale", 0.9f);
                mat.EnableKeyword("_NORMALMAP");
            }
            else
            {
                // Last resort: create a plain material with whatever URP shader is available
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                          ?? Shader.Find("Standard")
                          ?? Shader.Find("Hidden/InternalErrorShader");
                mat = new Material(shader);
                mat.SetColor("_BaseColor", new Color(0.42f, 0.40f, 0.37f, 1f));
            }
            renderer.sharedMaterial = mat;

            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

            Terrain = go.GetComponent<Terrain>();
            if (Terrain == null)
            {
                var terrainGo = new GameObject("TerrainProxy");
                terrainGo.transform.SetParent(transform);
                terrainGo.transform.position = Vector3.zero;
                terrainGo.AddComponent<MeshCollider>().sharedMesh = mesh;
                Terrain = terrainGo.AddComponent<Terrain>();
            }
        }

        // Seamless fractal noise -> tangent-space normal map (DXT5nm layout: x in alpha, y in green).
        static Texture2D MakeRegolithNormalMap()
        {
            const int n = 256;
            var h = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float v = 0f, amp = 1f, tot = 0f;
                for (int o = 0; o < 4; o++)
                {
                    float f = 6f * (1 << o) / n;
                    float a = Mathf.PerlinNoise(x * f, y * f);
                    float b = Mathf.PerlinNoise((x - n) * f, y * f);
                    float c = Mathf.PerlinNoise(x * f, (y - n) * f);
                    float d = Mathf.PerlinNoise((x - n) * f, (y - n) * f);
                    float u = x / (float)n, w = y / (float)n;
                    v += amp * (a * (1 - u) * (1 - w) + b * u * (1 - w) + c * (1 - u) * w + d * u * w);
                    tot += amp; amp *= 0.5f;
                }
                h[y * n + x] = v / tot;
            }
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = h[y * n + (x + 1) % n] - h[y * n + (x + n - 1) % n];
                float dy = h[((y + 1) % n) * n + x] - h[((y + n - 1) % n) * n + x];
                var nrm = new Vector3(-dx * 14f, -dy * 14f, 1f).normalized;
                byte nx = (byte)Mathf.RoundToInt((nrm.x * 0.5f + 0.5f) * 255f);
                byte ny = (byte)Mathf.RoundToInt((nrm.y * 0.5f + 0.5f) * 255f);
                px[y * n + x] = new Color32(255, ny, ny, nx);
            }
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            t.SetPixels32(px); t.Apply(true);
            return t;
        }

        void BuildSky()
        {
            RenderSettings.skybox = null;
            DynamicGI.UpdateEnvironment();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.03f, 0.04f, 0.07f);
            RenderSettings.ambientEquatorColor = new Color(0.10f, 0.10f, 0.11f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.05f, 0.04f);
            RenderSettings.fog = false;

            var sunGo = new GameObject("Sun (low polar sun)");
            sunGo.transform.SetParent(transform);
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.color = new Color(1f, 0.97f, 0.90f);
            Sun.intensity = 3.4f;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 1f;
            Sun.shadowNormalBias = 0.4f;
            Sun.shadowBias = 0.02f;
            SetSun(1f);

            SunDisk = MakeDisk("Sun disk", "Materials/SunGlow", 22f);
            SunCorona = MakeDisk("Sun corona", "Materials/SunGlow", 55f);

            var coronaRenderer = SunCorona.GetComponent<Renderer>();
            if (coronaRenderer != null)
            {
                var mat = new Material(coronaRenderer.sharedMaterial);
                mat.SetColor("_BaseColor", new Color(6f, 4.5f, 2.0f, 0.5f));
                coronaRenderer.material = mat;
            }

            Earth = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            Earth.name = "Earth (near the horizon, as seen from the south pole)";
            Object.Destroy(Earth.GetComponent<Collider>());
            var earthMat = Resources.Load<Material>("Materials/Earth");
            if (earthMat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Standard")
                        ?? Shader.Find("Hidden/InternalErrorShader");
                earthMat = new Material(s);
                earthMat.SetColor("_BaseColor", Color.blue);
            }
            Earth.GetComponent<Renderer>().sharedMaterial = earthMat;
            Earth.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Earth.localScale = Vector3.one * 40f;
            Earth.SetParent(transform);

            Stars = BuildStars().transform;
            horizonHaze = BuildHorizonHaze();
        }

        Transform MakeDisk(string name, string matPath, float scale)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            t.name = name;
            Object.Destroy(t.GetComponent<Collider>());
            var r = t.GetComponent<Renderer>();
            var mat = Resources.Load<Material>(matPath);
            if (mat == null)
            {
                Shader s = Shader.Find("Sprites/Default");
                mat = new Material(s != null ? s : Shader.Find("Hidden/InternalErrorShader"));
                mat.color = new Color(1f, 0.8f, 0.4f, 1f);
            }
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.localScale = Vector3.one * scale;
            t.SetParent(transform);
            return t;
        }

        GameObject BuildStars()
        {
            var rnd = new System.Random(42);
            const int n = 5200;
            var v = new Vector3[n * 4];
            var tri = new int[n * 6];
            var col = new Color[n * 4];

            for (int i = 0; i < n; i++)
            {
                var dir = new Vector3(
                    (float)rnd.NextDouble() * 2f - 1f,
                    (float)rnd.NextDouble() * 2f - 1f,
                    (float)rnd.NextDouble() * 2f - 1f).normalized;
                if (dir.y < -0.15f) dir.y = -dir.y;

                var c = dir * 1450f;

                double mag = rnd.NextDouble();
                float s;
                if (mag < 0.03) s = 4.5f + (float)rnd.NextDouble() * 3.0f;
                else if (mag < 0.15) s = 2.2f + (float)rnd.NextDouble() * 2.0f;
                else s = 0.8f + (float)rnd.NextDouble() * 1.4f;

                Color tint;
                double cls = rnd.NextDouble();
                float b = 0.55f + (float)rnd.NextDouble() * 0.45f;
                if (cls < 0.18)
                    tint = new Color(b * 0.75f, b * 0.88f, b);
                else if (cls < 0.65)
                    tint = new Color(b, b, b * 0.92f);
                else
                    tint = new Color(b, b * 0.80f, b * 0.60f);

                var right = Vector3.Cross(dir, Vector3.up).normalized;
                if (right == Vector3.zero) right = Vector3.right;
                var up2 = Vector3.Cross(right, dir);

                v[i * 4] = c - right * s - up2 * s;
                v[i * 4 + 1] = c + right * s - up2 * s;
                v[i * 4 + 2] = c + right * s + up2 * s;
                v[i * 4 + 3] = c - right * s + up2 * s;
                for (int k = 0; k < 4; k++) col[i * 4 + k] = tint;

                tri[i * 6] = i * 4; tri[i * 6 + 1] = i * 4 + 2; tri[i * 6 + 2] = i * 4 + 1;
                tri[i * 6 + 3] = i * 4; tri[i * 6 + 4] = i * 4 + 3; tri[i * 6 + 5] = i * 4 + 2;
            }

            var mesh = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                vertices = v,
                triangles = tri,
                colors = col
            };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 3200f);

            var go = new GameObject("Stars");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Resources.Load<Material>("Materials/Stars") ?? new Material(Shader.Find("Sprites/Default"));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(transform);
            return go;
        }

        Transform BuildHorizonHaze()
        {
            const int segs = 64;
            const float hazeR = 1300f, hazeH = 80f, hazeY = 20f;
            var v = new Vector3[segs * 2];
            var tri = new int[segs * 6];
            var col = new Color[segs * 2];

            for (int i = 0; i < segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                float x = Mathf.Cos(a) * hazeR, z = Mathf.Sin(a) * hazeR;
                v[i * 2] = new Vector3(x, hazeY - hazeH * 0.5f, z);
                v[i * 2 + 1] = new Vector3(x, hazeY + hazeH * 0.5f, z);
                col[i * 2] = new Color(0.75f, 0.65f, 0.50f, 0.18f);
                col[i * 2 + 1] = new Color(0.6f, 0.55f, 0.42f, 0f);
            }

            for (int i = 0; i < segs; i++)
            {
                int n = (i + 1) % segs;
                int b = i * 6;
                tri[b] = i * 2; tri[b + 1] = n * 2; tri[b + 2] = n * 2 + 1;
                tri[b + 3] = i * 2; tri[b + 4] = n * 2 + 1; tri[b + 5] = i * 2 + 1;
            }

            var mesh = new Mesh { vertices = v, triangles = tri, colors = col };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 3000f);

            var go = new GameObject("Horizon Haze");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Resources.Load<Material>("Materials/Beam") ?? new Material(Shader.Find("Sprites/Default"));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(transform);
            return go.transform;
        }

        public void SetSun(float sun)
        {
            if (Sun == null) return;

            float elev = Mathf.Lerp(-4f, 13f, sun);
            Sun.transform.rotation = Quaternion.Euler(elev, SunTravelAzimuth, 0f);
            Sun.intensity = Mathf.Lerp(0f, 3.4f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.55f, sun)));
            Sun.color = Color.Lerp(
                new Color(1.0f, 0.68f, 0.38f),
                new Color(1.0f, 0.97f, 0.90f),
                Mathf.Clamp01(Mathf.InverseLerp(0.1f, 0.7f, sun)));
            Sun.shadowStrength = Mathf.Lerp(0f, 1f, Mathf.InverseLerp(0.08f, 0.45f, sun));

            float earthshine = Mathf.Lerp(0.18f, 0.07f, sun);
            RenderSettings.ambientSkyColor = Color.Lerp(
                new Color(earthshine, earthshine * 1.05f, earthshine * 1.15f),
                new Color(0.06f, 0.07f, 0.12f),
                sun);
            RenderSettings.ambientEquatorColor = Color.Lerp(
                new Color(0.12f, 0.12f, 0.15f),
                new Color(0.22f, 0.22f, 0.24f),
                sun);
            RenderSettings.ambientGroundColor = Color.Lerp(
                new Color(0.04f, 0.04f, 0.03f),
                new Color(0.08f, 0.07f, 0.05f),
                sun);

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

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var p = cam.transform.position;

            if (Stars) Stars.position = p;
            if (horizonHaze) horizonHaze.position = new Vector3(p.x, 0f, p.z);

            if (Sun)
            {
                if (SunDisk) SunDisk.position = p - Sun.transform.forward * 1350f;
                if (SunCorona) SunCorona.position = p - Sun.transform.forward * 1340f;
            }

            if (Earth)
                Earth.position = p + Quaternion.Euler(-EarthElevation, EarthAzimuth, 0f) * Vector3.forward * 1280f;
        }

        void ScatterRocks()
        {
            var rnd = new System.Random(7);
            string[] small = { "Rock", "Rock_34W5ymEePk", "Rock_b7gRkv0cEa" };
            string[] large = { "RockLarge", "RockLarge_d2VWOdthtR" };
            var rockMat = Resources.Load<Material>("Materials/MoonRock");
            var parent = new GameObject("Rocks").transform;
            parent.SetParent(transform);

            for (int i = 0; i < 260; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2;
                float d = Mathf.Lerp(26f, PlayRadius, Mathf.Sqrt((float)rnd.NextDouble()));
                float x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d;
                bool big = rnd.NextDouble() < 0.12;
                string name = big ? large[rnd.Next(large.Length)] : small[rnd.Next(small.Length)];
                float s = big ? 0.6f + (float)rnd.NextDouble() * 1.4f : 0.12f + (float)(rnd.NextDouble() * rnd.NextDouble()) * 0.9f;
                var rock = Spawn(name, OnGround(x, z, -0.25f * s), Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0), s, parent);
                if (rock == null) continue;
                if (rockMat) foreach (var r in rock.GetComponentsInChildren<Renderer>()) r.sharedMaterial = rockMat;
                if (s > 0.35f) AddBoxCollider(rock);
            }
        }

        public void BuildOutpost(Loadout kit)
        {
            var root = new GameObject("Outpost").transform;
            root.SetParent(transform);

            Hab = Spawn("GeodesicDome", OnGround(0, 0), Quaternion.identity, 1f, root, "HAB - Habitat Module").transform;
            AddBoxCollider(Hab.gameObject, 0.8f);

            var airlock = Spawn("Connector", OnGround(5.6f, 0), Quaternion.Euler(0, 180, 0), 1f, root, "SUI - Suit Station / airlock");
            if (airlock) airlock.transform.position += Vector3.down * 1.1f;
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
            GaragePoint = OnGround(2f, 17f);
            RoverStart = OnGround(9f, 17f);

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

        public static GameObject Spawn(string model, Vector3 pos, Quaternion rot, float scale, Transform parent, string label = null)
        {
            var prefab = Resources.Load<GameObject>("Models/" + model);
            if (prefab == null)
            {
                Debug.LogWarning("Missing model " + model);
                return null;
            }
            var go = Object.Instantiate(prefab, pos, rot, parent);
            go.transform.localScale = Vector3.one * scale;
            go.name = label ?? model;
            ModelMaterialFixer.Fix(go);
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
            lr.sharedMaterial = Resources.Load<Material>("Materials/Line") ?? new Material(Shader.Find("Sprites/Default"));
            lr.loop = true;
            lr.useWorldSpace = true;
            lr.widthMultiplier = width;
            lr.startColor = lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.positionCount = 0;
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

            var renderer = go.GetComponent<Renderer>();
            var baseMat = Resources.Load<Material>("Materials/Beam");
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
            var material = baseMat != null ? new Material(baseMat) : new Material(shader);
            material.color = c;
            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
