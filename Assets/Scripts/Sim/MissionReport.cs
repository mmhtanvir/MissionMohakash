using System.Linq;
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

            Add("Goals", 30f * m.PrimaryDone / Levels.PrimaryTotal, 30);
            Add("Science", 15f * Mathf.Min(1f, science / 12f), 15);
            Add("Staying safe", Mathf.Max(0f, 12f - 2f * m.CriticalEvents - 4f * m.NearFailures - Mathf.Max(0f, m.Dose - MissionModel.DoseAllowance)), 12);
            Add("Saving supplies", 10f * Mathf.Min(1f, reserves / 0.4f), 10);
            Add("Looking after your suit", 8f * m.SuitIntegrity / 100f, 8);
            Add("Exploring", 5f * Mathf.Min(1f, m.FarthestFromBase / 150f), 5);
            Add("Bonus goals", 10f * optional / 3f, 10);
            Add("Making your own air", 5f * Mathf.Min(1f, Mathf.Max(0f, local) / 0.3f), 5);
            Add("Finishing early", time, 5);

            Add("Doing it yourself (no supply calls)", 5f * (1f - m.Gateway.Calls / (float)Resupply.MaxCalls), 5);

            if (m.Tasks != null && m.Tasks.Enabled) { m.Tasks.Finish(); Add("Steps done (Tasks A-G)", 5f * m.Tasks.Completion, 5); }

            float total = 0, max = 0;
            foreach (var b in Breakdown) { total += b.points; max += b.max; }
            total *= 100f / max;   // keeps the score on a 0-100 scale regardless of which items were added
            if (m.Failed) total = Mathf.Min(total, 40f);   // GDD: failed mission capped at 40, "Learning Mode"
            Score = Mathf.RoundToInt(total);
            Stars = Score >= 85 ? 3 : Score >= 70 ? 2 : Score >= 50 ? 1 : 0;

            if (m.Failed) { Title = "MISSION OVER  -  let's try again!"; Summary = "What went wrong: " + m.FailCause + "."; }
            else if (m.AllPrimaryDone) { Title = $"MISSION {Levels.Current.N} DONE!  -  {Levels.Current.Name}"; Summary = "You did every goal. Great work, astronaut!"; }
            else { Title = "TIME'S UP!"; Summary = $"You stayed safe and did {m.PrimaryDone} of {Levels.PrimaryTotal} goals."; }

            BuildSuggestions(m);
        }

        void Add(string n, float p, float max) => Breakdown.Add((n, p, max));

        void BuildSuggestions(MissionModel m)
        {
            bool batteryShort = m.CauseChain.Exists(c => c.Contains("dark"));
            if (m.BatteryCap <= 0f)
                Suggestions.Add("Pack a battery. Without one, every machine stops when it gets dark.");
            else if (batteryShort || m.BatteryCap < 20f && m.Events.Exists(e => e.Text.Contains("turned off:")))
                Suggestions.Add("Fill the battery before it gets dark, and switch off the oxygen maker and radio while it is dark. A second battery helps too.");
            var lv = Levels.Current;
            if (lv.SeisGoal && !m.P1Seismometer)
                Suggestions.Add(lv.SeisSite != null ? $"Put the quake sensor inside the orange circle, and do it early: it needs {MissionModel.SeisNeededHours:0} hours to listen."
                    : m.SeisDeployed && m.SeisDistance < MissionModel.SeisMinDistance
                    ? $"Your quake sensor was too close to the base. Carry it past the orange ring, and do it early: it needs {MissionModel.SeisNeededHours:0} hours to listen."
                    : $"Set up the quake sensor early, past the orange ring. It needs {MissionModel.SeisNeededHours:0} hours to listen.");
            if (lv.RadGoal && !m.P2Radiation) Suggestions.Add("Read the radiation meter next to the base at least once.");
            if (m.CauseChain.Exists(c => c.Contains("CO2") || c.Contains("cartridge"))) Suggestions.Add("Pack the air cleaner and its air filters. You breathe out bad air (CO2) all the time!");
            if (m.CauseChain.Exists(c => c.Contains("suit") || c.Contains("Suit"))) Suggestions.Add("Watch your suit air and head home early, or take the rover. Stay inside the blue ring.");
            if (m.Dose > 10f) Suggestions.Add($"Stay inside during the solar storm (hour {MissionModel.SpeStart:0} to {MissionModel.SpeEnd:0}).");
            if (lv.SampleGoal && lv.Objectives.Any(o => !o.Bonus && !o.Done(m) && o.Label(m).Contains("rocks")))
                Suggestions.Add("Take the rover to the far blue lights, pick up several rocks in one trip, and bring them inside.");
            if (lv.Mars && m.O2Produced < 4f && lv.N == 5) Suggestions.Add("Turn the oxygen maker ON (Tab) and give it lots of power. An extra solar panel helps.");
            if (lv.Faults.Length > 0 && m.FaultsFixed < lv.Faults.Length) Suggestions.Add("Fix broken machines right away (Tab, then FIX). Spare parts kits fix them instantly.");
            if (m.O2Vented > 0.2f) Suggestions.Add("Some air was wasted because the tank was full. Pack an air tank to save it.");
            if (Suggestions.Count == 0) Suggestions.Add("Great job! Try packing differently next time, or go for every bonus goal.");
        }

        /// <summary>"Real world / In the game / Simplified" info cards (GDD §27).</summary>
        public static readonly (string topic, string real, string game)[] RealVsGame =
        {
            ("Sun and dark", "Near the Moon's south pole the sun stays low in the sky. Hills get lots of sun, but deep craters stay dark.",
                "Solar panels work in the sun. When it gets dark, only the battery keeps the base running."),
            ("Making air", "Electricity can split water into oxygen (to breathe) and hydrogen. On Mars, NASA's MOXIE made oxygen from Mars air.",
                "The oxygen maker uses power to make air: from water on the Moon, from Mars air on Mars."),
            ("Spacesuits", "A spacesuit is like a tiny spaceship you wear. It only has air for a few hours.",
                "Your suit has air for about 8 hours. Go back before it runs out!"),
            ("Quake sensors", "Apollo astronauts put quake sensors on the Moon. They need quiet ground far from noisy machines.",
                "Put the sensor far from the base, so the base's shaking doesn't hide the quakes."),
            ("Radiation", "The Moon has no air to block radiation from the sun. Solar storms can be dangerous.",
                "Stay inside during a solar storm. The base keeps you safe."),
            ("The ground", "Real missions use maps made by spacecraft flying around the Moon.",
                "The hills in this game are made up, but shaped like a real Moon ridge."),
        };
    }
}
