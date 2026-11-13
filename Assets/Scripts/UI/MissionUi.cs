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
        VisualElement root, hud, markers, alerts, systems, pause, fade, banner, leftCol, linkDot;
        /// <summary>Exposes the root UI element so MinimapSystem can attach its panel.</summary>
        public VisualElement Root => root;
        Label bannerText, promptText, clockText, hintText;
        readonly Dictionary<string, Label> stats = new Dictionary<string, Label>();
        readonly Dictionary<string, (VisualElement fill, Label val)> bars = new Dictionary<string, (VisualElement, Label)>();
        readonly List<(Label label, VisualElement dot, Func<bool> done, Func<string> text)> objectives = new List<(Label, VisualElement, Func<bool>, Func<string>)>();
        readonly List<Button> speedButtons = new List<Button>();
        VisualElement eva, rover, prompt, controls, watch, remote; Label remoteInfo;
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
            else if (!m.Paused && m.Model != null && !m.SystemsOpen) Game.SetCursorFree(false);
        }
        Label evaWalk, roverRange;
        readonly Dictionary<Interactable, VisualElement> markerViews = new Dictionary<Interactable, VisualElement>();
        // Floating level readouts on outdoor hardware (O2 store, power store, rover batteries, suit tank). Outside only, within GaugeRange.
        readonly List<(Func<Vector3> pos, Func<string> text, Func<float> level, VisualElement view)> gauges = new List<(Func<Vector3>, Func<string>, Func<float>, VisualElement)>();
        const float GaugeRange = 40f;
        public void AddGauge(Func<Vector3> pos, Func<string> text, Func<float> level)
        {
            var v = Ui.Box(markers, "marker"); Ui.Text(v, "", "marker-label");
            gauges.Add((pos, text, level, v));
        }
        readonly List<(Button btn, PowerLoad load)> loadButtons = new List<(Button, PowerLoad)>();
        readonly List<(Button btn, PowerLoad load)> repairButtons = new List<(Button, PowerLoad)>();
        readonly List<(Label lbl, PowerLoad load)> loadStates = new List<(Label, PowerLoad)>();
        readonly List<(VisualElement dot, PowerLoad load)> loadDots = new List<(VisualElement, PowerLoad)>();
        Label sysSummary, sysShadow; Button ogsBtn;
        VisualElement inventory; Label invSeis, invSamples, invStored, invTorchState; Button invTorchBtn;
        readonly List<(VisualElement box, Label name, Label count)> invSlots = new List<(VisualElement, Label, Label)>();
        VisualElement invWeightRow, invWeightFill; Label invWeight;
        Label astroStatusBadge;
        VisualElement hintBox, linkRow;
        bool objectivesCollapsed; VisualElement objBody; Label objHeaderLbl; Button objToggleBtn;
        Label sunCountdownLbl;
        VisualElement hudKeyHints;

        public void ToggleObjectives()
        {
            objectivesCollapsed = !objectivesCollapsed;
            if (objBody != null) Ui.Show(objBody, !objectivesCollapsed);
            if (objToggleBtn != null) objToggleBtn.text = objectivesCollapsed ? "▼" : "▲";
        }

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
            var p = Ui.Panel(scr); p.style.width = 1720; p.style.height = 960;
            p.style.paddingLeft = 32; p.style.paddingRight = 32; p.style.paddingTop = 24; p.style.paddingBottom = 20;

            // Header row
            var head = Ui.Box(p, "row");
            var hTitle = Ui.Text(head, $"MISSION {Levels.Current.N}: {Levels.Current.Name}", "h1");
            hTitle.style.letterSpacing = 2; hTitle.style.color = new Color(0.92f, 0.96f, 1f);
            Ui.Box(head, "grow");
            var chip1 = Ui.Text(head, Levels.Current.Mars ? "Jezero crater, Mars" : "Lunar south pole", "chip");
            chip1.style.fontSize = 16; chip1.style.paddingTop = 4; chip1.style.paddingBottom = 4;
            var chip2 = Ui.Text(head, $"{MissionModel.DurationH:0} hours", "chip");
            chip2.style.fontSize = 16; chip2.style.paddingTop = 4; chip2.style.paddingBottom = 4;

            var sub = Ui.Text(p, Levels.Current.Header, "kicker-blue");
            sub.style.fontSize = 20; sub.style.marginBottom = 10;

            var narrative = Ui.Text(p, Levels.Current.Narrative, "text");
            narrative.style.fontSize = 22; narrative.style.color = new Color(0.9f, 0.94f, 1f); narrative.style.marginBottom = 16;

            // Two-column layout matching MissionBriefing.dc.html
            var cols = Ui.Box(p, "row", "grow"); cols.style.alignItems = Align.Stretch;

            // Left column: MISSION PLAN
            var leftBox = Ui.Box(cols, "col"); leftBox.style.width = 810; leftBox.style.marginRight = 20;
            var planPanel = Ui.Box(leftBox, "col", "grow");
            planPanel.style.backgroundColor = new Color(0.02f, 0.06f, 0.13f, 0.84f);
            Ui.SetBorder(planPanel, 1, new Color(0.43f, 0.68f, 1f, 0.38f), 6);
            planPanel.style.paddingLeft = 20; planPanel.style.paddingRight = 20;
            planPanel.style.paddingTop = 16; planPanel.style.paddingBottom = 16;

            var pHeader = Ui.Text(planPanel, "WHAT YOU WILL DO", "section");
            pHeader.style.color = new Color(0.56f, 0.77f, 1f); pHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            pHeader.style.marginBottom = 12;

            var steps = Levels.Current.Steps;

            for (int i = 0; i < steps.Length; i++)
            {
                var sRow = Ui.Box(planPanel, "row");
                sRow.style.marginBottom = 10; sRow.style.alignItems = Align.Center;
                Ui.Text(sRow, (i + 1).ToString(), "step-circle");
                var stxt = Ui.Text(sRow, steps[i], "text"); stxt.style.fontSize = 20;
                stxt.style.whiteSpace = WhiteSpace.Normal; stxt.style.color = new Color(0.91f, 0.94f, 1f);
            }

            // Right column: KEY IDEAS
            var rightBox = Ui.Box(cols, "col"); rightBox.style.width = 810;
            var ideasPanel = Ui.Box(rightBox, "col", "grow");
            ideasPanel.style.backgroundColor = new Color(0.02f, 0.06f, 0.13f, 0.84f);
            Ui.SetBorder(ideasPanel, 1, new Color(0.43f, 0.68f, 1f, 0.38f), 6);
            ideasPanel.style.paddingLeft = 20; ideasPanel.style.paddingRight = 20;
            ideasPanel.style.paddingTop = 16; ideasPanel.style.paddingBottom = 16;

            var iHeader = Ui.Text(ideasPanel, "GOOD TO KNOW", "section");
            iHeader.style.color = new Color(0.56f, 0.77f, 1f); iHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            iHeader.style.marginBottom = 12;

            var ideas = new[]
            {
                ("Sun power", "Solar panels only work when the sun shines. The battery saves power for later.", new Color(1f, 0.81f, 0.36f)),
                ("Dark time", Levels.Current.NightEvery > 0f
                    ? $"Every night it is dark for {MissionModel.ShadowLength:0} hours. The first night starts at hour {MissionModel.ShadowStart:0}."
                    : $"At hour {MissionModel.ShadowStart:0} it gets dark for {MissionModel.ShadowLength:0} hours.", new Color(0.65f, 0.71f, 0.99f)),
                ("Air", Levels.Current.Mars ? "The oxygen maker makes air out of Mars air. It needs lots of power."
                                            : "The oxygen maker turns water into air. It needs lots of power.", new Color(0.43f, 0.7f, 1f)),
                ("Going outside", "Your suit has air for a few hours. Come back before it runs out!", new Color(0.22f, 0.85f, 0.96f)),
                Levels.Current.StormStart >= 0f ? ("Dust storm", $"From hour {Levels.Current.StormStart:0} to {Levels.Current.StormEnd:0} the sun is weak and machines can break.", new Color(1f, 0.71f, 0.28f))
                : MissionModel.SpeStart >= 0f ? ("Solar storm", $"Around hour {MissionModel.SpeStart:0} the sun sends out a storm. Be inside!", new Color(1f, 0.71f, 0.28f))
                : ("Radiation", "Space has invisible radiation. The base keeps you safe.", new Color(1f, 0.71f, 0.28f))
            };

            foreach (var (title, desc, col) in ideas)
            {
                var icCard = Ui.Box(ideasPanel, "concept-card");
                var dot = Ui.Box(icCard);
                dot.style.width = 12; dot.style.height = 12; Ui.SetRadius(dot, 6);
                dot.style.backgroundColor = col; dot.style.marginRight = 14; dot.style.flexShrink = 0;
                var info = Ui.Box(icCard, "col", "grow");
                var t = Ui.Text(info, title, "small");
                t.style.unityFontStyleAndWeight = FontStyle.Bold; t.style.color = col;
                var d = Ui.Text(info, desc, "text-small");
                d.style.color = new Color(0.85f, 0.9f, 0.98f);
            }

            // Controls strip
            var ctlPanel = Ui.Box(p, "row");
            ctlPanel.style.marginTop = 14; ctlPanel.style.paddingLeft = 16; ctlPanel.style.paddingRight = 16;
            ctlPanel.style.paddingTop = 8; ctlPanel.style.paddingBottom = 8;
            Ui.SetBorder(ctlPanel, 1, new Color(0.43f, 0.68f, 1f, 0.35f), 4);
            ctlPanel.style.backgroundColor = new Color(0.04f, 0.1f, 0.21f, 0.85f);
            var ctlH = Ui.Text(ctlPanel, "CONTROLS: ", "tiny");
            ctlH.style.color = new Color(0.56f, 0.77f, 1f); ctlH.style.unityFontStyleAndWeight = FontStyle.Bold;
            ctlH.style.marginRight = 10;

            foreach (var (k, a) in new[] { ("WASD", "Walk"), ("Mouse", "Look"), ("E", "Use"), ("R", "Rover"), ("Tab", "Base controls"), ("1 2 3", "Speed up time"), ("Esc", "Menu") })
            {
                var item = Ui.Box(ctlPanel, "row"); item.style.marginRight = 16;
                Ui.Text(item, k, "key-cap");
                var lb = Ui.Text(item, a, "small"); lb.style.color = new Color(0.85f, 0.9f, 0.98f);
            }

            // Footer
            var foot = Ui.Box(p, "row"); foot.style.marginTop = 14; foot.style.alignItems = Align.Center;
            var note = Ui.Text(foot, "The numbers in this game are made simple for learning.", "small", "muted");
            note.style.flexGrow = 1;

            Ui.Button(foot, "BACK", () => Game.Load(Game.MenuScene));
            var cta = Ui.Button(foot, "NEXT: PACK YOUR ROCKET  >", () => { scr.RemoveFromHierarchy(); onContinue(); }, "btn-primary", "btn-big");
            cta.style.minWidth = 280; cta.style.fontSize = 22;

            TestAdvance = _ => { scr.RemoveFromHierarchy(); onContinue(); };
        }

        struct ExtraItemInfo { public string Key, Name, Cat, CatLabel, Benefit, Desc; }

        /// <summary>What one extra really adds on top of the standard gear (from Catalog, so cards match the fit check).</summary>
        static (float mass, float cost) ExtraDelta(string key)
        {
            var a = Catalog.Simple(new string[0]); var b = Catalog.Simple(new[] { key });
            return (b.Mass - a.Mass, b.Cost - a.Cost);
        }

        HashSet<string> loadoutPicks;   // survives BACK from the planner / briefing
        static readonly ExtraItemInfo[] ExtrasMeta = new[]
        {
            new ExtraItemInfo { Key = "rover", Name = "Rover + garage", Cat = "MOBILITY", CatLabel = "GETTING AROUND", Benefit = "Drive 3× faster", Desc = "A space car and a garage to charge it. Much faster than walking!" },
            new ExtraItemInfo { Key = "bat2", Name = "Second battery", Cat = "POWER", CatLabel = "POWER", Benefit = "Power for longer", Desc = "Keeps the lights on when it gets dark." },
            new ExtraItemInfo { Key = "sol3", Name = "Extra solar panel", Cat = "POWER", CatLabel = "POWER", Benefit = "More sun power", Desc = "One more solar panel. Fills the battery faster." },
            new ExtraItemInfo { Key = "tank", Name = "Air tank", Cat = "LIFE SUPPORT", CatLabel = "AIR & WATER", Benefit = "Save more air", Desc = "Keeps extra oxygen (air) for later, so none is wasted." },
            new ExtraItemInfo { Key = "water", Name = "Extra water (10 L)", Cat = "LIFE SUPPORT", CatLabel = "AIR & WATER", Benefit = "More water", Desc = "More water to drink. The oxygen maker uses water too." },
            new ExtraItemInfo { Key = "wtank", Name = "Water tank (40 L)", Cat = "LIFE SUPPORT", CatLabel = "AIR & WATER", Benefit = "Hold more water", Desc = "A big tank, so you have room for extra water." },
            new ExtraItemInfo { Key = "spare", Name = "Spare parts kit", Cat = "REPAIR", CatLabel = "FIXING", Benefit = "Fix things fast", Desc = "Parts to fix your suit or a broken machine right away." },
            new ExtraItemInfo { Key = "cart", Name = "Extra air filter", Cat = "LIFE SUPPORT", CatLabel = "AIR & WATER", Benefit = "Clean air for longer", Desc = "Cleans out the bad air (CO₂) you breathe out." }
        };

        /// <summary>Loadout screen matching Loadout.dc.html: standard gear (13 packed), categorized extras, rocket capacity bars, detail inspector, and packed list.</summary>
        public void ShowLoadout(Action<Loadout> onLaunch)
        {
            var chosen = loadoutPicks ??= new HashSet<string> { "rover" };
            string activeTab = "ALL";
            string inspectedId = "rover";

            var scr = Ui.Box(root, "screen", "center", "dim");
            scr.pickingMode = PickingMode.Position;
            var p = Ui.Panel(scr); p.style.width = 1720; p.style.height = 960;
            p.style.paddingLeft = 32; p.style.paddingRight = 32; p.style.paddingTop = 24; p.style.paddingBottom = 20;

            // Header row
            var head = Ui.Box(p, "row", "noshrink");
            var hTitle = Ui.Text(head, "PACK YOUR ROCKET", "h1");
            hTitle.style.letterSpacing = 2; hTitle.style.color = new Color(0.92f, 0.96f, 1f);
            Ui.Box(head, "grow");
            Ui.Text(head, $"MISSION {Levels.Current.N}: {Levels.Current.Name}", "kicker-blue").style.fontSize = 20;

            var sub = Ui.Text(p, "The things you need are already packed. Click to add extras, if they fit in the rocket.", "text", "muted");
            sub.style.marginBottom = 12;

            var mainRow = Ui.Box(p, "row", "grow"); mainRow.style.alignItems = Align.Stretch;

            // Left Column: STANDARD GEAR (width 360)
            var stdCol = Ui.Box(mainRow, "col"); stdCol.style.width = 360; stdCol.style.marginRight = 16;
            var stdPanel = Ui.Box(stdCol, "col", "grow");
            stdPanel.style.backgroundColor = new Color(0.02f, 0.06f, 0.13f, 0.84f);
            Ui.SetBorder(stdPanel, 1, new Color(0.43f, 0.68f, 1f, 0.38f), 6);
            stdPanel.style.paddingLeft = 16; stdPanel.style.paddingRight = 16;
            stdPanel.style.paddingTop = 14; stdPanel.style.paddingBottom = 14;

            var sH = Ui.Text(stdPanel, "ALREADY PACKED", "section");
            sH.style.color = new Color(0.56f, 0.77f, 1f); sH.style.unityFontStyleAndWeight = FontStyle.Bold;
            var sSub = Ui.Text(stdPanel, "You always get these.", "tiny", "muted");
            sSub.style.marginBottom = 10;

            var stdScroll = Ui.Add(stdPanel, new ScrollView(), "grow");
            foreach (var (_, _, label) in Catalog.Standard)
            {
                var gearPill = Ui.Box(stdScroll, "row");
                gearPill.style.height = 36; gearPill.style.marginBottom = 6;
                gearPill.style.backgroundColor = new Color(0.08f, 0.18f, 0.33f, 0.55f);
                Ui.SetBorder(gearPill, 1, new Color(0.43f, 0.68f, 1f, 0.25f), 4);
                gearPill.style.paddingLeft = 10; gearPill.style.paddingRight = 10;
                var dot = Ui.Box(gearPill); dot.style.width = 6; dot.style.height = 6; Ui.SetRadius(dot, 3);
                dot.style.backgroundColor = new Color(0.35f, 0.89f, 0.79f); dot.style.marginRight = 8;
                var lbl = Ui.Text(gearPill, label, "small"); lbl.style.color = new Color(0.85f, 0.91f, 0.98f);
            }

            // Center Column: EXTRAS (width 830)
            var extCol = Ui.Box(mainRow, "col"); extCol.style.width = 830; extCol.style.marginRight = 16;
            var extPanel = Ui.Box(extCol, "col", "grow");
            extPanel.style.backgroundColor = new Color(0.02f, 0.06f, 0.13f, 0.84f);
            Ui.SetBorder(extPanel, 1, new Color(0.43f, 0.68f, 1f, 0.38f), 6);
            extPanel.style.paddingLeft = 16; extPanel.style.paddingRight = 16;
            extPanel.style.paddingTop = 14; extPanel.style.paddingBottom = 14;

            var eH = Ui.Text(extPanel, "EXTRAS YOU CAN ADD", "section");
            eH.style.color = new Color(0.56f, 0.77f, 1f); eH.style.unityFontStyleAndWeight = FontStyle.Bold;
            var eSub = Ui.Text(extPanel, "Click a card to add it. Click again to take it out. Grey cards don't fit.", "tiny", "muted");
            eSub.style.marginBottom = 10;

            // Tabs row
            var tabRow = Ui.Box(extPanel, "row", "noshrink");
            Ui.Show(tabRow, false);   // 8 cards fit on one page; category tabs were one more thing to read
            tabRow.style.marginBottom = 12;
            var tabBtns = new Dictionary<string, Button>();

            var cardScroll = Ui.Add(extPanel, new ScrollView(), "grow");
            var cardGrid = Ui.Box(cardScroll, "row", "wrap");

            // Right Column: LIMITS & DETAILS (width 430)
            var rightCol = Ui.Box(mainRow, "col", "grow");
            var rightPanel = Ui.Box(rightCol, "col", "grow");
            rightPanel.style.backgroundColor = new Color(0.02f, 0.06f, 0.13f, 0.84f);
            Ui.SetBorder(rightPanel, 1, new Color(0.43f, 0.68f, 1f, 0.38f), 6);
            rightPanel.style.paddingLeft = 16; rightPanel.style.paddingRight = 16;
            rightPanel.style.paddingTop = 14; rightPanel.style.paddingBottom = 14;

            var capH = Ui.Text(rightPanel, "HOW FULL IS THE ROCKET?", "section");
            capH.style.color = new Color(0.56f, 0.77f, 1f); capH.style.unityFontStyleAndWeight = FontStyle.Bold;

            Ui.Text(rightPanel, "WEIGHT", "tiny", "muted").style.marginTop = 6;
            var (_, massFill) = Ui.Bar(rightPanel, "bar-big");
            var massText = Ui.Text(rightPanel, "", "tiny"); massText.style.marginBottom = 6;

            Ui.Text(rightPanel, "MONEY", "tiny", "muted");
            var (_, budgetFill) = Ui.Bar(rightPanel, "bar-big");
            var budgetText = Ui.Text(rightPanel, "", "tiny"); budgetText.style.marginBottom = 14;

            // Detail inspector box
            var detailBox = Ui.Box(rightPanel, "col");
            detailBox.style.backgroundColor = new Color(0.05f, 0.12f, 0.24f, 0.7f);
            Ui.SetBorder(detailBox, 1, new Color(0.43f, 0.68f, 1f, 0.3f), 6);
            detailBox.style.paddingLeft = 12; detailBox.style.paddingRight = 12;
            detailBox.style.paddingTop = 10; detailBox.style.paddingBottom = 10; detailBox.style.marginBottom = 14;

            var dTitle = Ui.Text(detailBox, "", "h3"); dTitle.style.color = Color.white;
            var dCat = Ui.Text(detailBox, "", "tiny"); dCat.style.color = new Color(0.4f, 0.8f, 1f); dCat.style.marginBottom = 4;
            var dFx = Ui.Text(detailBox, "", "text-small"); dFx.style.color = new Color(0.35f, 0.89f, 0.79f); dFx.style.unityFontStyleAndWeight = FontStyle.Bold;
            var dDesc = Ui.Text(detailBox, "", "tiny"); dDesc.style.color = new Color(0.8f, 0.88f, 0.96f); dDesc.style.whiteSpace = WhiteSpace.Normal; dDesc.style.marginBottom = 6;
            var dStatus = Ui.Text(detailBox, "", "tiny"); dStatus.style.unityFontStyleAndWeight = FontStyle.Bold;

            // Packed list
            var pkH = Ui.Text(rightPanel, "YOUR EXTRAS", "section");
            pkH.style.color = new Color(0.56f, 0.77f, 1f); pkH.style.unityFontStyleAndWeight = FontStyle.Bold;
            var packedScroll = Ui.Add(rightPanel, new ScrollView(), "grow");

            var tipLbl = Ui.Text(rightPanel, "Too full? Press ✕ to take something out.", "tiny", "muted");
            tipLbl.style.marginTop = 6;

            // Bottom bar
            var foot = Ui.Box(p, "row"); foot.style.marginTop = 14; foot.style.alignItems = Align.Center;
            Ui.Button(foot, "BACK", () => { scr.RemoveFromHierarchy(); ShowBriefing(() => ShowLoadout(onLaunch)); });
            Ui.Button(foot, "Expert: full cargo list", () => { scr.RemoveFromHierarchy(); ShowAdvancedLoadout(onLaunch); });
            Ui.Box(foot, "grow");
            var launch = Ui.Button(foot, "NEXT: PLAN YOUR DAYS  >", () =>
            {
                scr.RemoveFromHierarchy();
                StartCoroutine(FadeThen(() => onLaunch(Catalog.Simple(chosen))));
            }, "btn-primary", "btn-big");
            launch.style.minWidth = 280; launch.style.fontSize = 22;

            // Visual elements dictionary for card views
            var cardViews = new Dictionary<string, (VisualElement card, Label chk, Label statusLbl, VisualElement topRow)>();

            void UpdateInspector()
            {
                var meta = System.Array.Find(ExtrasMeta, x => x.Key == inspectedId);
                if (meta.Key == null) meta = ExtrasMeta[0];
                var kit = Catalog.Simple(chosen);
                bool isPacked = chosen.Contains(meta.Key);
                bool fits = isPacked || Catalog.Simple(chosen.Concat(new[] { meta.Key })).WithinLimits;

                dTitle.text = meta.Name;
                dCat.text = meta.CatLabel;
                dFx.text = meta.Benefit;
                dDesc.text = meta.Desc;
                dStatus.text = !fits ? "It won't fit. Take something out first." : isPacked ? "In the rocket!" : "Not packed. Click the card to add it.";
                dStatus.style.color = !fits ? new Color(1f, 0.45f, 0.45f) : isPacked ? new Color(1f, 0.69f, 0.4f) : new Color(0.6f, 0.75f, 0.9f);
            }

            void Refresh()
            {
                var kit = Catalog.Simple(chosen);
                float massRatio = kit.Mass / Catalog.M1Cargo;
                float budgetRatio = kit.Cost / Catalog.M1Budget;
                float maxFill = Mathf.Max(massRatio, budgetRatio);

                Ui.SetBar(massFill, massRatio, kit.Mass > Catalog.M1Cargo ? Severity.Critical : kit.Mass > 0.9f * Catalog.M1Cargo ? Severity.Warning : Severity.Good);
                Ui.SetBar(budgetFill, budgetRatio, kit.Cost > Catalog.M1Budget ? Severity.Critical : kit.Cost > 0.9f * Catalog.M1Budget ? Severity.Warning : Severity.Good);

                massText.text = $"{kit.Mass:0} of {Catalog.M1Cargo:0} kg";
                budgetText.text = $"{kit.Cost:0} of {Catalog.M1Budget:0} coins";

                launch.SetEnabled(kit.WithinLimits);

                // Update cards
                foreach (var em in ExtrasMeta)
                {
                    if (!cardViews.TryGetValue(em.Key, out var cv)) continue;
                    bool isPacked = chosen.Contains(em.Key);
                    bool fits = isPacked || Catalog.Simple(chosen.Concat(new[] { em.Key })).WithinLimits;

                    bool tabMatch = activeTab == "ALL" || em.Cat == activeTab;
                    cv.card.style.display = tabMatch ? DisplayStyle.Flex : DisplayStyle.None;

                    cv.card.EnableInClassList("extra-card-selected", isPacked);
                    cv.card.EnableInClassList("extra-card-dim", !fits && !isPacked);

                    cv.chk.text = isPacked ? "✓" : "";
                    cv.chk.EnableInClassList("chk-box-on", isPacked);

                    cv.statusLbl.text = !fits && !isPacked ? "TOO BIG" : isPacked ? "PACKED" : "";
                    cv.statusLbl.style.color = !fits ? new Color(1f, 0.45f, 0.45f) : new Color(1f, 0.7f, 0.35f);
                }

                // Update packed list
                packedScroll.Clear();
                foreach (var em in ExtrasMeta)
                {
                    if (!chosen.Contains(em.Key)) continue;
                    var itemKey = em.Key;
                    var row = Ui.Box(packedScroll, "row");
                    row.style.height = 36; row.style.marginBottom = 6;
                    row.style.paddingLeft = 10; row.style.paddingRight = 6;
                    row.style.backgroundColor = new Color(0.79f, 0.34f, 0.05f, 0.2f);
                    Ui.SetBorder(row, 1, new Color(1f, 0.69f, 0.4f, 0.45f), 4);

                    var nameLbl = Ui.Text(row, em.Name, "small"); nameLbl.style.flexGrow = 1; nameLbl.style.color = Color.white;
                    var rmBtn = Ui.Button(row, "✕", () =>
                    {
                        chosen.Remove(itemKey);
                        inspectedId = itemKey;
                        Refresh();
                    }, "btn-small");
                    rmBtn.style.color = new Color(1f, 0.8f, 0.6f);
                }

                UpdateInspector();
            }

            // Tabs
            foreach (var t in new[] { "ALL", "POWER", "LIFE SUPPORT", "MOBILITY", "REPAIR" })
            {
                var tabName = t;
                var btn = Ui.Button(tabRow, tabName, () =>
                {
                    activeTab = tabName;
                    foreach (var kv in tabBtns) kv.Value.EnableInClassList("cat-tab-on", kv.Key == activeTab);
                    Refresh();
                }, "cat-tab");
                tabBtns[tabName] = btn;
            }
            tabBtns["ALL"].AddToClassList("cat-tab-on");

            // Build cards
            foreach (var em in ExtrasMeta)
            {
                var extraKey = em.Key;
                var card = Ui.Box(cardGrid, "extra-card");
                card.pickingMode = PickingMode.Position;   // Ui.Box is click-through by default

                var topRow = Ui.Box(card, "row");
                var catLabel = Ui.Text(topRow, em.CatLabel, "tiny");
                catLabel.style.color = new Color(0.4f, 0.75f, 1f); catLabel.style.flexGrow = 1;
                var chk = Ui.Text(topRow, "", "chk-box");

                var title = Ui.Text(card, em.Name, "text-small");
                title.style.color = Color.white; title.style.unityFontStyleAndWeight = FontStyle.Bold;

                var fx = Ui.Text(card, em.Benefit, "tiny");
                fx.style.color = new Color(0.35f, 0.89f, 0.79f);

                var botRow = Ui.Box(card, "row");
                botRow.style.marginTop = StyleKeyword.Auto;
                var (dm, dc) = ExtraDelta(em.Key);
                var metaLbl = Ui.Text(botRow, $"{dm:0} kg · {dc:0} coins", "tiny", "muted");
                metaLbl.style.flexGrow = 1;
                var statusLbl = Ui.Text(botRow, "", "tiny");

                card.RegisterCallback<ClickEvent>(_ =>
                {
                    inspectedId = extraKey;
                    if (chosen.Contains(extraKey))
                    {
                        chosen.Remove(extraKey);
                    }
                    else
                    {
                        if (Catalog.Simple(chosen.Concat(new[] { extraKey })).WithinLimits)
                            chosen.Add(extraKey);
                    }
                    Refresh();
                });

                cardViews[em.Key] = (card, chk, statusLbl, topRow);
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

        // ================================================================== 72 h planner

        static (Label val, Label sub) MakeSummaryTile(VisualElement parent, string title)
        {
            var t = Ui.Box(parent, "summary-tile");
            var l = Ui.Text(t, title, "tiny"); l.style.color = new Color(0.56f, 0.69f, 0.84f); l.style.unityFontStyleAndWeight = FontStyle.Bold;
            var v = Ui.Text(t, "", "h1"); v.style.color = Color.white; v.style.fontSize = 28;
            var s = Ui.Text(t, "", "tiny"); s.style.color = new Color(0.62f, 0.71f, 0.83f);
            return (v, s);
        }

        static void MakeTimelineLegend(VisualElement parent, string text, Color c)
        {
            var r = Ui.Box(parent, "row"); r.style.marginRight = 14; r.style.marginBottom = 4;
            var dot = Ui.Box(r); dot.style.width = 10; dot.style.height = 10; Ui.SetRadius(dot, 3);
            dot.style.backgroundColor = c; dot.style.marginRight = 6;
            var l = Ui.Text(r, text, "tiny"); l.style.color = new Color(0.79f, 0.85f, 0.92f);
        }

        /// <summary>Pre-launch schedule matching PlanHours.dc.html: 12 6-hour blocks, dropdown activities with tags, event badges, summary tiles, and 72-hour timeline.</summary>
        public void ShowPlanner(Loadout kit, Action<MissionPlan> onLaunch, Action onBack)
        {
            var plan = MissionPlan.Suggested(kit);
            var acts = MissionPlan.Available(kit);
            var names = acts.Select(MissionPlan.Name).ToList();
            Action refresh = null;

            var scr = Ui.Box(root, "screen", "center", "dim");
            scr.pickingMode = PickingMode.Position;
            var p = Ui.Panel(scr); p.style.width = 1720; p.style.height = 960;
            p.style.paddingLeft = 32; p.style.paddingRight = 32; p.style.paddingTop = 24; p.style.paddingBottom = 20;

            // Header row
            var head = Ui.Box(p, "row", "noshrink");
            var hTitle = Ui.Text(head, $"PLAN YOUR {MissionModel.DurationH:0} HOURS", "h1");
            hTitle.style.letterSpacing = 2; hTitle.style.color = new Color(0.92f, 0.96f, 1f);
            Ui.Box(head, "grow");
            Ui.Text(head, $"MISSION {Levels.Current.N}: {Levels.Current.Name}", "kicker-blue").style.fontSize = 20;

            var sub = Ui.Text(p, "We made a plan for you! You can change any line, or just press START MISSION. Don't forget to sleep!", "text", "muted");
            sub.style.marginBottom = 12;

            var mainRow = Ui.Box(p, "row", "grow"); mainRow.style.alignItems = Align.Stretch;

            // Left / Center column: 12-block table (width 1100)
            var tableCol = Ui.Box(mainRow, "col"); tableCol.style.width = 1100; tableCol.style.marginRight = 20;
            var tablePanel = Ui.Box(tableCol, "col", "grow");
            tablePanel.style.backgroundColor = new Color(0.02f, 0.06f, 0.13f, 0.84f);
            Ui.SetBorder(tablePanel, 1, new Color(0.43f, 0.68f, 1f, 0.38f), 6);
            tablePanel.style.paddingLeft = 16; tablePanel.style.paddingRight = 16;
            tablePanel.style.paddingTop = 12; tablePanel.style.paddingBottom = 12;

            var tHead = Ui.Box(tablePanel, "row", "noshrink");
            tHead.style.borderBottomWidth = 1; tHead.style.borderBottomColor = new Color(0.43f, 0.68f, 1f, 0.25f);
            tHead.style.paddingBottom = 8; tHead.style.marginBottom = 8;
            var colH1 = Ui.Text(tHead, "WHEN", "section"); colH1.style.width = 96; colH1.style.color = new Color(0.5f, 0.61f, 0.77f);
            var colH2 = Ui.Text(tHead, "WHAT YOU DO", "section"); colH2.style.width = 540; colH2.style.color = new Color(0.5f, 0.61f, 0.77f);
            var colH3 = Ui.Text(tHead, "WHAT HAPPENS", "section", "grow"); colH3.style.color = new Color(0.5f, 0.61f, 0.77f);

            var tableScroll = Ui.Add(tablePanel, new ScrollView(), "grow");
            var drops = new DropdownField[MissionPlan.Blocks];
            var typeTags = new Label[MissionPlan.Blocks];

            for (int i = 0; i < MissionPlan.Blocks; i++)
            {
                int b = i;
                var r = Ui.Box(tableScroll, "row");
                r.style.height = 44; r.style.marginBottom = 4; r.style.alignItems = Align.Center;

                var hrsLbl = Ui.Text(r, MissionPlan.Hours(b), "text-small");
                hrsLbl.style.width = 110; hrsLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                hrsLbl.style.color = new Color(0.84f, 0.89f, 0.96f);

                var d = Ui.Add(r, new DropdownField(names, acts.IndexOf(plan.At[b])));
                d.style.width = 460; d.style.fontSize = 17;
                drops[b] = d;

                var tagLbl = Ui.Text(r, "", "tiny");
                tagLbl.style.width = 65; tagLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                tagLbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                tagLbl.style.marginLeft = 6;
                tagLbl.style.width = 80; typeTags[b] = tagLbl;

                d.RegisterValueChangedCallback(_ =>
                {
                    plan.At[b] = acts[d.index];
                    refresh?.Invoke();
                });

                var evText = MissionPlan.Events(b);
                var evRow = Ui.Box(r, "row", "grow"); evRow.style.marginLeft = 16;
                if (!string.IsNullOrEmpty(evText))
                {
                    var evBadge = Ui.Box(evRow, "row");
                    evBadge.style.paddingLeft = 10; evBadge.style.paddingRight = 10;
                    evBadge.style.paddingTop = 4; evBadge.style.paddingBottom = 4;

                    Color evColor = evText.Contains("Solar storm") ? new Color(1f, 0.71f, 0.28f)
                                  : evText.Contains("Dark") ? new Color(0.78f, 0.82f, 1f)
                                  : evText.Contains("Moonquake") ? new Color(1f, 0.55f, 0.55f)
                                  : new Color(0.21f, 0.89f, 0.79f);

                    Ui.SetBorder(evBadge, 1, new Color(evColor.r, evColor.g, evColor.b, 0.5f), 12);
                    evBadge.style.backgroundColor = new Color(evColor.r, evColor.g, evColor.b, 0.12f);

                    var evDot = Ui.Box(evBadge); evDot.style.width = 8; evDot.style.height = 8; Ui.SetRadius(evDot, 4);
                    evDot.style.backgroundColor = evColor; evDot.style.marginRight = 6;
                    var evLbl = Ui.Text(evBadge, evText, "tiny"); evLbl.style.color = evColor; evLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                }
            }

            // Right Column: SUMMARY & TIMELINE (width 540)
            var rightCol = Ui.Box(mainRow, "col", "grow");
            var rightPanel = Ui.Box(rightCol, "col", "grow");
            rightPanel.style.backgroundColor = new Color(0.02f, 0.06f, 0.13f, 0.84f);
            Ui.SetBorder(rightPanel, 1, new Color(0.43f, 0.68f, 1f, 0.38f), 6);
            rightPanel.style.paddingLeft = 18; rightPanel.style.paddingRight = 18;
            rightPanel.style.paddingTop = 14; rightPanel.style.paddingBottom = 14;

            var statBanner = Ui.Box(rightPanel, "col");
            statBanner.style.paddingLeft = 14; statBanner.style.paddingRight = 14;
            statBanner.style.paddingTop = 10; statBanner.style.paddingBottom = 10;
            Ui.SetBorderWidth(statBanner, 1);
            Ui.SetRadius(statBanner, 6);
            statBanner.style.marginBottom = 14;
            var statBannerText = Ui.Text(statBanner, "", "small"); statBannerText.style.unityFontStyleAndWeight = FontStyle.Bold;

            // 3 Summary tiles
            var (tileSleepVal, tileSleepSub) = MakeSummaryTile(rightPanel, "SLEEP");
            var (tileEvaVal, tileEvaSub) = MakeSummaryTile(rightPanel, "TRIPS OUTSIDE");
            var (tileWorkVal, tileWorkSub) = MakeSummaryTile(rightPanel, "INSIDE THE BASE");

            // 72-Hour Timeline
            var tlH = Ui.Text(rightPanel, "YOUR PLAN AT A GLANCE", "section");
            tlH.style.color = new Color(0.56f, 0.77f, 1f); tlH.style.unityFontStyleAndWeight = FontStyle.Bold;
            tlH.style.marginTop = 6;

            var timelineBar = Ui.Box(rightPanel, "row");
            timelineBar.style.height = 20; Ui.SetRadius(timelineBar, 4); timelineBar.style.overflow = Overflow.Hidden;
            timelineBar.style.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.8f);
            timelineBar.style.marginBottom = 10;

            var segs = new VisualElement[MissionPlan.Blocks];
            for (int b = 0; b < MissionPlan.Blocks; b++)
            {
                var seg = Ui.Box(timelineBar);
                seg.style.flexGrow = 1; seg.style.height = Length.Percent(100);
                segs[b] = seg;
            }

            // Legend
            var leg = Ui.Box(rightPanel, "row", "wrap");
            MakeTimelineLegend(leg, "Outside", new Color(1f, 0.54f, 0.16f));
            MakeTimelineLegend(leg, "Sleep", new Color(0.56f, 0.66f, 1f));
            MakeTimelineLegend(leg, "Inside", new Color(0.21f, 0.89f, 0.79f));

            // Footer
            var foot = Ui.Box(p, "row"); foot.style.marginTop = 14; foot.style.alignItems = Align.Center;
            var sugBtn = Ui.Button(foot, "UNDO MY CHANGES", () =>
            {
                var s = MissionPlan.Suggested(kit);
                for (int b = 0; b < MissionPlan.Blocks; b++)
                {
                    plan.At[b] = s.At[b];
                    drops[b].SetValueWithoutNotify(MissionPlan.Name(s.At[b]));
                }
                refresh?.Invoke();
            });
            sugBtn.style.fontSize = 17;

            Ui.Box(foot, "grow");
            Ui.Button(foot, "BACK TO PACKING", () => { scr.RemoveFromHierarchy(); onBack(); });
            var startBtn = Ui.Button(foot, "START MISSION  >", () =>
            {
                scr.RemoveFromHierarchy();
                StartCoroutine(FadeThen(() => onLaunch(plan)));
            }, "btn-primary", "btn-big");
            startBtn.style.minWidth = 280; startBtn.style.fontSize = 22;

            refresh = () =>
            {
                for (int b = 0; b < MissionPlan.Blocks; b++)
                {
                    var a = plan.At[b];
                    bool isSleep = a == Activity.Sleep;
                    bool isEva = MissionPlan.Eva(a);
                    Color c = isSleep ? new Color(0.56f, 0.66f, 1f) : isEva ? new Color(1f, 0.54f, 0.16f) : new Color(0.21f, 0.89f, 0.79f);
                    segs[b].style.backgroundColor = c;

                    typeTags[b].text = isSleep ? "SLEEP" : isEva ? "OUTSIDE" : "INSIDE";
                    typeTags[b].style.color = c;
                }

                int sleep = plan.At.Count(a => a == Activity.Sleep);
                int eva = plan.At.Count(MissionPlan.Eva);
                int work = MissionPlan.Blocks - sleep - eva;

                tileSleepVal.text = $"{sleep * MissionPlan.BlockH} h";
                tileSleepSub.text = $"you need about {Mathf.Max(2, MissionPlan.Blocks / 4) * MissionPlan.BlockH} h";

                tileEvaVal.text = $"{eva}";
                tileEvaSub.text = $"{eva * MissionPlan.BlockH} hours outside";

                tileWorkVal.text = $"{work * MissionPlan.BlockH} h";
                tileWorkSub.text = "eat, drink, look after the base";

                var w = plan.Warnings(kit);
                bool ok = w.Count == 0;
                statBanner.style.backgroundColor = ok ? new Color(0.21f, 0.89f, 0.79f, 0.12f) : new Color(1f, 0.71f, 0.28f, 0.12f);
                Ui.SetBorderColor(statBanner, ok ? new Color(0.21f, 0.89f, 0.79f, 0.45f) : new Color(1f, 0.71f, 0.28f, 0.55f));
                statBannerText.style.color = ok ? new Color(0.21f, 0.89f, 0.79f) : new Color(1f, 0.81f, 0.54f);
                statBannerText.text = ok ? "Your plan looks great!" : string.Join("\n", w.Select(x => "! " + x));
            };

            refresh();
            TestAdvance = _ => { scr.RemoveFromHierarchy(); StartCoroutine(FadeThen(() => onLaunch(plan))); };
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
            gauges.Clear();

            // Top bar: life-critical + time matching hud_unpacked.html
            var top = Ui.Box(hud, "hud-top", "row");
            var tp = Ui.Panel(top, "row", "hud-card"); tp.style.paddingTop = 8; tp.style.paddingBottom = 8;
            // Wrist watch (Q): the habitat's status, readable from anywhere on the surface. Hidden until the astronaut checks it.
            watch = Ui.Panel(hud, "hud-card", "col"); watch.pickingMode = PickingMode.Ignore;
            watch.style.position = Position.Absolute; watch.style.bottom = 110; watch.style.left = Length.Percent(50);
            watch.style.translate = new Translate(Length.Percent(-50), 0); watch.style.width = 600;
            watch.style.borderTopLeftRadius = watch.style.borderTopRightRadius = watch.style.borderBottomLeftRadius = watch.style.borderBottomRightRadius = 40;
            watch.style.backgroundColor = new Color(0.06f, 0.08f, 0.11f, 0.97f); watch.style.paddingLeft = watch.style.paddingRight = 28;
            watch.style.borderTopWidth = watch.style.borderBottomWidth = watch.style.borderLeftWidth = watch.style.borderRightWidth = 4;
            watch.style.borderTopColor = watch.style.borderBottomColor = watch.style.borderLeftColor = watch.style.borderRightColor = new Color(0.55f, 0.6f, 0.68f);
            Ui.Text(watch, "WRIST WATCH  -  HOW IS THE BASE?", "card-title");
            var ws = Ui.Box(watch, "row"); ws.style.flexWrap = Wrap.Wrap;
            foreach (var (key, name) in new[] { ("power", "SOLAR POWER"), ("battery", "BATTERY"), ("o2", "AIR TANK"), ("water", "WATER"), ("food", "FOOD"), ("samples", "ROCK SAMPLES"), ("co2", "BAD AIR (CO2)"), ("sun", "SUNLIGHT"), ("rc", "ROBOT ROVER") })
            {
                var s = Ui.Box(ws, "stat", "col"); s.style.width = 160; s.style.marginTop = 6; s.style.marginBottom = 6;
                Ui.Text(s, name, "stat-name");
                stats[key] = Ui.Text(s, "-", "stat-value");
            }
            Ui.Text(watch, "[Q] Hide watch", "tiny", "muted").style.marginTop = 4;
            Ui.Show(watch, false);

            var sunBox = Ui.Box(tp, "stat", "col");
            Ui.Text(sunBox, "SUNLIGHT", "stat-name");
            sunCountdownLbl = Ui.Text(sunBox, "--", "stat-value");
            sunCountdownLbl.style.fontSize = 17; sunCountdownLbl.style.color = new Color(0.4f, 0.8f, 1f);

            var time = Ui.Box(tp, "stat", "col"); time.style.marginLeft = 16;
            Ui.Text(time, "TIME", "stat-name");
            clockText = Ui.Text(time, "", "stat-value");
            var speeds = Ui.Box(tp, "row"); speeds.style.marginLeft = 10;
            foreach (var (label, val) in new[] { ("Pause", 0), ("1x", 1), ("2x", 2), ("4x", 4) })
            {
                var b = Ui.Button(speeds, label, () => m.SetSpeed(val), "speed-btn");
                b.userData = val; speedButtons.Add(b);
            }

            // Left column: stacked cards (ASTRONAUT vitals, EVA suit when outside, my plan)
            leftCol = Ui.Box(hud, "hud-left", "col");
            var left = Ui.Panel(leftCol, "hud-card");

            var astroHead = Ui.Box(left, "row");
            Ui.Text(astroHead, "YOU", "card-title");
            Ui.Box(astroHead, "grow");
            astroStatusBadge = Ui.Text(astroHead, "OK", "status-badge", "stable");

            // every bar: full = good (Food = not hungry, Energy = not tired)
            foreach (var (key, name) in new[] { ("health", "Health"), ("hunger", "Food"), ("hydration", "Water"), ("fatigue", "Energy") })
                VitalRow(left, key, name);
            if (Levels.Current.SpeStart >= 0) VitalRow(left, "dose", "Radiation");   // only levels with a solar storm

            // spacesuit card (shown outside only)
            eva = Ui.Panel(leftCol, "hud-card");
            Ui.Text(eva, "SPACESUIT", "card-title");
            foreach (var (key, name) in new[] { ("evaTime", "Air left"), ("suit", "Suit health") })
                VitalRow(eva, key, name);
            evaWalk = Ui.Text(eva, "", "small", "muted");

            // Rover card (shown while driving)
            rover = Ui.Panel(leftCol, "hud-card");
            Ui.Text(rover, "ROVER", "card-title");
            VitalRow(rover, "rBatt", "Rover battery");
            roverRange = Ui.Text(rover, "", "small", "muted");

            // RC-1 handheld controller (V inside the habitat): live rover camera on its screen + battery
            remote = Ui.Panel(hud, "hud-card", "col"); remote.pickingMode = PickingMode.Ignore;
            remote.style.position = Position.Absolute; remote.style.bottom = 110; remote.style.left = Length.Percent(50);
            remote.style.translate = new Translate(Length.Percent(-50), 0); remote.style.width = 520;
            remote.style.borderTopLeftRadius = remote.style.borderTopRightRadius = remote.style.borderBottomLeftRadius = remote.style.borderBottomRightRadius = 28;
            remote.style.backgroundColor = new Color(0.1f, 0.11f, 0.13f, 0.96f); remote.style.paddingLeft = remote.style.paddingRight = 20;
            Ui.Text(remote, "RC-1 REMOTE ROVER CONTROLLER", "card-title");
            var feed = Ui.Box(remote); feed.style.width = 480; feed.style.height = 300;
            feed.style.backgroundImage = Background.FromRenderTexture(m.RemoteCam.targetTexture);
            feed.style.borderTopWidth = feed.style.borderBottomWidth = feed.style.borderLeftWidth = feed.style.borderRightWidth = 3;
            feed.style.borderTopColor = feed.style.borderBottomColor = feed.style.borderLeftColor = feed.style.borderRightColor = new Color(0.02f, 0.02f, 0.03f);
            VitalRow(remote, "rcBatt", "Battery");
            remoteInfo = Ui.Text(remote, "", "small", "muted");

            // Right: Lunar day clock + Objectives panel
            var right = Ui.Panel(hud, "hud-right", "hud-card");

            // what to do now: the first thing a player reads
            hintBox = Ui.Box(right, "col");
            Ui.Text(hintBox, "WHAT TO DO NOW", "card-title");
            hintText = Ui.Text(hintBox, "", "small", "cyan"); hintText.style.fontSize = 19;
            Ui.Box(hintBox, "card-divider");

            linkRow = Ui.Box(right, "row"); linkDot = Ui.Dot(linkRow, Severity.Warning);   // shown only when the Earth link is down
            stats["link"] = Ui.Text(linkRow, "", "small", "muted");

            // Objectives header with collapsible button
            var objHead = Ui.Box(right, "row");
            objHeaderLbl = Ui.Text(objHead, "GOALS", "card-title");
            Ui.Box(objHead, "grow");
            objToggleBtn = Ui.Button(objHead, "▲", ToggleObjectives, "btn-small");
            objToggleBtn.style.paddingLeft = 6; objToggleBtn.style.paddingRight = 6;

            objBody = Ui.Box(right, "col");
            var mo = m.Model;
            foreach (var o in Levels.Current.Objectives) { var ob = o; AddObjective(objBody, () => ob.Done(mo), () => ob.Label(mo), ob.Bonus); }

            // Prompt + alerts + banner
            var bottom = Ui.Box(hud, "hud-bottom");
            prompt = Ui.Box(bottom, "prompt", "row");
            promptText = Ui.Text(prompt, "");
            alerts = Ui.Box(hud, "hud-alerts", "col");
            banner = Ui.Box(hud, "banner"); bannerText = Ui.Text(banner, "", "banner-box"); Ui.Show(banner, false);

            // Bottom-left: key hints matching hud_unpacked.html
            hudKeyHints = Ui.Box(hud, "row");
            hudKeyHints.style.position = Position.Absolute;
            hudKeyHints.style.left = 24; hudKeyHints.style.bottom = 14;
            MakeHintItem(hudKeyHints, "Tab", "Base controls");
            MakeHintItem(hudKeyHints, "O", "Goals");
            MakeHintItem(hudKeyHints, "M", "Map");
            MakeHintItem(hudKeyHints, "Q", "Base status");
            MakeHintItem(hudKeyHints, "H", "Help");

            if (GameInput.Touch)
            {
                TouchControls.Build(hud, k => k != UnityEngine.InputSystem.Key.C || (m.Remote != null && m.Remote.Driving));
                Ui.Show(hudKeyHints, false);                       // keyboard hints mean nothing on a phone
                alerts.style.bottom = 290;                         // above the move stick
                prompt.style.maxWidth = Length.Percent(46);        // between the stick and the action buttons
            }

            BuildSystemsPanel();
            BuildInventoryPanel();
            BuildStoresPanel();
            BuildPlanBoard();
            BuildTasksPanel();
            BuildPauseMenu();
        }

        static Label AddRoutineSlot(VisualElement parent, string title, Color col)
        {
            var s = Ui.Box(parent, "col");
            s.style.marginRight = 20; s.style.marginLeft = 6;
            var t = Ui.Text(s, title, "tiny"); t.style.color = col; t.style.unityFontStyleAndWeight = FontStyle.Bold;
            var v = Ui.Text(s, "--", "tiny"); v.style.color = new Color(0.9f, 0.94f, 1f);
            return v;
        }

        static void MakeHintItem(VisualElement parent, string key, string label)
        {
            var item = Ui.Box(parent, "row"); item.style.marginRight = 14; item.style.alignItems = Align.Center;
            var k = Ui.Text(item, key, "key-cap"); k.style.fontSize = 12;
            var l = Ui.Text(item, label, "tiny"); l.style.color = new Color(0.79f, 0.85f, 0.92f);
        }

        /// <summary>Objective suffix naming the block(s) the player planned for it.</summary>
        string Planned(Activity a)
        {
            var at = Enumerable.Range(0, MissionPlan.Blocks).Where(b => m.Plan.At[b] == a).Select(MissionPlan.Hours).ToList();
            return at.Count == 0 ? "  - not planned" : "  - " + string.Join(", ", at);
        }

        void VitalRow(VisualElement parent, string key, string name)
        {
            var r = Ui.Box(parent, "row", "vital");
            Ui.Text(r, name, "small", "grow"); var v = Ui.Text(r, "", "small");
            bars[key] = (Ui.Bar(parent, "bar-thin").fill, v);
        }

        void AddObjective(VisualElement parent, Func<bool> done, Func<string> text, bool optional = false)
        {
            var r = Ui.Box(parent, "row", "objective");
            var d = Ui.Dot(r, Severity.Info);
            var l = Ui.Text(r, "", "small", optional ? "muted" : "");
            l.enableRichText = true; l.style.flexShrink = 1;
            objectives.Add((l, d, done, text));
        }

        void Update()
        {
            if (hud == null || m.Model == null) return;
            var mo = m.Model;

            if (GameInput.Objectives) ToggleObjectives();

            if (astroStatusBadge != null)
            {
                bool critAstro = mo.Health < 30 || mo.Dose > 20 || (!mo.Inside && mo.EvaLeft < 0.6f);
                bool warnAstro = mo.Health < 60 || mo.Hunger < 30 || mo.Hydration < 30 || mo.Fatigue > 60 || mo.Dose > 15;
                astroStatusBadge.text = critAstro ? "DANGER" : warnAstro ? "CAREFUL" : "OK";
                astroStatusBadge.RemoveFromClassList("stable");
                astroStatusBadge.RemoveFromClassList("warn");
                astroStatusBadge.RemoveFromClassList("crit");
                astroStatusBadge.AddToClassList(critAstro ? "crit" : warnAstro ? "warn" : "stable");
            }

            stats["power"].text = mo.Supply >= mo.Demand ? "Enough" : "Not enough";
            stats["power"].style.color = mo.Supply >= mo.Demand ? new Color(0.5f, 0.9f, 0.6f) : new Color(1f, 0.75f, 0.3f);
            stats["battery"].text = mo.BatteryCap > 0 ? $"{mo.BatteryPct:0%}" : "none";
            Colour(stats["battery"], Ui.Level(mo.BatteryPct, 0.25f, 0.1f));
            stats["o2"].text = mo.CabinO2Cap > 0 ? $"{mo.CabinO2 / mo.CabinO2Cap:0%}" : "-"; Colour(stats["o2"], Ui.Level(mo.CabinO2, 2f, 1f));
            stats["water"].text = $"{mo.Water:0} L"; Colour(stats["water"], Ui.Level(mo.Water, 3f, 1f));
            stats["food"].text = $"{mo.Food / 2500f:0} days";
            if (stats.TryGetValue("samples", out var sLbl))
                sLbl.text = $"{mo.SamplesDelivered + mo.SamplesCarried:0.00} kg";
            stats["co2"].text = mo.CO2 > 0.6f ? "DANGER" : mo.CO2 > 0.4f ? "High" : "OK"; Colour(stats["co2"], mo.CO2 > 0.6f ? Severity.Critical : mo.CO2 > 0.4f ? Severity.Warning : Severity.Good);
            stats["sun"].text = mo.Sun < 0.05f ? "Dark" : $"{mo.Sun:0%}";

            if (sunCountdownLbl != null)
            {
                float ds = MissionModel.DarkStart(mo.Hour), de = ds + MissionModel.ShadowLength;
                sunCountdownLbl.text = mo.Hour < ds ? $"Dark in {Mathf.Ceil(ds - mo.Hour):0} h" : mo.Hour < de ? $"Dark: {Mathf.Ceil(de - mo.Hour):0} h left" : "Sunny";
            }

            clockText.text = $"Hour {Mathf.Floor(mo.Hour):0} of {MissionModel.DurationH:0}";

            foreach (var b in speedButtons) b.EnableInClassList("speed-on", (int)b.userData == m.Speed);

            SetStat("health", mo.Health / 100f, $"{mo.Health:0}%", Ui.Level(mo.Health, 60, 30));
            SetStat("hunger", mo.Hunger / 100f, $"{mo.Hunger:0}%", Ui.Level(mo.Hunger, 40, 15));   // 100 = fed
            SetStat("hydration", mo.Hydration / 100f, $"{mo.Hydration:0}%", Ui.Level(mo.Hydration, 40, 15));
            SetStat("fatigue", 1f - mo.Fatigue / 100f, $"{100f - mo.Fatigue:0}%", Ui.Level(100f - mo.Fatigue, 60, 30));   // shown as energy
            SetStat("dose", mo.Dose / MissionModel.DoseAllowance, $"{mo.Dose / MissionModel.DoseAllowance:0%}", mo.Dose > 20 ? Severity.Critical : mo.Dose > 15 ? Severity.Warning : Severity.Good);
            SetStat("suit", mo.SuitIntegrity / 100f, $"{mo.SuitIntegrity:0}%", Ui.Level(mo.SuitIntegrity, 60, 30));
            SetStat("evaTime", mo.EvaLeft / MissionModel.SuitCO2Max, $"{mo.EvaLeft:0.0} h", Ui.Level(mo.EvaLeft, 1.5f, 0.6f));

            Ui.Show(linkRow, m.Kit.Has("COM") && !mo.Net.Connected);
            stats["link"].text = mo.Load("COM")?.Powered != true ? "No call to Earth: the radio has no power" : "No call to Earth: fix the antenna at the radio desk";
            Colour(stats["link"], mo.Net.Connected ? Severity.Info : Severity.Warning);
            Ui.SetDot(linkDot, mo.Net.Connected ? Severity.Good : Severity.Warning);

            int doneCount = 0;
            foreach (var o in objectives)
            {
                bool done = o.done();
                if (done) doneCount++;
                o.label.text = o.text();
                Ui.SetDot(o.dot, done ? Severity.Good : Severity.Info);
            }
            if (objHeaderLbl != null) objHeaderLbl.text = $"GOALS  {doneCount} of {objectives.Count} done";

            Ui.Show(hintBox, Game.Hints);
            if (Game.Hints) hintText.text = GameInput.Words(NextHint(mo));

            Ui.Show(eva, !mo.Inside);
            if (!mo.Inside)
            {
                float need = m.HoursToWalkBack, left = mo.EvaLeft - 0.5f;
                bool ok = need < left;
                evaWalk.text = ok ? $"Home (airlock) is {m.DistanceToHab:0} m away" : $"Home is {m.DistanceToHab:0} m away. Go back now!";
                evaWalk.style.color = ok ? (need > left * 0.7f ? new Color(1f, 0.8f, 0.3f) : new Color(0.6f, 0.95f, 0.7f)) : new Color(1f, 0.4f, 0.35f);
            }

            var rc = m.Remote; float rcPct = mo.RemoteBattery / 100f;
            if (rc != null) {
            stats["rc"].text = $"{mo.RemoteBattery:0}%{(mo.RemoteDocked ? " charging" : "")}"; Colour(stats["rc"], Ui.Level(rcPct, 0.4f, 0.15f));
            Ui.Show(remote, rc.Driving);
            Ui.Show(watch, watchOn && !rc.Driving);
            if (rc.Driving)
            {
                SetStat("rcBatt", rcPct, $"{mo.RemoteBattery:0}%", Ui.Level(rcPct, 0.4f, 0.15f));
                float far = new Vector2(rc.transform.position.x - m.World.Hab.position.x, rc.transform.position.z - m.World.Hab.position.z).magnitude;
                remoteInfo.text = GameInput.Words($"{far:0} m from home. Can drive about {mo.RemoteBattery / MissionModel.RemotePctPerMeter:0} m more.\n[C] change camera   [V] stop driving");
            }
            }

            bool driving = m.Rover != null && m.Rover.Driving;
            Ui.Show(rover, driving);
            if (driving)
            {
                SetStat("rBatt", mo.RoverBattery / mo.RoverCap, $"{mo.RoverBattery / mo.RoverCap:0%}", Ui.Level(mo.RoverBattery / mo.RoverCap, 0.4f, 0.15f));
                roverRange.text = $"Can drive about {mo.RoverBattery / 0.12f * 10f:0} m more. Park at the garage to charge.";   // 0.12 PWh per 10 m (Loadout ROV-SC)
            }

            UpdatePrompt(mo);
            UpdateMarkers();
            UpdateSystems();
            UpdateInventory();
            UpdateStores();
            UpdatePlanBoard();
            UpdateTasks();

            bool crit = mo.O2ZeroHours > 0 || (!mo.Inside && mo.EvaLeft < 0.5f) || (mo.SpeActive && !mo.Inside);
            Ui.Show(banner, crit && !mo.Ended);
            if (crit) bannerText.text = mo.O2ZeroHours > 0 ? $"THE BASE IS OUT OF AIR! Press Tab and turn on the oxygen maker. ({2f - mo.O2ZeroHours:0.0} h left)"
                                     : mo.SpeActive && !mo.Inside ? "SOLAR STORM! Get inside the base now!"
                                     : "YOUR SUIT IS ALMOST OUT OF AIR! Go back to the airlock now!";
            Ui.Show(hud, m.Current == Mission1.Phase.Operations);
        }

        public string NextHintPublic() => NextHint(m.Model);

        string NextHint(MissionModel mo)
        {
            var tk = mo.Tasks;
            bool guided = tk != null && tk.Enabled && !tk.AutoPass;
            if (guided)
            {
                // the procedures (Tasks A-G) decide what comes next; the hint just points at the right place
                if (mo.SpeActive)
                    return !mo.Inside ? "Solar particle event: get back inside the habitat now!"
                         : mo.Sheltering ? "Stay in the shielded corner until the event is over, then press E there once more to be cleared."
                         : "Solar particle event: go to the radiation shelter (red marker, east bench) and press E.";
                if (mo.Inside && mo.EvaLeft < 0.6f && tk.Get("C").State != TaskState.Passed)
                    return "Suit is refilling. Speed up time (2/3) or sleep at the bunk first.";
                if (tk.Get("A").State != TaskState.Passed)
                    return mo.Inside ? "Task A: open the power & life support console (E at the console, or Tab) and read the checklist."
                                     : "Task A first: go inside the habitat and open the power & life support console.";
                if (mo.Inside && tk.Get("C").State == TaskState.Active && !tk.CanExit(out var why))
                    return "Before the EVA: " + why;
                if (mo.Inside && tk.Get("C").State == TaskState.Active)
                    return m.Player != null && !m.Player.SuitedIndoors ? "EVA ready: put the suit on at the alcove (E), then use the airlock door."
                                                                       : "EVA ready: use the airlock door.";
                if (mo.SeisDeployed)
                {
                    var step = tk.Prompt(Spot.Seis, m.Player != null ? m.Player.transform.eulerAngles.y : 0f);
                    if (step != null) return "Walk to the deployed quake sensor and press E: " + step;
                }
            }
            if (mo.Inside && mo.EvaLeft < 0.6f) return "Suit is refilling. Speed up time (2/3) or sleep at the bunk, then use the airlock door.";
            if (mo.Inside && mo.SamplesDelivered > m.SamplesAnalyzed) return "Look at your rocks at the lab bench (cyan marker).";
            var lv = Levels.Current;
            if (lv.SeisGoal && m.Kit.Has("SEIS") && !mo.SeisDeployed && !mo.SeisCarried) return "Pick up the quake sensor box next to the airlock (orange marker).";
            if (mo.SeisCarried) return lv.SeisSite != null ? "Carry it to the marked site (orange beacon), step inside the circle and press E. The rover (R) is faster."
                                                           : "Carry it beyond the orange 80 m ring, then press E to deploy. The rover (R) is faster.";
            if (mo.SeisDeployed && !mo.SeisAtSite) return lv.SeisSite != null ? "Quake sensor is not at the marked site. Pick it up and set it down inside the orange circle."
                                                                               : "Quake sensor too close to the base. Pick it up and move it past the orange ring.";
            if (lv.RadGoal && mo.RadReadings == 0 && m.Kit.Has("RAD")) return "Log a radiation reading at the RAD post beside the habitat.";
            if (lv.N <= 2 && mo.SamplesDelivered + mo.SamplesCarried < 2 && m.Kit.Has("SAMP")) return "Optional: collect 2 rocks (cyan beacons), then bring them inside.";
            if (mo.SamplesCarried > 0) return "Bring the rocks into the habitat to deliver them.";
            if (guided)
            {
                // any procedure still waiting on the player
                foreach (var t in tk.All)
                {
                    if (t.State != TaskState.Active) continue;
                    var s = t.Steps.Find(z => !z.Done);
                    if (s != null && s.At != Spot.Auto) return $"Task {t.Id}: {s.Text}.";
                }
            }
            if (mo.Hydration < 30f) return mo.Inside ? "You are thirsty: drink at the water dispenser by the galley (E)." : "You are thirsty: head back inside to drink.";
            if (mo.Hunger < 30f) return mo.Inside ? "You are hungry: eat at the galley (E)." : "You are hungry: head back inside to eat.";
            if (mo.Fatigue > 70f && mo.Inside) return "You are tired: sleep at the bunk (E) or eat at the galley.";
            if (!mo.Inside) return "Head back to the airlock. Inside you can speed up time and manage power (Tab).";
            var broken = mo.Broken.Keys.FirstOrDefault();
            if (broken != null) return $"{mo.Load(broken)?.Name} is broken: open the power panel (Tab) and press REPAIR.";
            var todo = Levels.Current.Objectives.FirstOrDefault(o => !o.Bonus && !o.Done(mo) && !o.Label(mo).StartsWith("Stay safe") && !o.Label(mo).StartsWith("Keep the air"));
            if (todo != null) return "Next goal: " + System.Text.RegularExpressions.Regex.Replace(todo.Label(mo), "<.*?>", "").Trim();
            float ds = MissionModel.DarkStart(mo.Hour);
            if (mo.Hour < ds) return $"It gets dark at hour {ds:0}. Press Tab and switch off things you don't need, so the battery lasts the night.";
            return $"Keep the base running until hour {MissionModel.DurationH:0}. Watch the battery and the air tank.";
        }

        void UpdatePrompt(MissionModel mo)
        {
            string text = null;
            if (m.Rover != null && m.Rover.Driving) text = $"<b>WASD</b> drive (Shift = faster)   <b>R</b> get out";
            else if (!m.SystemsOpen)
            {
                if (m.Focus != null) text = $"<b>E</b>  {m.Focus.Prompt()}";
                if (!mo.Inside && m.NearRover) text = (text != null ? text + "     " : "") + $"<b>R</b>  Get in the rover";
            }
            if (text == null && mo.SeisCarried && !mo.Inside && m.Focus == null && !(m.Rover != null && m.Rover.Driving))
            {
                var p = m.Player.transform.position; p.y = 0; var h = m.World.Hab.position; h.y = 0;
                float d = Vector3.Distance(p, h);
                if (Levels.Current.SeisSite is Vector2 site)
                {
                    float ds = Vector2.Distance(new Vector2(p.x, p.z), site);
                    text = ds <= LevelDef.SeisSiteRadius ? "<b>E</b>  Deploy quake sensor here (inside the marked site)"
                                                         : $"<b>E</b>  Deploy quake sensor here ({ds:0} m from the marked site - follow the orange beacon)";
                }
                else
                    text = d >= MissionModel.SeisMinDistance ? $"<b>E</b>  Deploy quake sensor here ({d:0} m from habitat - quiet site)"
                                                             : $"<b>E</b>  Deploy quake sensor here ({d:0} m - TOO CLOSE, need {MissionModel.SeisMinDistance:0} m)";
            }
            Ui.Show(prompt, text != null);
            if (text != null && GameInput.Touch)
                text = GameInput.Words(text).Replace("<b>E</b>", "<b>USE</b>").Replace("<b>R</b>", "<b>ROVER</b>").Replace("<b>WASD</b> drive (Shift = faster)", "<b>Stick</b> drive (RUN = faster)");
            if (text != null) promptText.text = text.Replace("<b>", "<color=#FF963C><b>").Replace("</b>", "</b></color>");
        }

        void SetStat(string key, float t, string text, Severity s)
        {
            if (!bars.TryGetValue(key, out var b)) return;   // row not on this level / card
            var (fill, val) = b;
            Ui.SetBar(fill, t, s); val.text = text;
        }

        static void Colour(Label l, Severity s) =>
            l.style.color = s == Severity.Critical ? new Color(1f, 0.4f, 0.35f) : s == Severity.Warning ? new Color(1f, 0.82f, 0.3f) : new Color(0.93f, 0.95f, 0.98f);

        void UpdateMarkers()
        {
            var cam = Camera.main;
            bool show = !m.SystemsOpen;
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
                bool next = i == Target;
                bool vis = show && i.Indoor == m.Model.Inside && i.isActiveAndEnabled && (next || i.ShowMarker()) && sp.z > 0;
                v.EnableInClassList("marker-next", next);
                Ui.Show(v, vis);
                if (!vis) continue;
                var panelSize = markers.layout.size;
                v.style.left = sp.x * panelSize.x; v.style.top = (1f - sp.y) * panelSize.y;
                float d = Vector3.Distance(m.ActivePosition, i.transform.position);
                ((Label)v[0]).text = next ? $"NEXT: {i.Label}  {d:0} m" : $"{i.Label}  {d:0} m";
            }
            foreach (var (pos, text, level, v) in gauges)
            {
                var p = pos();
                var sp = cam.WorldToViewportPoint(p);
                bool vis = show && !m.Model.Inside && sp.z > 0 && Vector3.Distance(m.ActivePosition, p) < GaugeRange;
                Ui.Show(v, vis);
                if (!vis) continue;
                var panelSize = markers.layout.size;
                v.style.left = sp.x * panelSize.x; v.style.top = (1f - sp.y) * panelSize.y;
                var l = (Label)v[0]; l.text = text();
                float t = level();
                Colour(l, t < 0.15f ? Severity.Critical : t < 0.4f ? Severity.Warning : Severity.Good);
            }
            foreach (var dead in markerViews.Keys.Where(k => k == null || !Interactable.All.Contains(k)).ToList())
            {
                markerViews[dead].RemoveFromHierarchy(); markerViews.Remove(dead);
            }
        }

        public void Toast(string text, Severity s)
        {
            if (alerts == null) return;
            text = GameInput.Words(text);
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
            Ui.Text(head, "BASE CONTROLS", "h1"); Ui.Box(head, "grow");
            gatewayBtn = Ui.Button(head, "Call for supplies", () => { Ui.Show(gateway, !Ui.Visible(gateway)); gateway.BringToFront(); });
            Ui.Button(head, "Close (Tab)", m.ToggleSystems);
            sysSummary = Ui.Text(p, "", "text");
            sysShadow = Ui.Text(p, "", "text", "accent");
            Ui.Text(p, "Every machine uses power. If there is not enough, the least important ones turn off first.", "small", "muted");

            var hdr = Ui.Box(p, "table-row");
            Ui.Text(hdr, "", "dot"); Ui.Text(hdr, "Machine", "cell-name", "muted"); Ui.Text(hdr, "Uses", "cell", "muted"); Ui.Text(hdr, "What it is doing", "cell-wide", "muted"); Ui.Text(hdr, "Switch", "cell", "muted");
            foreach (var l in m.Model.Loads)
            {
                var r = Ui.Box(p, "table-row");
                var dot = Ui.Dot(r, Severity.Good);
                Ui.Text(r, l.Name, "cell-name");
                Ui.Text(r, l.Pw >= 0.8f ? "a lot" : l.Pw >= 0.3f ? "some" : "a little", "cell");
                var st = Ui.Text(r, "", "cell-wide");
                loadStates.Add((st, l)); loadDots.Add((dot, l));
                var rl = l;
                var rep = Ui.Button(r, "FIX", () => { var msg = m.Model.StartRepair(rl.Id); if (msg != null) Toast(msg, Severity.Info); }, "btn-small", "btn-primary");
                rep.style.width = 100; repairButtons.Add((rep, l)); Ui.Show(rep, false);
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
                var help = Ui.Text(p, MachineHelp(l.Id), "tiny", "muted"); help.style.marginLeft = 34; help.style.marginTop = -4; help.style.marginBottom = 4;
            }
            Ui.Text(p, "Oxygen maker: OFF, AUTO (turns on by itself when there is spare power) or ON." + (Levels.Current.Faults.Length > 0 ? $" Broken machine? Press FIX. With spare parts it is fixed right away, otherwise it takes {MissionModel.RepairHours:0} hours." : ""), "small", "muted").style.marginTop = 8;
            Ui.Show(systems, false);
            BuildGatewayPanel();
        }

        static string MachineHelp(string id) => id switch
        {
            "HAB" => "Keeps the base warm and bright.",
            "SCR" => "Cleans the bad air (CO2) you breathe out.",
            "OGS" => Levels.Current.Mars ? "Makes fresh air out of Mars air." : "Makes fresh air out of water.",
            "SUI" => "Fills your spacesuit with air.",
            "COM" => "Lets you talk to Earth and call for supplies.",
            "RAD" => "Measures space radiation.",
            "SEIS" => "Listens for quakes.",
            "GAR" => "Charges the rover when it is parked.",
            _ => "",
        };

        // ================================================================== Gateway resupply (from the systems panel)

        VisualElement gateway; Button gatewayBtn, gatewaySend; Label gatewayInfo, gatewayMsg;
        readonly List<(Toggle t, Label stock, SupplyItem item)> gatewayRows = new List<(Toggle, Label, SupplyItem)>();

        void BuildGatewayPanel()
        {
            gateway = Ui.Box(root, "screen", "center");
            var p = Ui.Panel(gateway); p.style.width = 820;
            var head = Ui.Box(p, "row");
            Ui.Text(head, "CALL FOR SUPPLIES", "h1"); Ui.Box(head, "grow");
            Ui.Button(head, "Close", () => Ui.Show(gateway, false));
            Ui.Text(p, $"Running low? A space station can send a drone with supplies. It lands {Resupply.DelayH:0} hours after you call " +
                       $"and carries up to {Resupply.DroneKg:0} kg. You can only call {Resupply.MaxCalls} times, and each call lowers your score a little.", "text-small");
            foreach (var it in Resupply.Items)
            {
                var r = Ui.Box(p, "row");
                var t = Ui.Add(r, new Toggle { label = $"{it.Name}  ({it.Kg:0} kg)" }, "extra", "grow");
                t.RegisterValueChangedCallback(_ => RefreshGateway());
                var st = Ui.Text(r, "", "small"); st.style.width = 200;
                gatewayRows.Add((t, st, it));
            }
            gatewayInfo = Ui.Text(p, "", "text-small"); gatewayInfo.style.marginTop = 8;
            gatewayMsg = Ui.Text(p, "", "small", "accent");
            var row = Ui.Box(p, "row"); row.style.justifyContent = Justify.FlexEnd; row.style.marginTop = 8;
            gatewaySend = Ui.Button(row, "Send the drone", () =>
            {
                var cart = gatewayRows.Where(g => g.t.value).Select(g => g.item).ToList();
                var why = m.Model.Gateway.Request(m.Model, cart);
                gatewayMsg.text = why ?? $"The drone is on its way! It lands at hour {m.Model.Hour + Resupply.DelayH:0}.";
                if (why == null) foreach (var g in gatewayRows) g.t.SetValueWithoutNotify(false);
                RefreshGateway();
            }, "btn-primary");
            Ui.Show(gateway, false);
        }

        void RefreshGateway()
        {
            var mo = m.Model; var gw = mo.Gateway;
            float kg = gatewayRows.Where(g => g.t.value).Sum(g => g.item.Kg);
            foreach (var (t, stock, item) in gatewayRows)
            {
                var blocked = item.Blocked?.Invoke(mo);
                stock.text = blocked != null ? blocked : (item.Low(mo) ? "LOW: " : "") + item.Stock(mo);
                Colour(stock, item.Low(mo) ? Severity.Warning : Severity.Info);
                t.SetEnabled(blocked == null || t.value);
            }
            string pending = gw.Pending.Count == 0 ? "none" : string.Join("; ", gw.Pending.Select(p => $"hour {p.due:0}: {string.Join(", ", p.items.Select(i => i.Name))}"));
            gatewayInfo.text = $"Drone load {kg:0} of {Resupply.DroneKg:0} kg     Calls left: {Resupply.MaxCalls - gw.Calls}\nOn the way: {pending}";
            gatewaySend.SetEnabled(mo.Net.Connected && kg > 0f && kg <= Resupply.DroneKg && gw.Calls < Resupply.MaxCalls);
            if (!mo.Net.Connected) gatewayInfo.text = "The radio is not working. Fix the antenna at the radio desk first.\n" + gatewayInfo.text;
        }

        void UpdateSystems()
        {
            bool open = m.SystemsOpen && !m.Model.Ended;
            Ui.Show(systems, open);
            if (!open) { Ui.Show(gateway, false); return; }
            if (Ui.Visible(gateway)) RefreshGateway();
            gatewayBtn.text = m.Model.Gateway.Pending.Count > 0 ? "Supply drone on its way" : "Call for supplies";
            var mo = m.Model;
            bool filling = mo.Supply >= mo.Demand;
            sysSummary.text = $"Battery {mo.BatteryPct:0%}  -  " + (mo.Sun < 0.05f ? "No sun right now: the base runs on the battery." : filling ? "The solar panels make more than the base uses: the battery is filling up." : "The base uses more than the solar panels make: the battery is going down.")
                            + (mo.Dust > 0.1f ? $"  Dust on the panels: {mo.Dust:0%}." : "");
            sysSummary.style.color = mo.Sun >= 0.05f && filling ? new Color(0.5f, 0.9f, 0.6f) : new Color(1f, 0.8f, 0.4f);
            float dStart = MissionModel.DarkStart(mo.Hour), toShadow = dStart - mo.Hour;
            bool enough = mo.Battery >= mo.ShadowEnergyNeeded;
            sysShadow.text = mo.InShadow ? $"It is dark: {Mathf.Ceil(dStart + MissionModel.ShadowLength - mo.Hour):0} hours left. Only the battery works now."
                : toShadow > 0 && dStart < MissionModel.DurationH
                ? $"It gets dark in {Mathf.Ceil(toShadow):0} hours, for {MissionModel.ShadowLength:0} hours. " + (enough ? "The battery has enough for now." : "The battery is NOT full enough yet. Switch off what you don't need!")
                : "";
            foreach (var (b, l) in repairButtons) Ui.Show(b, mo.IsBroken(l.Id));
            foreach (var (lbl, l) in loadStates) lbl.text = l.Powered ? "Running" : (string.IsNullOrEmpty(l.Why) ? "Off" : l.Why);
            foreach (var (dot, l) in loadDots) Ui.SetDot(dot, l.Powered ? Severity.Good : (l.Why != null && (l.Why.StartsWith("NO POWER") || l.Why.StartsWith("BROKEN"))) ? Severity.Critical : Severity.Warning);
            foreach (var (b, l) in loadButtons) { b.text = l.Enabled ? "ON" : "OFF"; b.EnableInClassList("btn-on", l.Enabled); b.EnableInClassList("btn-off", !l.Enabled); }
            if (ogsBtn != null) { ogsBtn.text = mo.Ogs.ToString().ToUpper(); ogsBtn.EnableInClassList("btn-on", mo.Ogs != OgsMode.Off); ogsBtn.EnableInClassList("btn-off", mo.Ogs == OgsMode.Off); }
        }

        // ================================================================== Inventory panel (F)

        void BuildInventoryPanel()
        {
            inventory = Ui.Box(root, "screen", "center");
            var p = Ui.Panel(inventory); p.style.width = 760;
            var head = Ui.Box(p, "row");
            Ui.Text(head, "INVENTORY", "h1"); Ui.Box(head, "grow");
            Ui.Button(head, "Close (F)", m.ToggleInventory);

            Ui.Text(p, "CARRYING", "section");
            invSeis = Ui.Text(p, "", "text");
            invSamples = Ui.Text(p, "", "text");
            var grid = Ui.Box(p, "row"); grid.style.flexWrap = Wrap.Wrap; grid.style.width = 5 * 132;
            for (int i = 0; i < Inventory.BagSlots; i++)
            {
                var c = Ui.Box(grid, "col"); c.style.width = 124; c.style.height = 62; c.style.marginTop = c.style.marginBottom = c.style.marginLeft = c.style.marginRight = 4;
                c.style.borderTopWidth = c.style.borderBottomWidth = c.style.borderLeftWidth = c.style.borderRightWidth = 1;
                c.style.borderTopColor = c.style.borderBottomColor = c.style.borderLeftColor = c.style.borderRightColor = new Color(0.47f, 0.63f, 0.86f, 0.5f);
                c.style.backgroundColor = new Color(0.12f, 0.2f, 0.35f, 0.6f); c.style.justifyContent = Justify.Center; c.style.alignItems = Align.Center;
                invSlots.Add((c, Ui.Text(c, "", "small"), Ui.Text(c, "", "small", "cyan")));
            }
            invWeightRow = Ui.Box(p, "col"); invWeightRow.style.marginTop = 6;
            invWeight = Ui.Text(invWeightRow, "", "small");
            invWeightFill = Ui.Bar(invWeightRow, "bar-big").fill;
            var tr = Ui.Box(p, "table-row");
            Ui.Text(tr, "Suit torch (standard EVA gear)", "cell-name");
            invTorchState = Ui.Text(tr, "", "cell");
            invTorchBtn = Ui.Button(tr, "", () => m.Torch.Toggle(), "btn-small"); invTorchBtn.style.width = 130;

            Ui.Text(p, "HABITAT STORES", "section");
            invStored = Ui.Text(p, "", "text");

            Ui.Text(p, "MISSION CARGO", "section");
            var list = Ui.Add(p, new ScrollView()); list.style.maxHeight = 320;
            foreach (var def in Catalog.Mission1)
            {
                int n = m.Kit.Count(def.Id); if (n == 0) continue;
                var r = Ui.Box(list, "table-row");
                Ui.Text(r, def.Name, "cell-name");
                Ui.Text(r, "x" + n, "cell");
                Ui.Text(r, def.Purpose, "small", "muted").style.flexShrink = 1;
            }
            Ui.Show(inventory, false);
        }

        bool watchOn;
        public void ToggleWatch() => watchOn = !watchOn;

        void UpdateInventory()
        {
            bool open = m.InventoryOpen && !m.Model.Ended;
            Ui.Show(inventory, open);
            if (!open) return;
            var mo = m.Model;
            invSeis.text = !m.Kit.Has("SEIS") ? "No seismometer packed"
                : mo.SeisCarried ? "Seismometer: carried in both hands"
                : mo.SeisDeployed ? "Seismometer: deployed" : "Seismometer: not picked up yet";
            var inv = mo.Inv;
            invSamples.text = inv.HasBag ? $"EVA bag: {inv.Slots.Count} / {Inventory.BagSlots} slots used"
                                         : $"Pockets: {inv.Slots.Count} / {Inventory.PocketSlots} slots used (take the EVA bag by the cargo bags for 20)";
            for (int i = 0; i < invSlots.Count; i++)
            {
                var (box, name, count) = invSlots[i];
                Ui.Show(box, i < inv.Capacity);
                var slot = i < inv.Slots.Count ? inv.Slots[i] : null;
                name.text = slot?.Item.Name ?? "";
                count.text = slot == null ? "" : $"{slot.Count} / {Inventory.PerSlot}";
            }
            Ui.Show(invWeightRow, inv.HasBag);
            invWeight.text = $"Weight {inv.Kg:0.#} / {Inventory.MaxKg:0} kg";
            float wt = inv.Kg / Inventory.MaxKg;
            Ui.SetBar(invWeightFill, wt, wt >= 1f ? Severity.Critical : wt > 0.8f ? Severity.Warning : Severity.Good);
            invStored.text = $"Samples stored: {mo.SamplesDelivered} / {mo.SampleCapacity}     Spare parts kits: {mo.Spares}";
            invTorchState.text = m.Torch.On ? "ON" : "OFF";
            invTorchBtn.text = m.Torch.On ? "Turn off (T)" : "Turn on (T)";
        }

        // ================================================================== Plan board (habitat wall, or J)

        VisualElement planBoard; Label planTasks, planWarn; float planNext;
        readonly List<(DropdownField d, Label status)> planRows = new List<(DropdownField, Label)>();
        List<Activity> planActs;

        void BuildPlanBoard()
        {
            planBoard = Ui.Box(root, "screen", "center", "dim");
            var p = Ui.Panel(planBoard); p.style.width = 1500; p.style.maxHeight = Length.Percent(92);
            var head = Ui.Box(p, "row");
            Ui.Text(head, "MY PLAN", "h1"); Ui.Box(head, "grow");
            Ui.Button(head, "Close (J / Esc)", m.TogglePlan);
            var cols = Ui.Box(p, "row"); cols.style.alignItems = Align.FlexStart;

            // left: the 72 h schedule; blocks that have not started can still be changed
            var left = Ui.Box(cols, "col"); left.style.width = 820; left.style.marginRight = 24;
            Ui.Text(left, "WHEN AND WHAT  (you can still change the later ones)", "section");
            planActs = MissionPlan.Available(m.Kit);
            var names = planActs.Select(MissionPlan.Name).ToList();
            for (int i = 0; i < MissionPlan.Blocks; i++)
            {
                int b = i;
                var r = Ui.Box(left, "row"); r.style.marginBottom = 2;
                Ui.Text(r, MissionPlan.Hours(b), "small").style.width = 70;
                var d = Ui.Add(r, new DropdownField(names, planActs.IndexOf(m.Plan.At[b])));
                d.style.width = 420; d.style.fontSize = 15;
                d.RegisterValueChangedCallback(_ => { m.Plan.At[b] = planActs[d.index]; planNext = 0f; });
                var st = Ui.Text(r, "", "small"); st.style.width = 100; st.style.marginLeft = 8;
                Ui.Text(r, MissionPlan.Events(b), "small", "cyan").style.flexShrink = 1;
                planRows.Add((d, st));
            }
            planWarn = Ui.Text(left, "", "small"); planWarn.enableRichText = true; planWarn.style.marginTop = 6;

            // right: every task and step, ticked off, with where it is done
            var right = Ui.Box(cols, "col", "grow");
            Ui.Text(right, "STEPS", "section");
            var scroll = Ui.Add(right, new ScrollView()); scroll.style.maxHeight = 720;
            planTasks = Ui.Text(scroll, "", "small"); planTasks.enableRichText = true;
            Ui.Show(planBoard, false);
        }

        void UpdatePlanBoard()
        {
            bool open = m.PlanOpen && !m.Model.Ended;
            Ui.Show(planBoard, open);
            if (!open || Time.unscaledTime < planNext) return;
            planNext = Time.unscaledTime + 0.5f;
            var mo = m.Model; var plan = m.Plan;
            int now = MissionPlan.BlockAt(mo.Hour);
            for (int b = 0; b < planRows.Count; b++)
            {
                var (d, st) = planRows[b];
                d.SetEnabled(b > now);
                d.SetValueWithoutNotify(MissionPlan.Name(plan.At[b]));
                bool done = plan.Done(b, mo);
                var (txt, col) = b > now ? ("later", new Color(0.6f, 0.66f, 0.75f))
                    : done ? ("done", new Color(0.44f, 0.86f, 0.55f))
                    : b == now ? ("NOW", new Color(1f, 0.7f, 0.43f))
                    : plan.At[b] == Activity.Sleep ? ("missed", new Color(0.6f, 0.66f, 0.75f)) : ("overdue", new Color(1f, 0.48f, 0.48f));
                st.text = txt; st.style.color = col;
            }
            var w = plan.Warnings(m.Kit);
            planWarn.text = w.Count == 0 ? "<color=#6fdc8c>Your plan looks great!</color>" : string.Join("\n", w.Select(x => "<color=#ffb46e>! </color>" + x));

            var sb = new System.Text.StringBuilder();
            var t = mo.Tasks;
            foreach (var k in t.All)
            {
                string state = k.State == TaskState.Passed ? "<color=#6fdc8c>DONE</color>" : k.State == TaskState.Failed ? "<color=#ff7a7a>FAILED</color>"
                             : k.State == TaskState.Locked ? "<color=#8aa0aa>not open yet</color>" : "<color=#ffb46e>open</color>";
                string when = k.From > 0f ? $"  <color=#8aa0aa>h{k.From:0}-{Mathf.Min(k.To, MissionModel.DurationH):0}</color>" : "";
                sb.Append($"<b>Task {k.Id}: {k.Title}</b>  {state}{when}\n<color=#8aa0aa>{k.Location}</color>\n");
                foreach (var s in k.Steps)
                {
                    string where = Station(s.At);
                    sb.Append(s.Done ? "<color=#6fdc8c>[x]</color> " : "[ ] ").Append(s.Text);
                    sb.Append(where != null ? $"  <color=#8aa0aa>@ {where}</color>\n" : "  <color=#8aa0aa>(automatic)</color>\n");
                }
                sb.Append('\n');
            }
            planTasks.text = sb.ToString().TrimEnd();
        }

        // ================================================================== Stores log board (habitat wall)

        VisualElement stores; float storesNext;
        readonly List<(Label stock, Label note, Func<MissionModel, (string stock, string note, Severity s)> read)> storeRows
            = new List<(Label, Label, Func<MissionModel, (string, string, Severity)>)>();

        void BuildStoresPanel()
        {
            stores = Ui.Box(root, "screen", "center");
            var p = Ui.Panel(stores); p.style.width = 900;
            var head = Ui.Box(p, "row");
            Ui.Text(head, "STORES LOG", "h1"); Ui.Box(head, "grow");
            Ui.Button(head, "Close (Esc)", m.ToggleStores);
            Ui.Text(p, "Everything the outpost has in stock right now. Running low? Call Lunar Gateway from the systems panel (Tab) once comms work.", "small", "muted");
            var hdr = Ui.Box(p, "table-row");
            Ui.Text(hdr, "Item", "cell-name", "muted"); Ui.Text(hdr, "In stock", "cell-pw", "muted"); Ui.Text(hdr, "Notes", "cell-wide");

            Severity Lvl(bool crit, bool warn) => crit ? Severity.Critical : warn ? Severity.Warning : Severity.Good;
            void Row(string name, Func<MissionModel, (string, string, Severity)> read)
            {
                var r = Ui.Box(p, "table-row");
                Ui.Text(r, name, "cell-name");
                storeRows.Add((Ui.Text(r, "", "cell-pw"), Ui.Text(r, "", "cell-wide"), read));
            }
            Row("Water", x =>
            {
                float perH = 0.125f + (x.Load("OGS")?.Powered == true ? 0.11f : 0f);
                return ($"{x.Water:0.0} / {x.WaterCap:0} L", $"uses ~{perH * 24f:0.0} L/day (crew + oxygen maker): {x.Water / perH / 24f:0.0} days left", Lvl(x.Water < 5f, x.Water < 12f));
            });
            Row("Food", x => ($"{x.FoodDaysLeft:0.0} days", $"last meal {(x.LastMealHour < 0f ? "never" : $"h{x.LastMealHour:0}")}, {x.MealsEaten} eaten", Lvl(x.FoodDaysLeft < 0.5f, x.FoodDaysLeft < 1f)));
            Row("Cabin oxygen", x => ($"{x.CabinO2:0.0} / {x.CabinO2Cap:0} kg", $"made {x.O2Produced:0.0} kg, used {x.O2Consumed:0.0} kg, vented {x.O2Vented:0.0} kg", Lvl(x.CabinO2 < 1f, x.CabinO2 < 2f)));
            Row("CO2 scrubber cartridges", x => !x.Kit.Has("SCR") ? ("none", "no CO2 scrubber packed", Severity.Critical)
                : ($"{x.CartridgeKg:0.0} kg left", "about 1 kg of CO2 per crew-day", Lvl(x.CartridgeKg < 0.5f, x.CartridgeKg < 1f)));
            Row("Battery", x => ($"{x.BatteryPct:0%}", x.Supply >= x.Demand ? "filling up" : "going down", Lvl(x.BatteryPct < 0.15f, x.BatteryPct < 0.35f)));
            Row("Spare parts kits", x => ($"{x.Spares}", $"suit integrity {x.SuitIntegrity:0}% (one kit = +50%)", Lvl(false, x.Spares == 0 && x.SuitIntegrity < 60f)));
            Row("Rock samples", x => ($"{x.SamplesDelivered} / {x.SampleCapacity}", $"stored in the habitat, {m.SamplesAnalyzed} analysed", Severity.Good));
            Row("Rover battery", x => x.RoverCap <= 0f ? ("no rover", "", Severity.Info) : ($"{x.RoverBattery / x.RoverCap:0%}", "charges at the garage", Lvl(x.RoverBattery < 0.5f, x.RoverBattery < 1.5f)));
            Row("Carried by you", x => ($"{x.Inv.Slots.Count} / {x.Inv.Capacity} slots", $"{x.Inv.Kg:0.#} / {Inventory.MaxKg:0} kg{(x.Inv.HasBag ? ", EVA bag" : ", pockets only")}", Severity.Info));
            Row("Gateway resupply", x => ($"{Resupply.MaxCalls - x.Gateway.Calls} calls left",
                x.Gateway.Pending.Count == 0 ? (x.Net.Connected ? "no drone on the way" : "comms down: fix the antenna first")
                                             : "drone arriving h" + string.Join(", h", x.Gateway.Pending.Select(d => d.due.ToString("0"))), x.Net.Connected ? Severity.Good : Severity.Warning));
            Ui.Show(stores, false);
        }

        void UpdateStores()
        {
            bool open = m.StoresOpen && !m.Model.Ended;
            Ui.Show(stores, open);
            if (!open || Time.unscaledTime < storesNext) return;
            storesNext = Time.unscaledTime + 0.25f;
            foreach (var (stock, note, read) in storeRows)
            {
                var (st, nt, sev) = read(m.Model);
                stock.text = st; note.text = nt; Colour(stock, sev == Severity.Good ? Severity.Info : sev);
            }
        }

        // ================================================================== Tasks checklist panel (HUD overlay)

        VisualElement tasksPanel; Label tasksLbl; float tasksNext;

        void BuildTasksPanel()
        {
            tasksPanel = Ui.Panel(leftCol, "hud-card"); tasksPanel.pickingMode = PickingMode.Ignore;
            tasksLbl = Ui.Text(tasksPanel, "", "small");
            tasksLbl.enableRichText = true; tasksLbl.style.whiteSpace = WhiteSpace.Normal;
        }

        void UpdateTasks()
        {
            var mo = m.Model; var t = mo.Tasks; var plan = m.Plan;
            plan.Track(mo);
            Ui.Show(tasksPanel, !mo.Ended);
            if (Time.unscaledTime < tasksNext) return;
            tasksNext = Time.unscaledTime + 0.25f;

            // "My plan": the current block and the next two, then the steps for what the player chose to do now
            var sb = new System.Text.StringBuilder();
            int now = MissionPlan.BlockAt(mo.Hour);
            sb.Append($"<color=#8aa0aa>MY PLAN   hour {mo.Hour:0} / {MissionModel.DurationH:0}</color>\n");
            int missed = Enumerable.Range(0, now).Where(b => !plan.Done(b, mo) && plan.At[b] != Activity.Sleep).DefaultIfEmpty(-1).First();
            if (missed >= 0) sb.Append($"<color=#ff7a7a>Still to do: {MissionPlan.Hours(missed)} {MissionPlan.Name(plan.At[missed])}</color>\n");
            for (int b = now; b < Mathf.Min(now + 2, MissionPlan.Blocks); b++)
            {
                bool done = plan.Done(b, mo);
                string line = $"{(done ? "[x]" : b == now ? ">" : "[ ]")} {MissionPlan.Hours(b)}  {MissionPlan.Name(plan.At[b])}";
                sb.Append(done ? $"<color=#6fdc8c>{line}</color>" : b == now ? $"<color=#ffb46e><b>{line}</b></color>" : $"<color=#8aa0aa>{line}</color>");
                sb.Append('\n');
            }
            sb.Append('\n');

            // what to do now, and WHERE: every line names the station, its distance, and the route if it is on the other side of the airlock
            var act = plan.At[now];
            Target = null;
            bool evaOk = t.Get("C").State == TaskState.Passed || !t.Enabled || t.AutoPass;
            if (act == Activity.Sleep) Do(sb, $"Sleep (E at the bunk). Slept {plan.Slept(now):0.0} / {MissionPlan.SleepNeededH:0} h", "Bunk");
            else if (act == Activity.Chores) Do(sb, "Eat and drink (galley), then check power and air (Tab)", "Galley");
            else if (act == Activity.Radiation && evaOk && mo.RadReadings == 0) Do(sb, "Log a radiation reading", "Radiation Monitor");
            else if (act == Activity.Samples && evaOk && !mo.S1Samples)
                Do(sb, mo.SamplesCarried > 0 && mo.SamplesCarried + mo.SamplesDelivered >= 2 ? "Bring the samples inside" : "Collect a rock sample",
                   mo.SamplesCarried + mo.SamplesDelivered >= 2 ? "Airlock" : NearestSample());
            if (t.Enabled && !t.AutoPass)
            {
                var ids = MissionPlan.TaskIds(act);
                var cur = ids.Select(c => t.Get(c.ToString())).FirstOrDefault(k => k.State != TaskState.Passed);
                if (cur != null) AppendTask(sb, cur);
                // hazard procedures interrupt any plan
                foreach (var k in new[] { t.Get("E"), t.Get("F") })
                    if (k.State == TaskState.Active && k != cur) { sb.Append("<color=#ff7a7a><b>ALERT - not in your plan:</b></color>\n"); Target = null; AppendTask(sb, k); }
            }
            tasksLbl.text = sb.ToString().TrimEnd();
        }

        /// <summary>The station the plan panel points at; its world marker is highlighted as NEXT.</summary>
        public Interactable Target { get; private set; }

        Interactable Find(string label)
        {
            Interactable best = null; float bd = float.MaxValue;
            foreach (var i in Interactable.All)
            {
                if (i.Label != label || !i.isActiveAndEnabled || !i.Available()) continue;
                float d = Vector3.Distance(m.ActivePosition, i.transform.position);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        string NearestSample()
        {
            var s = Interactable.All.Where(i => i.Label != null && i.Label.StartsWith("Sample ") && i.Label != "Sample lab" && i.isActiveAndEnabled && i.Available())
                                    .OrderBy(i => Vector3.Distance(m.ActivePosition, i.transform.position)).FirstOrDefault();
            return s?.Label;
        }

        /// <summary>"WHERE" line for a station label: distance when it is on your side of the airlock, else the way there.</summary>
        string Where(string label)
        {
            if (label == null) return "<color=#8aa0aa>happens by itself</color>";
            var it = Find(label);
            if (it == null) return label;
            bool inside = m.Model.Inside;
            if (Target == null) Target = it.Indoor == inside ? it : Find(inside ? "Airlock door" : "Airlock");   // other side: point at the way through
            if (it.Indoor && !inside) return $"{label}  <color=#ffb46e>inside the habitat: go in through the airlock</color>";
            if (!it.Indoor && inside) return $"{label}  <color=#ffb46e>outside: suit up, then the airlock door</color>";
            return $"{label}  <color=#6fdc8c>{Vector3.Distance(m.ActivePosition, it.transform.position):0} m</color>  <color=#8aa0aa>(look for the NEXT marker)</color>";
        }

        void Do(System.Text.StringBuilder sb, string what, string where)
        {
            sb.Append($"<b>NOW:</b> {what}\n<color=#8aa0aa>WHERE:</color> {Where(where)}\n");
        }

        static string Station(Spot s) => s switch
        {
            Spot.Systems => "Power & life support", Spot.Comms => "Mission Control link", Spot.SuitDock => "Suit dock",
            Spot.Door => "Airlock door", Spot.Airlock => "Airlock door", Spot.Shelter => "Radiation shelter", Spot.Seis => "Seismometer",
            _ => null,
        };

        void AppendTask(System.Text.StringBuilder sb, MissionTask k)
        {
            sb.Append($"<b>Task {k.Id}: {k.Title}</b>  <color=#8aa0aa>{k.Steps.Count(s => s.Done)}/{k.Steps.Count} done</color>\n");
            if (k.State == TaskState.Locked)
            {
                string first = k.Id == "D" ? "C (Prepare for EVA)" : "A (Mission readiness checklist)";
                sb.Append($"<color=#8aa0aa>Not open yet: finish Task {first} first.</color>\n"); return;
            }
            var steps = k.Steps.Where(s => !s.Done).ToList();
            if (steps.Count == 0) return;
            var st = steps[0];
            string where = Station(st.At);
            if (st.Id == "D1" || st.Id == "D2")   // carry / deploy happen by walking, not at a station
                where = m.Model.SeisCarried ? null : "Seismometer";
            sb.Append($"<b>NOW:</b> {st.Text}\n");
            sb.Append($"<color=#8aa0aa>WHERE:</color> {((st.Id == "D1" || st.Id == "D2") && m.Model.SeisCarried ? "walk past the orange 80 m ring, then press E to set it down" : Where(where))}");
            if (st.At == Spot.Systems) sb.Append("  <color=#8aa0aa>or press Tab anywhere</color>");
            sb.Append('\n');
            if (steps.Count > 1) sb.Append($"<color=#8aa0aa>Then: {steps[1].Text}{(steps.Count > 2 ? $"  (+{steps.Count - 2} more)" : "")}</color>\n");
        }

        // ================================================================== Habitat panel

        // ================================================================== Pause

        void BuildPauseMenu()
        {
            pause = Ui.Box(root, "screen", "center", "dim");
            var p = Ui.Panel(pause); p.style.width = 520; p.style.alignItems = Align.Stretch;
            Ui.Text(p, "PAUSED", "h1");
            Ui.Button(p, "Resume", () => m.SetPaused(false), "btn-primary");
            Ui.Button(p, "Controls", ToggleControls);
            Ui.Button(p, "Lunar Survival Intelligence", () => LunarIntelligenceUi.Show(root));
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
            Ui.Show(hud, false); Ui.Show(systems, false);
            var scr = Ui.Box(root, "screen", "center", "dim"); scr.pickingMode = PickingMode.Position;
            var p = Ui.Panel(scr); p.style.width = 1600; p.style.height = 960;
            var head = Ui.Box(p, "row", "noshrink");
            Ui.Text(head, r.Title, "h1", mo.Failed ? "bad" : mo.AllPrimaryDone ? "good" : "accent");
            Ui.Box(head, "grow");
            Ui.Text(head, $"SCORE {r.Score}  " + new string('★', r.Stars) + new string('☆', 3 - r.Stars) + (r.Stars == 0 ? "  (50 to pass)" : ""), "h1", "accent");
            Ui.Text(p, r.Summary, "text", "noshrink");

            var cols = Ui.Box(p, "row", "grow"); cols.style.alignItems = Align.Stretch;
            var c1 = Ui.Add(cols, new ScrollView(), "grow"); c1.style.width = Length.Percent(34); c1.style.paddingRight = 16;
            Ui.Text(c1, "HOW YOU DID", "h2");
            foreach (var (n, pts, max) in r.Breakdown)
            {
                var row = Ui.Box(c1, "row"); Ui.Text(row, n, "small", "grow"); Ui.Text(row, $"{pts:0} of {max:0}", "small");
                Ui.SetBar(Ui.Bar(c1).fill, pts / max, pts / max > 0.66f ? Severity.Good : pts / max > 0.33f ? Severity.Warning : Severity.Critical);
            }
            Ui.Text(c1, "GOALS", "h2");
            foreach (var o in Levels.Current.Objectives) Obj(c1, o.Done(mo), o.Label(mo));
            Obj(c1, mo.S2Oxygen, "Bonus: air tank 3 kg or more at the end");
            Obj(c1, mo.S3SuitOk, "Bonus: suit never badly damaged");
            if (mo.QuakeRecorded) Ui.Text(c1, "Bonus: your sensor heard a moonquake!", "text", "good");

            var c2 = Ui.Add(cols, new ScrollView(), "grow"); c2.style.width = Length.Percent(33); c2.style.paddingRight = 16;
            Ui.Text(c2, mo.Failed ? "WHAT WENT WRONG" : "BIG MOMENTS", "h2");
            if (mo.CauseChain.Count == 0) Ui.Text(c2, "Nothing went wrong. Super mission!", "text", "good");
            for (int i = 0; i < mo.CauseChain.Count; i++) Ui.Text(c2, (i == 0 ? "" : "-> ") + mo.CauseChain[i], "text", mo.Failed && i == mo.CauseChain.Count - 1 ? "bad" : "");
            Ui.Text(c2, "TRY THIS NEXT TIME", "h2");
            foreach (var s in r.Suggestions) Ui.Text(c2, "- " + s, "text");
            Ui.Text(c2, "MISSION DIARY", "h2");
            foreach (var e in mo.Events.Where(e => e.Sev != Severity.Info).Take(40))
            {
                var row = Ui.Box(c2, "row"); Ui.Dot(row, e.Sev); Ui.Text(row, e.ToString(), "small").style.flexShrink = 1;
            }

            var c3 = Ui.Add(cols, new ScrollView(), "grow"); c3.style.width = Length.Percent(33);
            Ui.Text(c3, "SCIENCE YOU COLLECTED", "h2");
            if (mo.Data.Count == 0) Ui.Text(c3, "No science this time.", "text", "muted");
            foreach (var d in mo.Data)
            {
                Ui.Text(c3, $"Hour {d.Hour:0}:  {d.What}", "small").style.marginBottom = 4;
            }
            Ui.Text(c3, "REAL LIFE vs THE GAME", "h2");
            foreach (var (topic, real, game) in MissionReport.RealVsGame)
            {
                Ui.Text(c3, topic, "h3");
                Ui.Text(c3, "Real life: " + real, "small");
                Ui.Text(c3, "In the game: " + game, "small", "muted").style.marginBottom = 6;
            }

            var btns = Ui.Box(p, "row", "noshrink"); btns.style.justifyContent = Justify.FlexEnd; btns.style.marginTop = 10;
            Ui.Button(btns, "Main menu", m.ToMenu);
            Ui.Button(btns, "Try again", m.Retry, "btn-primary", "btn-big");
        }

        static void Obj(VisualElement parent, bool done, string text)
        {
            var r = Ui.Box(parent, "row"); Ui.Dot(r, done ? Severity.Good : Severity.Critical); Ui.Text(r, text, "small");
        }
    }
}
