using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mohakash
{
    /// <summary>
    /// Runtime minimap drawn entirely in UI Toolkit (no extra camera, no render texture).
    /// It is a fixed 240 × 240 px circle in the bottom-right corner of the HUD.
    ///
    /// Features:
    ///   • Player dot (white arrow showing facing direction)
    ///   • Habitat (green circle)
    ///   • Rover (yellow triangle, only when it exists)
    ///   • Seismometer crate / deployed position (orange dot)
    ///   • Radiation monitor (cyan dot)
    ///   • Sample sites A/B/C (teal diamond, fades when collected)
    ///   • Solar arrays (small yellow squares)
    ///   • North indicator label
    ///   • Zoom: scroll-wheel or +/- buttons (range 40–320 m)
    ///   • Compass rose (N/E/S/W labels) that rotates with the player heading
    ///
    /// Usage:
    ///   var mm = gameObject.AddComponent&lt;MinimapSystem&gt;();
    ///   mm.Init(uiRoot, mission1);
    ///   // Then each frame Mission1 calls mm.Tick();
    /// </summary>
    public class MinimapSystem : MonoBehaviour
    {
        // ── Layout constants ────────────────────────────────────────────────
        const int   MapSize     = 240;   // pixels – the clipping circle diameter
        const int   HalfMap     = MapSize / 2;
        const float MinRange    = 40f;
        const float MaxRange    = 320f;
        const float DefaultRange = 120f; // metres shown from edge to edge (radius = half)

        // ── State ───────────────────────────────────────────────────────────
        Mission1 m;
        VisualElement mapContainer;    // the circular clip area
        VisualElement playerDot;
        VisualElement habDot;
        VisualElement roverDot;
        VisualElement seisDot;
        Label         rangeLabel;
        Label         northLabel;
        readonly List<(VisualElement dot, System.Func<Vector3> pos, System.Func<bool> visible)> icons
            = new List<(VisualElement, System.Func<Vector3>, System.Func<bool>)>();

        float range = DefaultRange;    // current metres-per-half-map

        // ── Public API ──────────────────────────────────────────────────────

        /// <summary>Call once after Mission1.Launch() has finished setting up the world.</summary>
        public void Init(VisualElement hudRoot, Mission1 mission)
        {
            m = mission;
            BuildUi(hudRoot);
            RegisterIcons();
        }

        /// <summary>Call every frame from Mission1.Update() while in Operations phase.</summary>
        public void Tick()
        {
            if (m == null || m.World == null || m.Player == null) return;

            Vector3 playerPos = m.Player.transform.position;
            float   playerYaw = m.Player.transform.eulerAngles.y;

            // ── Update player arrow ─────────────────────────────────────
            PlaceDot(playerDot, playerPos, playerPos, playerYaw);
            playerDot.style.rotate = new Rotate(Angle.Degrees(-playerYaw));  // arrow faces forward

            // ── Update hab dot ─────────────────────────────────────────
            PlaceDot(habDot, m.World.Hab.position, playerPos);

            // ── Update rover dot ───────────────────────────────────────
            if (roverDot != null)
            {
                bool hasRover = m.Rover != null;
                Ui.Show(roverDot, hasRover);
                if (hasRover)
                    PlaceDot(roverDot, m.Rover.transform.position, playerPos,
                             m.Rover.transform.eulerAngles.y);
            }

            // ── Update seismometer dot ─────────────────────────────────
            if (seisDot != null && m.Model != null)
            {
                bool seisVisible = m.Model.SeisDeployed || m.Model.SeisCarried;
                Ui.Show(seisDot, seisVisible);
                if (m.Model.SeisDeployed && m.Model.SeisPos != Vector3.zero)
                    PlaceDot(seisDot, m.Model.SeisPos, playerPos);
                else if (m.Model.SeisCarried)
                    PlaceDot(seisDot, playerPos + m.Player.transform.forward * 0.5f, playerPos);
            }

            // ── Update generic icons ───────────────────────────────────
            foreach (var (dot, getPos, getVis) in icons)
            {
                bool vis = getVis();
                Ui.Show(dot, vis);
                if (vis) PlaceDot(dot, getPos(), playerPos);
            }
        }

        // ── Zoom ────────────────────────────────────────────────────────────

        void ZoomIn()  { range = Mathf.Max(MinRange,  range * 0.65f); UpdateRangeLabel(); }
        void ZoomOut() { range = Mathf.Min(MaxRange, range * 1.55f); UpdateRangeLabel(); }

        void UpdateRangeLabel()
        {
            if (rangeLabel != null) rangeLabel.text = $"±{Mathf.RoundToInt(range)} m";
        }

        // ── Coordinate mapping ───────────────────────────────────────────────

        /// <summary>Projects world position onto the minimap 0..MapSize square centred on origin.</summary>
        Vector2 WorldToMap(Vector3 worldPos, Vector3 origin)
        {
            float dx = worldPos.x - origin.x;
            float dz = worldPos.z - origin.z;
            float px = HalfMap + (dx / range) * HalfMap;
            float py = HalfMap - (dz / range) * HalfMap;   // Z+ is up on map
            return new Vector2(px, py);
        }

        void PlaceDot(VisualElement dot, Vector3 worldPos, Vector3 origin, float extraRotDeg = -999f)
        {
            if (dot == null) return;
            var uv = WorldToMap(worldPos, origin);
            // Clamp to within the circle (r = HalfMap - half dot size)
            float halfDot = dot.resolvedStyle.width * 0.5f;
            if (halfDot == 0) halfDot = 6f;
            float cx = uv.x - HalfMap, cy = uv.y - HalfMap;
            float maxR = HalfMap - halfDot - 2f;
            if (cx * cx + cy * cy > maxR * maxR)
            {
                float mag = Mathf.Sqrt(cx * cx + cy * cy);
                cx = cx / mag * maxR; cy = cy / mag * maxR;
                uv = new Vector2(HalfMap + cx, HalfMap + cy);
            }
            dot.style.left = uv.x - halfDot;
            dot.style.top  = uv.y - halfDot;
        }

        // ── UI construction ──────────────────────────────────────────────────

        void BuildUi(VisualElement hudRoot)
        {
            // ── Outer wrapper (bottom-right corner) ─────────────────────
            var wrap = new VisualElement();
            wrap.name = "minimap-wrap";
            wrap.style.position   = Position.Absolute;
            wrap.style.right      = 18;
            wrap.style.bottom     = 18;
            wrap.style.width      = MapSize + 2;    // +2 for border
            wrap.style.flexDirection = FlexDirection.Column;
            wrap.style.alignItems = Align.Center;
            hudRoot.Add(wrap);

            // ── Title bar ────────────────────────────────────────────────
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.width         = Length.Percent(100);
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.marginBottom  = 3;

            var titleLbl = new Label("MAP");
            titleLbl.style.color    = new Color(0.75f, 0.85f, 1f);
            titleLbl.style.fontSize = 10;
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(titleLbl);

            rangeLabel = new Label($"±{Mathf.RoundToInt(range)} m");
            rangeLabel.style.color    = new Color(0.6f, 0.6f, 0.6f);
            rangeLabel.style.fontSize = 9;
            header.Add(rangeLabel);

            wrap.Add(header);

            // ── Zoom buttons ─────────────────────────────────────────────
            var zoomRow = new VisualElement();
            zoomRow.style.flexDirection  = FlexDirection.Row;
            zoomRow.style.justifyContent = Justify.FlexEnd;
            zoomRow.style.width          = Length.Percent(100);
            zoomRow.style.marginBottom   = 2;

            var btnPlus = MakeZoomButton("+");
            btnPlus.clicked += ZoomIn;
            var btnMinus = MakeZoomButton("−");
            btnMinus.clicked += ZoomOut;
            zoomRow.Add(btnPlus);
            zoomRow.Add(btnMinus);
            wrap.Add(zoomRow);

            // ── Map circle ───────────────────────────────────────────────
            mapContainer = new VisualElement();
            mapContainer.name = "minimap";
            mapContainer.style.width          = MapSize;
            mapContainer.style.height         = MapSize;
            mapContainer.style.overflow       = Overflow.Hidden;
            mapContainer.style.position       = Position.Relative;
            mapContainer.style.backgroundColor = new Color(0.04f, 0.06f, 0.09f, 0.88f);
            mapContainer.style.borderTopLeftRadius     = HalfMap;
            mapContainer.style.borderTopRightRadius    = HalfMap;
            mapContainer.style.borderBottomLeftRadius  = HalfMap;
            mapContainer.style.borderBottomRightRadius = HalfMap;
            mapContainer.style.borderTopWidth    = 1.5f;
            mapContainer.style.borderRightWidth  = 1.5f;
            mapContainer.style.borderBottomWidth = 1.5f;
            mapContainer.style.borderLeftWidth   = 1.5f;
            mapContainer.style.borderTopColor    = new Color(0.3f, 0.45f, 0.65f, 0.9f);
            mapContainer.style.borderRightColor  = new Color(0.3f, 0.45f, 0.65f, 0.9f);
            mapContainer.style.borderBottomColor = new Color(0.3f, 0.45f, 0.65f, 0.9f);
            mapContainer.style.borderLeftColor   = new Color(0.3f, 0.45f, 0.65f, 0.9f);
            wrap.Add(mapContainer);

            // ── Crosshair lines ──────────────────────────────────────────
            AddCrosshair(mapContainer);

            // ── Concentric range rings ────────────────────────────────────
            // Two static rings at 1/4 and 1/2 radius for distance reference
            AddRing(mapContainer, HalfMap * 0.5f, new Color(1f, 1f, 1f, 0.06f));
            AddRing(mapContainer, HalfMap * 1.0f, new Color(1f, 1f, 1f, 0.10f));

            // ── North label ──────────────────────────────────────────────
            northLabel = new Label("N");
            northLabel.style.position   = Position.Absolute;
            northLabel.style.top        = 4;
            northLabel.style.left       = HalfMap - 6;
            northLabel.style.color      = new Color(0.7f, 0.75f, 0.85f);
            northLabel.style.fontSize   = 9;
            northLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            mapContainer.Add(northLabel);

            // ── Permanent dots ────────────────────────────────────────────
            habDot  = MakeDot(mapContainer, 14, new Color(0.25f, 1f, 0.45f), "⌂", tooltip: "Habitat");
            playerDot = MakeDot(mapContainer, 12, new Color(1f, 1f, 1f), "▲", tooltip: "You");

            // ── Optional dots (visible only when relevant) ───────────────
            roverDot = MakeDot(mapContainer, 10, new Color(1f, 0.88f, 0.2f), "◆", tooltip: "Rover");
            Ui.Show(roverDot, false);

            seisDot = MakeDot(mapContainer, 9, new Color(1f, 0.6f, 0.15f), "●", tooltip: "Seismometer");
            Ui.Show(seisDot, false);

            // ── Legend strip ─────────────────────────────────────────────
            var legend = new VisualElement();
            legend.style.flexDirection = FlexDirection.Row;
            legend.style.flexWrap      = Wrap.Wrap;
            legend.style.marginTop     = 4;
            legend.style.width         = Length.Percent(100);
            AddLegend(legend, new Color(0.25f, 1f, 0.45f), "HAB");
            AddLegend(legend, new Color(1f, 0.88f, 0.2f),  "ROV");
            AddLegend(legend, new Color(1f, 0.6f, 0.15f),  "SEIS");
            AddLegend(legend, new Color(0.3f, 0.85f, 0.95f), "SMPL");
            wrap.Add(legend);
        }

        void RegisterIcons()
        {
            if (m == null) return;

            // ── Radiation monitor ─────────────────────────────────────────
            if (m.Kit != null && m.Kit.Has("RAD"))
            {
                var radPos = m.World.OnGround(6f, -8f);
                var radDot = MakeDot(mapContainer, 9, new Color(0.3f, 0.85f, 0.95f), "◉", tooltip: "Rad monitor");
                icons.Add((radDot,
                    () => radPos,
                    () => m.Model != null && m.Model.RadReadings == 0));
            }

            // ── Sample sites ──────────────────────────────────────────────
            foreach (var site in m.SampleSites)
            {
                var capturedSite = site;
                var dot = MakeDot(mapContainer, 9, new Color(0.3f, 0.85f, 0.95f), "◈", tooltip: capturedSite.Label ?? "Sample");
                icons.Add((dot,
                    () => capturedSite.transform.position,
                    () => capturedSite.gameObject.activeSelf));
            }

            // ── Solar arrays ──────────────────────────────────────────────
            foreach (var arr in m.World.SolarArrays)
            {
                var capturedArr = arr;
                var dot = MakeDot(mapContainer, 7, new Color(1f, 0.95f, 0.3f), "■", tooltip: "Solar array");
                icons.Add((dot,
                    () => capturedArr.position,
                    () => true));
            }

            // ── Lander ────────────────────────────────────────────────────
            var landerPos = m.World.OnGround(-30f, 18f);
            var landerDot = MakeDot(mapContainer, 11, new Color(0.7f, 0.5f, 1f), "▼", tooltip: "Lander");
            icons.Add((landerDot, () => landerPos, () => true));
        }

        // ── UI element helpers ───────────────────────────────────────────────

        /// <summary>Creates a positioned dot label inside the map container.</summary>
        static VisualElement MakeDot(VisualElement parent, int size, Color color,
                                      string symbol = "●", string tooltip = "")
        {
            var lbl = new Label(symbol);
            lbl.style.position  = Position.Absolute;
            lbl.style.width     = size;
            lbl.style.height    = size;
            lbl.style.color     = color;
            lbl.style.fontSize  = size - 2;
            lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            // Drop-shadow via text shadow (Unity 2022+)
            lbl.style.textShadow = new TextShadow
            {
                color      = new Color(0, 0, 0, 0.7f),
                offset     = new Vector2(1, 1),
                blurRadius = 2f
            };
            if (!string.IsNullOrEmpty(tooltip)) lbl.tooltip = tooltip;
            parent.Add(lbl);
            return lbl;
        }

        static Button MakeZoomButton(string label)
        {
            var btn = new Button { text = label };
            btn.style.width   = 18;
            btn.style.height  = 16;
            btn.style.fontSize = 12;
            btn.style.marginLeft  = 2;
            btn.style.backgroundColor = new Color(0.15f, 0.18f, 0.22f, 0.8f);
            btn.style.color           = new Color(0.8f, 0.85f, 0.9f);
            btn.style.borderTopWidth    = 1;
            btn.style.borderRightWidth  = 1;
            btn.style.borderBottomWidth = 1;
            btn.style.borderLeftWidth   = 1;
            btn.style.borderTopColor    = new Color(0.3f, 0.4f, 0.5f);
            btn.style.borderRightColor  = new Color(0.3f, 0.4f, 0.5f);
            btn.style.borderBottomColor = new Color(0.3f, 0.4f, 0.5f);
            btn.style.borderLeftColor   = new Color(0.3f, 0.4f, 0.5f);
            btn.style.borderTopLeftRadius     = 3;
            btn.style.borderTopRightRadius    = 3;
            btn.style.borderBottomLeftRadius  = 3;
            btn.style.borderBottomRightRadius = 3;
            btn.style.paddingTop    = 0;
            btn.style.paddingBottom = 0;
            btn.style.paddingLeft   = 0;
            btn.style.paddingRight  = 0;
            return btn;
        }

        static void AddCrosshair(VisualElement parent)
        {
            // Horizontal
            var h = new VisualElement();
            h.style.position        = Position.Absolute;
            h.style.top             = HalfMap - 0.5f;
            h.style.left            = 0;
            h.style.width           = MapSize;
            h.style.height          = 1;
            h.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
            parent.Add(h);

            // Vertical
            var v = new VisualElement();
            v.style.position        = Position.Absolute;
            v.style.left            = HalfMap - 0.5f;
            v.style.top             = 0;
            v.style.width           = 1;
            v.style.height          = MapSize;
            v.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
            parent.Add(v);
        }

        static void AddRing(VisualElement parent, float r, Color color)
        {
            // Approximate a circle with a square with rounded corners equal to half-size.
            float d = r * 2f;
            var ring = new VisualElement();
            ring.style.position        = Position.Absolute;
            ring.style.left            = HalfMap - r;
            ring.style.top             = HalfMap - r;
            ring.style.width           = d;
            ring.style.height          = d;
            ring.style.borderTopWidth    = 1;
            ring.style.borderRightWidth  = 1;
            ring.style.borderBottomWidth = 1;
            ring.style.borderLeftWidth   = 1;
            ring.style.borderTopColor    = color;
            ring.style.borderRightColor  = color;
            ring.style.borderBottomColor = color;
            ring.style.borderLeftColor   = color;
            ring.style.borderTopLeftRadius     = r;
            ring.style.borderTopRightRadius    = r;
            ring.style.borderBottomLeftRadius  = r;
            ring.style.borderBottomRightRadius = r;
            ring.style.backgroundColor = new Color(0, 0, 0, 0);
            parent.Add(ring);
        }

        static void AddLegend(VisualElement parent, Color color, string label)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems    = Align.Center;
            row.style.marginRight   = 6;
            row.style.marginTop     = 2;

            var dot = new VisualElement();
            dot.style.width           = 6;
            dot.style.height          = 6;
            dot.style.backgroundColor = color;
            dot.style.borderTopLeftRadius     = 3;
            dot.style.borderTopRightRadius    = 3;
            dot.style.borderBottomLeftRadius  = 3;
            dot.style.borderBottomRightRadius = 3;
            dot.style.marginRight     = 3;
            row.Add(dot);

            var lbl = new Label(label);
            lbl.style.color    = new Color(0.65f, 0.65f, 0.65f);
            lbl.style.fontSize = 8;
            row.Add(lbl);

            parent.Add(row);
        }
    }
}
