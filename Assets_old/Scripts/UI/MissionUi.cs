using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mohakash
{
    /// <summary>All Mission 1 screens: briefing, loadout, HUD, systems panel, habitat panel, pause menu, report.</summary>
    public class MissionUi : MonoBehaviour
    {
        Mission1 m;
        /// <summary>AutoTest hook: advances the current pre-launch screen (briefing -> loadout -> launch).</summary>
        public Action<Loadout> TestAdvance;
        VisualElement root, hud, markers, alerts, systems, inside, pause, fade, banner;
        /// <summary>Exposes the root UI element so MinimapSystem can attach its panel.</summary>
        public VisualElement Root => root;
        Label bannerText, promptText, clockText, hintText;
        readonly Dictionary<string, Label> stats = new Dictionary<string, Label>();
        readonly Dictionary<string, (VisualElement fill, Label val)> bars = new Dictionary<string, (VisualElement, Label)>();
        readonly List<(Label label, VisualElement dot, Func<bool> done, Func<string> text)> objectives = new List<(Label, VisualElement, Func<bool>, Func<string>)>();
        readonly List<Button> speedButtons = new List<Button>();
        VisualElement eva, prompt, controls;
        public bool ControlsOpen => controls != null && controls.style.display != DisplayStyle.None;

        /// <summary>Controls list overlay (H / F1, or from the pause menu). Mission time keeps running.</summary>
        public void ToggleControls()
        {
            if (controls == null)
            {
                controls = Ui.Box(root, "screen", "center", "dim");
                var p = Ui.Panel(controls); p.style.width = 1000;
                ControlsUi.Build(p);
                var row = Ui.Box(p, "row"); row.style.justifyContent = Justify.FlexEnd; row.style.marginTop = 10;
                Ui.Button(row, "Close (H)", ToggleControls, "btn-primary");
                Ui.Show(controls, false);
            }
            bool open = !ControlsOpen;
            Ui.Show(controls, open);
            controls.BringToFront();
            if (open) Game.SetCursorFree(true);
            else if (!m.Paused && m.Model != null && !m.Model.Inside && !m.SystemsOpen) Game.SetCursorFree(false);
        }
        Label evaWalk;
        readonly Dictionary<Interactable, VisualElement> markerViews = new Dictionary<Interactable, VisualElement>();
        readonly List<(Button btn, PowerLoad load)> loadButtons = new List<(Button, PowerLoad)>();
        readonly List<(Label lbl, PowerLoad load)> loadStates = new List<(Label, PowerLoad)>();
        readonly List<(VisualElement dot, PowerLoad load)> loadDots = new List<(VisualElement, PowerLoad)>();
        Label sysSummary, sysShadow, insideStatus; Button ogsBtn, restBtn, repairBtn;

        public void Init(Mission1 mission)
        {
            m = mission;
            root = Ui.CreateRoot();
            fade = Ui.Box(root, "fade");
            fade.style.opacity = 0f;
        }

        // ================================================================== Briefing

        public void ShowBriefing(Action onContinue)
        {
            var scr = Ui.Box(root, "screen", "center", "dim");
            scr.pickingMode = PickingMode.Position;
            var p = Ui.Panel(scr); p.style.width = 980;
            Ui.Text(p, "MISSION BRIEFING", "h1");
            Ui.Text(p, "Mission 1: First Light   -   Moon, lunar south pole   -   72 mission hours", "kicker-blue");
            Ui.Text(p, "Astronaut, you have landed alone on a sunny ridge near the Moon's south pole. Set up the outpost, keep yourself alive, " +
                       "place a seismometer far from the base to listen for moonquakes, and survive until the ride home at hour 72. " +
                       "Everything you bring is limited, so plan ahead.", "text");
            Ui.Box(p, "divider");
            Ui.Text(p, "MISSION PLAN", "section");
            foreach (var step in new[] { "1. Pack your rocket.", "2. Pick up the seismometer crate by the airlock.",
                                         "3. Carry it past the orange ring (80 m) and press E to set it up.", "4. Log a radiation reading at the post next to the habitat.",
                                         "5. Bonus: collect 2 rock samples.", "6. Go inside, speed up time and keep power and air going until hour 72." })
                Ui.Text(p, step, "text-small");
            Ui.Box(p, "divider");
            Ui.Text(p, "KEY IDEAS", "section");
            foreach (var idea in new[] { "<b>Solar power</b>: only works in sunlight, so the battery stores the extra.",
                                         "<b>Shadow</b>: at hour 40 the sun disappears for 6 hours.",
                                         "<b>Oxygen</b>: the oxygen maker turns water into air but needs a lot of power.",
                                         "<b>Spacewalk (EVA)</b>: your suit's oxygen runs out, so stay inside the cyan ring.",
                                         "<b>Radiation</b>: a solar storm comes around hour 22, so be inside then." })
                Ui.Text(p, idea, "text-small");
            Ui.Text(p, "Controls: WASD move - mouse look - E use - R rover - Tab power panel - 1/2/3 time speed - Esc menu", "note");
            Ui.Text(p, "Note: all numbers in this game are simplified for learning, not real engineering values.", "note");
            var row = Ui.Box(p, "row"); row.style.justifyContent = Justify.FlexEnd; row.style.marginTop = 10;
            Ui.Button(row, "Back", () => Game.Load(Game.MenuScene));
            Ui.Button(row, "Continue to Loadout", () => { scr.RemoveFromHierarchy(); onContinue(); }, "btn-primary");
            TestAdvance = _ => { scr.RemoveFromHierarchy(); onContinue(); };
        }

        /// <summary>Simple loadout: standard gear always packed + extras ticked until the rocket is full (GDD 540 kg / 125 Cr).</summary>
        public void ShowLoadout(Action<Loadout> onLaunch)
        {
            var chosen = new HashSet<string> { "rover" };
            var scr = Ui.Box(root, "screen", "center", "dim");
            scr.pickingMode = PickingMode.Position;
            var p = Ui.Panel(scr); p.style.width = 900;
            Ui.Text(p, "LOADOUT", "h1");
            Ui.Text(p, "STANDARD GEAR (always packed)", "section");
            var chips = Ui.Box(p, "row", "wrap");
            foreach (var (_, _, label) in Catalog.Standard) Ui.Text(chips, label, "chip");
            Ui.Box(p, "divider");
            Ui.Text(p, "EXTRAS - tick what you want until the rocket is full", "section");
            var rows = new List<(Catalog.Extra e, Toggle t)>();
            Label capText = null; Button launch = null; VisualElement capFill = null;
            foreach (var e in Catalog.Extras)
            {
                var t = Ui.Add(p, new Toggle { value = chosen.Contains(e.Key) }, "extra");
                var extra = e;
                t.RegisterValueChangedCallback(ev => { if (ev.newValue) chosen.Add(extra.Key); else chosen.Remove(extra.Key); Refresh(); });
                t.label = $"{extra.Name}  -  {extra.Benefit}";
                rows.Add((extra, t));
            }
            Ui.Text(p, "ROCKET CAPACITY", "section");
            capFill = Ui.Bar(p, "bar-big").fill;
            capText = Ui.Text(p, "", "text-small");
            var row = Ui.Box(p, "row"); row.style.justifyContent = Justify.FlexEnd; row.style.marginTop = 10;
            Ui.Button(row, "Advanced: full cargo list", () => { scr.RemoveFromHierarchy(); ShowAdvancedLoadout(onLaunch); });
            Ui.Button(row, "Back", () => { scr.RemoveFromHierarchy(); ShowBriefing(() => ShowLoadout(onLaunch)); });
            launch = Ui.Button(row, "LAUNCH MISSION", () => { scr.RemoveFromHierarchy(); StartCoroutine(FadeThen(() => onLaunch(Catalog.Simple(chosen)))); }, "btn-primary");

            void Refresh()
            {
                var kit = Catalog.Simple(chosen);
                float fill = Mathf.Max(kit.Mass / Catalog.M1Cargo, kit.Cost / Catalog.M1Budget);
                Ui.SetBar(capFill, fill, fill > 1f ? Severity.Critical : fill > 0.9f ? Severity.Warning : Severity.Good);
                capText.text = $"Mass {kit.Mass:0} / {Catalog.M1Cargo:0} kg     Budget {kit.Cost:0} / {Catalog.M1Budget:0} Cr" + (kit.WithinLimits ? "" : "     TOO MUCH - untick something");
                launch.SetEnabled(kit.WithinLimits);
                foreach (var (e, t) in rows)
                {
                    bool fits = chosen.Contains(e.Key) || Catalog.Simple(chosen.Append(e.Key)).WithinLimits;
                    t.SetEnabled(fits);
                    t.EnableInClassList("extra-on", chosen.Contains(e.Key));
                }
            }
            Refresh();
            TestAdvance = k => { scr.RemoveFromHierarchy(); StartCoroutine(FadeThen(() => onLaunch(k ?? Catalog.Simple(chosen)))); };
        }

        public void ShowAdvancedLoadout(Action<Loadout> onLaunch)
        {
            var kit = Catalog.Recommended();
            var scr = Ui.Box(root, "screen", "center", "dim");
            scr.pickingMode = PickingMode.Position;
            var p = Ui.Panel(scr); p.style.width = 1700; p.style.height = 960;
            var head = Ui.Box(p, "row", "noshrink");
            Ui.Text(head, "PRE-LAUNCH LOADOUT", "h1");
            Ui.Text(head, "GAME UNITS - not NASA specifications", "label-tag");
            Ui.Text(p, "Three limits at once: budget (Cr), cargo mass (kg) and what each machine needs. You cannot take everything.", "text", "muted");

            var body = Ui.Box(p, "row", "grow"); body.style.alignItems = Align.Stretch;
            var listBox = Ui.Box(body, "col", "grow");
            var header = Ui.Box(listBox, "table-row");
            Ui.Text(header, "Item", "cell-name", "muted"); Ui.Text(header, "", "btn-small").style.width = 30; Ui.Text(header, "Qty", "qty", "muted"); Ui.Text(header, "", "btn-small").style.width = 30;
            Ui.Text(header, "kg", "cell", "muted"); Ui.Text(header, "Cr", "cell", "muted"); Ui.Text(header, "PW", "cell-pw", "muted"); Ui.Text(header, "What it does", "cell-wide");
            var scroll = Ui.Add(listBox, new ScrollView(), "scroll");

            var side = Ui.Box(body, "col", "limits"); side.style.marginLeft = 20;
            Ui.Text(side, "CARGO MASS", "h3"); var massBar = Ui.Bar(side); var massLbl = Ui.Text(side, "", "small");
            Ui.Text(side, "BUDGET", "h3"); var costBar = Ui.Bar(side); var costLbl = Ui.Text(side, "", "small");
            Ui.Text(side, "POWER PREVIEW", "h3"); var powerLbl = Ui.Text(side, "", "small");
            Ui.Text(side, "WARNINGS", "h2");
            var warnBox = Ui.Box(side, "col");
            var buttons = Ui.Box(side, "row", "wrap"); buttons.style.marginTop = 10;
            Button launch = null;

            var qtyLabels = new Dictionary<string, Label>();
            void Refresh()
            {
                foreach (var kv in qtyLabels) kv.Value.text = kit.Count(kv.Key).ToString();
                Ui.SetBar(massBar.fill, kit.Mass / Catalog.M1Cargo, kit.Mass > Catalog.M1Cargo ? Severity.Critical : kit.Mass > 0.95f * Catalog.M1Cargo ? Severity.Warning : Severity.Good);
                Ui.SetBar(costBar.fill, kit.Cost / Catalog.M1Budget, kit.Cost > Catalog.M1Budget ? Severity.Critical : kit.Cost > 0.95f * Catalog.M1Budget ? Severity.Warning : Severity.Good);
                massLbl.text = $"{kit.Mass:0} / {Catalog.M1Cargo:0} kg   (margin {Catalog.M1Cargo - kit.Mass:0} kg)";
                costLbl.text = $"{kit.Cost:0} / {Catalog.M1Budget:0} Cr   (margin {Catalog.M1Budget - kit.Cost:0} Cr)";
                float gen = kit.Count("SOL-S") * 1.0f;
                float load = Catalog.Mission1.Where(e => kit.Has(e.Id) && e.Power < 0 && e.Id != "OGS" && e.Id != "GAR").Sum(e => -e.Power);
                powerLbl.text = $"Solar peak {gen:0.0} PW  vs  base load {load:0.00} PW (+1.0 PW while OGS runs)\n" +
                                $"Battery {kit.Count("BAT") * 10} PWh  vs  shadow window need ~{load * 6:0.0} PWh";
                warnBox.Clear();
                foreach (var w in kit.Warnings()) Ui.Text(warnBox, "- " + w, "warning-line");
                if (kit.Warnings().Count == 0) Ui.Text(warnBox, "No warnings. Good plan!", "good");
                launch?.SetEnabled(kit.WithinLimits);
            }

            foreach (var e in Catalog.Mission1)
            {
                var r = Ui.Box(scroll, "table-row"); r.pickingMode = PickingMode.Position;
                var nameBox = Ui.Box(r, "row", "cell-name");
                Ui.Text(nameBox, e.Name);
                if (e.Required) Ui.Text(nameBox, "required", "label-tag");
                var id = e.Id;
                var minus = Ui.Button(r, "-", () => { kit.Set(id, kit.Count(id) - 1); Refresh(); }, "btn-small");
                qtyLabels[id] = Ui.Text(r, "0", "qty");
                var plus = Ui.Button(r, "+", () => { kit.Set(id, kit.Count(id) + 1); Refresh(); }, "btn-small");
                if (e.Required) { minus.SetEnabled(false); plus.SetEnabled(false); }
                Ui.Text(r, e.Mass.ToString("0"), "cell"); Ui.Text(r, e.Cost.ToString("0"), "cell");
                Ui.Text(r, e.PowerText ?? (e.Power == 0 ? "0" : e.Power.ToString("+0.00;-0.00")), "cell-pw");
                Ui.Text(r, e.Purpose, "cell-wide");
            }

            Ui.Button(buttons, "Recommended plan (GDD)", () => { kit = Catalog.Recommended(); Refresh(); });
            Ui.Button(buttons, "Clear", () => { kit = Catalog.RequiredOnly(); Refresh(); });
            launch = Ui.Button(side, "LAUNCH  >", () =>
            {
                scr.RemoveFromHierarchy();
                StartCoroutine(FadeThen(() => onLaunch(kit)));
            }, "btn-primary", "btn-big");
            TestAdvance = k => { scr.RemoveFromHierarchy(); StartCoroutine(FadeThen(() => onLaunch(k ?? kit))); };
            Ui.Button(side, "< Simple loadout", () => { scr.RemoveFromHierarchy(); ShowLoadout(onLaunch); });
            Refresh();
        }

        System.Collections.IEnumerator FadeThen(Action a)
        {
            fade.style.opacity = 1f;
            yield return new WaitForSecondsRealtime(0.9f);
            a();
            yield return new WaitForSecondsRealtime(0.3f);
            fade.style.opacity = 0f;
        }

        // ================================================================== HUD

        public void StartHud()
        {
            hud = Ui.Box(root, "screen");
            markers = Ui.Box(hud, "screen");

            // Top bar: life-critical + time
            var top = Ui.Box(hud, "hud-top", "row");
            var tp = Ui.Panel(top, "row"); tp.style.paddingTop = 8; tp.style.paddingBottom = 8;
            foreach (var (key, name) in new[] { ("power", "POWER PW (in/out)"), ("battery", "BATTERY"), ("o2", "CABIN O2"), ("water", "WATER"), ("food", "FOOD"), ("co2", "CO2"), ("sun", "SUN") })
            {
                var s = Ui.Box(tp, "stat", "col");
                Ui.Text(s, name, "stat-name");
                stats[key] = Ui.Text(s, "-", "stat-value");
            }
            var time = Ui.Box(tp, "stat", "col"); time.style.marginLeft = 20;
            Ui.Text(time, "MISSION TIME", "stat-name");
            clockText = Ui.Text(time, "", "stat-value");
            var speeds = Ui.Box(tp, "row"); speeds.style.marginLeft = 10;
            foreach (var (label, val) in new[] { ("||", 0), ("1x", 1), ("2x", 2), ("4x", 4) })
            {
                var b = Ui.Button(speeds, label, () => m.SetSpeed(val), "speed-btn");
                b.userData = val; speedButtons.Add(b);
            }

            // Left: astronaut
            var left = Ui.Panel(hud, "hud-left");
            Ui.Text(left, "ASTRONAUT", "h3");
            foreach (var (key, name) in new[] { ("health", "Health"), ("fatigue", "Fatigue"), ("dose", "Dose (game-mSv)"), ("suit", "Suit integrity") })
            {
                var r = Ui.Box(left, "row"); Ui.Text(r, name, "small", "grow"); var v = Ui.Text(r, "", "small");
                var b = Ui.Bar(left); b.bar.style.marginBottom = 6;
                bars[key] = (b.fill, v);
            }

            // Right: link + objectives
            var right = Ui.Panel(hud, "hud-right");
            var link = Ui.Box(right, "row"); var linkDot = Ui.Dot(link, Severity.Good);
            stats["link"] = Ui.Text(link, "", "small");
            Ui.Text(right, "OBJECTIVES", "h3");
            var mo = m.Model;
            AddObjective(right, () => mo.P1Seismometer, () => $"Seismometer >= 80 m, powered 24 h  ({mo.SeisGoodHours:0.0}/24 h{(mo.SeisDeployed && mo.SeisDistance < MissionModel.SeisMinDistance ? ", TOO CLOSE" : "")})");
            AddObjective(right, () => mo.P2Radiation, () => $"Log 1 radiation reading  ({mo.RadReadings}/1)");
            AddObjective(right, () => mo.P3Survive, () => $"Survive 72 h  ({mo.Hour:0}/72 h)");
            AddObjective(right, () => mo.S1Samples, () => $"Optional: 2 regolith samples ({mo.SamplesDelivered} delivered, {mo.SamplesCarried} carried)", true);
            hintText = Ui.Text(right, "", "small", "cyan"); hintText.style.marginTop = 6;
            Ui.Text(right, "Press H for the controls list", "small", "muted").style.marginTop = 4;

            // EVA panel
            eva = Ui.Panel(hud, "eva");
            Ui.Text(eva, "EVA  -  SUIT A", "h3");
            foreach (var (key, name) in new[] { ("sO2", "Suit O2"), ("sBatt", "Suit battery"), ("sCO2", "CO2 absorber") })
            {
                var r = Ui.Box(eva, "row"); Ui.Text(r, name, "small", "grow"); var v = Ui.Text(r, "", "small");
                var b = Ui.Bar(eva); b.bar.style.marginBottom = 5;
                bars[key] = (b.fill, v);
            }
            evaWalk = Ui.Text(eva, "", "small");

            // Prompt + alerts + banner
            var bottom = Ui.Box(hud, "hud-bottom");
            prompt = Ui.Box(bottom, "prompt", "row");
            promptText = Ui.Text(prompt, "");
            alerts = Ui.Box(hud, "hud-alerts", "col");
            banner = Ui.Box(hud, "banner"); bannerText = Ui.Text(banner, "", "banner-box"); Ui.Show(banner, false);

            BuildSystemsPanel();
            BuildInsidePanel();
            BuildPauseMenu();
        }

        void AddObjective(VisualElement parent, Func<bool> done, Func<string> text, bool optional = false)
        {
            var r = Ui.Box(parent, "row", "objective");
            var d = Ui.Dot(r, Severity.Info);
            var l = Ui.Text(r, "", "small", optional ? "muted" : "");
            objectives.Add((l, d, done, text));
        }

        void Update()
        {
            if (hud == null || m.Model == null) return;
            var mo = m.Model;

            stats["power"].text = $"{mo.Supply:0.0} / {mo.Demand:0.0}";
            stats["power"].style.color = mo.Supply >= mo.Demand ? new Color(0.5f, 0.9f, 0.6f) : new Color(1f, 0.75f, 0.3f);
            stats["battery"].text = mo.BatteryCap > 0 ? $"{mo.BatteryPct:0%} ({mo.Battery:0.0} PWh)" : "none";
            Colour(stats["battery"], Ui.Level(mo.BatteryPct, 0.25f, 0.1f));
            stats["o2"].text = $"{mo.CabinO2:0.00} kg"; Colour(stats["o2"], Ui.Level(mo.CabinO2, 2f, 1f));
            stats["water"].text = $"{mo.Water:0.0} L"; Colour(stats["water"], Ui.Level(mo.Water, 3f, 1f));
            stats["food"].text = $"{mo.Food / 2500f:0.0} days";
            stats["co2"].text = $"{mo.CO2:0.00} kg"; Colour(stats["co2"], mo.CO2 > 0.6f ? Severity.Critical : mo.CO2 > 0.4f ? Severity.Warning : Severity.Good);
            stats["sun"].text = mo.InShadow ? "SHADOW" : $"{mo.Sun:0%}";
            clockText.text = $"h {mo.Hour:00.0} / 72";
            foreach (var b in speedButtons) b.EnableInClassList("speed-on", (int)b.userData == m.Speed);

            SetStat("health", mo.Health / 100f, $"{mo.Health:0}%", Ui.Level(mo.Health, 60, 30));
            SetStat("fatigue", mo.Fatigue / 100f, $"{mo.Fatigue:0}%", mo.Fatigue > 70 ? Severity.Critical : mo.Fatigue > 40 ? Severity.Warning : Severity.Good);
            SetStat("dose", mo.Dose / MissionModel.DoseAllowance, $"{mo.Dose:0.0} / 20", mo.Dose > 20 ? Severity.Critical : mo.Dose > 15 ? Severity.Warning : Severity.Good);
            SetStat("suit", mo.SuitIntegrity / 100f, $"{mo.SuitIntegrity:0}%", Ui.Level(mo.SuitIntegrity, 60, 30));
            SetStat("sO2", mo.SuitO2 / MissionModel.SuitO2Max, $"{mo.SuitO2:0.0} h", Ui.Level(mo.SuitO2, 1f, 0.5f));
            SetStat("sBatt", mo.SuitBatt / MissionModel.SuitBattMax, $"{mo.SuitBatt:0.0} h", Ui.Level(mo.SuitBatt, 1f, 0.5f));
            SetStat("sCO2", mo.SuitCO2 / MissionModel.SuitCO2Max, $"{mo.SuitCO2:0.0} h", Ui.Level(mo.SuitCO2, 1f, 0.5f));

            bool com = mo.Load("COM")?.Powered ?? false;
            stats["link"].text = com ? "Earth link OK (~1.3 s delay)  |  local network OK" : "Earth link OFFLINE (no COM power)  |  local network OK";

            foreach (var o in objectives)
            {
                bool done = o.done();
                o.label.text = o.text();
                Ui.SetDot(o.dot, done ? Severity.Good : Severity.Info);
            }
            hintText.text = Game.Hints ? "Next: " + NextHint(mo) : "";

            Ui.Show(eva, !mo.Inside);
            if (!mo.Inside)
            {
                float need = m.HoursToWalkBack, left = mo.EvaLeft - 0.5f;
                bool ok = need < left;
                evaWalk.text = $"Airlock {m.DistanceToHab:0} m  -  walk back ~{need:0.0} h  -  usable suit time {Mathf.Max(0, left):0.0} h";
                evaWalk.style.color = ok ? (need > left * 0.7f ? new Color(1f, 0.8f, 0.3f) : new Color(0.6f, 0.95f, 0.7f)) : new Color(1f, 0.4f, 0.35f);
            }

            UpdatePrompt(mo);
            UpdateMarkers();
            UpdateSystems();
            UpdateInside();

            bool crit = mo.O2ZeroHours > 0 || (!mo.Inside && mo.EvaLeft < 0.5f) || (mo.SpeActive && !mo.Inside);
            Ui.Show(banner, crit && !mo.Ended);
            if (crit) bannerText.text = mo.O2ZeroHours > 0 ? $"CABIN O2 EMPTY - failure in {2f - mo.O2ZeroHours:0.0} h. Switch the OGS on / get power to it!"
                                     : mo.SpeActive && !mo.Inside ? "SOLAR PARTICLE EVENT - get inside the habitat!"
                                     : "SUIT RESERVE - return to the airlock now!";
            Ui.Show(hud, m.Current == Mission1.Phase.Operations);
        }

        string NextHint(MissionModel mo)
        {
            if (mo.Inside && mo.EvaLeft < 0.6f) return "Suit is refilling. Speed up time (2/3) or rest, then exit with E.";
            if (m.Kit.Has("SEIS") && !mo.SeisDeployed && !mo.SeisCarried) return "Pick up the seismometer crate next to the airlock (orange marker).";
            if (mo.SeisCarried) return "Carry it beyond the orange 80 m ring, then press E to deploy. The rover (R) is faster.";
            if (mo.SeisDeployed && mo.SeisDistance < MissionModel.SeisMinDistance) return "Seismometer too close to the base. Pick it up and move it past the orange ring.";
            if (mo.RadReadings == 0 && m.Kit.Has("RAD")) return "Log a radiation reading at the RAD post beside the habitat.";
            if (mo.SamplesDelivered + mo.SamplesCarried < 2 && m.Kit.Has("SAMP")) return "Optional: collect 2 regolith samples (cyan beacons), then bring them inside.";
            if (mo.SamplesCarried > 0) return "Bring the samples into the habitat to deliver them.";
            if (!mo.Inside) return "Head back to the airlock. Inside you can speed up time and manage power (Tab).";
            if (mo.Hour < MissionModel.ShadowStart) return "Before hour 40: charge the battery. Open Systems (Tab) to switch loads off for the shadow window.";
            return "Keep the base alive until hour 72. Watch the battery and cabin O2.";
        }

        void UpdatePrompt(MissionModel mo)
        {
            string text = null;
            if (m.Rover != null && m.Rover.Driving) text = $"<b>WASD</b> drive (Shift = full speed)   <b>R</b> get out   -   rover battery {mo.RoverBattery:0.0}/{mo.RoverCap:0} PWh";
            else if (!mo.Inside && !m.SystemsOpen)
            {
                if (m.Focus != null) text = $"<b>E</b>  {m.Focus.Prompt()}";
                if (m.NearRover) text = (text != null ? text + "     " : "") + $"<b>R</b>  Board rover ({mo.RoverBattery:0.0} PWh)";
            }
            if (text == null && mo.SeisCarried && !mo.Inside && m.Focus == null && !(m.Rover != null && m.Rover.Driving))
            {
                var p = m.Player.transform.position; p.y = 0; var h = m.World.Hab.position; h.y = 0;
                float d = Vector3.Distance(p, h);
                text = d >= MissionModel.SeisMinDistance ? $"<b>E</b>  Deploy seismometer here ({d:0} m from habitat - quiet site)"
                                                         : $"<b>E</b>  Deploy seismometer here ({d:0} m - TOO CLOSE, need 80 m)";
            }
            Ui.Show(prompt, text != null);
            if (text != null) promptText.text = text.Replace("<b>", "<color=#FF963C><b>").Replace("</b>", "</b></color>");
        }

        void SetStat(string key, float t, string text, Severity s)
        {
            var (fill, val) = bars[key];
            Ui.SetBar(fill, t, s); val.text = text;
        }

        static void Colour(Label l, Severity s) =>
            l.style.color = s == Severity.Critical ? new Color(1f, 0.4f, 0.35f) : s == Severity.Warning ? new Color(1f, 0.82f, 0.3f) : new Color(0.93f, 0.95f, 0.98f);

        void UpdateMarkers()
        {
            var cam = Camera.main;
            bool show = !m.Model.Inside && !m.SystemsOpen;
            foreach (var i in Interactable.All)
            {
                if (i.Label == null) continue;
                if (!markerViews.TryGetValue(i, out var v))
                {
                    v = Ui.Box(markers, "marker");
                    Ui.Text(v, "", "marker-label");
                    var pin = Ui.Box(v, "marker-pin"); pin.style.backgroundColor = i.MarkerColor;
                    markerViews[i] = v;
                }
                var world = i.transform.position + Vector3.up * 2.4f;
                var sp = cam.WorldToViewportPoint(world);
                bool vis = show && i.isActiveAndEnabled && i.ShowMarker() && sp.z > 0;
                Ui.Show(v, vis);
                if (!vis) continue;
                var panelSize = markers.layout.size;
                v.style.left = sp.x * panelSize.x; v.style.top = (1f - sp.y) * panelSize.y;
                float d = Vector3.Distance(m.ActivePosition, i.transform.position);
                ((Label)v[0]).text = $"{i.Label}  {d:0} m";
            }
            foreach (var dead in markerViews.Keys.Where(k => k == null || !Interactable.All.Contains(k)).ToList())
            {
                markerViews[dead].RemoveFromHierarchy(); markerViews.Remove(dead);
            }
        }

        public void Toast(string text, Severity s)
        {
            if (alerts == null) return;
            var a = Ui.Box(alerts, "alert", "row");
            a.AddToClassList(s == Severity.Critical ? "crit-bg" : s == Severity.Warning ? "warn-bg" : s == Severity.Good ? "good-bg" : "");
            Ui.Dot(a, s);
            var hour = m.Model != null ? $"h{m.Model.Hour:00.0}  " : "";
            var l = Ui.Text(a, hour + text, "small"); l.style.flexShrink = 1;
            while (alerts.childCount > 4) alerts.RemoveAt(0);
            StartCoroutine(Expire(a, s == Severity.Critical ? 14f : 8f));
        }

        System.Collections.IEnumerator Expire(VisualElement e, float t)
        {
            yield return new WaitForSecondsRealtime(t);
            e.RemoveFromHierarchy();
        }

        // ================================================================== Systems panel (Tab)

        void BuildSystemsPanel()
        {
            systems = Ui.Box(root, "screen", "center");
            var p = Ui.Panel(systems); p.style.width = 1060;
            var head = Ui.Box(p, "row");
            Ui.Text(head, "OUTPOST SYSTEMS  -  POWER BUS", "h1"); Ui.Box(head, "grow");
            Ui.Button(head, "Close (Tab)", m.ToggleSystems);
            sysSummary = Ui.Text(p, "", "text");
            sysShadow = Ui.Text(p, "", "text", "accent");
            Ui.Text(p, "When supply + battery cannot cover the load, the lowest priority is shed first: 6 rover charging -> 4 science -> 3 comms -> 2 suit -> 1 life support.", "small", "muted");

            var hdr = Ui.Box(p, "table-row");
            Ui.Text(hdr, "", "dot"); Ui.Text(hdr, "Load", "cell-name", "muted"); Ui.Text(hdr, "PW", "cell", "muted"); Ui.Text(hdr, "Priority", "cell", "muted"); Ui.Text(hdr, "State", "cell-wide"); Ui.Text(hdr, "Switch", "cell", "muted");
            foreach (var l in m.Model.Loads)
            {
                var r = Ui.Box(p, "table-row");
                var dot = Ui.Dot(r, Severity.Good);
                Ui.Text(r, l.Name, "cell-name");
                Ui.Text(r, l.Pw.ToString("0.00"), "cell");
                Ui.Text(r, l.Priority.ToString(), "cell");
                var st = Ui.Text(r, "", "cell-wide");
                loadStates.Add((st, l)); loadDots.Add((dot, l));
                if (l.Id == "OGS")
                {
                    ogsBtn = Ui.Button(r, "", () => { m.Model.Ogs = (OgsMode)(((int)m.Model.Ogs + 1) % 3); }, "btn-small");
                    ogsBtn.style.width = 110;
                }
                else if (!l.Locked)
                {
                    var load = l;
                    var b = Ui.Button(r, "", () => load.Enabled = !load.Enabled, "btn-small");
                    b.style.width = 110;
                    loadButtons.Add((b, l));
                }
                else { var t = Ui.Text(r, "always on", "cell", "muted"); t.style.width = 110; }
            }
            Ui.Text(p, "OGS modes: OFF / AUTO (runs when battery > 60% and there is sun) / ON. Electrolysis: 1.0 PW + 0.11 L water per hour -> 0.10 kg O2/h.", "small", "muted").style.marginTop = 8;
            Ui.Show(systems, false);
        }

        void UpdateSystems()
        {
            bool open = m.SystemsOpen && !m.Model.Ended;
            Ui.Show(systems, open);
            if (!open) return;
            var mo = m.Model;
            sysSummary.text = $"Solar {mo.SolarPeak:0.0} PW peak x sun {mo.Sun:0%} x (1 - dust {mo.Dust:0%}) = {mo.Supply:0.00} PW   |   Load {mo.Demand:0.00} PW   |   Battery {mo.Battery:0.0}/{mo.BatteryCap:0} PWh";
            float toShadow = MissionModel.ShadowStart - mo.Hour;
            sysShadow.text = toShadow > 0
                ? $"Shadow window in {toShadow:0.0} h: 6 h at your current base load needs ~{mo.ShadowEnergyNeeded:0.0} PWh (battery max {mo.BatteryCap:0} PWh)."
                : mo.InShadow ? $"IN SHADOW: {MissionModel.ShadowEnd - mo.Hour:0.0} h left. Running on battery only." : "Shadow window passed.";
            foreach (var (lbl, l) in loadStates) lbl.text = l.Powered ? "Running" : (string.IsNullOrEmpty(l.Why) ? "Off" : l.Why);
            foreach (var (dot, l) in loadDots) Ui.SetDot(dot, l.Powered ? Severity.Good : (l.Why != null && l.Why.StartsWith("SHED")) ? Severity.Critical : Severity.Warning);
            foreach (var (b, l) in loadButtons) { b.text = l.Enabled ? "ON" : "OFF"; b.EnableInClassList("btn-on", l.Enabled); b.EnableInClassList("btn-off", !l.Enabled); }
            if (ogsBtn != null) { ogsBtn.text = mo.Ogs.ToString().ToUpper(); ogsBtn.EnableInClassList("btn-on", mo.Ogs != OgsMode.Off); ogsBtn.EnableInClassList("btn-off", mo.Ogs == OgsMode.Off); }
        }

        // ================================================================== Habitat panel

        void BuildInsidePanel()
        {
            inside = Ui.Box(root, "screen");
            var p = Ui.Panel(inside); p.style.position = Position.Absolute; p.style.left = 12; p.style.bottom = 200; p.style.width = 560;
            Ui.Text(p, "INSIDE THE HABITAT", "h2");
            insideStatus = Ui.Text(p, "", "text");
            var row = Ui.Box(p, "row", "wrap");
            Ui.Button(row, "Exit to surface (E)", m.ExitHabitat, "btn-primary");
            restBtn = Ui.Button(row, "Rest", m.ToggleRest);
            repairBtn = Ui.Button(row, "Repair suit (spare)", m.RepairSuit);
            Ui.Button(row, "Systems (Tab)", m.ToggleSystems);
            Ui.Text(p, "Time speed: buttons at the top or keys 1 / 2 / 3 (1x / 2x / 4x), P = pause.", "small", "muted");
            Ui.Show(inside, false);
        }

        void UpdateInside()
        {
            var mo = m.Model;
            bool show = mo.Inside && !m.SystemsOpen && !mo.Ended;
            Ui.Show(inside, show);
            if (!show) return;
            insideStatus.text = $"Suit refill: {mo.EvaLeft:0.0} / 4.0 h EVA time   |   Suit integrity {mo.SuitIntegrity:0}%   |   Spares {mo.Spares}\n" +
                                $"Samples delivered {mo.SamplesDelivered}   |   CO2 cartridges {mo.CartridgeKg:0.0} kg capacity left   |   O2 vented {mo.O2Vented:0.00} kg";
            restBtn.text = mo.Resting ? "Wake up" : "Rest (4x)";
            repairBtn.SetEnabled(mo.Spares > 0 && mo.SuitIntegrity < 100);
        }

        // ================================================================== Pause

        void BuildPauseMenu()
        {
            pause = Ui.Box(root, "screen", "center", "dim");
            var p = Ui.Panel(pause); p.style.width = 520; p.style.alignItems = Align.Stretch;
            Ui.Text(p, "PAUSED", "h1");
            Ui.Button(p, "Resume", () => m.SetPaused(false), "btn-primary");
            Ui.Button(p, "Controls", ToggleControls);
            SettingsUi.Build(p);
            Ui.Button(p, "Restart mission", m.Retry);
            Ui.Button(p, "Main menu", m.ToMenu);
            Ui.Button(p, "Quit game", Game.Quit);
            Ui.Show(pause, false);
            pause.schedule.Execute(() => Ui.Show(pause, m.Paused)).Every(100);
        }

        // ================================================================== Report

        public void ShowReport(MissionReport r, MissionModel mo)
        {
            Ui.Show(hud, false); Ui.Show(systems, false); Ui.Show(inside, false);
            var scr = Ui.Box(root, "screen", "center", "dim"); scr.pickingMode = PickingMode.Position;
            var p = Ui.Panel(scr); p.style.width = 1600; p.style.height = 960;
            var head = Ui.Box(p, "row", "noshrink");
            Ui.Text(head, r.Title, "h1", mo.Failed ? "bad" : mo.PrimaryDone == 3 ? "good" : "accent");
            Ui.Box(head, "grow");
            Ui.Text(head, $"SCORE {r.Score}  " + new string('*', r.Stars) + (r.Stars == 0 ? "  (pass = 50)" : ""), "h1", "accent");
            Ui.Text(p, r.Summary, "text", "noshrink");

            var cols = Ui.Box(p, "row", "grow"); cols.style.alignItems = Align.Stretch;
            var c1 = Ui.Add(cols, new ScrollView(), "grow"); c1.style.width = Length.Percent(34); c1.style.paddingRight = 16;
            Ui.Text(c1, "SCORE BREAKDOWN (GDD §26)", "h2");
            foreach (var (n, pts, max) in r.Breakdown)
            {
                var row = Ui.Box(c1, "row"); Ui.Text(row, n, "small", "grow"); Ui.Text(row, $"{pts:0.0} / {max:0}", "small");
                Ui.SetBar(Ui.Bar(c1).fill, pts / max, pts / max > 0.66f ? Severity.Good : pts / max > 0.33f ? Severity.Warning : Severity.Critical);
            }
            Ui.Text(c1, "OBJECTIVES", "h2");
            Obj(c1, mo.P1Seismometer, $"Seismometer >= 80 m, 24 h powered ({mo.SeisGoodHours:0.0} h)");
            Obj(c1, mo.P2Radiation, $"Radiation reading logged ({mo.RadReadings})");
            Obj(c1, mo.P3Survive, $"Survived 72 h (reached h{mo.Hour:0.0})");
            Obj(c1, mo.S1Samples, $"Optional: 2 samples ({mo.SamplesDelivered})");
            Obj(c1, mo.S2Oxygen, $"Optional: end O2 >= 3 kg ({mo.CabinO2:0.0} kg)");
            Obj(c1, mo.S3SuitOk, $"Optional: no suit damage (min {mo.MinIntegrity:0}%)");
            if (mo.QuakeRecorded) Ui.Text(c1, "Bonus: moonquake recorded at hour 52!", "text", "good");

            var c2 = Ui.Add(cols, new ScrollView(), "grow"); c2.style.width = Length.Percent(33); c2.style.paddingRight = 16;
            Ui.Text(c2, mo.Failed ? "WHY IT HAPPENED (cause chain)" : "KEY MOMENTS", "h2");
            if (mo.CauseChain.Count == 0) Ui.Text(c2, "No critical chain of events. Clean mission.", "text", "good");
            for (int i = 0; i < mo.CauseChain.Count; i++) Ui.Text(c2, (i == 0 ? "" : "-> ") + mo.CauseChain[i], "text", mo.Failed && i == mo.CauseChain.Count - 1 ? "bad" : "");
            Ui.Text(c2, "WHAT WOULD WORK NEXT TIME", "h2");
            foreach (var s in r.Suggestions) Ui.Text(c2, "- " + s, "text");
            Ui.Text(c2, "TIMELINE", "h2");
            foreach (var e in mo.Events.Where(e => e.Sev != Severity.Info).Take(40))
            {
                var row = Ui.Box(c2, "row"); Ui.Dot(row, e.Sev); Ui.Text(row, e.ToString(), "small").style.flexShrink = 1;
            }

            var c3 = Ui.Add(cols, new ScrollView(), "grow"); c3.style.width = Length.Percent(33);
            Ui.Text(c3, "SCIENCE DATA LOG (provenance)", "h2");
            if (mo.Data.Count == 0) Ui.Text(c3, "No data collected.", "text", "muted");
            foreach (var d in mo.Data)
            {
                Ui.Text(c3, $"h{d.Hour:00.0}  [{d.Instrument}]  {d.What}", "small");
                Ui.Text(c3, $"     where: {d.Where}  |  {d.Conditions}", "small", "muted").style.marginBottom = 6;
            }
            Ui.Text(c3, "REAL WORLD vs IN THE GAME", "h2");
            foreach (var (topic, real, game) in MissionReport.RealVsGame)
            {
                Ui.Text(c3, topic, "h3");
                Ui.Text(c3, "Real: " + real, "small");
                Ui.Text(c3, "Game: " + game, "small", "muted").style.marginBottom = 6;
            }

            var btns = Ui.Box(p, "row", "noshrink"); btns.style.justifyContent = Justify.FlexEnd; btns.style.marginTop = 10;
            Ui.Button(btns, "Main menu", m.ToMenu);
            Ui.Button(btns, "Retry mission", m.Retry, "btn-primary", "btn-big");
        }

        static void Obj(VisualElement parent, bool done, string text)
        {
            var r = Ui.Box(parent, "row"); Ui.Dot(r, done ? Severity.Good : Severity.Critical); Ui.Text(r, text, "small");
        }
    }
}
