using UnityEngine;
using UnityEngine.UIElements;

namespace Mohakash
{
    /// <summary>Main menu over a live 3D lunar backdrop: Play (Mission Select), How to play, Settings, Credits, Quit.</summary>
    public class MainMenu : MonoBehaviour
    {
        VisualElement root, side, select, left;
        Astronaut astro;

        struct MissionInfo { public int N; public string Name, World, Difficulty, Objective; }
        static readonly MissionInfo[] Missions =
        {
            new MissionInfo { N = 1, Name = "FIRST FLIGHT", World = "MOON", Difficulty = "★☆☆☆", Objective = "Basic tutorial and game controls" },
            new MissionInfo { N = 2, Name = "START THE OUTPOST", World = "MOON", Difficulty = "★★☆☆", Objective = "Install a seismometer at a designated location" },
            new MissionInfo { N = 3, Name = "LUNAR SAMPLE COLLECTION", World = "MOON", Difficulty = "★★☆☆", Objective = "Drill and collect lunar rock samples" },
            new MissionInfo { N = 4, Name = "LUNAR SURVIVAL", World = "MOON", Difficulty = "★★★☆", Objective = "Survive for a specified number of days" },
            new MissionInfo { N = 5, Name = "MARS EXPLORATION", World = "MARS", Difficulty = "★★★☆", Objective = "Produce oxygen from Martian air and collect rock samples" },
            new MissionInfo { N = 6, Name = "DUST STORM SURVIVAL", World = "MARS", Difficulty = "★★★★", Objective = "Survive a dust storm and repair damaged equipment" },
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
            Ui.Box(left, "logo").style.backgroundImage = Resources.Load<Texture2D>("UI/Logo");
            Ui.Text(left, "Junior Astronaut Mission Trainer", "subtitle-white");
            Ui.Text(left, "Plan. Build. Survive. Discover.", "tagline");
            // One lit button at a time: hover moves focus, :focus carries the orange style.
            var play = MenuButton("PLAY", "play", ShowMissionSelect);
            MenuButton("HOW TO PLAY", "how", () => ShowSide(BuildHowTo));
            MenuButton("LUNAR INTELLIGENCE", "how", () => LunarIntelligenceUi.Show(root));
            MenuButton("CONTROLS", "controls", () => ShowSide(ControlsUi.Build));
            MenuButton("SETTINGS", "settings", () => ShowSide(BuildSettings));
            MenuButton("CREDITS", "credits", () => ShowSide(BuildCredits));
            MenuButton("QUIT", "quit", Game.Quit).AddToClassList("btn-quit");
            play.schedule.Execute(play.Focus);
            var foot = Ui.Text(root, "Not an official NASA product; no NASA endorsement implied.", "small", "muted");
            foot.style.position = Position.Absolute; foot.style.left = 110; foot.style.bottom = 30;
        }

        /// <summary>Plate art (Resources/UI/Menu) is rendered from the design HTML; the plate overhangs 20px for the glow.</summary>
        Button MenuButton(string text, string icon, System.Action onClick)
        {
            var b = Ui.Button(left, "", onClick, "btn-menu");
            Ui.Box(b, "mm-bg");
            Ui.Box(b, "mm-ico").style.backgroundImage = Resources.Load<Texture2D>("UI/Menu/Icon_" + icon);
            Ui.Text(b, text, "mm-label");
            b.RegisterCallback<PointerEnterEvent>(_ => b.Focus());
            return b;
        }

        public void StartMission() => Game.StartLevel(1);

        void ShowSide(System.Action<VisualElement> build)
        {
            side?.RemoveFromHierarchy();
            side = Ui.Panel(root);
            side.style.position = Position.Absolute; side.style.right = 90; side.style.top = 150; side.style.width = 760; side.style.maxHeight = 780;
            var head = Ui.Box(side, "row"); Ui.Box(head, "grow");
            Ui.Button(head, "Close", () => { side.RemoveFromHierarchy(); side = null; }, "btn-small");
            build(side);
        }

        /// <summary>Mission Select overlay: one card per mission (Moon = blue, Mars = orange) matching MissionSelect.dc.html.</summary>
        public void ShowMissionSelect()
        {
            side?.RemoveFromHierarchy(); side = null;
            select?.RemoveFromHierarchy();
            select = Ui.Box(root, "screen", "center", "dim"); select.pickingMode = PickingMode.Position;
            Ui.Show(left, false);
            var p = Ui.Panel(select); p.style.width = 1680;
            var head = Ui.Box(p, "row");
            var hTitle = Ui.Text(head, "LEVEL SELECT", "h1");
            hTitle.style.color = new Color(0.95f, 0.98f, 1f);
            hTitle.style.letterSpacing = 3;
            Ui.Box(head, "grow");
            var closeBtn = Ui.Button(head, "CLOSE", () => { select.RemoveFromHierarchy(); Ui.Show(left, true); });
            closeBtn.style.minWidth = 140; closeBtn.style.fontSize = 20;

            var grid = Ui.Box(p, "row"); grid.style.alignItems = Align.Stretch; grid.style.marginTop = 14;
            grid.style.flexWrap = Wrap.Wrap; grid.style.justifyContent = Justify.Center;   // 6 levels: 3 per row
            foreach (var mi in Missions)
            {
                bool playable = true;   // every level is playable; passing one still records it
                int n = mi.N;
                bool moon = mi.World == "MOON";
                var card = Ui.Box(grid, "mcard", moon ? "mcard-moon" : "mcard-mars");
                if (n == 1) card.AddToClassList("mcard-selected");

                Ui.Text(card, $"LEVEL {mi.N}", "small", "muted");
                var title = Ui.Text(card, mi.Name, "h2-white");
                title.style.fontSize = 30; title.style.color = Color.white;
                Ui.Text(card, $"[ {mi.World} ]", moon ? "blue" : "accent");

                var diffRow = Ui.Box(card, "row");
                Ui.Text(diffRow, "Difficulty: ", "text-small");
                var diffStars = Ui.Text(diffRow, mi.Difficulty, "yellow");
                diffStars.style.fontSize = 22; diffStars.style.color = new Color(1f, 0.79f, 0.3f);

                Ui.Text(card, "Objective: " + mi.Objective, "text-small");
                Ui.Text(card, Game.IsUnlocked(n + 1) ? "Status: PASSED" : "Status: AVAILABLE", "cyan");
                Ui.Text(card, "Best score: " + (Game.BestScore(n) > 0 ? Game.BestScore(n).ToString() : "--"), "small");
                Ui.Box(card, "grow");

                var b = Ui.Button(card, playable ? $"LAUNCH LEVEL {mi.N}" : "COMING IN NEXT BUILD", () => Game.StartLevel(n), playable ? "btn-primary" : null);
                b.SetEnabled(playable);
                if (playable)
                {
                    b.style.fontSize = 22;
                    b.style.paddingTop = 10;
                    b.style.paddingBottom = 10;
                }
            }
            var note = Ui.Text(p, "Every level is open. Score 50+ and meet the main objectives to mark a level as passed.", "text-small");
            note.style.color = new Color(0.36f, 0.78f, 1f);
            note.style.marginTop = 14;
            note.style.unityTextAlign = TextAnchor.MiddleCenter;
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
            var resBtn = Ui.Button(s, "OPEN LUNAR RESEARCH & DATA SOURCES DASHBOARD", () => LunarIntelligenceUi.Show(root), "btn-primary");
            resBtn.style.marginTop = 12; resBtn.style.marginBottom = 12;
            Ui.Text(s, "Not an official NASA product. No NASA endorsement is implied. No NASA logos are used.", "small", "muted");
        }
    }
}
