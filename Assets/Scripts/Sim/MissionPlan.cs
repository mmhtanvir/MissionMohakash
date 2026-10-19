using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mohakash
{
    public enum Activity { Sleep, Setup, Seismometer, Radiation, Samples, Shelter, Power, Chores, CloseOut }

    /// <summary>The player's own 72 h schedule, chosen before launch: one activity per 6 h block. Drives the HUD "My plan" list.</summary>
    public class MissionPlan
    {
        public const int BlockH = 6, Blocks = (int)MissionModel.DurationH / BlockH;
        public const float SleepNeededH = 4f;   // a sleep block counts as done after 4 h at the bunk
        public readonly Activity[] At = new Activity[Blocks];
        readonly float[] slept = new float[Blocks];
        float lastHour;

        public static string Name(Activity a) => a switch
        {
            Activity.Sleep       => "Sleep at the bunk",
            Activity.Setup       => "Set up the habitat (Tasks A + B)",
            Activity.Seismometer => "Spacewalk: place the seismometer (C + D)",
            Activity.Radiation   => "Spacewalk: log a radiation reading",
            Activity.Samples     => "Spacewalk: collect rock samples",
            Activity.Shelter     => "Shelter from the solar storm (Task E)",
            Activity.Power       => "Save power through the shadow (Task F)",
            Activity.Chores      => "Eat, wash and check systems",
            _                    => "Close out and send the report (Task G)",
        };

        /// <summary>Procedure tasks that belong to an activity, in order (the first unfinished one is shown on the HUD).</summary>
        public static string TaskIds(Activity a) => a switch
        {
            Activity.Setup => "AB", Activity.Seismometer => "CD", Activity.Radiation => "C", Activity.Samples => "C",
            Activity.Shelter => "E", Activity.Power => "F", Activity.CloseOut => "G", _ => "",
        };

        public static bool Eva(Activity a) => a == Activity.Seismometer || a == Activity.Radiation || a == Activity.Samples;

        /// <summary>Activities the loadout makes possible (no seismometer packed = no seismometer spacewalk).</summary>
        public static List<Activity> Available(Loadout kit) =>
            ((Activity[])Enum.GetValues(typeof(Activity))).Where(a =>
                (a != Activity.Seismometer || kit.Has("SEIS")) &&
                (a != Activity.Radiation || kit.Has("RAD")) &&
                (a != Activity.Samples || kit.Has("SAMP"))).ToList();

        public static int From(int b) => b * BlockH;
        public static string Hours(int b) => $"h{From(b)}-{From(b) + BlockH}";
        public static int BlockAt(float hour) => Mathf.Clamp((int)(hour / BlockH), 0, Blocks - 1);
        static bool Overlaps(int b, float s, float e) => From(b) < e && From(b) + BlockH > s;

        /// <summary>Scheduled mission events inside a block (shown in the planner and the HUD).</summary>
        public static string Events(int b)
        {
            var ev = new List<string>();
            if (Overlaps(b, MissionModel.SpeStart, MissionModel.SpeEnd)) ev.Add($"Solar storm h{MissionModel.SpeStart:0}-{MissionModel.SpeEnd:0}");
            if (Overlaps(b, MissionModel.ShadowStart, MissionModel.ShadowEnd)) ev.Add($"Shadow (no sun) h{MissionModel.ShadowStart:0}-{MissionModel.ShadowEnd:0}");
            if (Overlaps(b, MissionModel.QuakeHour, MissionModel.QuakeHour + 0.1f)) ev.Add($"Moonquake ~h{MissionModel.QuakeHour:0}");
            if (b == Blocks - 1) ev.Add("Ride home h72");
            return string.Join(", ", ev);
        }

        /// <summary>Suggested plan: setup, spacewalks early, shelter/power on the event blocks, 4 sleep blocks (24 h).</summary>
        public static MissionPlan Suggested(Loadout kit)
        {
            var p = new MissionPlan();
            var s = new[] { Activity.Setup, Activity.Seismometer, Activity.Radiation, Activity.Shelter, Activity.Sleep, Activity.Samples,
                            Activity.Power, Activity.Sleep, Activity.Chores, Activity.Sleep, Activity.CloseOut, Activity.Sleep };
            var ok = Available(kit);
            for (int i = 0; i < Blocks; i++) p.At[i] = ok.Contains(s[i]) ? s[i] : Activity.Chores;
            return p;
        }

        /// <summary>Plain-language problems with the plan. Advice only: the player may launch anyway.</summary>
        public List<string> Warnings(Loadout kit)
        {
            var w = new List<string>();
            int sleep = At.Count(a => a == Activity.Sleep);
            if (sleep < 3) w.Add($"Only {sleep * BlockH} h of sleep planned. Astronauts sleep about 8 h a day: plan 3-4 sleep blocks or fatigue will hurt you.");
            int firstEva = Array.FindIndex(At, Eva), setup = Array.IndexOf(At, Activity.Setup);
            if (setup < 0) w.Add("No habitat setup planned. Task A must be done before any spacewalk.");
            else if (firstEva >= 0 && firstEva < setup) w.Add("A spacewalk is planned before the habitat setup. Task A unlocks the spacewalk, so set up first.");
            for (int b = 0; b < Blocks; b++)
            {
                if (Eva(At[b]) && Overlaps(b, MissionModel.SpeStart, MissionModel.SpeEnd))
                    w.Add($"{Hours(b)}: spacewalk during the solar storm. Outside, the radiation dose is high.");
                if (Eva(At[b]) && Overlaps(b, MissionModel.ShadowStart, MissionModel.ShadowEnd))
                    w.Add($"{Hours(b)}: spacewalk in the shadow window. No sunlight and the battery is tight.");
            }
            if (!Enumerable.Range(0, Blocks).Any(b => At[b] == Activity.Shelter && Overlaps(b, MissionModel.SpeStart, MissionModel.SpeEnd)))
                w.Add($"Plan 'Shelter' for the block with the solar storm (h{MissionModel.SpeStart:0}).");
            if (!Enumerable.Range(0, Blocks).Any(b => At[b] == Activity.Power && Overlaps(b, MissionModel.ShadowStart - 4f, MissionModel.ShadowEnd)))
                w.Add($"Plan 'Save power' just before or during the shadow (h{MissionModel.ShadowStart:0}-{MissionModel.ShadowEnd:0}).");
            int seis = Array.IndexOf(At, Activity.Seismometer);
            if (kit.Has("SEIS") && (seis < 0 || From(seis) > MissionModel.DurationH - MissionModel.SeisNeededHours - BlockH))
                w.Add($"The seismometer needs {MissionModel.SeisNeededHours:0} h of recording: place it before hour {MissionModel.DurationH - MissionModel.SeisNeededHours - BlockH:0}.");
            if (kit.Has("RAD") && !At.Contains(Activity.Radiation)) w.Add("No radiation reading planned. It is a main objective.");
            int close = Array.IndexOf(At, Activity.CloseOut);
            if (close < 0) w.Add("No close-out planned. The final report is part of your score.");
            else if (From(close) + BlockH <= MissionModel.ShadowEnd) w.Add($"Close-out only opens after hour {MissionModel.ShadowEnd:0}. Move it later.");
            return w;
        }

        /// <summary>Called every frame: counts time spent resting in each block.</summary>
        public void Track(MissionModel mo)
        {
            if (mo.Resting && mo.Hour > lastHour) slept[BlockAt(lastHour)] += mo.Hour - lastHour;
            lastHour = mo.Hour;
        }

        public float Slept(int b) => slept[b];

        public bool Done(int b, MissionModel mo)
        {
            var a = At[b];
            if (a == Activity.Sleep) return slept[b] >= SleepNeededH;
            if (a == Activity.Radiation) return mo.RadReadings > 0;
            if (a == Activity.Samples) return mo.S1Samples;
            if (a == Activity.Chores) return mo.Hour >= From(b) + BlockH;
            return TaskIds(a).All(id => mo.Tasks.Get(id.ToString()).State == TaskState.Passed);
        }
    }
}
