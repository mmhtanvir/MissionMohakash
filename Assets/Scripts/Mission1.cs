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
        public Astronaut Player { get; private set; }
        public Rover Rover { get; private set; }
        public OrbitCamera Cam { get; private set; }
        public MissionUi Ui { get; private set; }
        public MissionReport Report { get; private set; }
        public Loadout Kit { get; private set; }

        public int Speed { get; private set; } = 1;   // 0 pause, 1, 2, 4
        public float TestSpeed = 1f;                  // AutoTest only
        public bool Paused { get; private set; }      // pause menu
        public bool SystemsOpen { get; private set; }
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
        HabitatInterior Interior;
        public int SamplesAnalyzed { get; private set; }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        void Awake()
        {
            Interactable.All.Clear();
            var cam = SceneKit.MakeCamera();
            World = new GameObject("Moon World").AddComponent<MoonWorld>();
            World.BuildEnvironment();

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
            Ui.ShowBriefing(() => { Current = Phase.Loadout; Ui.ShowLoadout(Launch); });
        }

        // ------------------------------------------------------------------ Launch / landing

        public void Launch(Loadout kit)
        {
            Kit = kit.Clone();
            Model = new MissionModel(Kit);
            Model.OnEvent += OnModelEvent;
            World.BuildOutpost(Kit);
            BuildInteractables();
            Interior = HabitatInterior.Build(this);

            Player = Astronaut.Create(World.AirlockPoint + new Vector3(2.5f, 0.2f, -1.5f));
            Player.transform.rotation = Quaternion.Euler(0, MoonWorld.EarthAzimuth - 25f, 0);
            if (Kit.Has("ROV-SC"))
            {
                Rover = Rover.Create(World, World.RoverStart);
                Rover.UseEnergy = m => Model.UseRoverEnergy(m);
            }

            noiseRing = World.MakeRing("Seismometer noise radius (80 m)", new Color(1f, 0.55f, 0.15f, 0.9f), 0.45f);
            World.SetRing(noiseRing, World.Hab.position, MissionModel.SeisMinDistance);
            walkRing = World.MakeRing("Walk-back circle", new Color(0.3f, 0.85f, 0.95f, 0.8f), 0.35f);

            Cam.AutoOrbit = false; Cam.Target = Player.transform; Cam.Distance = 4.4f; Cam.Height = 1.5f;
            Cam.Snap(MoonWorld.EarthAzimuth - 25f, 8f);   // sun behind the camera, Earth on the horizon
            Model.Inside = false;
            Model.Log(Severity.Info, "You are outside, next to the airlock. First: pick up the seismometer crate.");
            Current = Phase.Operations;
            Ui.StartHud();
            // Minimap – created after the world and interactables are fully built
            Minimap = gameObject.AddComponent<MinimapSystem>();
            Minimap.Init(Ui.Root, this);
            FullMap = gameObject.AddComponent<FullMapSystem>();
            FullMap.Init(Ui.Root, this);
            Game.SetCursorFree(false);
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
                var rad = Interactable.Create("Radiation Monitor", World.OnGround(6f, -8f), 3f, parent);
                rad.Prompt = () => "Log a radiation reading";
                rad.Use = () => Model.LogRadiationReading();
                rad.ShowMarker = () => Model.RadReadings == 0;
            }

            var sites = new[] { ("A", new Vector2(46, 52)), ("B", new Vector2(-96, -44)), ("C", new Vector2(128, -70)) };
            foreach (var (name, xz) in sites)
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

            foreach (var a in World.SolarArrays)
            {
                var clean = Interactable.Create(null, a.position, 3.2f, parent);
                clean.Available = () => Model.Dust > 0.02f;
                clean.Prompt = () => $"Clean dust off the solar arrays ({Model.Dust:0%} dust)";
                clean.Use = () => Model.CleanArrays();
            }

            var lander = Interactable.Create(null, World.OnGround(-30f, 18f), 7f, parent);
            lander.Prompt = () => "Lander: the crew vehicle that brought you (mission ends at hour 72)";
            lander.Use = () => Ui.Toast("The lander is your ride home. Survive to hour 72 and complete the objectives.", Severity.Info);
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
            pick.Prompt = () => "Pick up the seismometer (to move it)";
            pick.Use = () =>
            {
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

        void InteriorCamera(float yaw, float pitch)
        {
            Cam.Target = Player.transform; Cam.AutoOrbit = false; Cam.Distance = 2.9f; Cam.Height = 1.45f;
            Cam.ClampToTerrain = false; Cam.MaxPitch = 36f; Cam.Snap(yaw, pitch);
        }

        IEnumerator EnterRoutine()
        {
            cinematic = true; SystemsOpen = false; Game.SetCursorFree(false);
            Player.BeginScript();
            var hatch = World.OuterHatch;
            if (hatch != null)
            {
                yield return Player.WalkTo(World.OnGround(8.1f, 0f));
                yield return Player.TurnTo(-90f);
                yield return hatch.SetOpen(true);
                yield return Player.WalkTo(World.OnGround(6.2f, 0f));
                hatch.Open = false;
                yield return hatch.Wait();
            }
            Model.EnterHabitat();
            Player.SetIndoors(true, suited: true);
            Player.Teleport(HabitatInterior.VestibulePoint); Player.BeginScript(); Player.EndScript();   // re-disable the controller after Teleport
            Player.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            InteriorCamera(180f, 10f);                               // from inside the room, facing the door
            yield return Interior.AirlockDoor.SetOpen(true);
            yield return Player.WalkTo(HabitatInterior.InsideDoorPoint);
            Interior.AirlockDoor.Open = false;
            Cam.Snap(235f, 8f);                                      // three-quarter view of the suit alcove
            yield return Player.SuitOff(HabitatInterior.DockStand, HabitatInterior.DockYaw, HabitatInterior.HelmetShelf, HabitatInterior.SuitHang);
            Player.EndScript(); cinematic = false;
            Ui.Toast("Inside the habitat. Suit stowed in the alcove. Walk to a station and press E: bunk, lab, console, suit dock, comms, health scan, window.", Severity.Info);
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
                Player.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                Cam.Target = Player.transform; Cam.AutoOrbit = false; Cam.Distance = 2.9f; Cam.Height = 1.45f;
                Cam.ClampToTerrain = false; Cam.MaxPitch = 36f; Cam.Snap(0f, 12f);
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
            if (Model.EvaLeft < 0.6f) { Model.ExitHabitat(); Ui?.Toast($"Suit not ready: {Model.EvaLeft:0.0} h. Rest or speed up time while it refills.", Severity.Warning); return; }
            if (Interior == null || Player == null || !Player.gameObject.activeSelf || Player.InBed) { ExitHabitatInstant(); return; }
            StartCoroutine(ExitRoutine());
        }

        IEnumerator ExitRoutine()
        {
            cinematic = true; SystemsOpen = false; Game.SetCursorFree(false);
            if (Model.Resting) ToggleRest();
            Player.BeginScript();
            Cam.Snap(235f, 8f);
            yield return Player.SuitOn(HabitatInterior.DockStand, HabitatInterior.DockYaw);
            yield return Player.WalkTo(HabitatInterior.InsideDoorPoint);
            yield return Player.TurnTo(180f);
            yield return Interior.AirlockDoor.SetOpen(true);
            yield return Player.WalkTo(HabitatInterior.VestibulePoint);
            yield return Interior.AirlockDoor.SetOpen(false);
            if (!Model.ExitHabitat()) { Player.EndScript(); cinematic = false; yield break; }
            Cam.ClampToTerrain = true; Cam.MaxPitch = 70f;
            Player.SetIndoors(false);
            Player.Teleport(World.OnGround(6.2f, 0f)); Player.BeginScript(); Player.EndScript();
            Player.transform.rotation = Quaternion.LookRotation(Vector3.right);
            Cam.Target = Player.transform; Cam.Distance = 4.4f; Cam.Height = 1.5f; Cam.AutoOrbit = false;
            Cam.Snap(-90f, 10f);                                     // outside, looking back at the hatch
            var hatch = World.OuterHatch;
            if (hatch != null) yield return hatch.SetOpen(true);
            yield return Player.WalkTo(World.OnGround(World.AirlockPoint.x + 1.8f, World.AirlockPoint.z));
            if (hatch != null) hatch.Open = false;
            SetSpeed(1);
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
            Ui.Toast($"Suit: {Model.EvaLeft:0.0} h of EVA ready, integrity {Model.SuitIntegrity:0}%, {Model.Spares} spare kit(s). Oxygen, battery and CO2 refill while you are inside.", Severity.Info);
        }

        public void CommsCall()
        {
            bool com = Model.Load("COM")?.Powered ?? false;
            if (!com) { Ui.Toast("Earth link OFFLINE: the COM load has no power. Switch it on at the power & life support console.", Severity.Warning); return; }
            Ui.Toast("Mission Control (1.3 s radio delay): " + Ui.NextHintPublic(), Severity.Info);
            Model.Log(Severity.Info, "Called Mission Control for a status check.");
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
            if (GameInput.Pause) { if (SystemsOpen) ToggleSystems(); else SetPaused(!Paused); }
            if (Paused) { GameInput.EndFrame(); return; }

            if (GameInput.Systems) ToggleSystems();
            if (GameInput.Help) Ui.ToggleControls();
            int sk = GameInput.SpeedKey; if (sk >= 0) SetSpeed(sk);

            HandleWorldInput();
            SyncSleep();

            float hours = Time.deltaTime * Speed * TestSpeed / RealSecondsPerHour;
            Model.Sprinting = Player.IsSprinting && !Model.Inside;
            Model.Driving = Rover != null && Rover.Driving;
            Model.RoverNearGarage = Rover != null && !Rover.Driving && Flat(Rover.transform.position - World.GaragePoint).magnitude < 9f;
            if (!Model.Inside) Model.FarthestFromBase = Mathf.Max(Model.FarthestFromBase, Flat(ActivePosition - World.Hab.position).magnitude);
            Model.Advance(hours);
            Player.SpeedFactor = Model.Fatigue > 70f ? 0.75f : 1f;

            World.SetSun(Model.Sun);
            UpdateRings();
            Minimap?.Tick();
            FullMap?.Tick();

            if (Model.Ended) EndMission();
            GameInput.EndFrame();
        }

        public Vector3 ActivePosition => Rover != null && Rover.Driving ? Rover.transform.position : Player.transform.position;

        void HandleWorldInput()
        {
            if (Model.Inside && Model.Resting && GameInput.Move.sqrMagnitude > 0.04f) ToggleRest();   // walking away wakes you up
            bool canAct = !SystemsOpen && !Player.Dead;
            Player.Frozen = !canAct || (Rover != null && Rover.Driving);

            Focus = null;
            if (!canAct) return;
            if (Player.Busy) return;                         // walking to / crouching for / lowering an object

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
                else if (Model.SeisCarried && !Model.Inside) DeploySeismometer();
            }
        }

        public bool NearRover => Rover != null && !Rover.Driving && Player && Flat(Rover.transform.position - Player.transform.position).magnitude < 4.5f;

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

        /// <summary>How far you can be and still walk back with the 30-min reserve intact (GDD §18.1).</summary>
        public float WalkBackRadius => Mathf.Max(0f, Model.EvaLeft - 0.5f) * WalkMetersPerHour;
        public float HoursToWalkBack => DistanceToHab / WalkMetersPerHour;

        void EndMission()
        {
            Current = Phase.Report;
            if (Model.Failed && !Model.Inside && Player.gameObject.activeSelf) Player.Die();
            if (Rover) Rover.Driving = false;
            Report = new MissionReport(Model);
            Game.RecordScore(1, Report.Score);
            if (!Model.Failed && Model.PrimaryDone == 3 && Report.Score >= 50) Game.MarkPassed(1);
            StartCoroutine(ShowReportSoon());
        }

        IEnumerator ShowReportSoon()
        {
            yield return new WaitForSecondsRealtime(Model.Failed ? 2.5f : 1f);
            Game.SetCursorFree(true);
            Ui.ShowReport(Report, Model);
        }

        public void Retry() => Game.Load(Game.Mission1Scene);
        public void ToMenu() => Game.Load(Game.MenuScene);

        // AutoTest helpers
        public void TestTeleport(Vector3 xz) { if (Player.gameObject.activeSelf) Player.Teleport(World.OnGround(xz.x, xz.z, 0.3f)); }
        public void TestDeploySeis() => DeploySeismometer(true);
        public void TestEnterHabitat() => EnterHabitatInstant();
    }
}
