using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mohakash
{
    public enum Spot { Auto, Systems, Comms, SuitDock, Door, Airlock, Shelter, Seis }
    public enum TaskState { Locked, Active, Passed, Failed }
    public struct StepCtx { public Vector3 Pos; public float Yaw, SlopeDeg; }

    public class TaskStep
    {
        public string Id, Text, Result; public Spot At; public bool Remote, Done;
        public Func<MissionModel, StepCtx, (bool ok, string msg)> Verify;
    }

    public class MissionTask
    {
        public string Id, Title, Location, Pass, Fail;
        public float From, To = float.MaxValue;
        public Func<MissionModel, bool> Unlock = x => true, PassExtra;
        public Action<MissionModel> OnFail;
        public readonly List<TaskStep> Steps = new List<TaskStep>();
        public TaskState State = TaskState.Locked;
        public float Progress => Steps.Count == 0 ? 0f : Steps.Count(s => s.Done) / (float)Steps.Count;
    }

    /// <summary>Stage-1 procedures (Tasks A-G). Steps verify real simulation state; nothing here touches scene objects.</summary>
    public class MissionTasks
    {
        public bool Enabled, AutoPass;
        public int Version;
        public Vector3 PlayerPos; public float ShelterDist = 99f;
        public Action<Severity, string> Say = (s, t) => { };
        public Func<bool> ManifestValid = () => true;
        public const float NorthYaw = 0f;
        public readonly List<MissionTask> All = new List<MissionTask>();
        public float Completion => All.Count == 0 ? 0f : All.Average(t => t.State == TaskState.Passed ? 1f : 0.5f * t.Progress);

        readonly MissionModel m;
        float doseAtSpe = -1f, riskQuality = 1f; bool essentialLost, surveyed, exitWarned; string siteMsg = "";

        public MissionTasks(MissionModel model) { m = model; Build(); }

        static (bool ok, string msg) Ok(string s) => (true, s);
        static (bool ok, string msg) No(string s) => (false, s);
        static float Dist(StepCtx c) => new Vector2(c.Pos.x, c.Pos.z).magnitude;
        public MissionTask Get(string id) => All.Find(t => t.Id == id);

        MissionTask T(string id, string title, string loc, string pass, string fail,
                      Func<MissionModel, bool> unlock = null, float from = 0f, float to = float.MaxValue)
        {
            var t = new MissionTask { Id = id, Title = title, Location = loc, Pass = pass, Fail = fail, From = from, To = to };
            if (unlock != null) t.Unlock = unlock;
            All.Add(t); return t;
        }

        static void S(MissionTask t, Spot at, string text,
                      Func<MissionModel, StepCtx, (bool ok, string msg)> v, bool remote = false)
            => t.Steps.Add(new TaskStep { Id = t.Id + (t.Steps.Count + 1), At = at, Text = text, Verify = v, Remote = remote });

        void Build()
        {
            const float SpeS = MissionModel.SpeStart, SpeE = MissionModel.SpeEnd;
            const float ShS  = MissionModel.ShadowStart, ShE = MissionModel.ShadowEnd;
            bool hasCom = m.Kit.Has("COM");   // without a comms console every Earth-link step is skipped, never a dead end

            // ---- A  Mission readiness checklist ----
            var a = T("A", "Mission readiness checklist", "Habitat console (power & life support)",
                "Every mandatory system has a verified status and the manifest is valid.",
                "Missing equipment locks out the related science objectives.");
            S(a, Spot.Systems, "Review the mission plan and hazard briefing", (x, c) =>
                Ok($"Briefing: solar particle event h{SpeS:0}-{SpeE:0}, shadow window h{ShS:0}-{ShE:0}, moonquake window near h{MissionModel.QuakeHour:0}. Mission length {MissionModel.DurationH:0} h."));
            S(a, Spot.Systems, "Check oxygen, water, food, power and battery reserves", (x, c) =>
            {
                bool low = x.CabinO2 < 3f || x.Water < 10f || x.BatteryPct < 0.3f;
                return Ok((low ? "LOW RESERVE: " : "Reserves nominal: ") +
                          $"O2 {x.CabinO2:0.0}/{x.CabinO2Cap:0} kg, water {x.Water:0}/{x.WaterCap:0}, food {x.Food:0}, power {x.Supply:0.0} in / {x.Demand:0.0} out, battery {x.BatteryPct:0%}.");
            });
            S(a, Spot.Systems, "Review the equipment inventory and its mass", (x, c) =>
            {
                float mass = 0, cost = 0; int units = 0;
                foreach (var d in Catalog.Mission1) { int n = x.Kit.Count(d.Id); units += n; mass += n * d.Mass; cost += n * d.Cost; }
                return Ok($"Manifest: {units} items, mass {mass:0}/{Catalog.M1Cargo:0}, cost {cost:0}/{Catalog.M1Budget:0}.");
            });
            S(a, Spot.Auto, "Select the science and repair cargo", (x, c) =>
            {
                var miss = new[] { "SEIS", "SAMP", "SPARE", "RAD", "COM" }.Where(id => !x.Kit.Has(id)).ToArray();
                return Ok(miss.Length == 0
                    ? "Seismometer, sampling kit, repair kit, radiation monitor and comms are packed."
                    : "NOT PACKED: " + string.Join(", ", miss) + ". Related objectives will be limited.");
            });
            S(a, Spot.Auto, "Confirm the equipment fits the loadout limits",
                (x, c) => ManifestValid() ? Ok("Cargo is within the loadout limits.") : No("Cargo exceeds the loadout limits."));
            S(a, Spot.Systems, "Begin the mission (required items present)", (x, c) =>
            {
                var miss = Catalog.Mission1.Where(d => d.Required && !x.Kit.Has(d.Id)).Select(d => d.Name).ToArray();
                return miss.Length == 0
                    ? Ok("All required equipment is present. Mission start confirmed.")
                    : No("Required equipment missing: " + string.Join(", ", miss));
            });

            // ---- B  Commission the habitat ----
            var b = T("B", "Commission the habitat", "Power console, life-support console, comms terminal",
                "Power, pressure, atmosphere and comms are in a safe, operational state.",
                "Unresolved faults carry into later operations.",
                x => Get("A").State == TaskState.Passed);
            S(b, Spot.Systems, "Verify solar-array orientation and output", (x, c) =>
                x.Supply >= 0.6f * x.SolarPeak && x.Dust < 0.2f
                    ? Ok($"Arrays: {x.Supply:0.0} of {x.SolarPeak:0.0} peak, dust {x.Dust:0%}.")
                    : No($"Array output low ({x.Supply:0.0} of {x.SolarPeak:0.0} peak, dust {x.Dust:0%}). Clean the arrays outside or check for shadow."));
            S(b, Spot.Systems, "Check battery charge and power demand", (x, c) =>
                x.BatteryPct >= 0.3f && x.Supply >= x.Demand
                    ? Ok($"Battery {x.BatteryPct:0%}, supply {x.Supply:0.0} covers demand {x.Demand:0.0}.")
                    : No($"Battery {x.BatteryPct:0%}, supply {x.Supply:0.0} vs demand {x.Demand:0.0}. Switch off optional loads in the power panel, then check again."));
            S(b, Spot.Systems, "Confirm oxygen generation / supply is operational", (x, c) =>
                x.CabinO2 >= 3f && (!x.Kit.Has("OGS") || x.Load("OGS").Enabled)
                    ? Ok($"Cabin O2 {x.CabinO2:0.0} kg, generation available.")
                    : No("Cabin O2 is below 3 kg or the oxygen generator is switched off. Enable OGS in the power panel."));
            S(b, Spot.Systems, "Verify CO2 removal and cabin pressure", (x, c) =>
            {
                if (!x.Kit.Has("SCR")) return Ok("WARNING: no CO2 scrubber packed. CO2 will build up.");
                if (!x.Load("SCR").Powered) return No("CO2 scrubber has no power.");
                if (x.CartridgeKg <= 0f) return No("No scrubber cartridge left.");
                if (x.CabinPressureKpa < 95f) return No($"Cabin pressure {x.CabinPressureKpa:0.0} kPa is low: check cabin oxygen.");
                return Ok($"CO2 {x.CO2:0.00} kg, scrubber on, cabin {x.CabinPressureKpa:0.0} kPa.");
            });
            S(b, Spot.Systems, "Check water reserves and thermal control", (x, c) =>
                x.Water >= 10f && x.Load("HAB").Powered
                    ? Ok($"Water {x.Water:0}/{x.WaterCap:0}, thermal control running.")
                    : No("Water below 10 or the habitat load has no power."));
            if (hasCom)
            {
            S(b, Spot.Comms, "Inspect the antenna, cabling, power supply and terminal", (x, c) =>
                x.Net.Inspected ? Ok("Antenna, cabling, power and terminal inspected.")
                : No("Open the comms workstation and run diagnostics."));
            S(b, Spot.Comms, "Verify antenna orientation and an unobstructed line of sight", (x, c) =>
            {
                var l = x.Net.Link("OUT", "EARTH");
                return l != null && l.State != LinkState.Unavailable && x.Net.PointingErrDeg < 1.5f && x.Net.CableLossDb < 1f
                    ? Ok(l.Reason)
                    : No((l?.Reason ?? "No link data.") + " Diagnose it in the workstation.");
            });
            S(b, Spot.Comms, "Identify the available communication paths", (x, c) =>
                x.Net.PathsReviewed ? Ok("Paths identified: direct-to-Earth, relay R-1, surface links.")
                : No("Open 'Available paths' in the workstation."));
            S(b, Spot.Comms, "Establish a link with Earth mission control", (x, c) =>
                x.Net.LinkEstablished ? Ok("Link established.")
                : No("Press 'Establish link' in the workstation."));
            S(b, Spot.Comms, "Send a telemetry packet and confirm the acknowledgement", (x, c) =>
                x.Net.PacketAcked ? Ok("Mission Control acknowledged the packet.")
                : No("Press 'Send test packet'."));
            S(b, Spot.Comms, "Verify voice, status telemetry and science-data transmission separately", (x, c) =>
                x.Net.VoiceOk && x.Net.TlmOk && x.Net.SciOk
                    ? Ok("Voice, telemetry and science data verified.")
                    : No($"Still to verify: {(x.Net.VoiceOk ? "" : "voice ")}{(x.Net.TlmOk ? "" : "telemetry ")}{(x.Net.SciOk ? "" : "science-data")}."));
            S(b, Spot.Comms, "Inspect link quality: SNR, data rate and latency", (x, c) =>
            {
                var l = x.Net.Link("OUT", "EARTH");
                return x.Net.QualityViewed
                    ? Ok($"SNR {l?.SnrDb:0.0} dB, {l?.RateMbps:0.###} Mbps, latency {l?.LatencyS:0.0} s.")
                    : No("Select the Earth node in the workstation to read the link budget.");
            });
            S(b, Spot.Comms, "Open the orbital map: relays, stations and windows", (x, c) =>
                x.Net.MapOpened ? Ok("Orbital map reviewed.")
                : No("Open the orbital view in the workstation."));
            S(b, Spot.Comms, "Save the link test results for Mission 2", (x, c) =>
                x.Net.CommunicationsTestComplete ? Ok("Link map saved for Mission 2.")
                : No("Press 'Save link results' (needs a verified Earth link and voice)."));
            }
            else S(b, Spot.Auto, "No comms console packed: Earth link tests skipped", (x, c) =>
                Ok("No comms console in the loadout: there is no Earth link, so the link tests are skipped and science data stays on board."));

            S(b, Spot.Systems, "Review fault messages before going outside", (x, c) =>
            {
                int n = x.Events.Count(e => e.Sev == Severity.Warning || e.Sev == Severity.Critical);
                return Ok(n == 0 ? "No fault messages." : $"{n} warning/critical message(s) in the log reviewed.");
            });

            // ---- C  Prepare for EVA ----
            var cT = T("C", "Prepare for EVA", "Suit dock and airlock",
                "Suit ready, airlock cycled correctly, EVA starts with all checks passed.",
                "An incomplete check blocks the EVA.",
                x => Get("A").State == TaskState.Passed);
            S(cT, Spot.SuitDock, "Inspect the suit and life-support backpack", (x, c) =>
                x.SuitIntegrity >= 60f
                    ? Ok($"Suit and backpack inspected, integrity {x.SuitIntegrity:0}%.")
                    : No($"Suit integrity {x.SuitIntegrity:0}%: repair it with a spare kit first."));
            S(cT, Spot.SuitDock, "Verify O2, suit pressure, battery, CO2 and cooling", (x, c) =>
                x.EvaLeft >= 0.6f && x.Load("SUI").Powered
                    ? Ok($"O2 {x.SuitO2:0.0} h, battery {x.SuitBatt:0.0} h, CO2 {x.SuitCO2:0.0} h, cooling nominal.")
                    : No($"Suit still charging ({x.EvaLeft:0.0} h). Wait or rest: the suit station refills it."));
            S(cT, hasCom ? Spot.Comms : Spot.Auto, "Review route, return distance and emergency procedure", (x, c) =>
                !x.Kit.Has("COM") ? Ok("No comms: you plan alone. Turn back at 1 h of suit time; go inside at the first radiation alert.")
                : x.Load("COM").Powered ? Ok("Reviewed with Mission Control: turn back at 1 h of suit time, shelter at the first RAD ALERT, never carry on past a warning.")
                : No("Comms offline: power COM to review the plan with Mission Control."));
            S(cT, Spot.Door, "Check the radio and helmet lamp", (x, c) =>
                Ok(x.Load("COM")?.Powered == true ? "Radio check good, helmet lamp OK." : "Helmet lamp OK. No radio link to Mission Control."));
            S(cT, Spot.Airlock, "Enter the airlock and close the inner hatch", (x, c) => Ok("Inner hatch closed and sealed."));
            S(cT, Spot.Airlock, "Depressurise before opening the outer hatch", (x, c) => Ok("Chamber at vacuum. Outer hatch released."));
            S(cT, Spot.Airlock, "Exit and confirm comms and suit telemetry", (x, c) =>
                Ok($"Telemetry nominal: suit {x.EvaLeft:0.0} h, comms {(x.Load("COM")?.Powered == true ? "linked" : "no link")}."));

            // ---- D  Deploy and verify the seismometer ----
            var d = T("D", "Deploy and verify the seismometer", "Science site at least 80 m from the habitat",
                "Self-test passes and the instrument is recording.",
                "Poor setup reduces data quality or leaves the station offline.",
                x => x.Kit.Has("SEIS") && Get("C").State == TaskState.Passed);
            S(d, Spot.Auto, "Carry the instrument to the deployment site", (x, c) =>
                x.SeisDeployed || (x.SeisCarried && Dist(c) >= MissionModel.SeisMinDistance)
                    ? Ok("Instrument at the site.") : No(""));
            S(d, Spot.Auto, "Confirm the site is safe and suitable (press E to deploy)", (x, c) => surveyed ? Ok(siteMsg) : No(""));
            S(d, Spot.Seis, "Place and stabilise the instrument", (x, c) =>
            {
                if (c.SlopeDeg > 8f) x.SeisQuality *= 0.8f;
                return Ok("Base seated and tamped into the regolith.");
            });
            S(d, Spot.Seis, "Orient it to north", (x, c) =>
            {
                float err = Mathf.Abs(Mathf.DeltaAngle(c.Yaw, NorthYaw));
                if (err > 15f) return No($"Orientation off by {err:0} deg. Face north and try again.");
                x.SeisQuality *= Mathf.Lerp(1f, 0.85f, err / 15f);
                return Ok($"Oriented within {err:0} deg of north.");
            });
            S(d, Spot.Seis, "Connect / activate the power system", (x, c) =>
                x.Load("SEIS").Powered ? Ok("Power connected.")
                : No("No power reaching the seismometer. Free capacity in the power panel (switch off a low-priority load), then try again."));
            S(d, Spot.Seis, "Run the self-test and verify telemetry", (x, c) =>
            {
                if (!x.Load("SEIS").Powered) return No("Self-test aborted: no power.");
                if (x.SeisQuality < 0.5f) return No("Self-test FAILED: tilt/orientation out of range. Pick it up and redeploy on better ground.");
                x.SeisInstalled = true;
                return Ok($"Self-test PASSED. Noise floor {(x.SeisDistance >= MissionModel.SeisMinDistance ? "low" : "HIGH (base vibration)")}, data quality {x.SeisQuality:0%}. Recording.");
            });
            S(d, Spot.Seis, "Record the location and deployment time", (x, c) =>
            {
                x.Data.Add(new DataRecord { Hour = x.Hour, Instrument = "SEIS",
                    What = "Commissioned: self-test passed",
                    Where = $"x {x.SeisPos.x:0}, z {x.SeisPos.z:0}",
                    Conditions = $"data quality {x.SeisQuality:0%}" });
                return Ok("Deployment location and time logged in the science log.");
            });

            // ---- E  Respond to the radiation alert (h22-26) ----
            var e = T("E", "Respond to the radiation alert", "Wherever you are, then the habitat shelter",
                "You followed the hazard procedure and kept the modelled dose low.",
                "Dose rises and hurts the Safety score.",
                x => true, SpeS - (1f / 6f), SpeE + 1f);
            e.PassExtra = x => x.Dose - doseAtSpe <= 0.4f;
            e.OnFail = x => { x.NearFailures++; x.Log(Severity.Critical, "Radiation procedure failed: avoidable dose taken during the solar particle event."); };
            S(e, Spot.Auto, "Receive the alert and check status and location", (x, c) =>
                Ok(x.Inside ? "RAD ALERT: solar particle event. You are inside the habitat."
                            : $"RAD ALERT: solar particle event. You are {Dist(c):0} m out: stop work and return NOW."));
            S(e, Spot.Auto, "Stop outdoor work and return by the safest route", (x, c) =>
                x.Inside ? Ok($"Back inside {(x.Hour - (SpeS - 1f / 6f)) * 60f:0} min after the alert.") : No(""));
            S(e, Spot.Shelter, "Follow the shelter procedure (shielded corner, east bench)", (x, c) =>
            {
                x.Sheltering = true;
                return Ok("In the shielded corner: the dose rate is cut to a fraction of the open cabin. Stay here.");
            });
            S(e, Spot.Auto, "Confirm you are in the protected area", (x, c) => x.Sheltering ? Ok("In the protected area.") : No(""));
            S(e, Spot.Shelter, "Wait for clearance, then resume operations", (x, c) =>
            {
                if (x.Hour < SpeE) return No($"Not cleared: {SpeE - x.Hour:0.0} h of the event remain. Stay in the shelter.");
                x.Sheltering = false;
                return Ok("Cleared by Mission Control: surface operations may resume.");
            });

            // ---- F  Survive the power deficit (h40-46) ----
            var f = T("F", "Survive the power deficit", "Power console (or suit display)",
                "Essential loads stayed powered for the whole event.",
                "Non-critical systems shut down and science data is lost.",
                x => true, ShS - 2f, ShE + 1.5f);
            f.PassExtra = x => !essentialLost;
            f.OnFail = x => x.Log(Severity.Warning, "Power contingency failed: essential loads dropped during the shadow window.");
            S(f, Spot.Systems, "Notice falling solar output and compare battery with the load", (x, c) =>
                Ok($"Shadow in {Mathf.Max(0f, ShS - x.Hour):0.0} h. Battery {x.Battery:0.0} PWh, load {x.Demand:0.0} PW."), true);
            S(f, Spot.Systems, "Switch off optional equipment so the battery lasts", (x, c) =>
            {
                float need = x.Demand * (ShE - Mathf.Max(x.Hour, ShS));
                return x.Battery >= need
                    ? Ok($"Plan holds: {x.Battery:0.0} PWh stored, ~{need:0.0} needed.")
                    : No($"Battery {x.Battery:0.0} PWh but ~{need:0.0} needed. Switch off COM / RAD / OGS / garage in the power panel and check again.");
            }, true);
            S(f, Spot.Systems, "Check rover and science station energy", (x, c) =>
                Ok($"Rover {x.RoverBattery:0.0}/{x.RoverCap:0.0} PWh. Seismometer: " +
                   (!x.SeisDeployed ? "not deployed." : x.Load("SEIS").Powered ? "powered." : "UNPOWERED: data is being lost.")), true);
            S(f, Spot.Auto, "Keep life support, comms and thermal control powered", (x, c) =>
                x.Hour >= ShE ? (essentialLost ? No("") : Ok("Life support, comms and thermal control stayed powered.")) : No(""));
            S(f, Spot.Auto, "Wait for illumination to return", (x, c) => x.Hour >= ShE ? Ok("Sunlight is returning.") : No(""));
            S(f, Spot.Auto, "Verify the battery recovers before optional work", (x, c) =>
                x.BatteryPct >= 0.25f ? Ok("Battery recovered: optional work may resume.") : No(""));

            // ---- G  Close out the mission (h46-72) ----
            var g = T("G", "Close out the mission", "Habitat console and comms",
                "Survived, systems operational, all mandatory objectives documented.",
                "Lower score and scientific return.",
                x => true, ShE, MissionModel.DurationH);
            S(g, Spot.Systems, "Inspect the science station and review its measurements", (x, c) =>
                Ok($"SEIS {x.SeisGoodHours:0.0}/{MissionModel.SeisNeededHours:0} good hours{(x.QuakeRecorded ? ", quake recorded" : "")}; {x.RadReadings} radiation reading(s); {x.Data.Count} data records."));
            S(g, Spot.Systems, "Retrieve samples and document their locations", (x, c) =>
                x.SampleCapacity == 0 ? Ok("No sampling kit packed: not applicable.")
                : x.SamplesCarried > 0 ? No($"{x.SamplesCarried} sample(s) still in your bag: bring them inside first.")
                : Ok($"{x.SamplesDelivered} sample(s) stored and documented."));
            S(g, Spot.Systems, "Check remaining oxygen, water, food, power and consumables", (x, c) =>
                Ok($"O2 {x.CabinO2:0.0}, water {x.Water:0}, food {x.Food:0}, battery {x.BatteryPct:0%}, cartridges {x.CartridgeKg:0.0} kg, spares {x.Spares}."));
            S(g, Spot.Systems, "Record faults, maintenance actions and missed objectives", (x, c) =>
            {
                var missed = new List<string>();
                if (!x.P1Seismometer) missed.Add("seismometer data");
                if (!x.P2Radiation) missed.Add("radiation reading");
                return Ok($"Faults {x.CriticalEvents}, near-failures {x.NearFailures}; missed: {(missed.Count == 0 ? "none" : string.Join(", ", missed))}.");
            });
            S(g, hasCom ? Spot.Comms : Spot.Auto, "Send the final science and engineering report", (x, c) =>
                !hasCom ? Ok("No comms console packed: the report is stored on board and returns with the lander.")
                : x.Load("COM")?.Powered == true
                    ? Ok($"Final report sent ({x.Data.Count} data records, {x.Events.Count} log entries).")
                    : No("Comms offline: cannot transmit. Power COM."));
            S(g, Spot.Systems, "Complete the mission debrief", (x, c) =>
                Ok("Debrief complete. Hold on until the mission clock ends."));
        }

        // ---- running it ----

        static bool NeedsInside(Spot s) =>
            s == Spot.Systems || s == Spot.Comms || s == Spot.SuitDock || s == Spot.Door || s == Spot.Shelter;

        public bool Do(Spot at, StepCtx c, bool one = false)
        {
            if (!Enabled || AutoPass) return false;
            bool handled = false, bad = false;
            var lines = new List<string>();
            var failed = new HashSet<string>();
            // several passes: finishing a step can complete the automatic steps behind it, which in turn unblocks more steps at this same spot
            for (int pass = 0; pass < 4; pass++)
            {
                bool progressed = false;
                foreach (var t in All)
                {
                    if (t.State != TaskState.Active) continue;
                    while (true)
                    {
                        var s = t.Steps.FirstOrDefault(z => !z.Done);
                        if (s == null || s.At != at || (NeedsInside(at) && !s.Remote && !m.Inside)) break;
                        handled = true;
                        var r = s.Verify(m, c);
                        if (!r.ok) { if (failed.Add(s.Id)) lines.Add($"[{s.Id}] {r.msg}"); bad = true; break; }
                        s.Done = true; s.Result = r.msg; lines.Add($"[{s.Id}] {r.msg}"); Version++; progressed = true;
                        if (one) break;
                    }
                }
                if (one || !progressed) break;
                foreach (var t in All) if (t.State == TaskState.Active) AutoRun(t);
            }
            if (handled) Say(bad ? Severity.Warning : Severity.Good, string.Join("\n", lines));
            return handled;
        }

        public string Prompt(Spot at, float yaw = 0f)
        {
            if (!Enabled || AutoPass) return null;
            foreach (var t in All)
            {
                if (t.State != TaskState.Active) continue;
                var s = t.Steps.FirstOrDefault(z => !z.Done);
                if (s != null && s.At == at)
                    return $"[{t.Id}] {s.Text}" + (s.Id == "D4" ? $"   (facing {yaw:0} deg, north is {NorthYaw:0})" : "");
            }
            return null;
        }

        public bool Has(Spot at) => Prompt(at) != null;

        public void Update()
        {
            if (!Enabled) return;
            if (AutoPass)
            {
                foreach (var t in All) { foreach (var s in t.Steps) s.Done = true; t.State = TaskState.Passed; }
                m.SeisInstalled = true; return;
            }
            if (doseAtSpe < 0f && m.Hour >= MissionModel.SpeStart) doseAtSpe = m.Dose;
            if (m.Hour >= MissionModel.ShadowStart && m.Hour < MissionModel.ShadowEnd
                && (!m.Load("HAB").Powered || (m.Kit.Has("SCR") && !m.Load("SCR").Powered)))
                essentialLost = true;
            if (m.Sheltering && ShelterDist > 3f)
            {
                m.Sheltering = false; Version++;
                Say(Severity.Warning, "You left the shielded corner. Go back in until the event is over.");
            }
            foreach (var t in All)
            {
                if (t.State == TaskState.Locked && m.Hour >= t.From && t.Unlock(m))
                { t.State = TaskState.Active; Version++; Say(Severity.Info, $"New task {t.Id}: {t.Title} ({t.Location})"); }
                if (t.State != TaskState.Active) continue;
                AutoRun(t);
                bool all = t.Steps.All(s => s.Done);
                if (all && (t.PassExtra == null || t.PassExtra(m)))
                { t.State = TaskState.Passed; Version++; Say(Severity.Good, $"Task {t.Id} complete: {t.Title}. {t.Pass}"); }
                else if (all || m.Hour > t.To)
                { t.State = TaskState.Failed; Version++; Say(Severity.Warning, $"Task {t.Id} failed: {t.Fail}"); t.OnFail?.Invoke(m); }
            }
        }

        void AutoRun(MissionTask t)
        {
            var c = new StepCtx { Pos = PlayerPos };
            while (true)
            {
                var s = t.Steps.FirstOrDefault(z => !z.Done);
                if (s == null || s.At != Spot.Auto) return;
                var r = s.Verify(m, c);
                if (!r.ok) return;
                s.Done = true; s.Result = r.msg; Version++;
                if (!string.IsNullOrEmpty(r.msg)) Say(Severity.Info, $"[{s.Id}] {r.msg}");
            }
        }

        // ---- EVA gate ----

        public bool CanExit(out string why)
        {
            why = null;
            if (!Enabled || AutoPass) return true;
            if (m.SpeActive) { why = "EVA not cleared: a solar particle event is in progress. Use the shelter."; return false; }
            if (Get("A").State != TaskState.Passed) { why = "Finish the readiness checklist (Task A) at the power & life support console first."; return false; }
            var pending = Get("C").Steps.FirstOrDefault(s => !s.Done && s.At != Spot.Door && s.At != Spot.Airlock);
            if (pending != null) { why = $"EVA checklist incomplete: {pending.Text}."; return false; }
            return true;
        }

        public void OnExit()
        {
            if (!Enabled || AutoPass || exitWarned || Get("B").State == TaskState.Passed) return;
            exitWarned = true; m.NearFailures++;
            Say(Severity.Warning, "You left with unverified habitat systems (Task B). Those faults travel with you: finish the console checks when you are back.");
        }

        // ---- seismometer site survey ----

        public string Survey(StepCtx c)
        {
            float d = Dist(c); string why = null;
            if (m.SpeActive) why = "A solar particle event is in progress: it is not safe to work outside.";
            else if (m.EvaLeft < 1f) why = $"Only {m.EvaLeft:0.0} h of suit time left: not enough margin to deploy and walk back.";
            else if (c.SlopeDeg > 8f) why = $"Ground slope {c.SlopeDeg:0} deg is too steep for a stable base. Find flatter ground.";
            else if (d < MissionModel.SeisMinDistance) why = $"Only {d:0} m from the habitat: base vibration will mask quakes (need {MissionModel.SeisMinDistance:0} m).";
            if (why == null) { surveyed = true; siteMsg = $"Site safe: slope {c.SlopeDeg:0} deg, {d:0} m from the habitat."; }
            return why;
        }

        public void AcceptRisk() { surveyed = true; siteMsg = "Site accepted despite warnings."; riskQuality = 0.8f; }

        public void OnSeisDeployed() { m.SeisInstalled = false; m.SeisQuality = riskQuality; riskQuality = 1f; }

        public void ResetSeis()
        {
            if (!Enabled || AutoPass) return;
            surveyed = false; m.SeisInstalled = false;
            var d = Get("D");
            foreach (var s in d.Steps) if (s.Id != "D1") s.Done = false;
            if (d.State == TaskState.Passed) d.State = TaskState.Active;
            Version++;
        }

        public void Finish()
        {
            foreach (var t in All)
                if (t.State == TaskState.Active || t.State == TaskState.Locked)
                    t.State = TaskState.Failed;
        }
    }
}
