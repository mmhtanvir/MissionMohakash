using System.Collections.Generic;
using UnityEngine;

namespace Mohakash
{
    /// <summary>Post-mission score (GDD §26) and the learning report (GDD §25.1).</summary>
    public class MissionReport
    {
        public readonly List<(string name, float points, float max)> Breakdown = new List<(string, float, float)>();
        public int Score, Stars;
        public string Title, Summary;
        public readonly List<string> Suggestions = new List<string>();

        public MissionReport(MissionModel m)
        {
            float science = (m.SeisDeployed && m.SeisDistance >= MissionModel.SeisMinDistance ? 2 : 0) + (m.QuakeRecorded ? 5 : 0)
                            + Mathf.Min(m.RadReadings, 2) + 2 * m.SamplesDelivered;
            float reserves = ((m.CabinO2 / m.CabinO2Cap) + (m.Water / m.WaterCap) + (m.BatteryCap > 0 ? m.BatteryPct : 0f)) / 3f;
            int optional = (m.S1Samples ? 1 : 0) + (m.S2Oxygen ? 1 : 0) + (m.S3SuitOk ? 1 : 0);
            float local = m.O2Consumed > 0 ? (m.O2Produced - m.O2Vented) / m.O2Consumed : 0f;
            float time = m.PrimaryDoneHour < 0 ? 0f : m.PrimaryDoneHour <= 0.8f * MissionModel.DurationH ? 5f
                         : 5f * Mathf.InverseLerp(MissionModel.DurationH, 0.8f * MissionModel.DurationH, m.PrimaryDoneHour);

            Add("Mission objectives", 30f * m.PrimaryDone / 3f, 30);
            Add("Scientific success", 15f * Mathf.Min(1f, science / 12f), 15);
            Add("Safety", Mathf.Max(0f, 12f - 2f * m.CriticalEvents - 4f * m.NearFailures - Mathf.Max(0f, m.Dose - MissionModel.DoseAllowance)), 12);
            Add("Resource efficiency", 10f * Mathf.Min(1f, reserves / 0.4f), 10);
            Add("Equipment preservation", 8f * m.SuitIntegrity / 100f, 8);
            Add("Exploration", 5f * Mathf.Min(1f, m.FarthestFromBase / 150f), 5);
            Add("Optional objectives", 10f * optional / 3f, 10);
            Add("Sustainability (O2 made locally)", 5f * Mathf.Min(1f, Mathf.Max(0f, local) / 0.3f), 5);
            Add("Time efficiency", time, 5);

            float total = 0; foreach (var b in Breakdown) total += b.points;
            if (m.Failed) total = Mathf.Min(total, 40f);   // GDD: failed mission capped at 40, "Learning Mode"
            Score = Mathf.RoundToInt(total);
            Stars = Score >= 85 ? 3 : Score >= 70 ? 2 : Score >= 50 ? 1 : 0;

            if (m.Failed) { Title = "MISSION FAILED  -  Learning Mode"; Summary = "Cause: " + m.FailCause + "."; }
            else if (m.PrimaryDone == 3) { Title = "MISSION COMPLETE  -  First Light"; Summary = "You proved the outpost basics work: power chain, breathable air and a quiet seismometer."; }
            else { Title = "MISSION ENDED  -  objectives incomplete"; Summary = $"You survived, but met {m.PrimaryDone} of 3 primary objectives."; }

            BuildSuggestions(m);
        }

        void Add(string n, float p, float max) => Breakdown.Add((n, p, max));

        void BuildSuggestions(MissionModel m)
        {
            bool batteryShort = m.CauseChain.Exists(c => c.Contains("shadow window"));
            if (m.BatteryCap <= 0f)
                Suggestions.Add("Bring at least one battery: without storage every machine stops the moment the sun is blocked.");
            else if (batteryShort || m.BatteryCap < 20f && m.Events.Exists(e => e.Text.Contains("lost power")))
                Suggestions.Add("Charge the battery to full before hour 40 and switch OGS/COM off during the shadow, or trade something (e.g. the spare kit + water) for a second battery.");
            if (!m.P1Seismometer)
                Suggestions.Add(m.SeisDeployed && m.SeisDistance < MissionModel.SeisMinDistance
                    ? $"Your seismometer was {m.SeisDistance:0} m out. Place it at least {MissionModel.SeisMinDistance:0} m away (follow the orange ring) and deploy it early: it needs {MissionModel.SeisNeededHours:0} powered hours."
                    : $"Deploy the seismometer early (it needs {MissionModel.SeisNeededHours:0} powered hours) and at least {MissionModel.SeisMinDistance:0} m from the habitat.");
            if (!m.P2Radiation) Suggestions.Add("Log at least one radiation reading at the monitor post next to the habitat (needs the RAD monitor in your loadout).");
            if (m.CauseChain.Exists(c => c.Contains("CO2") || c.Contains("cartridge"))) Suggestions.Add("Bring the CO2 scrubber with 2 cartridges: the crew makes about 3 kg of CO2 in 72 h.");
            if (m.CauseChain.Exists(c => c.Contains("suit") || c.Contains("Suit"))) Suggestions.Add("Watch the walk-back circle: turn around while the suit time still covers the walk home, or take the rover.");
            if (m.Dose > 10f) Suggestions.Add("Stay inside during the solar particle event (hours 22-25). The RAD monitor gives a 10-minute warning.");
            if (m.O2Vented > 0.2f) Suggestions.Add($"{m.O2Vented:0.0} kg of oxygen was vented because the cabin was full: an O2 tank stores it (generation is not storage).");
            if (Suggestions.Count == 0) Suggestions.Add("Try a lighter loadout with a second battery, or record the moonquake at hour 52 for bonus science.");
        }

        /// <summary>"Real world / In the game / Simplified" info cards (GDD §27).</summary>
        public static readonly (string topic, string real, string game)[] RealVsGame =
        {
            ("Solar power + shadow", "Near the lunar poles the Sun skims the horizon; ridges get long sunlight, crater floors stay dark (LOLA illumination studies).",
                "SOL-S makes 1.0 PW at full sun; a scripted 6 h shadow window at hour 40. Game units."),
            ("Electrolysis", "Electricity splits water into oxygen and hydrogen (real chemistry: ~1.125 kg water per kg O2).",
                "OGS: 1.0 PW + 0.1125 L/h water -> 0.10 kg O2/h. Hydrogen is vented for simplicity."),
            ("Suit as life support", "A spacesuit is a small, short-lived habitat; Apollo planned a 'walk-back limit'.",
                "Suit A: O2 4.5 h, battery 4.2 h, CO2 4.0 h; the shortest timer is your EVA limit. Cyan ring = walk-back circle."),
            ("Seismometer placement", "Apollo and InSight seismometers needed quiet ground away from vibration sources.",
                "Must be >= 80 m from the habitat (GDD 8 cells, 1 cell = 10 m in 3D). Moonquake window at hour 52 (timing dataset TBD)."),
            ("Radiation", "No atmosphere or magnetic field on the Moon; solar particle events raise dose quickly (VERIFY figures).",
                "Game-mSv: 20 per mission allowance. Outside during the event: 6 game-mSv/h. Not a safety limit."),
            ("Terrain", "Real mission planning uses LRO LOLA elevation, slope and roughness maps.",
                "This build's terrain is hand-made 'inspired by' a polar ridge, not real LOLA data yet (next milestone)."),
        };
    }
}
