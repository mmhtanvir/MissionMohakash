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
        public const int BlockH = 6;
        public static int Blocks => Mathf.CeilToInt(MissionModel.DurationH / BlockH);
        public const float SleepNeededH = 4f;   // a sleep block counts as done after 4 h at the bunk
        public readonly Activity[] At = new Activity[Blocks];
        readonly float[] slept = new float[Blocks];
        float lastHour;

        public static string Name(Activity a) => MissionModel.Lvl.Tutorial ? FullName(a) : System.Text.RegularExpressions.Regex.Replace(FullName(a), @" \((Tasks? )?[A-G]( \+ [A-G])*\)", "");

        static string FullName(Activity a) => a switch
        {
            Activity.Sleep       => "Sleep",
            Activity.Setup       => "Set up the base (Tasks A + B)",
            Activity.Seismometer => "Go outside: set up the quake sensor (C + D)",
            Activity.Radiation   => "Go outside: read the radiation meter",
            Activity.Samples     => "Go outside: pick up rocks",
            Activity.Shelter     => "Hide inside from the solar storm (Task E)",
            Activity.Power       => "Save power while it is dark (Task F)",
            Activity.Chores      => "Eat, wash and check the base",
            _                    => "Pack up and send your report (Task G)",
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
        public static string Hours(int b) => $"Hour {From(b)}-{From(b) + BlockH}";
        public static int BlockAt(float hour) => Mathf.Clamp((int)(hour / BlockH), 0, Blocks - 1);
        static bool Overlaps(int b, float s, float e) => From(b) < e && From(b) + BlockH > s;

        /// <summary>Scheduled mission events inside a block (shown in the planner and the HUD).</summary>
        public static string Events(int b)
        {
            var ev = new List<string>();
            var lv = MissionModel.Lvl;
            if (lv.StormStart >= 0f && Overlaps(b, lv.StormStart, lv.StormEnd)) ev.Add("Dust storm");
            foreach (var (fh, _) in lv.Faults) if (Overlaps(b, fh, fh + 0.1f)) ev.Add($"A machine breaks (hour {fh:0})");
            if (MissionModel.SpeStart >= 0f && Overlaps(b, MissionModel.SpeStart, MissionModel.SpeEnd)) ev.Add($"Solar storm (hour {MissionModel.SpeStart:0}-{MissionModel.SpeEnd:0})");
            for (float s0 = MissionModel.DarkStart(From(b)); s0 < From(b) + BlockH; s0 = MissionModel.DarkStart(s0 + MissionModel.ShadowLength))
            {
                ev.Add($"Dark, no sun (hour {s0:0}-{s0 + MissionModel.ShadowLength:0})");
                if (lv.NightEvery <= 0f) break;
            }
            if (Overlaps(b, MissionModel.QuakeHour, MissionModel.QuakeHour + 0.1f)) ev.Add($"Moonquake (hour {MissionModel.QuakeHour:0})");
            if (b == Blocks - 1) ev.Add($"Ride home (hour {MissionModel.DurationH:0})");
            return string.Join(", ", ev);
        }

        /// <summary>Suggested plan: setup, spacewalks early, shelter/power on the event blocks, 4 sleep blocks (24 h).</summary>
        public static MissionPlan Suggested(Loadout kit)
        {
            var p = new MissionPlan();
            Activity[] s = { Activity.Setup, Activity.Seismometer, Activity.Radiation, Activity.Shelter, Activity.Sleep, Activity.Samples,
                            Activity.Power, Activity.Sleep, Activity.Chores, Activity.Sleep, Activity.CloseOut, Activity.Sleep };
            var ok = Available(kit);
            if (!MissionModel.Lvl.Tutorial)
            {
                // any length: every 4th block is sleep, hazards get shelter / power, early blocks do the level's spacewalks
                var lv = MissionModel.Lvl;
                var evas = new List<Activity>();
                if (lv.SeisGoal) evas.Add(Activity.Seismometer);
                if (lv.SampleGoal) { evas.Add(Activity.Samples); evas.Add(Activity.Samples); }
                if (lv.RadGoal) evas.Add(Activity.Radiation);
                s = new Activity[Blocks];
                for (int i = 0; i < Blocks; i++) s[i] = i % 4 == 3 ? Activity.Sleep : Activity.Chores;
                s[0] = Activity.Setup;
                for (int i = 0; i < Blocks; i++)
                {
                    if (s[i] == Activity.Sleep) continue;
                    if (MissionModel.SpeStart >= 0f && Overlaps(i, MissionModel.SpeStart, MissionModel.SpeEnd)) s[i] = Activity.Shelter;
                    else if (MissionModel.DarkAt(From(i) + BlockH - 0.01f) || MissionModel.DarkAt(From(i))) s[i] = Activity.Power;
                }
                for (int i = 1, e = 0; i < Blocks && e < evas.Count; i++) if (s[i] == Activity.Chores) s[i] = evas[e++];
                if (s[Blocks - 1] != Activity.Sleep) s[Blocks - 1] = Activity.CloseOut;
            }
            for (int i = 0; i < Blocks; i++) p.At[i] = ok.Contains(s[i]) ? s[i] : Activity.Chores;
            return p;
        }

        /// <summary>Plain-language problems with the plan. Advice only: the player may launch anyway.</summary>
        public List<string> Warnings(Loadout kit)
        {
            var w = new List<string>();
            int sleep = At.Count(a => a == Activity.Sleep);
            if (sleep < Mathf.Max(2, Blocks / 4)) w.Add($"You only sleep {sleep * BlockH} hours. Pick Sleep at least {Mathf.Max(2, Blocks / 4)} times, or you will get very tired.");
            int firstEva = Array.FindIndex(At, Eva), setup = Array.IndexOf(At, Activity.Setup);
            var lvl = MissionModel.Lvl;
            if (!lvl.Tutorial) setup = -2;   // no Task A gate outside the tutorial
            if (setup == -1) w.Add("Pick 'Set up the base' first. You can't go outside before that.");
            else if (setup >= 0 && firstEva >= 0 && firstEva < setup) w.Add("You go outside before setting up the base. Set up the base first.");
            for (int b = 0; b < Blocks; b++)
            {
                if (Eva(At[b]) && Overlaps(b, MissionModel.SpeStart, MissionModel.SpeEnd))
                    w.Add($"{Hours(b)}: going outside in the solar storm is dangerous!");
                if (Eva(At[b]) && Overlaps(b, MissionModel.ShadowStart, MissionModel.ShadowEnd))
                    w.Add($"{Hours(b)}: going outside in the dark. There is no sun and the battery runs low.");
            }
            if (MissionModel.SpeStart >= 0f && !Enumerable.Range(0, Blocks).Any(b => At[b] == Activity.Shelter && Overlaps(b, MissionModel.SpeStart, MissionModel.SpeEnd)))
                w.Add($"Pick 'Hide inside from the solar storm' at hour {MissionModel.SpeStart:0}.");
            if (!Enumerable.Range(0, Blocks).Any(b => At[b] == Activity.Power && Overlaps(b, MissionModel.ShadowStart - 4f, MissionModel.ShadowEnd)))
                w.Add($"Pick 'Save power while it is dark' near hour {MissionModel.ShadowStart:0}.");
            int seis = Array.IndexOf(At, Activity.Seismometer);
            if (lvl.SeisGoal && kit.Has("SEIS") && (seis < 0 || From(seis) > MissionModel.DurationH - MissionModel.SeisNeededHours - BlockH))
                w.Add($"The quake sensor needs {MissionModel.SeisNeededHours:0} hours to listen. Set it up before hour {MissionModel.DurationH - MissionModel.SeisNeededHours - BlockH:0}.");
            if (lvl.RadGoal && kit.Has("RAD") && !At.Contains(Activity.Radiation)) w.Add("Pick 'Go outside: read the radiation meter'. It is one of your goals!");
            int close = Array.IndexOf(At, Activity.CloseOut);
            if (!lvl.Tutorial) { }
            else if (close < 0) w.Add("Pick 'Pack up and send your report' at the end. It counts for your score.");
            else if (From(close) + BlockH <= MissionModel.ShadowEnd) w.Add($"'Pack up and send your report' only works after hour {MissionModel.ShadowEnd:0}. Move it later.");
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
            if (a == Activity.Chores || !mo.Tasks.Enabled) return mo.Hour >= From(b) + BlockH;
            return TaskIds(a).All(id => mo.Tasks.Get(id.ToString()).State == TaskState.Passed);
        }
    }
}
