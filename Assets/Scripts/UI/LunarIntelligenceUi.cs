using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mohakash
{
    /// <summary>
    /// Fullscreen research & survival intelligence dashboard matching lunar-places-information.html.
    /// Provides real NASA exploration data: lunar south pole locations, environmental facts,
    /// solar illumination models, scientific APIs, and game conversion tools.
    /// </summary>
    public static class LunarIntelligenceUi
    {
        public struct LunarLocation
        {
            public string Id, Name, Coords, Terrain, History, Importance, Resources, Hazards, Objectives;
            public string IceInterpretation, Sunlight, Comms;
        }

        public static readonly LunarLocation[] Locations = new[]
        {
            new LunarLocation
            {
                Id = "shackleton",
                Name = "Shackleton Crater Rim",
                Coords = "89.9°S, 0.0°E (Lunar South Pole)",
                Terrain = "21 km-wide, 4.2 km-deep impact crater directly on the lunar south pole. Steep interior rim walls (up to 30°), interior permanently shadowed (PSR).",
                History = "Orbited and mapped by Clementine, Lunar Prospector, SMART-1, Kaguya, Chandrayaan-1, and NASA LRO (LOLA, Diviner, LROC). Candidate Artemis landing zone.",
                Importance = "Prime candidate for long-duration polar outposts due to peaks of near-permanent sunlight on the rim coupled with cryogenic cold traps inside.",
                Resources = "Water ice and volatile organic compounds trapped in PSR cold traps (temperatures ~40 K / -233°C). Highly illuminated rim ridges for solar power.",
                Hazards = "Extreme thermal gradients (390 K illuminated vs 40 K shadow), ultra-low solar elevation angles producing long razor-sharp shadows, steep slopes.",
                Objectives = "Establish south pole rim outpost, place seismic detector >80m from habitat noise, deploy solar arrays on high-illumination points, prospect PSR ice.",
                IceInterpretation = "Orbital reflectance and radar data confirm surface and subsurface hydrogen deposits consistent with water ice.",
                Sunlight = "86% to 92% annual illumination on elevated rim peaks; periodic grazing shadows during lunar winter.",
                Comms = "Intermittent to direct Earth line-of-sight depending on lunar libration (~14-day visibility cycles); relay satellite recommended."
            },
            new LunarLocation
            {
                Id = "malapert",
                Name = "Malapert Mountain (Peak of Eternal Light)",
                Coords = "85.9°S, 2.9°E (5.0 km elevation)",
                Terrain = "Massive highland massif rising over 5,000 meters above surrounding plains. Slopes average 10–15° on approach ridges.",
                History = "Identified since early 2000s as prime communications relay and power station site for Artemis and international lunar base concepts.",
                Importance = "Unrivaled continuous line-of-sight to Earth for high-bandwidth direct-to-Earth communications without orbital relay interruption.",
                Resources = "Continuous or near-continuous solar illumination (>88% of lunar year); stable thermal baseline compared to crater floors.",
                Hazards = "Significant elevation climb for rovers, electrostatic levitation of fine regolith dust, exposure to cosmic radiation and SPEs.",
                Objectives = "Build primary long-range communications array, test high-efficiency multi-junction solar towers, establish navigational beacon.",
                IceInterpretation = "Dry surface on peaks; acts as power hub beaming energy into nearby shadowed craters (Haworth, Shoemaker).",
                Sunlight = "Near-permanent illumination on peak; negligible seasonal darkness periods.",
                Comms = "Continuous direct Earth line-of-sight (>95% of time, unaffected by libration dips)."
            },
            new LunarLocation
            {
                Id = "ridge",
                Name = "Connecting Ridge (Shackleton–de Gerlache)",
                Coords = "89.4°S, 222.0°E",
                Terrain = "Elevated topographical ridge connecting Shackleton rim to de Gerlache crater. Narrow ridgeline flanked by steep drop-offs into darkness.",
                History = "High-priority Artemis III candidate region surveyed extensively by LRO Narrow Angle Camera (NAC).",
                Importance = "Offers viable surface traffic corridor between two major scientific PSR craters with high average solar availability.",
                Resources = "Illuminated high ground for rover charging stations; immediate access to deep shadow slope access points.",
                Hazards = "Narrow traversable path, abrupt lighting drop-offs, difficult boulder navigation, communication occlusion by intervening peaks.",
                Objectives = "Map safe rover driving corridor, install optical navigation markers, deploy mobile science station.",
                IceInterpretation = "Subsurface hydrogen detected by LEND instrument along crater margins.",
                Sunlight = "75% to 85% illumination; shadow periods brief but sharp.",
                Comms = "Periodic direct Earth visibility, requires relay node for shadow traverses."
            },
            new LunarLocation
            {
                Id = "degerlache",
                Name = "de Gerlache Crater Rim",
                Coords = "88.5°S, 271.0°E",
                Terrain = "Asymmetric crater ~32 km across with pronounced elevated northern rim and deeply shadowed southern floor.",
                History = "Named in honor of Belgian polar explorer Adrien de Gerlache; designated NASA Artemis candidate landing region.",
                Importance = "Provides moderate-slope access routes into ancient impact melt deposits and cold-trap volatile reservoirs.",
                Resources = "Water ice deposits, anorthositic regolith suitable for sintering and construction shielding.",
                Hazards = "Severe shadowing during lunar winter, micro-meteoroid exposure on open plateau.",
                Objectives = "Survey impact melt geology, test regolith 3D printing and microwave sintering techniques.",
                IceInterpretation = "Significant cold-trap area confirmed by Diviner radiometer temperature maps (<50 K).",
                Sunlight = "70% to 80% illumination on northern rim.",
                Comms = "Moderate Earth visibility; requires relay during southern orbital passes."
            },
            new LunarLocation
            {
                Id = "haworth",
                Name = "Haworth Crater (Cryogenic Deep)",
                Coords = "87.4°S, 355.0°E",
                Terrain = "Impact crater ~35 km diameter. Floor lies in total permanent darkness, receiving zero direct sunlight for billions of years.",
                History = "Observed by NASA LCROSS impactor mission and Diviner Lunar Radiometer.",
                Importance = "One of the coldest places in the solar system (recorded temperatures as low as 35 K / -238°C), acting as pristine volatile trap.",
                Resources = "High concentrations of hydrogen, water ice, methane, ammonia, and carbon dioxide ice.",
                Hazards = "Extreme cryogenic temperatures brittle-failing conventional steel and rubber; zero solar power; complete optical darkness.",
                Objectives = "Deploy radioisotope or beamed-power robotic probe, extract deep cryogenic core samples.",
                IceInterpretation = "Verified volatile trap containing ancient planetary ice records.",
                Sunlight = "0% direct sunlight inside interior; 100% permanent darkness.",
                Comms = "Completely shielded from Earth; orbital or rim relay mandatory."
            },
            new LunarLocation
            {
                Id = "taurus",
                Name = "Taurus–Littrow Valley",
                Coords = "20.2°N, 30.8°E (Apollo 17 Site)",
                Terrain = "Narrow valley bordered by the North and South Massifs in the Montes Taurus highlands near Mare Serenitatis.",
                History = "Landing site of Apollo 17 (Gene Cernan and Harrison Schmitt, December 1972) - the last human lunar mission of the 20th century.",
                Importance = "Showcases transition between highland bedrock and ancient mare basalt volcanism; benchmark for lunar chronology.",
                Resources = "Titanium-rich ilmenite basalts, orange pyroclastic volcanic glass beads containing trapped mantle volatiles.",
                Hazards = "Large boulder fields, steep massif scree slopes, lunar dust abrasiveness on spacesuit joints.",
                Objectives = "Analyze 50-year-old lunar equipment condition, sample sub-surface regolith layers, validate seismic velocity profile.",
                IceInterpretation = "Equatorial/temperate site: no surface water ice; minerals contain bound hydroxyl ions (OH-).",
                Sunlight = "Standard lunar equatorial day: 14.75 Earth days sunlight, 14.75 Earth days darkness.",
                Comms = "Continuous direct line-of-sight to Earth from the valley floor."
            }
        };

        public static void Show(VisualElement parentRoot, System.Action onClose = null)
        {
            var overlay = Ui.Box(parentRoot, "screen", "center", "dim");
            overlay.pickingMode = PickingMode.Position;
            overlay.style.backgroundColor = new Color(0.02f, 0.04f, 0.08f, 0.96f);

            var panel = Ui.Panel(overlay);
            panel.style.width = 1720;
            panel.style.height = 980;
            panel.style.paddingLeft = 32;
            panel.style.paddingRight = 32;
            panel.style.paddingTop = 24;
            panel.style.paddingBottom = 24;

            // Header
            var head = Ui.Box(panel, "row", "noshrink");
            var titleBox = Ui.Box(head, "col", "grow");
            Ui.Text(titleBox, "LUNAR SURVIVAL INTELLIGENCE", "h1").style.color = new Color(0.3f, 0.85f, 1f);
            Ui.Text(titleBox, "NASA Space Apps Research Dashboard · South Pole Environmental Science & Mission Architecture", "kicker-blue");

            var closeBtn = Ui.Button(head, "CLOSE [Esc]", () =>
            {
                overlay.RemoveFromHierarchy();
                onClose?.Invoke();
            }, "btn-primary");
            closeBtn.style.minWidth = 140;

            // Navigation tabs
            var tabRow = Ui.Box(panel, "row", "noshrink");
            tabRow.style.marginTop = 16;
            tabRow.style.marginBottom = 16;
            tabRow.style.borderBottomWidth = 1;
            tabRow.style.borderBottomColor = new Color(0.3f, 0.65f, 1f, 0.35f);
            tabRow.style.paddingBottom = 10;

            var contentArea = Ui.Box(panel, "col", "grow");

            string currentTab = "LOCATIONS";
            var tabButtons = new Dictionary<string, Button>();

            void SelectTab(string tab)
            {
                currentTab = tab;
                foreach (var kv in tabButtons)
                {
                    bool active = kv.Key == tab;
                    kv.Value.style.backgroundColor = active ? new Color(0.12f, 0.32f, 0.65f, 0.85f) : new Color(0.05f, 0.12f, 0.24f, 0.6f);
                    kv.Value.style.borderBottomColor = active ? new Color(1f, 0.65f, 0.2f) : Color.clear;
                    kv.Value.style.borderBottomWidth = active ? 3 : 0;
                    kv.Value.style.color = active ? Color.white : new Color(0.75f, 0.85f, 0.95f);
                }
                contentArea.Clear();
                switch (tab)
                {
                    case "LOCATIONS":   BuildLocationsTab(contentArea); break;
                    case "ENVIRONMENT": BuildEnvironmentTab(contentArea); break;
                    case "SUNLIGHT":    BuildSunlightTab(contentArea); break;
                    case "APIS":        BuildApisTab(contentArea); break;
                    case "GAME TOOLS":  BuildGameToolsTab(contentArea); break;
                    case "DATA SOURCES": BuildSourcesTab(contentArea); break;
                }
            }

            foreach (var t in new[] { "LOCATIONS", "ENVIRONMENT", "SUNLIGHT", "APIS", "GAME TOOLS", "DATA SOURCES" })
            {
                var tabName = t;
                var btn = Ui.Button(tabRow, tabName, () => SelectTab(tabName));
                btn.style.fontSize = 17;
                btn.style.paddingLeft = 20;
                btn.style.paddingRight = 20;
                btn.style.paddingTop = 8;
                btn.style.paddingBottom = 8;
                btn.style.marginRight = 10;
                tabButtons[tabName] = btn;
            }

            SelectTab("LOCATIONS");
        }

        static void BuildLocationsTab(VisualElement parent)
        {
            var row = Ui.Box(parent, "row", "grow");
            row.style.alignItems = Align.Stretch;

            // Left list
            var listScroll = Ui.Add(row, new ScrollView(), "col");
            listScroll.style.width = 460;
            listScroll.style.marginRight = 20;
            listScroll.style.borderRightWidth = 1;
            listScroll.style.borderRightColor = new Color(0.3f, 0.6f, 0.9f, 0.25f);

            var detailScroll = Ui.Add(row, new ScrollView(), "col", "grow");

            int selected = 0;
            var locCards = new List<VisualElement>();

            void ShowLoc(int idx)
            {
                selected = idx;
                for (int i = 0; i < locCards.Count; i++)
                {
                    locCards[i].style.backgroundColor = (i == idx) ? new Color(0.15f, 0.35f, 0.68f, 0.45f) : new Color(0.04f, 0.10f, 0.22f, 0.65f);
                    locCards[i].style.borderLeftColor = (i == idx) ? new Color(1f, 0.65f, 0.2f) : new Color(0.3f, 0.55f, 0.85f, 0.35f);
                    locCards[i].style.borderLeftWidth = (i == idx) ? 4 : 1;
                }

                detailScroll.Clear();
                var loc = Locations[idx];

                var headerBox = Ui.Box(detailScroll, "col");
                headerBox.style.marginBottom = 16;
                Ui.Text(headerBox, loc.Name.ToUpper(), "h1").style.color = new Color(1f, 0.7f, 0.25f);
                Ui.Text(headerBox, loc.Coords, "kicker-blue").style.fontSize = 20;

                // Quick stats row
                var statsRow = Ui.Box(detailScroll, "row");
                statsRow.style.marginBottom = 18;
                QuickPill(statsRow, "SUNLIGHT", loc.Sunlight, new Color(1f, 0.8f, 0.3f));
                QuickPill(statsRow, "EARTH LINK", loc.Comms, new Color(0.3f, 0.85f, 1f));
                QuickPill(statsRow, "ICE POTENTIAL", loc.IceInterpretation.Contains("confirmed") ? "HIGH CONFIRMED" : "TRAPPED VOLATILES", new Color(0.2f, 0.95f, 0.65f));

                SectionBox(detailScroll, "TERRAIN & GEOGRAPHY", loc.Terrain);
                SectionBox(detailScroll, "EXPLORATION HISTORY", loc.History);
                SectionBox(detailScroll, "SCIENTIFIC & STRATEGIC IMPORTANCE", loc.Importance);
                SectionBox(detailScroll, "RESOURCES & VOLATILES", loc.Resources);
                SectionBox(detailScroll, "ENVIRONMENTAL HAZARDS", loc.Hazards, new Color(1f, 0.45f, 0.4f));
                SectionBox(detailScroll, "OPERATIONAL OBJECTIVES", loc.Objectives, new Color(0.4f, 0.9f, 0.6f));
            }

            for (int i = 0; i < Locations.Length; i++)
            {
                int idx = i;
                var loc = Locations[i];
                var card = Ui.Box(listScroll, "col");
                card.pickingMode = PickingMode.Position;   // Ui.Box is click-through by default
                card.style.paddingLeft = 14; card.style.paddingRight = 14;
                card.style.paddingTop = 12; card.style.paddingBottom = 12;
                card.style.marginBottom = 10;
                Ui.SetBorder(card, 1, new Color(0.3f, 0.55f, 0.85f, 0.35f), 6);

                var title = Ui.Text(card, loc.Name, "h3");
                title.style.color = Color.white;
                var coords = Ui.Text(card, loc.Coords, "small");
                coords.style.color = new Color(0.5f, 0.75f, 1f);

                card.RegisterCallback<ClickEvent>(_ => ShowLoc(idx));
                locCards.Add(card);
            }

            ShowLoc(0);
        }

        static void QuickPill(VisualElement parent, string label, string val, Color col)
        {
            var p = Ui.Box(parent, "col");
            p.style.backgroundColor = new Color(0.04f, 0.10f, 0.22f, 0.8f);
            Ui.SetBorder(p, 1, new Color(col.r, col.g, col.b, 0.4f), 6);
            p.style.paddingLeft = 12; p.style.paddingRight = 12;
            p.style.paddingTop = 6; p.style.paddingBottom = 6;
            p.style.marginRight = 12;
            var l = Ui.Text(p, label, "tiny"); l.style.color = new Color(0.7f, 0.8f, 0.9f);
            var v = Ui.Text(p, val, "text-small"); v.style.color = col; v.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        static void SectionBox(VisualElement parent, string title, string content, Color? headerColor = null)
        {
            var box = Ui.Box(parent, "col");
            box.style.marginBottom = 14;
            box.style.backgroundColor = new Color(0.03f, 0.08f, 0.18f, 0.6f);
            Ui.SetBorder(box, 1, new Color(0.2f, 0.45f, 0.8f, 0.25f), 6);
            box.style.paddingLeft = 14; box.style.paddingRight = 14;
            box.style.paddingTop = 10; box.style.paddingBottom = 10;

            var t = Ui.Text(box, title, "small");
            t.style.unityFontStyleAndWeight = FontStyle.Bold;
            t.style.color = headerColor ?? new Color(0.5f, 0.8f, 1f);
            t.style.marginBottom = 4;

            var c = Ui.Text(box, content, "text-small");
            c.style.whiteSpace = WhiteSpace.Normal;
            c.style.color = new Color(0.88f, 0.92f, 0.98f);
        }

        static void BuildEnvironmentTab(VisualElement parent)
        {
            var scroll = Ui.Add(parent, new ScrollView(), "col", "grow");
            Ui.Text(scroll, "LUNAR ENVIRONMENTAL PARAMETERS (MEASURED VS DERIVED)", "h2").style.color = new Color(0.3f, 0.85f, 1f);
            Ui.Text(scroll, "Physical data measured by Apollo surface instruments, LRO, Clementine, and Diviner Radiometer.", "small", "muted");

            var table = Ui.Box(scroll, "col");
            table.style.marginTop = 14;
            Ui.SetBorder(table, 1, new Color(0.3f, 0.6f, 0.9f, 0.35f), 6);

            var (rows, headers) = GetEnvData();
            var headRow = Ui.Box(table, "table-row");
            headRow.style.backgroundColor = new Color(0.08f, 0.18f, 0.35f, 0.9f);
            foreach (var h in headers)
            {
                var l = Ui.Text(headRow, h, "small");
                l.style.unityFontStyleAndWeight = FontStyle.Bold;
                l.style.color = new Color(0.7f, 0.85f, 1f);
                l.style.width = (h == "VARIABLE" ? 280 : h == "VALUE" ? 220 : h == "UNIT" ? 120 : h == "TYPE" ? 140 : 600);
            }

            foreach (var r in rows)
            {
                var rowBox = Ui.Box(table, "table-row");
                rowBox.style.paddingTop = 8; rowBox.style.paddingBottom = 8;
                rowBox.style.borderBottomWidth = 1; rowBox.style.borderBottomColor = new Color(0.2f, 0.4f, 0.7f, 0.15f);

                Ui.Text(rowBox, r.Var, "text-small").style.width = 280;
                var valLbl = Ui.Text(rowBox, r.Val, "text-small");
                valLbl.style.width = 220; valLbl.style.unityFontStyleAndWeight = FontStyle.Bold; valLbl.style.color = new Color(1f, 0.75f, 0.3f);
                Ui.Text(rowBox, r.Unit, "small", "muted").style.width = 120;

                var typeLbl = Ui.Text(rowBox, r.Type, "tiny");
                typeLbl.style.width = 140;
                typeLbl.style.color = r.Type.Contains("Measured") ? new Color(0.3f, 0.9f, 0.5f) : new Color(0.4f, 0.75f, 1f);

                Ui.Text(rowBox, r.Source, "small", "muted").style.width = 600;
            }
        }

        struct EnvRow { public string Var, Val, Unit, Type, Source; }
        static (EnvRow[] rows, string[] headers) GetEnvData()
        {
            var headers = new[] { "VARIABLE", "VALUE", "UNIT", "TYPE", "METHOD & SOURCE" };
            var rows = new[]
            {
                new EnvRow { Var = "Solar Constant at 1 AU", Val = "1361", Unit = "W/m²", Type = "Measured", Source = "SORCE / TSIS-1 satellite observations" },
                new EnvRow { Var = "Surface Temp (Equatorial Noon)", Val = "390 (+117)", Unit = "K (°C)", Type = "Measured", Source = "Diviner Lunar Radiometer Experiment (LRO)" },
                new EnvRow { Var = "Surface Temp (Night / Shadow)", Val = "40 to 100 (-233 to -173)", Unit = "K (°C)", Type = "Measured", Source = "Diviner permanently shadowed region minimums" },
                new EnvRow { Var = "Lunar Surface Gravity", Val = "1.62", Unit = "m/s²", Type = "Measured", Source = "GRAIL lunar gravity mapping (~16.5% of Earth g)" },
                new EnvRow { Var = "Atmospheric Pressure", Val = "10⁻¹⁰ to 10⁻¹²", Unit = "Pa", Type = "Measured", Source = "Apollo 14 & 15 Cold Cathode Gauge Experiments" },
                new EnvRow { Var = "Synodic Day Length", Val = "29.53059", Unit = "Earth days", Type = "Calculated", Source = "NASA JPL Horizons lunar solar ephemeris" },
                new EnvRow { Var = "Lunar Axial Tilt to Ecliptic", Val = "1.54", Unit = "degrees", Type = "Calculated", Source = "IAU Working Group on Cartographic Coordinates" },
                new EnvRow { Var = "Regolith Bulk Density", Val = "1.50 to 1.90", Unit = "g/cm³", Type = "Measured", Source = "Apollo core samples (0 to 60 cm depth)" },
                new EnvRow { Var = "Galactic Cosmic Radiation", Val = "0.2 to 0.5", Unit = "mSv/day", Type = "Measured", Source = "CRaTER instrument on LRO" },
                new EnvRow { Var = "Solar Particle Event (SPE) Peak", Val = "100 to 500+", Unit = "mSv/event", Type = "Historical", Source = "Modeled after August 1972 event measurements" }
            };
            return (rows, headers);
        }

        static void BuildSunlightTab(VisualElement parent)
        {
            var scroll = Ui.Add(parent, new ScrollView(), "col", "grow");
            Ui.Text(scroll, "POLAR SUNLIGHT & SHADOW DYNAMICS", "h2").style.color = new Color(0.3f, 0.85f, 1f);
            Ui.Text(scroll, "How solar elevation angles govern outpost power, thermal stability, and mission windows.", "small", "muted");

            SectionBox(scroll, "THE 1.54° AXIAL TILT PHENOMENON",
                "Because the Moon's obliquity is only 1.54° relative to the ecliptic, the Sun always hovers close to the polar horizon. " +
                "Unlike Earth with 23.5° seasonal swings, lunar polar elevations oscillate within a narrow ±1.54° window over the 29.53-day synodic month. " +
                "Consequently, elevated rims experience grazing sunlight year-round, while depressed crater floors remain permanently in the dark.");

            SectionBox(scroll, "SHADOW WINDOWS & BATTERY SIZING",
                "Even on elevated ridges (such as Shackleton or Connecting Ridge), intervening mountain massifs cast long, moving shadows. " +
                "In Mission 1, a realistic 6-hour shadow window occurs around hour 40. Outpost batteries must be sized to carry all life support, " +
                "communications, and heater baselines during this occultation without dropping below critical discharge depth.");

            SectionBox(scroll, "SOLAR ELEVATION VS POWER GENERATION",
                "Flat horizontal ground receives almost zero flux (sin(elevation) ≈ 0.02). However, vertically oriented or sun-tracking solar arrays " +
                "intercept the low-grazing beams nearly perpendicular, achieving full solar constant irradiance (1,361 W/m²). This is why lunar polar " +
                "outposts utilize vertical solar towers rather than flat ground mats.");
        }

        static void BuildApisTab(VisualElement parent)
        {
            var scroll = Ui.Add(parent, new ScrollView(), "col", "grow");
            Ui.Text(scroll, "NASA & EXTERNAL SCIENTIFIC DATA APIS", "h2").style.color = new Color(0.3f, 0.85f, 1f);
            Ui.Text(scroll, "Open APIs and data archives used to design, benchmark, and verify lunar mission profiles.", "small", "muted");

            var apis = new[]
            {
                ("JPL Horizons Ephemeris API", "https://ssd.jpl.nasa.gov/api/horizons.api",
                 "Provides solar, Earth, and Moon positions, azimuth/elevation angles, and light-time delays for any mission timestamp."),
                ("NASA Earthdata CMR Search", "https://cmr.earthdata.nasa.gov/search/",
                 "Search catalog for global NASA planetary datasets including LRO LOLA DEMs and Lunar Prospector neutron spectrometer data."),
                ("PDS Lunar Orbital Data Explorer (ODE)", "https://ode.rsl.wustl.edu/moon/",
                 "REST API for searching and accessing calibrated data products from LRO, Clementine, Lunar Orbiter, and Chandrayaan-1."),
                ("NASA Moon Trek WMTS / API", "https://trek.nasa.gov/moon/",
                 "Interactive tiled web map services providing LOLA slope, roughness, illumination maps, and Artemis candidate site overlays."),
                ("NASA Open APIs (api.nasa.gov)", "https://api.nasa.gov/",
                 "Access to NASA imagery, space weather warnings (DONKI), and technical publication indexes (NTRS).")
            };

            foreach (var (name, url, desc) in apis)
            {
                var card = Ui.Box(scroll, "col");
                card.style.marginBottom = 12;
                card.style.backgroundColor = new Color(0.04f, 0.10f, 0.22f, 0.6f);
                Ui.SetBorder(card, 1, new Color(0.3f, 0.6f, 0.9f, 0.3f), 6);
                card.style.paddingLeft = 14; card.style.paddingRight = 14;
                card.style.paddingTop = 10; card.style.paddingBottom = 10;

                var t = Ui.Text(card, name, "h3"); t.style.color = new Color(1f, 0.75f, 0.25f);
                var u = Ui.Text(card, url, "small"); u.style.color = new Color(0.4f, 0.8f, 1f); u.style.marginBottom = 4;
                var d = Ui.Text(card, desc, "text-small"); d.style.whiteSpace = WhiteSpace.Normal;
            }
        }

        static void BuildGameToolsTab(VisualElement parent)
        {
            var scroll = Ui.Add(parent, new ScrollView(), "col", "grow");
            Ui.Text(scroll, "SIMULATION ENGINE & GAME BALANCE TOOLS", "h2").style.color = new Color(0.3f, 0.85f, 1f);
            Ui.Text(scroll, "Mapping real NASA physics into accessible educational simulation mechanics.", "small", "muted");

            SectionBox(scroll, "OXYGEN GENERATOR (OGS) ELECTROLYSIS RATIO",
                "Real physics: Electrolysis splits H2O into H2 and O2. Molecular weight H2O = 18 g/mol, O2 = 32 g/mol. " +
                "Stoichiometrically, producing 1.0 kg of O2 consumes ~1.125 kg (liters) of water. " +
                "In Mission Mohakash: The OGS consumes exactly 1.125 L of water per kg of oxygen generated, demonstrating closed-loop conservation.");

            SectionBox(scroll, "POWER BUS (PW & PWh GAME UNITS)",
                "For intuitive arithmetic, electrical power is normalized into Power Units (PW) where 1.0 PW corresponds to peak nominal solar array generation (~1 kW real scale). " +
                "Battery storage is measured in PWh (Power-Hours), meaning a 10 PWh battery sustains a 1.0 PW load for exactly 10 mission hours.");

            SectionBox(scroll, "SEISMOMETER NOISE THRESHOLD (80 METERS)",
                "Lunar regolith is an acoustic waveguide with extremely low attenuation. Machinery vibrations (fans, pumps, airlock cycling) " +
                "mask faint natural moonquake signals unless seismic packages are placed at least 80 meters away from habitat structural modules.");
        }

        static void BuildSourcesTab(VisualElement parent)
        {
            var scroll = Ui.Add(parent, new ScrollView(), "col", "grow");
            Ui.Text(scroll, "DATA SOURCES & SCIENTIFIC CITATIONS", "h2").style.color = new Color(0.3f, 0.85f, 1f);
            Ui.Text(scroll, "All scientific principles in Mission Mohakash reference verified NASA and planetary science literature.", "small", "muted");

            var citations = new[]
            {
                "NASA Artemis Plan (2020) - NASA’s Lunar Exploration Program Overview.",
                "Mazarico, E. et al. (2011) - Illumination conditions of the lunar polar regions using LRO LOLA topography, Icarus.",
                "Paige, D. A. et al. (2010) - Diviner Lunar Radiometer Observations of Cold Traps in the Moon's South Polar Region, Science.",
                "Heiken, G., Vaniman, D., & French, B. (1991) - Lunar Sourcebook: A User's Guide to the Moon, Cambridge University Press.",
                "Apollo 17 Preliminary Science Report (1973) - NASA SP-330, Lunar surface geology and cosmic ray experiments."
            };

            foreach (var c in citations)
            {
                var r = Ui.Box(scroll, "row");
                r.style.marginBottom = 10;
                Ui.Text(r, "• ", "text-small").style.color = new Color(1f, 0.7f, 0.2f);
                var t = Ui.Text(r, c, "text-small");
                t.style.whiteSpace = WhiteSpace.Normal;
                t.style.color = new Color(0.85f, 0.9f, 0.95f);
            }
        }
    }
}
