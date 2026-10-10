using System.Collections.Generic;
using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Milestone 1 world builder. Put this single component on an empty GameObject in an otherwise empty scene
    /// (menu: Mission Mohakash > Create Milestone 1 Scene does this for you). On Play it procedurally builds the
    /// lunar test site, lander, equipment, player, camera, mission state and HUD.
    /// ALL geometry here is a TEMPORARY PLACEHOLDER built from primitives. Values are fictional GAME UNITS.
    /// </summary>
    public class MissionBootstrap : MonoBehaviour
    {
        public int seed = 42;

        Transform world;
        readonly List<Vector3> keepClear = new List<Vector3>();
        static readonly Color Cyan = new Color(0.3f, 0.95f, 1f);
        static readonly Color Amber = new Color(1f, 0.7f, 0.1f);

        void Awake()
        {
            Interactable.All.Clear();
            SetupEnvironment();
            new GameObject("MissionState").AddComponent<MissionState>();
            world = new GameObject("World_PLACEHOLDER").transform;

            keepClear.Add(new Vector3(0f, 0f, 0f));
            keepClear.Add(new Vector3(0f, 0f, 10f));

            BuildGround();
            BuildLander();
            BuildEquipment();
            BuildRocks();
            BuildBackdrop();
            BuildPlayerAndCamera();
            new GameObject("HUD").AddComponent<HudUI>();
        }

        // ---------- environment ----------
        void SetupEnvironment()
        {
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.97f, 0.9f);
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(28f, -35f, 0f);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.10f, 0.13f);
            RenderSettings.fog = false;
        }

        void BuildGround()
        {
            Placeholder.Prim(PrimitiveType.Cube, "Ground", world, new Vector3(0f, -5f, 0f), new Vector3(520f, 10f, 520f),
                             Placeholder.Mat(new Color(0.42f, 0.40f, 0.38f)));
            var rng = new System.Random(seed + 1);
            var dark = Placeholder.Mat(new Color(0.30f, 0.29f, 0.28f));
            for (int i = 0; i < 16; i++)
            {
                float r = Range(rng, 4f, 14f);
                Vector3 p = RandomDisc(rng, 8f, 110f);
                Placeholder.Prim(PrimitiveType.Cylinder, "CraterDecal", world, new Vector3(p.x, 0.015f, p.z),
                                 new Vector3(r, 0.01f, r), dark, false);
            }
        }

        void BuildBackdrop()
        {
            var rng = new System.Random(seed + 2);
            var rock = Placeholder.Mat(new Color(0.28f, 0.27f, 0.27f));
            for (int i = 0; i < 16; i++)
            {
                float ang = (i / 16f) * Mathf.PI * 2f + Range(rng, -0.1f, 0.1f);
                float rad = Range(rng, 190f, 240f);
                float sx = Range(rng, 60f, 120f), sy = Range(rng, 20f, 55f);
                Placeholder.Prim(PrimitiveType.Sphere, "Mountain", world, new Vector3(Mathf.Cos(ang) * rad, -sy * 0.15f, Mathf.Sin(ang) * rad),
                                 new Vector3(sx, sy, sx), rock, false);
            }
            Placeholder.Prim(PrimitiveType.Sphere, "Earth", world, new Vector3(-350f, 180f, 450f), new Vector3(60f, 60f, 60f),
                             Placeholder.Unlit(new Color(0.2f, 0.45f, 0.9f)), false);

            var stars = new GameObject("Stars").transform;
            stars.SetParent(world, false);
            var starMat = Placeholder.Unlit(Color.white);
            for (int i = 0; i < 250; i++)
            {
                Vector3 d = new Vector3(Range(rng, -1f, 1f), Range(rng, 0.05f, 1f), Range(rng, -1f, 1f)).normalized * 900f;
                Placeholder.Prim(PrimitiveType.Sphere, "Star", stars, d, Vector3.one * 3f, starMat, false);
            }
        }

        void BuildRocks()
        {
            var rng = new System.Random(seed);
            int placed = 0, tries = 0;
            while (placed < 70 && tries < 600)
            {
                tries++;
                Vector3 p = RandomDisc(rng, 6f, 105f);
                bool ok = true;
                foreach (var k in keepClear) if (Vector3.Distance(new Vector3(p.x, 0f, p.z), k) < 7f) { ok = false; break; }
                if (!ok) continue;
                float s = Range(rng, 0.5f, 2.2f);
                float g = Range(rng, 0.3f, 0.48f);
                Placeholder.Prim(PrimitiveType.Sphere, "Rock", world, new Vector3(p.x, s * 0.25f, p.z),
                                 new Vector3(s, s * Range(rng, 0.5f, 0.9f), s * Range(rng, 0.8f, 1.2f)),
                                 Placeholder.Mat(new Color(g, g * 0.97f, g * 0.94f)));
                placed++;
            }
        }

        static float Range(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }

        static Vector3 RandomDisc(System.Random r, float minR, float maxR)
        {
            float a = Range(r, 0f, Mathf.PI * 2f);
            float d = Range(r, minR, maxR);
            return new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
        }

        // ---------- lander ----------
        void BuildLander()
        {
            var lander = new GameObject("Lander_PLACEHOLDER").transform;
            lander.SetParent(world, false);
            lander.position = new Vector3(0f, 0f, 10f);
            var gold = Placeholder.Mat(new Color(0.85f, 0.7f, 0.3f));
            var grey = Placeholder.Mat(new Color(0.6f, 0.6f, 0.62f));
            var dark = Placeholder.Mat(new Color(0.12f, 0.12f, 0.15f));

            Placeholder.Prim(PrimitiveType.Cube, "Body", lander, new Vector3(0f, 2.6f, 0f), new Vector3(4f, 2.4f, 4f), gold);
            Placeholder.Prim(PrimitiveType.Cylinder, "Roof", lander, new Vector3(0f, 3.95f, 0f), new Vector3(3f, 0.15f, 3f), grey, false);
            Placeholder.Prim(PrimitiveType.Cube, "Door", lander, new Vector3(0f, 2.1f, -2.03f), new Vector3(1.2f, 1.8f, 0.05f), dark, false);
            float[] sx = { -2.1f, 2.1f };
            foreach (float x in sx)
                foreach (float z in sx)
                {
                    Placeholder.Prim(PrimitiveType.Cylinder, "Leg", lander, new Vector3(x, 1.3f, z), new Vector3(0.12f, 1.3f, 0.12f), grey, false);
                    Placeholder.Prim(PrimitiveType.Cylinder, "Foot", lander, new Vector3(x, 0.05f, z), new Vector3(0.6f, 0.05f, 0.6f), grey, false);
                }
        }

        // ---------- equipment ----------
        Interactable Station(string name, Vector3 pos, float markerH, Vector3 lightLocalPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(world, false);
            go.transform.position = pos;
            var it = go.AddComponent<Interactable>();
            it.displayName = name;
            it.markerHeight = markerH;
            var m = Placeholder.Prim(PrimitiveType.Cube, "Marker", go.transform, Vector3.zero, Vector3.one * 0.35f,
                                     Placeholder.Unlit(Cyan), false, new Vector3(45f, 0f, 45f));
            it.marker = m.transform;
            var l = Placeholder.Prim(PrimitiveType.Sphere, "StatusLight", go.transform, lightLocalPos, Vector3.one * 0.14f,
                                     Placeholder.Unlit(Amber), false);
            it.statusLight = l.GetComponent<Renderer>();
            return it;
        }

        void BuildEquipment()
        {
            var metal = Placeholder.Mat(new Color(0.75f, 0.77f, 0.8f));
            var grey = Placeholder.Mat(new Color(0.35f, 0.36f, 0.4f));
            var blue = Placeholder.Mat(new Color(0.1f, 0.2f, 0.5f));
            var brown = Placeholder.Mat(new Color(0.45f, 0.3f, 0.22f));

            // 1. Seismometer
            var seismo = Station("Seismometer", new Vector3(18f, 0f, 12f), 1.6f, new Vector3(0.5f, 0.15f, 0f));
            seismo.prompt = "Deploy seismometer";
            seismo.powerCost = 40f; seismo.oxygenCost = 3f; seismo.scienceGain = 40f;
            seismo.completesObjective = "seismo";
            Placeholder.Prim(PrimitiveType.Cylinder, "Base", seismo.transform, new Vector3(0f, 0.05f, 0f), new Vector3(1f, 0.05f, 1f), grey, false);
            Placeholder.Prim(PrimitiveType.Sphere, "Dome", seismo.transform, new Vector3(0f, 0.3f, 0f), Vector3.one * 0.65f, metal, false);
            keepClear.Add(seismo.transform.position);

            // 2 + 3. Core sample sites
            MakeSampleSite("Core Sample Site A", new Vector3(-14f, 0f, 22f), "sampleA", brown, grey);
            MakeSampleSite("Core Sample Site B", new Vector3(25f, 0f, -18f), "sampleB", brown, grey);

            // 4. Solar charger (power for oxygen)
            var solar = Station("Portable Solar Charger", new Vector3(6f, 0f, -10f), 2f, new Vector3(1.3f, 0.3f, 0f));
            solar.prompt = "Recharge from solar panel";
            solar.powerGain = 25f; solar.oxygenCost = 6f;
            Placeholder.Prim(PrimitiveType.Cylinder, "Stand", solar.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.07f, 0.45f, 0.07f), grey, false);
            Placeholder.Prim(PrimitiveType.Cube, "Panel", solar.transform, new Vector3(0f, 0.95f, 0f), new Vector3(2.2f, 0.05f, 1.4f), blue, false, new Vector3(-35f, 0f, 0f));
            keepClear.Add(solar.transform.position);

            // 5. Comms relay (optional)
            var relay = Station("Comms Relay", new Vector3(-22f, 0f, -8f), 3.9f, new Vector3(0.55f, 0.4f, 0f));
            relay.prompt = "Set up comms relay";
            relay.powerCost = 20f; relay.oxygenCost = 2f; relay.scienceGain = 20f;
            relay.completesObjective = "relay"; relay.isOptional = true;
            Placeholder.Prim(PrimitiveType.Cube, "Base", relay.transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.8f, 0.3f, 0.8f), grey, false);
            Placeholder.Prim(PrimitiveType.Cylinder, "Mast", relay.transform, new Vector3(0f, 1.6f, 0f), new Vector3(0.08f, 1.5f, 0.08f), metal, true);
            Placeholder.Prim(PrimitiveType.Sphere, "Dish", relay.transform, new Vector3(0f, 3.1f, 0f), new Vector3(0.9f, 0.12f, 0.9f), metal, false, new Vector3(60f, 0f, 0f));
            keepClear.Add(relay.transform.position);

            // 6. Airlock pad (finish)
            var air = Station("Lander Airlock", new Vector3(0f, 0f, 7.4f), 2.6f, new Vector3(1.3f, 0.2f, 0f));
            air.prompt = "Enter airlock and end EVA";
            air.isFinish = true;
            air.requires = new[] { "seismo", "sampleA", "sampleB" };
            Placeholder.Prim(PrimitiveType.Cylinder, "Pad", air.transform, new Vector3(0f, 0.03f, 0f), new Vector3(2.4f, 0.03f, 2.4f),
                             Placeholder.Mat(new Color(0.1f, 0.5f, 0.45f)), false);
        }

        void MakeSampleSite(string name, Vector3 pos, string objective, Material rockMat, Material metal)
        {
            var site = Station(name, pos, 2.6f, new Vector3(0.6f, 0.4f, 0f));
            site.prompt = "Drill core sample";
            site.powerCost = 15f; site.oxygenCost = 2f; site.scienceGain = 15f;
            site.completesObjective = objective;
            Placeholder.Prim(PrimitiveType.Sphere, "SampleRock1", site.transform, new Vector3(0.0f, 0.15f, 0.2f), new Vector3(0.7f, 0.4f, 0.6f), rockMat, false);
            Placeholder.Prim(PrimitiveType.Sphere, "SampleRock2", site.transform, new Vector3(-0.4f, 0.1f, -0.2f), new Vector3(0.5f, 0.3f, 0.4f), rockMat, false);
            Placeholder.Prim(PrimitiveType.Cylinder, "DrillPole", site.transform, new Vector3(0.3f, 1f, 0f), new Vector3(0.08f, 1f, 0.08f), metal, false);
            Placeholder.Prim(PrimitiveType.Cube, "DrillHead", site.transform, new Vector3(0.3f, 2f, 0f), new Vector3(0.35f, 0.25f, 0.35f), metal, false);
            keepClear.Add(pos);
        }

        // ---------- player + camera ----------
        void BuildPlayerAndCamera()
        {
            var playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(0f, 0.1f, 0f);
            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.9f; cc.radius = 0.3f; cc.center = new Vector3(0f, 0.95f, 0f);
            cc.stepOffset = 0.3f; cc.skinWidth = 0.05f;
            var pc = playerGo.AddComponent<PlayerController>();
            pc.rig = AstronautPlaceholder.Build(playerGo.transform);
            Placeholder.SetLayerRecursive(playerGo, 2); // Ignore Raycast: camera spring-arm skips the player

            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1500f;
            cam.fieldOfView = 60f;
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            var tpc = cam.GetComponent<ThirdPersonCamera>();
            if (tpc == null) tpc = cam.gameObject.AddComponent<ThirdPersonCamera>();
            tpc.target = playerGo.transform;
            pc.cam = tpc;
        }
    }
}
