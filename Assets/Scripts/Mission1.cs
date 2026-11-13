using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Mission 1 "First Light" director: Briefing -> Loadout -> Landing -> Surface operations -> Report.
    /// Owns the world objects and translates player actions into MissionModel calls.
    /// </summary>
    public class Mission1 : MonoBehaviour
    {
        public enum Phase { Briefing, Loadout, Operations, Report }

        /// <summary>Real seconds per mission hour at 1x. GDD §8 proposes 10 s; the 3D walking scale needs 30 s (DECISIONS D4).</summary>
        public const float RealSecondsPerHour = 30f;

        public Phase Current { get; private set; } = Phase.Briefing;
        public MissionModel Model { get; private set; }
        public MoonWorld World { get; private set; }
        DustStorm Storm;
        public Astronaut Player { get; private set; }
        public Rover Rover { get; private set; }
        public Rover Remote { get; private set; }   // RC-1 remote explorer, driven from inside the habitat (V)
        public Vector3 RemoteDock { get; private set; }
        public const float RemoteDockRadius = 5f;
        public Camera RemoteCam { get; private set; }   // RC-1's chase camera, rendered onto the handheld controller's screen
        public bool RoverDeskOpen;
        public void ToggleRoverDesk() => RoverDeskOpen = !RoverDeskOpen;
        public OrbitCamera Cam { get; private set; }
        public MissionUi Ui { get; private set; }
        public MissionReport Report { get; private set; }
        public Loadout Kit { get; private set; }
        public MissionPlan Plan { get; private set; }

        public int Speed { get; private set; } = 1;   // 0 pause, 1, 2, 4
        public float TestSpeed = 1f;                  // AutoTest only
        public bool Paused { get; private set; }      // pause menu
        public bool SystemsOpen { get; private set; }
        public Torch Torch { get; private set; }
        public bool InventoryOpen { get; private set; }
        public bool StoresOpen { get; private set; }
        public bool PlanOpen { get; private set; }

        // ---- player map marks: G drops one where you stand, clicks on the full map (hold M) place / remove them ----
        public static readonly (string name, Color col)[] MarkKinds =
            { ("Flag", new Color(1f, 0.6f, 0.15f)), ("Danger", new Color(1f, 0.3f, 0.28f)), ("Note", new Color(0.4f, 0.85f, 1f)) };
        public const int MaxMarks = 20;
        public readonly List<Interactable> Marks = new List<Interactable>();
        public int MarkKind;
        int markSerial;
        Transform marksRoot;

        /// <summary>Marks reuse Interactable for their HUD / map markers; Available=false so E never selects them.</summary>
        public void AddMark(Vector3 pos, bool indoor)
        {
            if (Marks.Count >= MaxMarks) { Ui.Toast($"Map is full ({MaxMarks} marks). Remove one first (G on it, or right-click on the map).", Severity.Info); return; }
            if (marksRoot == null) marksRoot = new GameObject("Map marks").transform;
            var (name, col) = MarkKinds[MarkKind];
            var mk = Interactable.Create($"{name} {++markSerial}", pos, 0f, marksRoot);
            mk.Available = () => false; mk.Indoor = indoor; mk.MarkerColor = col;
            Marks.Add(mk);
            Ui.Toast($"{mk.Label} placed. It shows on the map and in view. Press G on it to remove.", Severity.Info);
        }

        public bool RemoveMarkNear(Vector3 pos, float radius, bool indoor)
        {
            Interactable best = null; float bd = radius;
            foreach (var mk in Marks)
            {
                if (mk.Indoor != indoor) continue;
                float d = Flat(mk.transform.position - pos).magnitude;
                if (d <= bd) { bd = d; best = mk; }
            }
            if (best == null) return false;
            Marks.Remove(best); Destroy(best.gameObject);
            Ui.Toast($"{best.Label} removed.", Severity.Info);
            return true;
        }
        public bool InsideView => Model != null && Model.Inside;
        public bool CarryingSeis => Model != null && Model.SeisCarried;
        public float DistanceToHab => Player ? Flat(Player.transform.position - World.AirlockPoint).magnitude : 0f;
        public float WalkMetersPerHour => Astronaut.WalkSpeed * RealSecondsPerHour;

        public readonly List<Interactable> SampleSites = new List<Interactable>();
        public Interactable Focus { get; private set; }

        GameObject seisCrate, seisDeployed;
        LineRenderer noiseRing, walkRing;
        float ringTimer;
        MinimapSystem Minimap;
        FullMapSystem FullMap;
        public HabitatInterior Interior { get; private set; }
        public int SamplesAnalyzed { get; private set; }

        public CommsWorkstation Comms { get; private set; }
        float overrideSurveyUntil;

        public StepCtx Ctx()
        {
            var p = Player.transform.position;
            return new StepCtx { Pos = p, Yaw = Player.transform.eulerAngles.y, SlopeDeg = Slope(p) };
        }

        float Slope(Vector3 p)
        {
            const float h = 2f;
            float dx = (World.OnGround(p.x + h, p.z).y - World.OnGround(p.x - h, p.z).y) / (2f * h);
            float dz = (World.OnGround(p.x, p.z + h).y - World.OnGround(p.x, p.z - h).y) / (2f * h);
            return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
        }

        void TryDeploySeismometer()
        {
            var tk = Model.Tasks;
            if (!tk.Enabled || tk.AutoPass) { DeploySeismometer(); return; }
            if (Time.time < overrideSurveyUntil) { tk.AcceptRisk(); overrideSurveyUntil = 0f; DeploySeismometer(); return; }
            string why = tk.Survey(Ctx());
            if (why == null) { DeploySeismometer(); return; }
            Ui.Toast(why + "\nPress E again within 4 s to deploy here anyway (data quality drops).", Severity.Warning);
            overrideSurveyUntil = Time.time + 4f;
        }

        public void ShelterUse()
        {
            if (Model.Tasks.Do(Spot.Shelter, Ctx())) return;
            if (Model.SpeActive && Model.Inside) { Model.Sheltering = true; Ui.Toast("You settle back into the shielded corner.", Severity.Good); return; }
            Ui.Toast("Radiation shelter: this is where you ride out a solar particle event.", Severity.Info);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        void Awake()
        {
            Interactable.All.Clear();
            var cam = SceneKit.MakeCamera();
            World = new GameObject("Moon World").AddComponent<MoonWorld>();
            World.BuildEnvironment();
            if (Levels.Current.Mars) World.MakeMars();

            Cam = cam.gameObject.AddComponent<OrbitCamera>();
            Cam.World = World;
            var pivot = new GameObject("Briefing pivot").transform;
            pivot.position = World.OnGround(0, 0, 4f);
            Cam.Target = pivot; Cam.Distance = 70f; Cam.Height = 10f; Cam.AutoOrbit = true; Cam.Snap(200f, 12f);

            Ui = gameObject.AddComponent<MissionUi>();
            Ui.Init(this);
        }

        void Start()
        {
            Game.SetCursorFree(true);
            System.Action<Loadout> plan = null;
            plan = kit => Ui.ShowPlanner(kit, p => { Plan = p; Launch(kit); }, () => Ui.ShowLoadout(plan));
            Ui.ShowBriefing(() => { Current = Phase.Loadout; Ui.ShowLoadout(plan); });
        }

        // ------------------------------------------------------------------ Launch / landing

        public void Launch(Loadout kit)
        {
            Kit = kit.Clone();
            Model = new MissionModel(Kit);
            Plan ??= MissionPlan.Suggested(Kit);
            Model.OnEvent += OnModelEvent;
            World.BuildOutpost(Kit);
            BuildInteractables();
            Interior = HabitatInterior.Build(this);
            HabitatAmenities.Build(this, Interior.transform);   // galley + wash corner

            Player = Astronaut.Create(World.AirlockPoint + new Vector3(2.5f, 0.2f, -1.5f));
            Player.transform.rotation = Quaternion.Euler(0, MoonWorld.EarthAzimuth - 25f, 0);
            Player.HardLanding += speed =>
            {
                float dmg = Mathf.Round(4f * (speed - Astronaut.HardLandingSpeed + 1f));
                Model.SuitIntegrity = Mathf.Max(0f, Model.SuitIntegrity - dmg);
                Model.Log(Severity.Warning, "Ouch! You fell and scratched your suit. Use smaller jumps.");
            };
            Torch = Player.gameObject.AddComponent<Torch>();
            Torch.Init(Cam.transform, () => Model.Inside || Player.Dead, Player.EvaRig);
            if (Kit.Has("ROV-SC"))
            {
                Rover = Rover.Create(World, World.RoverStart);
                Rover.UseEnergy = m => Model.UseRoverEnergy(m);
            }

            RemoteDock = World.RemoteDockPoint;   // beside the crewed rover, in front of the garage
            Remote = Rover.Create(World, RemoteDock, "Remote Rover (RC-1)", 1.3f, "MarsRover");   // HD-only model: scale = metres
            MoonWorld.Spawn("ChargingStation", RemoteDock + Vector3.down * 0.02f, Quaternion.identity, 2.6f, null, "RC-1 charging pad");   // no collider: RC-1 parks on it
            Remote.UseEnergy = m => Model.UseRemoteEnergy(m);
            RemoteCam = new GameObject("RC-1 camera").AddComponent<Camera>();
            RemoteCam.transform.SetParent(Remote.transform, false);
            SetRemoteView(true);
            RemoteCam.nearClipPlane = 0.05f; RemoteCam.targetTexture = new RenderTexture(640, 400, 24); RemoteCam.enabled = false;
            World.SetRing(World.MakeRing("Remote rover dock (5 m)", new Color(0.95f, 0.75f, 0.3f, 0.85f), 0.25f), RemoteDock, RemoteDockRadius);

            noiseRing = World.MakeRing("Seismometer noise radius (80 m)", new Color(1f, 0.55f, 0.15f, 0.9f), 0.45f);
            World.SetRing(noiseRing, World.Hab.position, MissionModel.SeisMinDistance);
            walkRing = World.MakeRing("Walk-back circle", new Color(0.3f, 0.85f, 0.95f, 0.8f), 0.35f);
            if (Kit.Has("GAR")) World.SetRing(World.MakeRing("Rover charging bay (9 m)", new Color(0.35f, 0.95f, 0.45f, 0.85f), 0.3f), World.GaragePoint, 9f);   // matches RoverNearGarage

            Cam.AutoOrbit = false; Cam.Target = Player.transform; Cam.Distance = 4.4f; Cam.Height = 1.5f;
            Cam.Snap(MoonWorld.EarthAzimuth - 25f, 8f);   // sun behind the camera, Earth on the horizon
            Model.Inside = false;
            Model.Log(Severity.Info, Levels.Current.Tutorial
                ? "You are outside, next to the door of your base. First, go inside and check the base at the computer (Task A)."
                : "You are outside, next to the door of your base. Look at WHAT TO DO NOW on the right.");
            Current = Phase.Operations;
            Ui.StartHud();
            AddGauges();
            // Minimap – created after the world and interactables are fully built
            Minimap = gameObject.AddComponent<MinimapSystem>();
            Minimap.Init(Ui.Root, this);
            FullMap = gameObject.AddComponent<FullMapSystem>();
            FullMap.Init(Ui.Root, this);

            // Tasks
            Model.Tasks.Enabled = Levels.Current.Tutorial;   // Task A-G procedures are the level 1 tutorial
            Model.Tasks.ManifestValid = () => Kit.WithinLimits;   // Task A: cargo within mass / budget limits
            Model.Tasks.Say = (sev, text) => { Ui.Toast(text, sev); Model.Log(sev, text.Replace("\n", "  ")); };

            // CommsNetwork
            var lv = Levels.Current.SampleSites;
            Model.Net.Init(World.ComPoint, p => World.HeightAt(p),
                           System.Array.ConvertAll(lv, s => s.name), System.Array.ConvertAll(lv, s => World.OnGround(s.xz.x, s.xz.y)),
                           () => Rover != null ? (Vector3?)Rover.transform.position : null);
            Comms = gameObject.AddComponent<CommsWorkstation>();
            Comms.Init(Ui.Root, this);

            Game.SetCursorFree(false);
        }

        /// <summary>Floating level readouts over the hardware: O2 storage, power storage, both rover batteries and the suit's tank.</summary>
        void AddGauges()
        {
            var up = Vector3.up * 3.2f;
            System.Func<float> o2 = () => Model.CabinO2Cap > 0 ? Model.CabinO2 / Model.CabinO2Cap : 0f;
            System.Func<string> o2Text = () => $"AIR TANK  {o2():0%}";
            var tanker = World.OnGround(World.AirlockPoint.x + 2f, 4.5f) + up;
            Ui.AddGauge(() => tanker, o2Text, o2);
            if (Kit.Has("TNK-O")) { var t = World.O2TanksPoint + up; Ui.AddGauge(() => t, o2Text, o2); }
            if (Kit.Has("BAT"))
            {
                var b = World.BatteryPoint + up;
                Ui.AddGauge(() => b, () => $"BATTERY  {Model.BatteryPct:0%}", () => Model.BatteryPct);
            }
            if (Rover != null)
                Ui.AddGauge(() => Rover.transform.position + Vector3.up * 2.6f,
                            () => $"ROVER BATTERY  {(Model.RoverCap > 0 ? Model.RoverBattery / Model.RoverCap : 0f):0%}", () => Model.RoverCap > 0 ? Model.RoverBattery / Model.RoverCap : 0f);
            Ui.AddGauge(() => Remote.transform.position + Vector3.up * 1.8f, () => $"ROBOT ROVER  {Model.RemoteBattery:0}%", () => Model.RemoteBattery / 100f);
            // suit tank: above the backpack, hidden while driving (the rover gauge is there)
            Ui.AddGauge(() => Rover != null && Rover.Driving ? Vector3.down * 1000f : Player.transform.position + Vector3.up * 2.3f - Player.transform.forward * 0.3f,
                        () => $"SUIT AIR  {Model.SuitO2:0.0} h", () => Model.SuitO2 / MissionModel.SuitO2Max);
        }

        void BuildInteractables()
        {
            var parent = new GameObject("Interactables").transform;

            var airlock = Interactable.Create("Airlock", World.AirlockPoint, 3.2f, parent);
            airlock.Prompt = () => "Enter the habitat (refill suit, rest, shelter)";
            airlock.Use = EnterHabitat;
            airlock.ShowMarker = () => !Model.Inside;

            if (Kit.Has("SEIS"))
            {
                seisCrate = MoonWorld.Spawn("PickupCrate", World.SeisCratePoint + Vector3.up * 0.45f, Quaternion.Euler(0, 20, 0), 1.0f, parent, "SEIS crate");
                var crate = Interactable.Create("Seismometer", World.SeisCratePoint, 2.6f, parent);
                crate.MarkerColor = new Color(1f, 0.6f, 0.2f);
                crate.Available = () => !Model.SeisCarried && !Model.SeisDeployed && seisCrate.activeSelf;
                crate.ShowMarker = crate.Available;
                crate.Prompt = () => "Pick up the seismometer";
                crate.Use = () =>
                {
                    var carried = MoonWorld.Spawn("PickupCrate", seisCrate.transform.position, seisCrate.transform.rotation, 1.0f, null, "SEIS (carried)");
                    if (carried == null) { Model.PickUpSeismometer(); seisCrate.SetActive(false); return; }
                    if (!Player.PickUp(carried, CarriedCrateScale, () => { Model.PickUpSeismometer(); seisCrate.SetActive(false); })) Destroy(carried);
                };
            }

            if (Kit.Has("RAD"))
            {
                var rad = Interactable.Create("Radiation Monitor", World.RadPoint, 3f, parent);
                rad.Prompt = () => "Log a radiation reading";
                rad.Use = () => Model.LogRadiationReading();
                rad.ShowMarker = () => Model.RadReadings == 0;
            }

            if (Levels.Current.SeisSite is Vector2 ss && Kit.Has("SEIS"))
            {
                var sp = World.OnGround(ss.x, ss.y);
                World.MakeBeacon(sp, new Color(1f, 0.55f, 0.15f, 0.2f), 14f);
                World.SetRing(World.MakeRing("Seismometer site", new Color(1f, 0.55f, 0.15f, 0.95f), 0.4f), sp, LevelDef.SeisSiteRadius);
                var mark = Interactable.Create("Seismometer site", sp, 0f, parent);
                mark.Available = () => false; mark.MarkerColor = new Color(1f, 0.55f, 0.15f);
                mark.ShowMarker = () => !Model.P1Seismometer;
            }

            foreach (var (name, xz) in Levels.Current.SampleSites)
            {
                var p = World.OnGround(xz.x, xz.y);
                var rock = MoonWorld.Spawn("Rock_34W5ymEePk", p + Vector3.down * 0.3f, Quaternion.Euler(0, xz.x, 0), 0.32f, parent, "Sample site " + name);
                if (rock) foreach (var r in rock.GetComponentsInChildren<Renderer>()) r.sharedMaterial = Resources.Load<Material>("Materials/SampleRock");
                var beacon = World.MakeBeacon(p, new Color(0.3f, 0.85f, 0.95f, 0.16f), 10f);
                var site = Interactable.Create("Sample " + name, p, 2.8f, parent);
                bool taken = false;
                site.Available = () => !taken;
                site.ShowMarker = () => !taken && Kit.Has("SAMP");
                site.Prompt = () => $"Collect regolith sample {name}";
                site.Use = () =>
                {
                    if (Player.Busy || Player.IsCarrying) { Ui.Toast(Player.IsCarrying ? "Your hands are full: put the seismometer down first (E in open ground)." : "One moment...", Severity.Info); return; }
                    if (!Model.CollectSample(name, Flat(p - World.Hab.position).magnitude)) return;
                    Model.Net.SurveyAt(name);   // radio check at this location
                    taken = true; beacon.SetActive(false);
                    var chunk = MoonWorld.Spawn("Rock_34W5ymEePk", p + Vector3.up * 0.05f, Quaternion.Euler(0, xz.x, 0), 0.08f, null, "Sample " + name + " (carried)");
                    if (chunk)
                    {
                        foreach (var r in chunk.GetComponentsInChildren<Renderer>()) r.sharedMaterial = Resources.Load<Material>("Materials/SampleRock");
                        if (!Player.PickUp(chunk, 0.08f, null, false)) Destroy(chunk);
                    }
                };
                SampleSites.Add(site);
            }

            for (int i = 0; i < World.SolarArrays.Count; i++)
            {
                var a = World.SolarArrays[i];
                int idx = i;   // capture for closures
                var clean = Interactable.Create(null, a.position, 3.2f, parent);
                clean.Available = () => idx < Model.PanelDust.Length && Model.PanelDust[idx] > 0.02f;
                clean.Prompt = () => idx < Model.PanelDust.Length
                    ? $"Clean panel {idx + 1} ({Model.PanelDust[idx]:0%} dust)"
                    : $"Clean dust off solar panel {idx + 1}";
                clean.Use = () => { if (!Model.CleanPanel(idx)) Ui.Toast("Panel is already clean.", Severity.Info); };
            }

            var lander = Interactable.Create(null, World.OnGround(Levels.Current.Lander.x, Levels.Current.Lander.y), 7f, parent);
            lander.Prompt = () => $"Lander: the crew vehicle that brought you (mission ends at hour {MissionModel.DurationH:0})";
            lander.Use = () => Ui.Toast($"The lander is your ride home. Survive to hour {MissionModel.DurationH:0} and complete the objectives.", Severity.Info);

            // Main O2 tanker beside the airlock: the habitat's oxygen supply (Model.CabinO2: the OGS fills it, the cabin breathes from it)
            // and an outdoor suit refill point, so you don't have to cycle the airlock
            var o2Pos = World.OnGround(World.AirlockPoint.x + 2f, 4.5f);   // beside the airlock ladder, clear of the hab walls
            MoonWorld.AddBoxCollider(MoonWorld.Spawn("OxygenTank", o2Pos, Quaternion.identity, 1.2f, parent, "Main O2 tanker"));
            var o2Post = Interactable.Create("Main O2 tanker", o2Pos, 3.2f, parent);
            o2Post.MarkerColor = new Color(0.43f, 0.7f, 1f);
            o2Post.Available = () => !Model.Inside;
            o2Post.ShowMarker = () => !Model.Inside && Model.EvaLeft < 2f;
            o2Post.Prompt = () => $"Refill suit oxygen ({Model.EvaLeft:0.0} h left, tanker {Model.CabinO2:0.0}/{Model.CabinO2Cap:0} kg)";
            o2Post.Use = () => { if (!cinematic) StartCoroutine(HoseSwapRoutine()); };

            // Outdoor hardware you can walk up to and read: every machine on the pad now answers when you press E
            System.Func<Vector3, float, System.Func<bool>> near = (pos, r) => () => !Model.Inside && Player != null && Flat(Player.transform.position - pos).magnitude < r;

            if (Kit.Has("OGS"))
                AddInspect(parent, "Oxygen plant", World.OgsPoint, 4.2f, "Inspect the oxygen plant", () =>
                {
                    var l = Model.Load("OGS");
                    string st = l == null ? "n/a" : l.Powered ? "RUNNING" : (string.IsNullOrEmpty(l.Why) ? "idle" : l.Why);
                    return $"Oxygen plant (electrolysis): {st}. Mode {Model.Ogs}. Makes 0.10 kg O2/h from water into the main O2 tanker by the airlock: {Model.CabinO2:0.0}/{Model.CabinO2Cap:0} kg, water {Model.Water:0} L. Change the mode in the Systems panel (Tab) inside.";
                }, near);
            if (Kit.Has("BAT"))
                AddInspect(parent, "Battery bank", World.BatteryPoint, 4.5f, "Read the battery bank", () =>
                    $"Battery bank: {Model.Battery:0.0}/{Model.BatteryCap:0} PWh ({Model.BatteryPct:0%}). Solar {Model.Supply:0.0} PW in, {Model.Demand:0.0} PW out. Charge it before the shadow window at hour {MissionModel.ShadowStart:0}.", near);
            if (Kit.Has("TNK-O"))
                AddInspect(parent, "Oxygen tanks", World.O2TanksPoint, 4.2f, "Read the oxygen tank gauge", () =>
                    $"Oxygen storage: cabin holds {Model.CabinO2:0.0} of {Model.CabinO2Cap:0} kg. {Model.O2Vented:0.00} kg has been vented so far (a full cabin cannot store more).", near);
            if (Kit.Has("TNK-W"))
                AddInspect(parent, "Water tank", World.WaterTankPoint, 3.6f, "Read the water tank gauge", () =>
                    $"Water: {Model.Water:0} of {Model.WaterCap:0} L. The oxygen plant uses 1.125 L per kg of O2; the crew drinks about 3 L a day.", near);
            if (Kit.Has("STO"))
                AddInspect(parent, "Storage pallet", World.StoragePoint, 4.2f, "Check the storage pallet", () =>
                    $"Storage: {Model.SamplesDelivered}/{Model.SampleCapacity} samples stored, {Model.SamplesCarried} in your bag, {Model.Spares} spare-parts kit(s), {Model.CartridgeKg:0.0} kg of scrubber capacity left.", near);
            if (Kit.Has("GAR"))
                AddInspect(parent, "Rover garage", World.GarageInspectPoint, 6f, "Check the rover garage", () =>
                {
                    var g = Model.Load("GAR");
                    string st = g == null ? "n/a" : g.Powered ? "CHARGING" : (string.IsNullOrEmpty(g.Why) ? "idle" : g.Why);
                    return $"Rover garage: {st}. " + (Rover != null ? $"Rover battery {Model.RoverBattery:0.0}/{Model.RoverCap:0} PWh. Park inside the 9 m charging zone in front of the door to recharge." : "No rover packed.");
                }, near);
            if (Kit.Has("COM"))
                AddInspect(parent, "Radio antenna", World.ComPoint, 4.5f, "Check the radio antenna", () =>
                {
                    var n = Model.Net;
                    n.Inspected = true; n.Version++;
                    n.Log.Add($"h{Model.Hour:0.0}  You checked the antenna outside.");
                    string verdict = n.PointingErrDeg > 1.5f ? "It is not pointing at Earth. Press 'Fix the aim' at the radio desk inside."
                                   : n.CableLossDb > 1f ? "The cable is loose. Press 'Fix the cable' at the radio desk inside."
                                   : "It looks good!";
                    return $"Radio antenna: {verdict}";
                }, near);
        }

        void AddInspect(Transform parent, string name, Vector3 pos, float radius, string prompt, System.Func<string> info, System.Func<Vector3, float, System.Func<bool>> near)
        {
            var it = Interactable.Create(name, pos, radius, parent);
            it.MarkerColor = new Color(0.55f, 0.75f, 0.95f);
            it.Prompt = () => prompt;
            it.ShowMarker = near(pos, 24f);
            it.Use = () => Ui.Toast(info(), Severity.Info);
        }

        // the seismometer crate is held in both hands (Astronaut.PickUp / PutDown); this is its size while carried
        const float CarriedCrateScale = 0.45f, PlacedCrateScale = 0.7f;

        void DeploySeismometer(bool instant = false)
        {
            var p = Player.transform.position + Player.transform.forward * 0.6f;
            p = World.OnGround(p.x, p.z);
            if (instant || !Player.IsCarrying) { Player.DropHeld(); FinishDeploy(p); return; }
            // lower the crate to the ground in front of the astronaut; the placed seismometer appears the moment the hands let go
            if (!Player.PutDown(p + Vector3.up * 0.3f, PlacedCrateScale, () => FinishDeploy(p))) { Player.DropHeld(); FinishDeploy(p); }
        }

        void FinishDeploy(Vector3 p)
        {
            float d = Flat(p - World.Hab.position).magnitude;
            Model.DeploySeismometer(d, p);
            if (seisDeployed) Destroy(seisDeployed);
            seisDeployed = new GameObject("SEIS (deployed)");
            seisDeployed.transform.position = p;
            var crate = MoonWorld.Spawn("PickupCrate", p + Vector3.up * 0.3f, Quaternion.identity, 0.7f, seisDeployed.transform, "Seismometer body");
            var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(mast.GetComponent<Collider>());
            mast.transform.SetParent(seisDeployed.transform);
            mast.transform.position = p + Vector3.up * 1.1f; mast.transform.localScale = new Vector3(0.05f, 0.6f, 0.05f);
            var lamp = new GameObject("Status light").AddComponent<Light>();
            lamp.transform.SetParent(seisDeployed.transform); lamp.transform.position = p + Vector3.up * 1.8f;
            lamp.type = LightType.Point; lamp.range = 6f; lamp.intensity = 3f;
            lamp.color = d >= MissionModel.SeisMinDistance ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.7f, 0.2f);
            var beacon = World.MakeBeacon(p, d >= MissionModel.SeisMinDistance ? new Color(0.3f, 1f, 0.4f, 0.16f) : new Color(1f, 0.6f, 0.2f, 0.16f), 12f);
            beacon.transform.SetParent(seisDeployed.transform, true);

            var pick = Interactable.Create("Seismometer", p, 2.6f, seisDeployed.transform);
            pick.MarkerColor = lamp.color;
            pick.Prompt = () => Model.Tasks.Prompt(Spot.Seis, Player.transform.eulerAngles.y) ?? "Pick up the seismometer (to move it)";
            pick.Use = () =>
            {
                if (Model.Tasks.Do(Spot.Seis, Ctx(), one: true)) return;
                var carried = MoonWorld.Spawn("PickupCrate", p + Vector3.up * 0.3f, Quaternion.identity, PlacedCrateScale, null, "SEIS (carried)");
                if (carried == null) { Model.PickUpSeismometer(); Destroy(seisDeployed); seisDeployed = null; return; }
                if (!Player.PickUp(carried, CarriedCrateScale, () => { Model.PickUpSeismometer(); Destroy(seisDeployed); seisDeployed = null; })) Destroy(carried);
            };
        }

        // ------------------------------------------------------------------ Habitat

        // Going in / out is a short sequence: outer hatch -> airlock tunnel -> inner door -> suit alcove (suit off), and back.
        bool cinematic;
        public bool InCinematic => cinematic;

        void EnterHabitat()
        {
            if (cinematic) return;
            if (Interior == null || Player == null || !Player.gameObject.activeSelf) { EnterHabitatInstant(); return; }
            StartCoroutine(EnterRoutine());
        }

        // the interior airlock door used to be at the old room's -Z; these turn the old directions to wherever it is now
        static Quaternion DoorFrame => Quaternion.Euler(0f, HabitatInterior.AirlockYaw, 0f);
        static Vector3 InShot(Vector3 oldOffset) => DoorFrame * oldOffset;

        void InteriorCamera(float yaw, float pitch)
        {
            Cam.Target = Player.transform; Cam.AutoOrbit = false; Cam.Distance = 2.9f; Cam.Height = 1.45f;
            Cam.ClampToTerrain = false; Cam.MaxPitch = 36f; Cam.Snap(yaw, pitch);
        }

        IEnumerator EnterRoutine()
        {
            cinematic = true; SystemsOpen = false; Game.SetCursorFree(false);
            Player.BeginScript();
            var hatch = World.OuterHatch; var ch = World.Chamber;
            if (hatch != null && ch != null)
            {
                // 1. outside, at the astronaut's eye height: walk to the foot of the stair, the hatch unlocks and swings open - and stays open
                Cam.FollowShot(new Vector3(2.6f, 0f, -0.9f));
                yield return Player.WalkTo(World.AirlockFront);
                yield return Player.TurnTo(-90f, 0.5f);
                yield return hatch.SetOpen(true);
                // 2. up the stair and through the open hatch, camera following right behind into the chamber
                Cam.FollowShot(new Vector3(1.9f, 0f, -0.6f), eye: 1.85f);          // off to the side of the life-support pack
                yield return Player.WalkTo(World.AirlockStairFoot, 1.1f, stop: false);
                yield return Player.WalkTo(World.AirlockThreshold, 0.9f, stop: false);
                yield return Player.WalkTo(World.AirlockInside, 1.1f);
                // 3. only now does the hatch close: turn to watch it, camera moves to the inner-door end looking back
                Cam.FollowShot(new Vector3(-1.3f, 0f, -0.7f), eye: 1.85f);
                yield return Player.TurnTo(90f, 0.8f);
                hatch.Open = false;
                yield return hatch.Wait();
                // 4. airlock cycle in the chamber
                yield return Player.TurnTo(-90f, 0.8f);
                yield return ch.Repressurize(Player, msg => Ui.Toast(msg, Severity.Info));
                // 5. inner door opens, step through into the habitat
                yield return ch.InnerDoor.SetOpen(true);
                Cam.FollowShot(new Vector3(1.3f, 0f, -0.7f), eye: 1.85f);
                yield return Player.WalkTo(World.AirlockInnerPoint, 1.1f);
                yield return Fade(1f, 0.35f);
                ch.InnerDoor.Open = false; ch.ShowIdle(false);
            }
            Model.EnterHabitat();
            Player.SetIndoors(true, suited: true);
            Player.Teleport(HabitatInterior.DoorwayPoint); Player.BeginScript(); Player.EndScript();   // re-disable the controller after Teleport
            Player.transform.rotation = Quaternion.LookRotation(DoorFrame * Vector3.forward);
            InteriorCamera(HabitatInterior.AirlockYaw, 10f);
            Interior.AirlockDoor.Open = true;
            Cam.FollowShot(InShot(new Vector3(0.8f, 0f, -1.0f)), snap: true);   // behind the astronaut, stepping into the room
            yield return Fade(0f, 0.35f);
            yield return Player.WalkTo(HabitatInterior.InsideDoorPoint, 1.2f);
            Interior.AirlockDoor.Open = false;
            Cam.EndShot();
            Cam.Snap(235f, 8f);                                                  // three-quarter view of the suit alcove
            yield return Player.SuitOff(HabitatInterior.DockStand, HabitatInterior.DockYaw, HabitatInterior.HelmetShelf, HabitatInterior.SuitHang);
            Player.EndScript(); cinematic = false;
            Ui.Toast("Inside the habitat. Suit stowed in the alcove. Walk to a station and press E: bunk, lab, console, suit dock, comms, health scan, window.", Severity.Info);
        }

        // full-screen black fade (only used at the inner airlock door, where the surface airlock joins the habitat interior)
        UnityEngine.UIElements.VisualElement fade;
        IEnumerator Fade(float to, float dur)
        {
            if (fade == null && Ui != null && Ui.Root != null)
            {
                fade = new UnityEngine.UIElements.VisualElement { name = "airlock-fade", pickingMode = UnityEngine.UIElements.PickingMode.Ignore };
                fade.style.position = UnityEngine.UIElements.Position.Absolute;
                fade.style.left = 0; fade.style.top = 0; fade.style.right = 0; fade.style.bottom = 0;
                fade.style.backgroundColor = Color.black; fade.style.opacity = 0f;
                Ui.Root.Add(fade);
            }
            if (fade == null) yield break;
            float from = fade.resolvedStyle.opacity;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime) { fade.style.opacity = Mathf.Lerp(from, to, t / dur); yield return null; }
            fade.style.opacity = to;
        }

        /// <summary>Old instant switch (scripted tests, or no interior).</summary>
        void EnterHabitatInstant()
        {
            Model.EnterHabitat();
            if (Interior != null)
            {
                // walk around inside: the astronaut moves into the interior room and the camera follows
                Player.gameObject.SetActive(true);
                Player.SetIndoors(true);
                Player.Teleport(HabitatInterior.SpawnPoint);
                Player.transform.rotation = Quaternion.LookRotation(DoorFrame * Vector3.forward);
                Cam.Target = Player.transform; Cam.AutoOrbit = false; Cam.Distance = 2.9f; Cam.Height = 1.45f;
                Cam.ClampToTerrain = false; Cam.MaxPitch = 36f; Cam.Snap(HabitatInterior.AirlockYaw, 12f);
                SystemsOpen = false;
                Game.SetCursorFree(false);
                Ui.Toast("Inside the habitat. Walk to a station and press E: bunk, lab, console, suit dock, comms, health scan, window.", Severity.Info);
            }
            else
            {
                Player.gameObject.SetActive(false);
                Cam.Target = World.Hab; Cam.Distance = 30f; Cam.Height = 3f; Cam.AutoOrbit = true;
                SystemsOpen = false;
                Game.SetCursorFree(true);
            }
        }

        public void ExitHabitat()
        {
            if (cinematic) return;
            if (!Model.Tasks.CanExit(out var exitWhy)) { Ui.Toast(exitWhy, Severity.Warning); return; }
            if (Model.EvaLeft < 0.6f) { Model.ExitHabitat(); Ui?.Toast($"Suit not ready: {Model.EvaLeft:0.0} h. Rest or speed up time while it refills.", Severity.Warning); return; }
            if (Interior == null || Player == null || !Player.gameObject.activeSelf || Player.InBed) { ExitHabitatInstant(); return; }
            if (!Player.SuitedIndoors)
            {
                Ui.Toast("You can't go outside without your EVA suit - there is no air out there. Put it on at the suit alcove next to the airlock (press E there).", Severity.Warning);
                return;
            }
            Model.Tasks.Do(Spot.Door, Ctx());   // radio / helmet lamp check
            StartCoroutine(ExitRoutine());
        }

        /// <summary>Suit alcove: put the EVA suit on (step in, helmet on, suit check) or take it off again.</summary>
        public void SuitUp()
        {
            if (cinematic || Player == null || !Model.Inside) return;
            StartCoroutine(SuitRoutine(!Player.SuitedIndoors));
        }

        IEnumerator SuitRoutine(bool on)
        {
            cinematic = true; SystemsOpen = false; Game.SetCursorFree(false);
            if (Model.Resting) ToggleRest();
            Player.BeginScript();
            Cam.Snap(235f, 8f);                                      // three-quarter view of the alcove
            if (on)
            {
                Ui.Toast("Suiting up: step in through the back hatch, then the helmet locks onto the neck ring.", Severity.Info);
                yield return Player.SuitOn(HabitatInterior.DockStand, HabitatInterior.DockYaw);
                Ui.Toast(Model.EvaLeft < 0.6f ? $"Suit on, but it is still refilling ({Model.EvaLeft:0.0} h). Wait or rest before you go out."
                                              : $"Suit check OK: pressure 29.6 kPa, O2 flowing, comms on. {Model.EvaLeft:0.0} h of EVA. Head to the airlock.", Severity.Good);
                Model.Tasks.Do(Spot.SuitDock, Ctx());   // Task C: the suit and backpack checks happen while suiting up
            }
            else
            {
                Player.SetIndoors(true, suited: true);
                yield return Player.SuitOff(HabitatInterior.DockStand, HabitatInterior.DockYaw, HabitatInterior.HelmetShelf, HabitatInterior.SuitHang);
                Ui.Toast("Suit stowed in the alcove. It keeps refilling while you are inside.", Severity.Info);
            }
            Player.EndScript(); cinematic = false;
        }

        // Outdoor refill: the suit's O2 line is unplugged before the tanker hose locks on. No air in between, so health drops.
        public const float HoseSwapSeconds = 4f, HoseSwapHarmPerSec = 2.5f;   // real seconds; ~10 health per swap

        IEnumerator HoseSwapRoutine()
        {
            if (!Model.CanRefillSuitOutside(out var why)) { Ui.Toast(why, Severity.Warning); yield break; }
            cinematic = true; Player.BeginScript();
            Ui.Toast($"Suit O2 line detached: no air until the tanker hose locks on ({HoseSwapSeconds:0} s). Hold your breath.", Severity.Warning);
            for (float t = 0f; t < HoseSwapSeconds && !Model.Ended; t += Time.deltaTime)
            {
                Model.Health = Mathf.Max(0f, Model.Health - HoseSwapHarmPerSec * Time.deltaTime);   // paused: deltaTime 0, no harm
                yield return null;
            }
            bool ok = Model.RefillSuitOutside(out var msg); Ui.Toast(msg, ok ? Severity.Good : Severity.Warning);
            Player.EndScript(); cinematic = false;
        }

        IEnumerator ExitRoutine()
        {
            cinematic = true; SystemsOpen = false; Game.SetCursorFree(false);
            if (Model.Resting) ToggleRest();
            Player.BeginScript();
            // 1. in the room: walk to the airlock door, it slides open, step into the doorway
            Cam.FollowShot(InShot(new Vector3(0.8f, 0f, 1.25f)));
            yield return Player.WalkTo(HabitatInterior.InsideDoorPoint);
            yield return Player.TurnTo(180f + HabitatInterior.AirlockYaw);
            yield return Interior.AirlockDoor.SetOpen(true);
            yield return Player.WalkTo(HabitatInterior.DoorwayPoint, 1.1f);
            yield return Fade(1f, 0.35f);
            Interior.AirlockDoor.Open = false;
            if (!Model.ExitHabitat()) { Cam.EndShot(); yield return Fade(0f, 0.3f); Player.EndScript(); cinematic = false; yield break; }
            Cam.ClampToTerrain = true; Cam.MaxPitch = 70f;
            Player.SetIndoors(false);
            var hatch = World.OuterHatch; var ch = World.Chamber;
            Cam.Target = Player.transform; Cam.Distance = 4.4f; Cam.Height = 1.5f; Cam.AutoOrbit = false;
            if (hatch != null && ch != null)
            {
                // 2. in the surface airlock chamber, coming through the inner door
                Player.Teleport(World.AirlockInnerPoint); Player.BeginScript(); Player.EndScript();
                Player.transform.rotation = Quaternion.LookRotation(Vector3.right);
                ch.InnerDoor.Open = true; ch.ShowIdle(true);
                Cam.FollowShot(new Vector3(1.3f, 0f, -0.7f), snap: true, eye: 1.85f);   // ahead of the astronaut, looking back at the inner door
                yield return Fade(0f, 0.35f);
                yield return Player.WalkTo(World.AirlockInside, 1.1f);
                ch.InnerDoor.Open = false;
                yield return ch.InnerDoor.Wait();
                Model.Tasks.Do(Spot.Airlock, Ctx(), one: true);   // C5: inner hatch closed and sealed
                // 3. pump down to vacuum
                yield return ch.Depressurize(msg => Ui.Toast(msg, Severity.Info));
                Model.Tasks.Do(Spot.Airlock, Ctx(), one: true);   // C6: depressurised before outer hatch opens
                // 4. the hatch unlocks, pops and swings open, and stays open while the camera follows the astronaut out and down the stair
                yield return hatch.SetOpen(true);
                Cam.FollowShot(new Vector3(-1.5f, 0f, -0.6f), eye: 1.85f);         // behind and beside the astronaut, out through the doorway
                yield return Player.WalkTo(World.AirlockThreshold, 1.0f, stop: false);
                yield return Player.WalkTo(World.AirlockStairFoot, 0.9f, stop: false);
                yield return Player.WalkTo(World.OnGround(World.AirlockPoint.x + 1.8f, World.AirlockPoint.z));
                // 5. outside: swing round to watch the hatch close behind
                Cam.FollowShot(new Vector3(1.6f, 0f, -2.2f));
                yield return Player.TurnTo(-110f, 0.8f);
                hatch.Open = false;
                yield return hatch.Wait();
                ch.ShowIdle(false);
            }
            else
            {
                Player.Teleport(World.AirlockPoint + new Vector3(1.8f, 0.3f, 0f));
                yield return Fade(0f, 0.35f);
            }
            Model.Tasks.OnExit();
            Model.Tasks.Do(Spot.Airlock, Ctx());   // C7: exit and confirm comms/suit telemetry (sweeps any remaining)
            SetSpeed(1);
            Cam.EndShot();
            Player.EndScript(); cinematic = false;
        }

        void ExitHabitatInstant()
        {
            if (!Model.ExitHabitat()) { Ui?.Toast($"Suit not ready: {Model.EvaLeft:0.0} h. Rest or speed up time while it refills.", Severity.Warning); return; }
            Cam.ClampToTerrain = true; Cam.MaxPitch = 70f;
            Player.gameObject.SetActive(true);
            Player.SetIndoors(false);
            Player.Teleport(World.AirlockPoint + new Vector3(1.8f, 0.3f, 0));
            Cam.Target = Player.transform; Cam.Distance = 4.4f; Cam.Height = 1.5f; Cam.AutoOrbit = false;
            SetSpeed(1);
            Game.SetCursorFree(false);
        }

        // keeps the character in the bunk exactly while the model says you are resting inside the habitat
        void SyncSleep()
        {
            if (Player == null || Interior == null) return;
            bool want = Model.Inside && Model.Resting;
            Player.SetSleeping(want, HabitatInterior.BedPosition, HabitatInterior.BedYaw, HabitatInterior.BedExit);
            Interior.UpdateBlanket(Player);
        }

        public void ToggleRest()
        {
            if (!Model.Inside) return;
            Model.Resting = !Model.Resting;
            if (Model.Resting) SetSpeed(4); else SetSpeed(1);
            Ui.Toast(Model.Resting ? "Resting: fatigue recovers. Time runs at 4x." : "Awake.", Severity.Info);
        }

        public void RepairSuit()
        {
            if (!Model.RepairSuit()) Ui.Toast("No spare parts kit left.", Severity.Warning);
        }

        // ------------------------------------------------------------------ Habitat stations

        static readonly string[] SampleFacts =
        {
            "light-grey anorthosite grains. Highland rock formed when the Moon's early magma ocean cooled, about 4.4 billion years ago.",
            "dark basalt fragments. Old lava flows rich in iron and titanium. Useful for building materials and finding oxygen.",
            "sharp, glassy regolith dust. It never weathers, so it clings to suits and seals. This is why airlock cleaning matters.",
            "a breccia chip: rock shattered and welded back together by an ancient impact. It records the Moon's bombardment history.",
        };

        public void AnalyzeSample()
        {
            if (Model.SamplesDelivered <= SamplesAnalyzed)
            {
                Ui.Toast(Model.SamplesCarried > 0 ? "Samples in your bag are delivered automatically when you enter the habitat."
                                                   : "No samples waiting. Collect regolith at the cyan beacons outside, then bring it in.", Severity.Info);
                return;
            }
            SamplesAnalyzed++;
            string fact = SampleFacts[(SamplesAnalyzed - 1) % SampleFacts.Length];
            Model.Log(Severity.Good, $"Sample {SamplesAnalyzed} analysed: {fact}");
            Ui.Toast($"Sample {SamplesAnalyzed} analysed: {fact}", Severity.Good);
        }

        public void SuitDockUse()
        {
            if (Model.Spares > 0 && Model.SuitIntegrity < 100f) { RepairSuit(); return; }
            if (Model.Tasks.Do(Spot.SuitDock, Ctx())) return;
            Ui.Toast($"Suit: {Model.EvaLeft:0.0} h of EVA ready, integrity {Model.SuitIntegrity:0}%, {Model.Spares} spare kit(s). Oxygen, battery and CO2 refill while you are inside.", Severity.Info);
        }

        public void CommsCall()
        {
            bool com = Model.Load("COM")?.Powered ?? false;
            if (!com) { Ui.Toast("Earth link OFFLINE: the COM load has no power. Switch it on at the power & life support console.", Severity.Warning); return; }
            Comms.OpenUi();
        }

        public void Galley()
        {
            if (Model.EatMeal(out var why)) return;   // success is logged (and toasted) by the model
            Ui.Toast(why, Severity.Info);
        }

        public void Drink()
        {
            if (Model.Drink(out var why)) return;
            Ui.Toast(why, Severity.Info);
        }

        public void WashUp()
        {
            if (Model.WashUp(out var why)) return;
            Ui.Toast(why, Severity.Info);
        }

        public void HealthScan()
        {
            var s = Model.Health < 50f || Model.Fatigue > 70f || Model.Dose > 20f ? Severity.Warning : Severity.Good;
            Ui.Toast($"Health scan: health {Model.Health:0}%, fatigue {Model.Fatigue:0}% (sleep at the bunk to recover), radiation dose {Model.Dose:0.0} of 20 allowed.", s);
        }

        public void WindowLook()
        {
            Cam.Snap(MoonWorld.EarthAzimuth, 6f);
            Ui.Toast("Outside: the lunar surface around the outpost, and Earth hanging about 384,400 km away. Radio takes ~1.3 s each way - the delay on your Mission Control link.", Severity.Info);
        }

        // ------------------------------------------------------------------ Controls

        public void SetSpeed(int s)
        {
            if (Model == null) return;
            if (!Model.Inside && s > 1) { Ui.Toast("Max 1x during EVA (GDD §8). Speed up inside the habitat.", Severity.Info); s = 1; }
            Speed = s;
        }

        public void SetPaused(bool p)
        {
            Paused = p;
            Time.timeScale = p ? 0f : 1f;
            Game.SetCursorFree(p || Model == null || SystemsOpen);
        }

        public void ToggleSystems()
        {
            SystemsOpen = !SystemsOpen;
            Game.SetCursorFree(SystemsOpen);
            if (SystemsOpen) Model.Tasks.Do(Spot.Systems, Ctx());
        }

        public void BagUse()
        {
            var inv = Model.Inv;
            if (!inv.HasBag) { inv.HasBag = true; Ui.Toast("EVA bag taken: 20 slots, up to 20 kg. Open the inventory with F.", Severity.Good); return; }
            var why = inv.DropBag();
            Ui.Toast(why ?? "EVA bag stowed. You have 4 pocket slots.", why == null ? Severity.Info : Severity.Warning);
        }

        public void TogglePlan()
        {
            PlanOpen = !PlanOpen;
            Game.SetCursorFree(PlanOpen);
        }

        public void ToggleStores()
        {
            StoresOpen = !StoresOpen;
            Game.SetCursorFree(StoresOpen);
        }

        public void ToggleInventory()
        {
            InventoryOpen = !InventoryOpen;
            Game.SetCursorFree(InventoryOpen);
        }

        void OnModelEvent(LogEntry e)
        {
            Ui.Toast(e.Text, e.Sev);
            if ((e.Sev == Severity.Warning || e.Sev == Severity.Critical) && Speed > 1 && !Model.Resting) Speed = 1;   // GDD auto-slow
            if (e.Sev == Severity.Critical && Model.Resting) { Model.Resting = false; Speed = 1; }
        }

        void Update()
        {
            if (Current != Phase.Operations) { GameInput.EndFrame(); return; }

            if (GameInput.Pause && Ui.ControlsOpen) { Ui.ToggleControls(); GameInput.EndFrame(); return; }
            if (GameInput.Pause && Comms != null && Comms.Open) { Comms.Close(); GameInput.EndFrame(); return; }
            if ((GameInput.Pause || GameInput.RemoteRover) && Remote.Driving) { ToggleRemote(); GameInput.EndFrame(); return; }
            if (GameInput.RemoteCamera && Remote.Driving) SetRemoteView(!RemoteFirstPerson);
            if ((GameInput.Pause || GameInput.Interact) && RoverDeskOpen) { RoverDeskOpen = false; GameInput.EndFrame(); return; }
            if (GameInput.Pause) { if (PlanOpen) TogglePlan(); else if (StoresOpen) ToggleStores(); else if (InventoryOpen) ToggleInventory(); else if (SystemsOpen) ToggleSystems(); else SetPaused(!Paused); }
            if (Paused) { GameInput.EndFrame(); return; }

            if (GameInput.Systems) { if (InventoryOpen) ToggleInventory(); ToggleSystems(); }
            if (GameInput.Inventory) { if (SystemsOpen) ToggleSystems(); ToggleInventory(); }
            if (GameInput.Help) Ui.ToggleControls();
            if (GameInput.Watch) Ui.ToggleWatch();
            if (GameInput.Plan) TogglePlan();
            if (GameInput.Mark && !Player.Dead && !RemoveMarkNear(Player.transform.position, 2f, Model.Inside)) AddMark(Player.transform.position, Model.Inside);
            int sk = GameInput.SpeedKey; if (sk >= 0) SetSpeed(sk);

            HandleWorldInput();
            SyncSleep();

            float hours = Time.deltaTime * Speed * TestSpeed / RealSecondsPerHour;
            Model.Sprinting = Player.IsSprinting && !Model.Inside;
            Model.Driving = Rover != null && Rover.Driving;
            Model.RemoteDocked = !Remote.Driving && Flat(Remote.transform.position - RemoteDock).magnitude < RemoteDockRadius;
            Model.RemoteFarthest = Mathf.Max(Model.RemoteFarthest, Flat(Remote.transform.position - World.Hab.position).magnitude);
            Model.RoverNearGarage = Rover != null && !Rover.Driving && Flat(Rover.transform.position - World.GaragePoint).magnitude < 9f;
            if (!Model.Inside) Model.FarthestFromBase = Mathf.Max(Model.FarthestFromBase, Flat(ActivePosition - World.Hab.position).magnitude);
            Model.Advance(hours);
            Player.SpeedFactor = Model.Fatigue > 70f ? 0.75f : 1f;

            // Tasks checklist
            var tk = Model.Tasks;
            tk.PlayerPos = Player.transform.position;
            tk.ShelterDist = Interior != null && Interior.ShelterSpot != null
                ? Vector3.Distance(Player.transform.position, Interior.ShelterSpot.transform.position) : 99f;
            tk.Update();

            // CommsNetwork
            Model.Net.Update(Time.unscaledDeltaTime);

            // Model.Sun is quartered in a dust storm (power); the look comes from DustStorm, not from dimming the sun away
            var lv = Levels.Current;
            bool storm = lv.InStorm(Model.Hour);
            World.SetSunNatural(LunarClock.ElevationDeg(Model.Hour), LunarClock.AzimuthDeg(Model.Hour), storm ? Mathf.Min(1f, Model.Sun * 4f) : Model.Sun);
            if (lv.StormStart >= 0f)
            {
                if (Storm == null) Storm = DustStorm.Create(World.transform);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Min(Mathf.InverseLerp(lv.StormStart, lv.StormStart + 1.5f, Model.Hour),
                                                             Mathf.InverseLerp(lv.StormEnd, lv.StormEnd - 1.5f, Model.Hour)));
                Storm.Tick(k, Model.Inside, World.Sun);
            }
            UpdateRings();
            UpdatePanelDustOverlays();
            Minimap?.Tick();
            FullMap?.Tick();

            if (Model.Ended) EndMission();
            GameInput.EndFrame();
        }

        public Vector3 ActivePosition => Rover != null && Rover.Driving ? Rover.transform.position : Player.transform.position;

        void HandleWorldInput()
        {
            if (Model.Inside && Model.Resting && GameInput.Move.sqrMagnitude > 0.04f) ToggleRest();   // walking away wakes you up
            bool canAct = !SystemsOpen && !InventoryOpen && !StoresOpen && !PlanOpen && !(Comms != null && Comms.Open) && !Player.Dead && !Remote.Driving && !RoverDeskOpen;
            Player.Frozen = !canAct || (Rover != null && Rover.Driving);

            Focus = null;
            if (!canAct) return;
            if (Player.Busy) return;                         // walking to / crouching for / lowering an object
            if (GameInput.RemoteRover)
            {
                if (Model.Inside && !cinematic) ToggleRemote();
                else Ui.Toast("The remote rover is driven from inside the habitat.", Severity.Info);
                return;
            }
            if (GameInput.Torch && Torch.Toggle()) Ui.Toast(Torch.On ? "Torch on." : "Torch off.", Severity.Info);

            if (Rover != null && Rover.Driving)
            {
                if (GameInput.Rover) LeaveRover();
                return;
            }

            if (Rover != null && Flat(Rover.transform.position - Player.transform.position).magnitude < 4.5f && GameInput.Rover) { BoardRover(); return; }   // the crate stays in the astronaut's hands while driving

            Focus = Interactable.Nearest(Player.transform.position);
            if (GameInput.Interact)
            {
                if (Focus != null) { Focus.Use(); Player.PlayInteract(); }
                else if (Model.SeisCarried && !Model.Inside) TryDeploySeismometer();
            }
        }

        public bool NearRover => Rover != null && !Rover.Driving && Player && Flat(Rover.transform.position - Player.transform.position).magnitude < 4.5f;

        /// <summary>Pick up / put down the RC-1 handheld controller. The astronaut stays put inside; the rover's camera feed shows
        /// on the controller's screen (MissionUi) and WASD drives the rover.</summary>
        public bool RemoteFirstPerson { get; private set; } = true;
        // ponytail: hand-placed for the 1.3 m MarsRover model; nudge these if the nose cam clips the body
        static readonly Vector3 RemoteNoseCam = new Vector3(0f, 0.95f, 0.6f), RemoteChaseCam = new Vector3(0f, 1.7f, -3.6f);
        void SetRemoteView(bool firstPerson)
        {
            RemoteFirstPerson = firstPerson;
            RemoteCam.transform.localPosition = firstPerson ? RemoteNoseCam : RemoteChaseCam;
            RemoteCam.transform.localRotation = Quaternion.Euler(firstPerson ? 6f : 14f, 0f, 0f);
            RemoteCam.fieldOfView = firstPerson ? 80f : 70f;
        }

        public void ToggleRemote()
        {
            Remote.Driving = !Remote.Driving;
            RemoteCam.enabled = Remote.Driving;
            if (Remote.Driving) Ui.Toast($"RC-1 controller on: battery {Model.RemoteBattery:0}%. WASD to drive, C to switch camera, V or Esc to put it down.", Severity.Info);
        }

        void BoardRover()
        {
            Rover.Driving = true;
            Player.gameObject.SetActive(false);
            Cam.Target = Rover.transform; Cam.Distance = 10f; Cam.Height = 2.2f;
        }

        void LeaveRover()
        {
            Rover.Driving = false;
            Player.gameObject.SetActive(true);
            Player.Teleport(Rover.ExitPoint);
            Cam.Target = Player.transform; Cam.Distance = 4.4f; Cam.Height = 1.5f;
        }

        void UpdateRings()
        {
            noiseRing.enabled = Model.SeisCarried || (Kit.Has("SEIS") && !Model.SeisDeployed);
            ringTimer -= Time.deltaTime;
            if (Model.Inside) { walkRing.enabled = false; return; }
            walkRing.enabled = true;
            if (ringTimer > 0) return;
            ringTimer = 0.4f;
            World.SetRing(walkRing, World.AirlockPoint, Mathf.Max(2f, WalkBackRadius));
        }

        void UpdatePanelDustOverlays()
        {
            if (World == null || Model == null || Model.PanelDust.Length == 0) return;
            for (int i = 0; i < World.SolarArrays.Count && i < Model.PanelDust.Length; i++)
            {
                var overlay = World.SolarArrays[i].GetComponent<SolarPanelDust>();
                if (overlay != null) overlay.SetDust(Model.PanelDust[i]);
            }
        }

        /// <summary>How far you can be and still walk back with the 30-min reserve intact (GDD §18.1).</summary>
        public float WalkBackRadius => Mathf.Max(0f, Model.EvaLeft - 0.5f) * WalkMetersPerHour;
        public float HoursToWalkBack => DistanceToHab / WalkMetersPerHour;

        void EndMission()
        {
            Current = Phase.Report;
            if (Model.Failed && !Model.Inside && Player.gameObject.activeSelf) Player.Die();
            if (Rover) Rover.Driving = false;
            if (Remote.Driving) ToggleRemote();
            Report = new MissionReport(Model);
            Game.RecordScore(Game.Level, Report.Score);
            if (!Model.Failed && Model.AllPrimaryDone && Report.Score >= 50) Game.MarkPassed(Game.Level);
            StartCoroutine(ShowReportSoon());
        }

        IEnumerator ShowReportSoon()
        {
            yield return new WaitForSecondsRealtime(Model.Failed ? 4.2f : 1f);   // let the 4 s Dying clip play out
            Game.SetCursorFree(true);
            Ui.ShowReport(Report, Model);
        }

        public void Retry() => Game.StartLevel(Game.Level);
        public void ToMenu() => Game.Load(Game.MenuScene);

        // AutoTest helpers
        public void TestTeleport(Vector3 xz) { if (Player.gameObject.activeSelf) Player.Teleport(World.OnGround(xz.x, xz.z, 0.3f)); }
        public void TestDeploySeis() => DeploySeismometer(true);
        public void TestEnterHabitat() => EnterHabitatInstant();
        public void TestEnterCinematic() => EnterHabitat();
    }
}
