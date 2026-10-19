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
        readonly Dictionary<Interactable, VisualElement> markDots = new Dictionary<Interactable, VisualElement>();

        // ── Inside: habitat floor plan (12 x 9 m room, north up) ────────────
        const float RoomPx = 32f;      // pixels per metre
        VisualElement insideWrap, room, insidePlayer;
        readonly List<(Rect r, float h)> furniture = new List<(Rect, float)>();   // top-down footprints in room metres, h = height
        readonly Dictionary<Interactable, (VisualElement box, Label lbl)> roomIcons = new Dictionary<Interactable, (VisualElement, Label)>();

        Vector2 RoomPos(Vector3 world)
        {
            var l = world - HabitatInterior.Origin;
            return new Vector2((l.x + HabitatInterior.W / 2f) * RoomPx, (HabitatInterior.D / 2f - l.z) * RoomPx);
        }

        void BuildInsideUi(VisualElement hudRoot)
        {
            insideWrap = new VisualElement();
            insideWrap.AddToClassList("hud-card");
            insideWrap.style.position = Position.Absolute; insideWrap.style.right = 18; insideWrap.style.bottom = 18;
            var title = new Label("HABITAT MAP   G = mark this spot");
            title.AddToClassList("card-title");
            insideWrap.Add(title);
            room = new VisualElement();
            room.style.width = HabitatInterior.W * RoomPx; room.style.height = HabitatInterior.D * RoomPx;
            room.style.backgroundColor = new Color(0.08f, 0.11f, 0.16f, 0.9f);
            room.style.borderTopWidth = room.style.borderBottomWidth = room.style.borderLeftWidth = room.style.borderRightWidth = 2f;
            var wall = new Color(0.55f, 0.65f, 0.8f);
            room.style.borderTopColor = room.style.borderBottomColor = room.style.borderLeftColor = room.style.borderRightColor = wall;
            insideWrap.Add(room);
            room.generateVisualContent += DrawFurniture;
            // airlock gap in the south wall
            var gap = new VisualElement();
            gap.style.position = Position.Absolute; gap.style.height = 4; gap.style.width = 1.4f * RoomPx;
            gap.style.left = (HabitatInterior.W / 2f - 0.7f) * RoomPx; gap.style.bottom = -3;
            gap.style.backgroundColor = new Color(0.3f, 1f, 0.5f);
            room.Add(gap);
            var n = new Label("N"); n.style.position = Position.Absolute; n.style.top = 2; n.style.right = 6; n.style.fontSize = 11; n.style.color = new Color(1, 1, 1, 0.6f);
            room.Add(n);
            insidePlayer = MakeDot(room, 16, Color.white, "▲", "You");
            Ui.Show(insideWrap, false);
            hudRoot.Add(insideWrap);
        }

        /// <summary>Reads the real furniture footprints from the built interior once (every renderer that stands on the floor).</summary>
        void ScanFurniture()
        {
            if (m.Interior == null) return;
            var o = HabitatInterior.Origin;
            foreach (var rend in m.Interior.GetComponentsInChildren<Renderer>())
            {
                var b = rend.bounds;
                float x0 = b.min.x - o.x, x1 = b.max.x - o.x, z0 = b.min.z - o.z, z1 = b.max.z - o.z, y0 = b.min.y - o.y, h = b.max.y - o.y;
                if (y0 > 1.2f || h < 0.25f) continue;                                            // ceiling / wall-hung items, floor strips
                if (x1 - x0 > HabitatInterior.W * 0.8f || z1 - z0 > HabitatInterior.D * 0.8f) continue;   // floor, walls, rails
                if (x1 < -HabitatInterior.W / 2f || x0 > HabitatInterior.W / 2f || z1 < -HabitatInterior.D / 2f || z0 > HabitatInterior.D / 2f) continue;   // airlock tunnel
                furniture.Add((Rect.MinMaxRect(x0, z0, x1, z1), h));
            }
            room.MarkDirtyRepaint();
        }

        void DrawFurniture(MeshGenerationContext mgc)
        {
            var pt = mgc.painter2D;
            // faint 1 m grid
            pt.lineWidth = 1f; pt.strokeColor = new Color(1f, 1f, 1f, 0.05f);
            for (float x = 1f; x < HabitatInterior.W; x += 1f) { pt.BeginPath(); pt.MoveTo(new Vector2(x * RoomPx, 0)); pt.LineTo(new Vector2(x * RoomPx, HabitatInterior.D * RoomPx)); pt.Stroke(); }
            for (float z = 1f; z < HabitatInterior.D; z += 1f) { pt.BeginPath(); pt.MoveTo(new Vector2(0, z * RoomPx)); pt.LineTo(new Vector2(HabitatInterior.W * RoomPx, z * RoomPx)); pt.Stroke(); }
            // furniture: taller = lighter, so lockers and counters read differently from stools and crates
            foreach (var (r, h) in furniture)
            {
                float k = Mathf.InverseLerp(0.3f, 2.4f, h);
                pt.fillColor = Color.Lerp(new Color(0.22f, 0.28f, 0.36f, 0.95f), new Color(0.45f, 0.53f, 0.64f, 0.95f), k);
                pt.strokeColor = new Color(0.65f, 0.75f, 0.88f, 0.5f);
                float x0 = (r.xMin + HabitatInterior.W / 2f) * RoomPx, x1 = (r.xMax + HabitatInterior.W / 2f) * RoomPx;
                float y0 = (HabitatInterior.D / 2f - r.yMax) * RoomPx, y1 = (HabitatInterior.D / 2f - r.yMin) * RoomPx;
                pt.BeginPath(); pt.MoveTo(new Vector2(x0, y0)); pt.LineTo(new Vector2(x1, y0)); pt.LineTo(new Vector2(x1, y1)); pt.LineTo(new Vector2(x0, y1)); pt.ClosePath();
                pt.Fill(); pt.Stroke();
            }
        }

        void TickInside()
        {
            if (furniture.Count == 0) ScanFurniture();
            var target = m.Ui.Target;
            foreach (var it in Interactable.All)
            {
                if (it == null || !it.Indoor || it.Label == null) continue;
                if (!roomIcons.TryGetValue(it, out var ic))
                {
                    var box = new VisualElement(); box.style.position = Position.Absolute; box.style.flexDirection = FlexDirection.Row; box.style.alignItems = Align.Center;
                    var dot = new VisualElement(); dot.style.width = 8; dot.style.height = 8; dot.style.backgroundColor = it.MarkerColor;
                    dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius = dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = 4;
                    box.Add(dot);
                    var lbl = new Label(); lbl.style.fontSize = 10; lbl.style.marginLeft = 2; lbl.style.color = new Color(0.85f, 0.9f, 1f);
                    box.Add(lbl);
                    room.Add(box);
                    roomIcons[it] = ic = (box, lbl);
                }
                bool next = it == target, mark = m.Marks.Contains(it);
                var p = RoomPos(it.transform.position);
                ic.box.style.left = p.x - 4; ic.box.style.top = p.y - 6;
                ic.lbl.text = next ? "NEXT: " + it.Label : it.Label;
                ic.lbl.style.color = next ? new Color(1f, 0.6f, 0.2f) : mark ? it.MarkerColor : new Color(0.85f, 0.9f, 1f, 0.85f);
                ic.lbl.style.unityFontStyleAndWeight = next || mark ? FontStyle.Bold : FontStyle.Normal;
                Ui.Show(ic.box, it.isActiveAndEnabled);
                if (next) ic.box.BringToFront();
            }
            foreach (var gone in new List<Interactable>(roomIcons.Keys))
                if (gone == null || !Interactable.All.Contains(gone)) { roomIcons[gone].box.RemoveFromHierarchy(); roomIcons.Remove(gone); }

            var pp = RoomPos(m.Player.transform.position);
            insidePlayer.style.left = pp.x - 8; insidePlayer.style.top = pp.y - 8;
            insidePlayer.style.rotate = new Rotate(Angle.Degrees(m.Player.transform.eulerAngles.y));
            insidePlayer.BringToFront();
        }

        // ── Public API ──────────────────────────────────────────────────────

        /// <summary>Call once after Mission1.Launch() has finished setting up the world.</summary>
        public void Init(VisualElement hudRoot, Mission1 mission)
        {
            m = mission;
            BuildUi(hudRoot);
            BuildInsideUi(hudRoot);
            RegisterIcons();
        }

        /// <summary>Call every frame from Mission1.Update() while in Operations phase.</summary>
        public void Tick()
        {
            if (m == null || m.World == null || m.Player == null) return;

            if (mapContainer != null && mapContainer.parent != null) Ui.Show(mapContainer.parent, !m.Model.Inside);
            Ui.Show(insideWrap, m.Model.Inside);
            if (m.Model != null && m.Model.Inside) { TickInside(); return; }
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

            // ── Player marks (outdoor) ─────────────────────────────────
            foreach (var mk in m.Marks)
            {
                if (mk.Indoor) continue;
                if (!markDots.TryGetValue(mk, out var md)) markDots[mk] = md = MakeDot(mapContainer, 11, mk.MarkerColor, "⚑", mk.Label);
                PlaceDot(md, mk.transform.position, playerPos);
            }
            foreach (var gone in new List<Interactable>(markDots.Keys))
                if (gone == null || !m.Marks.Contains(gone)) { markDots[gone].RemoveFromHierarchy(); markDots.Remove(gone); }

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

            var titleLbl = new Label("MAP  (hold M = full map)");
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
