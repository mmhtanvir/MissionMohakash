using System;
using System.Linq;
using UnityEngine;

namespace Mohakash
{
    /// <summary>One line on the objectives card / report. Label is re-read every frame so it can show progress.</summary>
    public class Objective
    {
        public Func<MissionModel, bool> Done;
        public Func<MissionModel, string> Label;
        public bool Bonus;
        public Objective(Func<MissionModel, bool> done, Func<MissionModel, string> label, bool bonus = false) { Done = done; Label = label; Bonus = bonus; }
    }

    /// <summary>
    /// Everything that makes one level different. All six levels run in the Mission1 scene with the same systems;
    /// a level only changes the clock, hazards, world (Moon / Mars), sites and objectives.
    /// </summary>
    public class LevelDef
    {
        public int N;
        public string Name, Narrative;
        public bool Mars;
        public bool Tutorial;                      // Task A-G procedures + EVA gates (level 1 only)
        public float DurationH = 72f;
        public float ShadowStart = 40f, ShadowEnd = 46f;
        public float NightEvery;                   // > 0: the dark window repeats every N hours (Mars nights)
        public float SpeStart = 22f, SpeEnd = 25f; // SpeStart < 0: no solar storm
        public float QuakeHour = 52f;
        public float SeisMinDistance = 80f, SeisNeededHours = 24f;
        public Vector2? SeisSite;                  // designated seismometer spot (must deploy within SeisSiteRadius)
        public const float SeisSiteRadius = 10f;
        public (string name, Vector2 xz)[] SampleSites =   // level 1's; every other level sets its own (SiteRules: no two levels share a spot)
            { ("A", new Vector2(46, 52)), ("B", new Vector2(-96, -44)), ("C", new Vector2(150, 0)) };
        public float StormStart = -1f, StormEnd = -1f;   // dust storm: dim sun, fast dust
        public (float hour, string load)[] Faults = new (float, string)[0];
        // Landing site: the outpost always sits at (0, 0), but each level lands on its own patch of the procedural terrain,
        // turned to its own heading, with its own rock scatter and its own lander spot.
        public Vector2 LandOffset; public float LandYaw;
        public Vector2 Lander = new Vector2(-30f, 18f); public float LanderYaw = 140f;
        public int RockSeed => 7 + 1000 * (N - 1);
        // Outpost yard (the habitat + airlock never move): which side the power/comms cluster and the garage sit on,
        // where the storage pallet stands and where the solar array line starts and which way it runs.
        public bool PowerNorth, GarageSouth;
        public Vector2 Storage = new Vector2(-6f, 11f); public float StorageYaw = 20f;
        public Vector2 SolarStart = new Vector2(-9f, -17f), SolarStep = new Vector2(5f, 0f);
        public string[] Steps;
        public bool SeisGoal, RadGoal, SampleGoal;   // set by the objective helpers below; the planner suggests these spacewalks
        public Objective[] Objectives;

        public bool InStorm(float h) => StormStart >= 0f && h >= StormStart && h < StormEnd;
        public string Place => Mars ? "MARS" : "MOON";
        public string Header => $"LEVEL {N:00}  {Name}  [ {Place} ]";
    }

    public static class Levels
    {
        static Objective Survive(LevelDef d) => new Objective(m => m.P3Survive, m => $"Stay safe until hour {d.DurationH:0}  <color=#8aa0aa>{m.Hour:0}/{d.DurationH:0} h</color>");
        static Objective Samples(LevelDef d, int n, bool bonus = false) { d.SampleGoal = true; return new Objective(m => m.SamplesDelivered >= n,
            m => $"{(bonus ? "Bonus: " : "")}Bring {n} rocks inside  <color=#8aa0aa>{m.SamplesDelivered}/{n}</color>", bonus); }
        static Objective Radiation(LevelDef d, bool bonus = false) { d.RadGoal = true; return new Objective(m => m.RadReadings >= 1,
            m => $"{(bonus ? "Bonus: " : "")}Read the radiation meter  <color=#8aa0aa>{m.RadReadings}/1</color>", bonus); }
        static Objective Seis(LevelDef d) { d.SeisGoal = true; return new Objective(m => m.P1Seismometer,
            m => $"Run the quake sensor for {d.SeisNeededHours:0} h  <color=#8aa0aa>{m.SeisGoodHours:0.0}/{d.SeisNeededHours:0} h{(m.SeisDeployed && !m.SeisAtSite ? (d.SeisSite != null ? ", NOT IN THE CIRCLE" : ", TOO CLOSE TO THE BASE") : "")}</color>"); }

        public static readonly LevelDef[] All = Build();

        static LevelDef[] Build()
        {
            var l1 = new LevelDef
            {
                N = 1, Name = "FIRST FLIGHT", Tutorial = true,
                Narrative = "You landed on the Moon! Your job: set up a quake sensor to listen for moonquakes, and stay safe until your ride home comes at hour 72.",
                Steps = new[] { "Pack your rocket.", "Grab the quake sensor box by the door.", "Carry it past the orange ring. Press E to set it up.",
                                "Read the radiation meter next to the base.", "Bonus: bring 2 rocks inside.", "Plan your day. Don't forget to sleep!",
                                "Go inside and keep the base running until hour 72." },
            };
            l1.Objectives = new[] { Seis(l1), Radiation(l1), Survive(l1), Samples(l1, 2, true) };

            var l2 = new LevelDef
            {
                N = 2, Name = "START THE OUTPOST", DurationH = 48f, ShadowStart = 30f, ShadowEnd = 36f, SpeStart = 14f, SpeEnd = 17f, QuakeHour = 38f,
                SeisNeededHours = 18f, SeisSite = new Vector2(98f, 19f),
                LandOffset = new Vector2(330f, -120f), LandYaw = 70f, Lander = new Vector2(-25f, -26f), LanderYaw = 60f,
                PowerNorth = true, GarageSouth = true, Storage = new Vector2(-6f, -12f), StorageYaw = -20f, SolarStart = new Vector2(-24f, -10f), SolarStep = new Vector2(0f, 5f),
                SampleSites = new[] { ("A", new Vector2(-35, 61)), ("B", new Vector2(-145, -39)), ("C", new Vector2(90, -141)) },
                Narrative = "Mission control found a quiet spot for your quake sensor. Take it there, set it up, and let it listen for 18 hours. Your ride home comes at hour 48.",
                Steps = new[] { "Pack your rocket.", "Grab the quake sensor box by the door.", "Follow the orange light to the circle.",
                                "Stand in the circle and press E to set it up.", "Read the radiation meter next to the base.",
                                "Keep the base running so the sensor can listen for 18 hours." },
            };
            l2.Objectives = new[] { Seis(l2), Radiation(l2), Survive(l2), Samples(l2, 2, true) };

            var l3 = new LevelDef
            {
                N = 3, Name = "LUNAR SAMPLE COLLECTION", DurationH = 48f, ShadowStart = 32f, ShadowEnd = 38f, SpeStart = 20f, SpeEnd = 23f, QuakeHour = 999f,
                LandOffset = new Vector2(-300f, 220f), LandYaw = 150f, Lander = new Vector2(30f, -22f), LanderYaw = 250f,
                Storage = new Vector2(12f, -14f), StorageYaw = 70f, SolarStart = new Vector2(-24f, -10f), SolarStep = new Vector2(0f, 5f),
                SampleSites = new[] { ("A", new Vector2(78, 45)), ("B", new Vector2(-134, 42)), ("C", new Vector2(0, -150)),
                                      ("D", new Vector2(-106, 106)), ("E", new Vector2(164, 44)) },
                Narrative = "Scientists on Earth want Moon rocks from 5 places. Each place has a blue light. Pick up a rock at each one and bring them all inside. Some are far away, so take the rover!",
                Steps = new[] { "Pack your rocket. The rover helps a lot!", "Go to a blue light and press E to pick up a rock.",
                                "Bring the rocks inside the base.", "Go inside to fill your suit with air between trips.", "Stay inside during the solar storm (hour 20 to 23)." },
            };
            l3.Objectives = new[] { Samples(l3, 5), Survive(l3), Radiation(l3, true) };

            var l4 = new LevelDef
            {
                N = 4, Name = "LUNAR SURVIVAL", DurationH = 120f, ShadowStart = 40f, ShadowEnd = 52f, SpeStart = 70f, SpeEnd = 74f, QuakeHour = 999f,
                LandOffset = new Vector2(220f, 320f), LandYaw = 230f, Lander = new Vector2(26f, 27f), LanderYaw = 200f,
                PowerNorth = true, Storage = new Vector2(-8f, -12f), StorageYaw = 40f, SolarStart = new Vector2(-4f, -18f), SolarStep = new Vector2(5f, 0f),
                SampleSites = new[] { ("A", new Vector2(68, -18)), ("B", new Vector2(-49, -184)), ("C", new Vector2(190, 0)) },
                Narrative = "Your ride home is late! You need to stay safe for 5 days (120 hours). It gets dark for 12 hours, and a solar storm comes on day 3. Make your air, water, food and power last.",
                Steps = new[] { "Pack extra batteries, water and air filters.", "Fill the battery before it gets dark at hour 40.",
                                "When it's dark (hour 40 to 52), switch off things you don't need.", "Stay inside during the solar storm (hour 70 to 74).",
                                "Running low? Call the space station for a supply drone.", "Keep the air tank above 2 kg at the end." },
            };
            l4.Objectives = new[] { Survive(l4),
                new Objective(m => m.Hour >= l4.DurationH && m.CabinO2 >= 2f, m => $"Keep the air tank above 2 kg at the end  <color=#8aa0aa>{m.CabinO2:0.0} kg</color>"),
                new Objective(m => m.Hour >= l4.DurationH && m.Health >= 50f, m => $"Bonus: finish with health 50% or more  <color=#8aa0aa>{m.Health:0}%</color>", true) };

            var l5 = new LevelDef
            {
                N = 5, Name = "MARS EXPLORATION", Mars = true, DurationH = 72f, ShadowStart = 18f, ShadowEnd = 24f, NightEvery = 24f, SpeStart = -1f, SpeEnd = -1f, QuakeHour = 999f,
                LandOffset = new Vector2(-260f, -280f), LandYaw = 300f, Lander = new Vector2(-30f, -14f), LanderYaw = 10f,
                GarageSouth = true, Storage = new Vector2(8f, 13f), StorageYaw = -30f, SolarStart = new Vector2(-12f, 17f), SolarStep = new Vector2(5f, 0f),
                SampleSites = new[] { ("A", new Vector2(95, -55)), ("B", new Vector2(-170, 0)), ("C", new Vector2(0, 130)) },
                Narrative = "Welcome to Mars! Here your oxygen maker makes air out of the Mars air, like NASA's MOXIE machine. Make 4 kg of oxygen and bring back 3 rocks. Every night it gets dark for 6 hours.",
                Steps = new[] { "Pack your rocket.", "Keep the oxygen maker on when you have spare power.",
                                "Pick up 3 rocks at the blue lights.", "Fill the battery before each night (hour 18, 42 and 66).", "Stay safe until hour 72." },
            };
            l5.Objectives = new[] { new Objective(m => m.O2Produced >= 4f, m => $"Make 4 kg of oxygen from Mars air  <color=#8aa0aa>{m.O2Produced:0.0}/4 kg</color>"),
                                    Samples(l5, 3), Survive(l5) };

            var l6 = new LevelDef
            {
                N = 6, Name = "DUST STORM SURVIVAL", Mars = true, DurationH = 72f, ShadowStart = 18f, ShadowEnd = 24f, NightEvery = 24f, SpeStart = -1f, SpeEnd = -1f, QuakeHour = 999f,
                StormStart = 8f, StormEnd = 56f,
                LandOffset = new Vector2(380f, 260f), LandYaw = 40f, Lander = new Vector2(-34f, -4f), LanderYaw = 100f,
                PowerNorth = true, GarageSouth = true, Storage = new Vector2(12f, -8f), StorageYaw = 90f, SolarStart = new Vector2(-10f, 20f), SolarStep = new Vector2(5f, 0f),
                SampleSites = new[] { ("A", new Vector2(-165, -95)), ("B", new Vector2(113, 65)), ("C", new Vector2(-200, 49)) },
                Faults = new[] { (14f, "SCR"), (30f, "OGS"), (44f, "SUI") },
                Narrative = "A big dust storm is coming! From hour 8 to 56 the sun is very weak and dust covers your solar panels. The storm will break some machines. Fix them fast!",
                Steps = new[] { "Pack extra batteries. The sun will be weak for 2 days.", "Wipe the dust off the solar panels (E).",
                                "When a machine breaks, press Tab and then FIX.", "A spare parts kit fixes it right away. Without one it takes 3 hours.",
                                "Stay safe until hour 72." },
            };
            l6.Objectives = new[] { Survive(l6),
                new Objective(m => m.FaultsFixed >= l6.Faults.Length, m => $"Fix the storm damage  <color=#8aa0aa>{m.FaultsFixed}/{l6.Faults.Length}</color>") };

            return new[] { l1, l2, l3, l4, l5, l6 };
        }

        public static LevelDef Current => All[Mathf.Clamp(Game.Level, 1, All.Length) - 1];
        public static int Primary(MissionModel m) => Current.Objectives.Count(o => !o.Bonus && o.Done(m));
        public static int PrimaryTotal => Current.Objectives.Count(o => !o.Bonus);
    }
}

namespace Mohakash
{
    /// <summary>Placement rules every level's sites must pass (checked by SimTests.LevelSitesAreUniqueAndReachable).</summary>
    public static class SiteRules
    {
        public const float MaxSlopeDeg = 12f, MinSpacing = 30f, MinFromBase = 60f, MaxFromBase = 260f;

        /// <summary>Worst slope over a radius-r patch, from the same height function the terrain mesh is built from.</summary>
        public static float SlopeDeg(float x, float z, float r)
        {
            float worst = 0f;
            for (int a = 0; a < 360; a += 45)
            {
                float dx = r * Mathf.Cos(a * Mathf.Deg2Rad), dz = r * Mathf.Sin(a * Mathf.Deg2Rad);
                float dh = MoonWorld.RawHeight(x + dx, z + dz) - MoonWorld.RawHeight(x, z);
                worst = Mathf.Max(worst, Mathf.Atan2(Mathf.Abs(dh), r) * Mathf.Rad2Deg);
            }
            return worst;
        }
    }
}
