using UnityEngine;
using UnityEngine.UIElements;

namespace Mohakash
{
    /// <summary>Main menu over a live 3D lunar backdrop: Play (Mission Select), How to play, Settings, Credits, Quit.</summary>
    public class MainMenu : MonoBehaviour
    {
        VisualElement root, side, select, left;
        Astronaut astro;

        struct MissionInfo { public int N; public string Name, World, Difficulty, Duration, Objective, Focus, New; }
        static readonly MissionInfo[] Missions =   // names, places and durations from the GDD §10
        {
            new MissionInfo { N = 1, Name = "FIRST LIGHT", World = "MOON", Difficulty = "*---", Duration = "72 mission hours",
                Objective = "Start the outpost, set up a seismometer far from base and survive", Focus = "Power + oxygen + spacewalks",
                New = "Habitat, solar, battery, oxygen maker, suit, rover, seismometer" },
            new MissionInfo { N = 2, Name = "INTO THE SHADOW", World = "MOON", Difficulty = "**--", Duration = "120 mission hours",
                Objective = "Find ice in a dark crater and turn it into water", Focus = "Water, darkness, communication",
                New = "Ice drill, water extractor, relay, generator" },
            new MissionInfo { N = 3, Name = "JEZERO AIR", World = "MARS", Difficulty = "***-", Duration = "148 mission hours (6 sols)",
                Objective = "Make oxygen from the Mars air and collect rock samples", Focus = "How Mars changes engineering",
                New = "MOXIE-inspired oxygen maker, weather station, science rover" },
            new MissionInfo { N = 4, Name = "ARCADIA ENDURANCE", World = "MARS", Difficulty = "****", Duration = "247 mission hours (10 sols)",
                Objective = "Survive a dust storm and a radio blackout while still doing science", Focus = "Everything combined",
                New = "Greenhouse, water recycling, all rovers and suits" },
        };

        void Awake()
        {
            Interactable.All.Clear();
            Time.timeScale = 1f;
            AudioListener.volume = Game.Volume;
            var cam = SceneKit.MakeCamera();
            var world = new GameObject("Moon World").AddComponent<MoonWorld>();
            world.BuildEnvironment();
            world.SetSun(1f);

            var dir = Quaternion.Euler(0, MoonWorld.EarthAzimuth + 12f, 0) * Vector3.forward;
            var spot = world.OnGround(0, 0);
            astro = Astronaut.Create(spot + Vector3.up * 0.2f);
            astro.Frozen = true;
            astro.transform.rotation = Quaternion.LookRotation(-dir);
            var right = Vector3.Cross(Vector3.up, dir);
            MoonWorld.Spawn("Rover", spot + dir * 4f + right * 5f, Quaternion.LookRotation(-right + dir), 0.62f, null, "Rover (menu)");
            MoonWorld.Spawn("SurfaceHabitat", spot + dir * 26f + right * 14f, Quaternion.Euler(0f, 35f, 0f), 1f, null, "Habitat (menu)");
            MoonWorld.Spawn("Spaceship", spot + dir * 34f - right * 4f, Quaternion.Euler(0, 200, 0), 1.1f, null, "Lander (menu)");

            cam.transform.position = spot - dir * 5.2f + Vector3.up * 1.5f - right * 2.2f;
            cam.transform.rotation = Quaternion.LookRotation(spot + dir * 40f + Vector3.up * 4.5f - cam.transform.position);
        }

        void Start()
        {
            Game.SetCursorFree(true);
            astro.PlayWave();
            root = Ui.CreateRoot();
            left = Ui.Box(root, "col"); left.style.position = Position.Absolute; left.style.left = 110; left.style.top = 150; left.style.width = 640;
            Ui.Text(left, "NASA SPACE APPS 2026", "kicker-blue");
            Ui.Text(left, "MISSION MOHAKASH", "title");
            Ui.Text(left, "Junior Astronaut Mission Trainer", "subtitle-white");
            Ui.Text(left, "Plan. Build. Survive. Discover.", "tagline");
            Ui.Button(left, "PLAY", ShowMissionSelect, "btn-primary", "btn-menu");
            Ui.Button(left, "How to play", () => ShowSide(BuildHowTo), "btn-menu");
            Ui.Button(left, "Controls", () => ShowSide(ControlsUi.Build), "btn-menu");
            Ui.Button(left, "Settings", () => ShowSide(BuildSettings), "btn-menu");
            Ui.Button(left, "Credits", () => ShowSide(BuildCredits), "btn-menu");
            Ui.Button(left, "Quit", Game.Quit, "btn-menu");
            var foot = Ui.Text(root, "Not an official NASA product; no NASA endorsement implied.", "small", "muted");
            foot.style.position = Position.Absolute; foot.style.left = 110; foot.style.bottom = 30;
        }

        public void StartMission() => Game.Load(Game.Mission1Scene);

        void ShowSide(System.Action<VisualElement> build)
        {
            side?.RemoveFromHierarchy();
            side = Ui.Panel(root);
            side.style.position = Position.Absolute; side.style.right = 90; side.style.top = 150; side.style.width = 760; side.style.maxHeight = 780;
            var head = Ui.Box(side, "row"); Ui.Box(head, "grow");
            Ui.Button(head, "Close", () => { side.RemoveFromHierarchy(); side = null; }, "btn-small");
            build(side);
        }

        /// <summary>Mission Select overlay: one card per mission (Moon = blue, Mars = orange).</summary>
        public void ShowMissionSelect()
        {
            side?.RemoveFromHierarchy(); side = null;
            select?.RemoveFromHierarchy();
            select = Ui.Box(root, "screen", "center", "dim"); select.pickingMode = PickingMode.Position;
            Ui.Show(left, false);
            var p = Ui.Panel(select); p.style.width = 1680;
            var head = Ui.Box(p, "row");
            Ui.Text(head, "MISSION SELECT", "h1", "blue"); Ui.Box(head, "grow");
            Ui.Button(head, "CLOSE", () => { select.RemoveFromHierarchy(); Ui.Show(left, true); });
            var grid = Ui.Box(p, "row"); grid.style.alignItems = Align.Stretch;
            foreach (var mi in Missions)
            {
                bool playable = mi.N == 1;
                bool moon = mi.World == "MOON";
                var card = Ui.Box(grid, "mcard", moon ? "mcard-moon" : "mcard-mars");
                Ui.Text(card, $"MISSION 0{mi.N}", "small", "muted");
                Ui.Text(card, mi.Name, "h2-white");
                Ui.Text(card, $"[ {mi.World} ]", moon ? "blue" : "accent");
                Ui.Text(card, "Difficulty: " + mi.Difficulty, "yellow");
                Ui.Text(card, "Duration: " + mi.Duration, "text-small");
                Ui.Text(card, "Objective: " + mi.Objective, "text-small");
                Ui.Text(card, "Focus: " + mi.Focus, "text-small", "purple");
                Ui.Text(card, "New: " + mi.New, "small", "muted");
                Ui.Text(card, playable ? "Status: AVAILABLE" : "Status: LOCKED", playable ? "cyan" : "muted");
                Ui.Text(card, "Best score: " + (playable && Game.BestScore(1) > 0 ? Game.BestScore(1).ToString() : "--"), "small");
                Ui.Box(card, "grow");
                var b = Ui.Button(card, playable ? "LAUNCH" : "COMING IN NEXT BUILD", StartMission, playable ? "btn-primary" : null);
                b.SetEnabled(playable);
            }
            Ui.Text(p, "Missions 2-4 are designed in the GDD but not playable in this build yet. Pass Mission 1 to unlock the next one.", "small", "muted");
        }

        void BuildHowTo(VisualElement p)
        {
            Ui.Text(p, "HOW TO PLAY", "h1");
            Ui.Text(p, "Your job", "h2");
            Ui.Text(p, "You are an astronaut alone on the Moon. Pack your rocket, keep the outpost running and finish the science task before the timer ends.", "text");
            Ui.Text(p, "Controls", "h2");
            foreach (var line in new[] { "WASD - walk      Mouse - look around      Shift - run      Space - jump",
                                         "E - use / pick up / set down      R - get in or out of the rover",
                                         "Tab - power panel (switch machines on/off)      1 / 2 / 3 - time speed      Esc - pause" })
                Ui.Text(p, line, "text-small");
            Ui.Text(p, "Tips", "h2");
            Ui.Text(p, "Follow the 'Next:' hint in the top-right. Green circle = OK, yellow diamond = warning, red square = danger. " +
                       "When outside, stay inside the cyan ring or your suit runs out before you get back.", "text");
        }

        void BuildSettings(VisualElement p)
        {
            Ui.Text(p, "SETTINGS", "h1");
            SettingsUi.Build(p);
            Ui.Button(p, "Reset progress (re-lock missions)", () => { PlayerPrefs.DeleteKey("passed1"); PlayerPrefs.DeleteKey("best1"); PlayerPrefs.Save(); });
        }

        void BuildCredits(VisualElement p)
        {
            var s = Ui.Add(p, new ScrollView(), "grow");
            Ui.Text(s, "CREDITS & DATA SOURCES", "h1");
            Ui.Text(s, "Game design", "h2");
            Ui.Text(s, "Junior Astronaut Mission Trainer GDD (OUTPOST ZERO, draft v1.0, 6 Oct 2026). Mission names, objectives and all gameplay numbers come from it and are GAME UNITS, not NASA specifications.", "small");
            Ui.Text(s, "3D models", "h2");
            Ui.Text(s, "Quaternius - Ultimate Space Kit and space-suit character (rover, habitat modules, solar panels, rocks, lander, astronaut animations). License: CC0 1.0 public domain. quaternius.com", "small");
            Ui.Text(s, "Technology", "h2");
            Ui.Text(s, "Unity 6.6 (URP), Unity Input System, glTFast (Unity Technologies) for GLB import.", "small");
            Ui.Text(s, "NASA data (status)", "h2");
            Ui.Text(s, "Planned per GDD §28: LRO LOLA south-polar elevation, slope and roughness; illumination from LOLA DEM + NASA ephemeris; Apollo passive seismic catalogue timing. NOT YET INTEGRATED: this build's terrain is hand-made and labelled 'inspired by'.", "small");
            Ui.Text(s, "Real facts used", "h2");
            Ui.Text(s, "Lunar gravity 1.62 m/s² (player jump). Water electrolysis ratio ~1.125 kg water per kg O2. Earth-Moon light time ~1.3 s. Apollo walk-back limit concept.", "small");
            Ui.Text(s, "Not an official NASA product. No NASA endorsement is implied. No NASA logos are used.", "small", "muted");
        }
    }
}
