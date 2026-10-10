using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mohakash
{
    /// <summary>
    /// Hold M: full-screen tactical map of the whole play area.
    /// Terrain is a hill-shaded relief image with contour lines and a 100 m grid, generated from the real terrain heights.
    /// On top: the cyan walk-back circle (it SHRINKS live as your EVA time drops), the 80 m seismometer-noise ring,
    /// every marked site, the habitat, lander, rover, your path and a heading arrow.
    /// </summary>
    public class FullMapSystem : MonoBehaviour
    {
        const float R = 480f;            // map shows x,z in [-R, R] metres (play radius is 460)
        const int   Px = 760;            // map size in UI pixels (reference 1920x1080)
        const int   TexN = 512;

        Mission1 m;
        VisualElement overlay, mapEl, layer;
        Label info;
        Texture2D tex;
        float fade;
        readonly List<Label> labels = new List<Label>();
        readonly List<Vector3> trail = new List<Vector3>();

        static readonly Color Cyan = new Color(0.30f, 0.90f, 1f);
        static readonly Color Orange = new Color(1f, 0.58f, 0.18f);

        public void Init(VisualElement root, Mission1 mission)
        {
            m = mission;
            overlay = new VisualElement { name = "full-map" };
            overlay.pickingMode = PickingMode.Ignore;
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0; overlay.style.top = 0; overlay.style.right = 0; overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0.01f, 0.02f, 0.04f, 0.80f);
            overlay.style.flexDirection = FlexDirection.Row;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.alignItems = Align.Center;
            overlay.style.display = DisplayStyle.None;
            overlay.style.opacity = 0f;
            root.Add(overlay);

            mapEl = new VisualElement { name = "full-map-image" };
            mapEl.pickingMode = PickingMode.Ignore;
            mapEl.style.width = Px; mapEl.style.height = Px;
            mapEl.style.flexShrink = 0;
            Border(mapEl, 2f, new Color(0.35f, 0.55f, 0.75f, 0.95f));
            overlay.Add(mapEl);

            layer = new VisualElement();
            layer.pickingMode = PickingMode.Ignore;
            layer.style.position = Position.Absolute;
            layer.style.left = 0; layer.style.top = 0; layer.style.width = Px; layer.style.height = Px;
            layer.generateVisualContent += Draw;
            mapEl.Add(layer);

            // edge coordinates every 200 m
            for (int v = -400; v <= 400; v += 200)
            {
                var lx = StaticLabel(v == 0 ? "0" : (v > 0 ? "+" + v : v.ToString()), 9, new Color(1, 1, 1, 0.55f));
                lx.style.left = ToPx(v, 0).x - 12; lx.style.top = Px - 14; lx.style.width = 24; lx.style.unityTextAlign = TextAnchor.MiddleCenter;
                var lz = StaticLabel(v == 0 ? "0" : (v > 0 ? "+" + v : v.ToString()), 9, new Color(1, 1, 1, 0.55f));
                lz.style.left = 3; lz.style.top = ToPx(0, v).y - 7;
            }
            var north = StaticLabel("N", 16, new Color(1, 1, 1, 0.9f));
            north.style.left = Px / 2 - 5; north.style.top = 4; north.style.unityFontStyleAndWeight = FontStyle.Bold;

            // side panel
            var side = new VisualElement();
            side.pickingMode = PickingMode.Ignore;
            side.style.width = 300; side.style.marginLeft = 24; side.style.flexShrink = 0;
            overlay.Add(side);
            side.Add(Text("MISSION MAP", 24, Color.white, true));
            side.Add(Text("Hold M to view. Release to return.", 12, new Color(0.7f, 0.78f, 0.9f), false));
            info = Text("", 14, new Color(0.88f, 0.92f, 1f), false);
            info.style.marginTop = 16; info.style.whiteSpace = WhiteSpace.Normal;
            side.Add(info);
            side.Add(Legend(Cyan, "Walk-back range (shrinks as your EVA time runs down)"));
            side.Add(Legend(Orange, "Seismometer noise limit (80 m from habitat)"));
            side.Add(Legend(new Color(0.3f, 1f, 0.5f), "Habitat / airlock"));
            side.Add(Legend(new Color(1f, 0.9f, 0.25f), "Rover"));
            side.Add(Legend(new Color(0.75f, 0.55f, 1f), "Lander"));
            side.Add(Legend(Color.white, "You (arrow = facing) and your path"));
        }

        // ── per-frame ───────────────────────────────────────────────────────────
        public void Tick()
        {
            if (m == null || m.World == null || m.Player == null) return;

            // record path
            var p = m.Player.transform.position;
            if (trail.Count == 0 || (trail[trail.Count - 1] - p).sqrMagnitude > 4f) { trail.Add(p); if (trail.Count > 400) trail.RemoveAt(0); }

            bool want = GameInput.MapHeld && !m.Model.Ended;
            fade = Mathf.MoveTowards(fade, want ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            if (fade <= 0f) { overlay.style.display = DisplayStyle.None; return; }
            if (overlay.resolvedStyle.display == DisplayStyle.None || overlay.style.display == DisplayStyle.None)
            {
                if (tex == null) { tex = BuildTexture(); mapEl.style.backgroundImage = new StyleBackground(tex); }
                overlay.style.display = DisplayStyle.Flex;
            }
            overlay.style.opacity = fade;

            float wb = m.WalkBackRadius;
            info.text =
                $"You are {m.DistanceToHab:0} m from the airlock.\n" +
                $"EVA time left: {m.Model.EvaLeft:0.0} h\n" +
                $"Safe walk-back range: {wb:0} m\n\n" +
                (m.Model.Inside ? "You are inside the habitat." :
                 m.DistanceToHab > wb ? "WARNING: you are outside the safe range. Head back now." :
                 "Stay inside the cyan circle and you can always walk home.");
            ApplyLabels();
            layer.MarkDirtyRepaint();
        }

        // ── drawing ─────────────────────────────────────────────────────────────
        Vector2 ToPx(float x, float z) => new Vector2((x + R) / (2f * R) * Px, (R - z) / (2f * R) * Px);
        Vector2 ToPx(Vector3 w) => ToPx(w.x, w.z);
        float MToPx(float metres) => metres / (2f * R) * Px;

        void Draw(MeshGenerationContext mgc)
        {
            if (m == null || m.World == null) return;
            var pt = mgc.painter2D;
            pending.Clear();

            // play-area boundary
            pt.lineWidth = 1.5f; pt.strokeColor = new Color(1, 1, 1, 0.35f);
            Circle(pt, ToPx(0, 0), MToPx(MoonWorld.PlayRadius), false, true);

            // seismometer noise ring
            if (m.Model.SeisCarried || (m.Kit != null && m.Kit.Has("SEIS") && !m.Model.SeisDeployed))
            {
                pt.lineWidth = 2.5f; pt.strokeColor = Orange; pt.fillColor = new Color(Orange.r, Orange.g, Orange.b, 0.07f);
                Circle(pt, ToPx(m.World.Hab.position), MToPx(MissionModel.SeisMinDistance), true, true);
                Tag("noise limit 80 m", ToPx(m.World.Hab.position) + new Vector2(0, -MToPx(MissionModel.SeisMinDistance) - 14), Orange, true);
            }

            // cyan walk-back circle (shrinks live)
            if (!m.Model.Inside)
            {
                float wb = Mathf.Max(2f, m.WalkBackRadius);
                var c = ToPx(m.World.AirlockPoint);
                pt.lineWidth = 3.5f; pt.strokeColor = Cyan; pt.fillColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.10f);
                Circle(pt, c, MToPx(wb), true, true);
                Tag($"walk-back {wb:0} m", c + new Vector2(0, MToPx(wb) + 4), Cyan, true);
            }

            // path
            if (trail.Count > 1)
            {
                pt.lineWidth = 2f; pt.strokeColor = new Color(1, 1, 1, 0.55f);
                pt.BeginPath(); pt.MoveTo(ToPx(trail[0]));
                for (int i = 1; i < trail.Count; i++) pt.LineTo(ToPx(trail[i]));
                pt.Stroke();
            }

            // scale bar (100 m)
            var sb = new Vector2(14, Px - 28);
            pt.lineWidth = 3; pt.strokeColor = Color.white;
            pt.BeginPath(); pt.MoveTo(sb); pt.LineTo(sb + new Vector2(MToPx(100f), 0)); pt.Stroke();
            Tag("100 m", sb + new Vector2(0, -14), Color.white, false);

            // sites from the mission's interactables
            foreach (var it in Interactable.All)
            {
                if (it == null || !it.isActiveAndEnabled || string.IsNullOrEmpty(it.Label)) continue;
                if (it.ShowMarker != null && !it.ShowMarker()) continue;
                var c = ToPx(it.transform.position);
                Marker(pt, c, 6f, it.MarkerColor);
                Tag(it.Label, c + new Vector2(9, -7), it.MarkerColor, false);
            }

            // habitat, lander, rover
            var hc = ToPx(m.World.Hab.position);
            Marker(pt, hc, 9f, new Color(0.3f, 1f, 0.5f)); Tag("HAB", hc + new Vector2(12, -8), new Color(0.3f, 1f, 0.5f), false);
            var lc = ToPx(m.World.OnGround(-30f, 18f));
            Marker(pt, lc, 7f, new Color(0.75f, 0.55f, 1f)); Tag("Lander", lc + new Vector2(10, -8), new Color(0.75f, 0.55f, 1f), false);
            if (m.Rover != null)
            {
                var rc = ToPx(m.Rover.transform.position);
                Marker(pt, rc, 6f, new Color(1f, 0.9f, 0.25f)); Tag("Rover", rc + new Vector2(9, -7), new Color(1f, 0.9f, 0.25f), false);
            }

            // player arrow
            var pp = ToPx(m.ActivePosition);
            float yaw = (m.Rover != null && m.Rover.Driving ? m.Rover.transform.eulerAngles.y : m.Player.transform.eulerAngles.y) * Mathf.Deg2Rad;
            var fwd = new Vector2(Mathf.Sin(yaw), -Mathf.Cos(yaw));     // +z is up on the map, so screen-y is flipped
            var rgt = new Vector2(-fwd.y, fwd.x);
            pt.fillColor = Color.white; pt.strokeColor = new Color(0, 0, 0, 0.9f); pt.lineWidth = 1.5f;
            pt.BeginPath();
            pt.MoveTo(pp + fwd * 13f); pt.LineTo(pp - fwd * 8f + rgt * 8f); pt.LineTo(pp - fwd * 4f); pt.LineTo(pp - fwd * 8f - rgt * 8f);
            pt.ClosePath(); pt.Fill(); pt.Stroke();
            Tag("YOU", pp + new Vector2(11, 6), Color.white, false);

        }

        static void Circle(Painter2D pt, Vector2 c, float r, bool fill, bool stroke)
        {
            pt.BeginPath();
            pt.Arc(c, r, Angle.Degrees(0), Angle.Degrees(360));
            pt.ClosePath();
            if (fill) pt.Fill();
            if (stroke) pt.Stroke();
        }

        static void Marker(Painter2D pt, Vector2 c, float r, Color col)
        {
            pt.fillColor = col; pt.strokeColor = new Color(0, 0, 0, 0.85f); pt.lineWidth = 1.5f;
            Circle(pt, c, r, true, true);
        }

        // Text labels are UI elements, which must not be created inside generateVisualContent.
        // Draw() only queues them; Tick() applies the queue on the next frame (1 frame lag, invisible).
        struct Pending { public string text; public Vector2 at; public Color col; public bool centred; }
        readonly List<Pending> pending = new List<Pending>();

        void Tag(string text, Vector2 at, Color col, bool centred)
            => pending.Add(new Pending { text = text, at = at, col = col, centred = centred });

        void ApplyLabels()
        {
            int used = 0;
            foreach (var pd in pending)
            {
                if (used >= labels.Count)
                {
                    var l = StaticLabel("", 11, Color.white);
                    l.style.unityFontStyleAndWeight = FontStyle.Bold;
                    l.style.textShadow = new TextShadow { color = new Color(0, 0, 0, 0.9f), offset = new Vector2(1, 1), blurRadius = 2f };
                    labels.Add(l);
                }
                var lab = labels[used++];
                lab.text = pd.text; lab.style.color = pd.col; lab.style.display = DisplayStyle.Flex;
                lab.style.left = pd.centred ? pd.at.x - 60f : pd.at.x; lab.style.top = pd.at.y;
                if (pd.centred) { lab.style.width = 120f; lab.style.unityTextAlign = TextAnchor.UpperCenter; }
                else { lab.style.width = StyleKeyword.Auto; lab.style.unityTextAlign = TextAnchor.UpperLeft; }
            }
            for (int i = used; i < labels.Count; i++) labels[i].style.display = DisplayStyle.None;
            pending.Clear();
        }

        // ── terrain relief image ────────────────────────────────────────────────
        Texture2D BuildTexture()
        {
            var w = m.World;
            var px = new Color32[TexN * TexN];
            float step = 2f * R / TexN;
            float hmin = float.MaxValue, hmax = float.MinValue;
            var hs = new float[TexN * TexN];
            for (int y = 0; y < TexN; y++)
            for (int x = 0; x < TexN; x++)
            {
                float h = w.GridHeight(-R + (x + 0.5f) * step, -R + (y + 0.5f) * step);
                hs[y * TexN + x] = h;
                if (h < hmin) hmin = h; if (h > hmax) hmax = h;
            }
            var L = new Vector3(-0.55f, 0.55f, 0.62f).normalized;           // low light from the NW, like the polar sun
            for (int y = 0; y < TexN; y++)
            for (int x = 0; x < TexN; x++)
            {
                int xm = Mathf.Max(0, x - 1), xp = Mathf.Min(TexN - 1, x + 1), ym = Mathf.Max(0, y - 1), yp = Mathf.Min(TexN - 1, y + 1);
                float gx = (hs[y * TexN + xp] - hs[y * TexN + xm]) / ((xp - xm) * step);
                float gz = (hs[yp * TexN + x] - hs[ym * TexN + x]) / ((yp - ym) * step);
                var n = new Vector3(-gx, 1f, -gz).normalized;
                float shade = Mathf.Clamp01(0.38f + 0.9f * Mathf.Max(0f, Vector3.Dot(n, L)) - 0.25f);
                float h = hs[y * TexN + x];
                float t = Mathf.InverseLerp(hmin, hmax, h);
                var baseC = Color.Lerp(new Color(0.26f, 0.25f, 0.24f), new Color(0.74f, 0.71f, 0.66f), t);
                var c = baseC * (0.55f + shade);

                // contour lines every 4 m, stronger every 20 m
                float slope = Mathf.Sqrt(gx * gx + gz * gz) + 0.03f;
                float f4 = Mathf.Repeat(h, 4f); float d4 = Mathf.Min(f4, 4f - f4);
                float f20 = Mathf.Repeat(h, 20f); float d20 = Mathf.Min(f20, 20f - f20);
                float wline = Mathf.Max(0.04f, slope * step * 0.6f);
                if (d20 < wline * 1.3f) c *= 0.55f; else if (d4 < wline) c *= 0.80f;

                float wx = -R + (x + 0.5f) * step, wz = -R + (y + 0.5f) * step;
                // 100 m grid
                float gxm = Mathf.Abs(Mathf.Repeat(wx + 50f, 100f) - 50f), gzm = Mathf.Abs(Mathf.Repeat(wz + 50f, 100f) - 50f);
                if (gxm < step * 0.6f || gzm < step * 0.6f) c = Color.Lerp(c, new Color(0.7f, 0.85f, 1f), 0.20f);

                // outside the play radius
                if (wx * wx + wz * wz > MoonWorld.PlayRadius * MoonWorld.PlayRadius) c = Color.Lerp(c, new Color(0.05f, 0.07f, 0.12f), 0.65f);
                c.a = 1f;
                px[y * TexN + x] = c;
            }
            var t2 = new Texture2D(TexN, TexN, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t2.SetPixels32(px); t2.Apply(false);
            return t2;
        }

        // ── small UI helpers ────────────────────────────────────────────────────
        static void Border(VisualElement e, float w, Color c)
        {
            e.style.borderTopWidth = e.style.borderRightWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = w;
            e.style.borderTopColor = e.style.borderRightColor = e.style.borderBottomColor = e.style.borderLeftColor = c;
        }

        Label StaticLabel(string t, int size, Color col)
        {
            var l = new Label(t);
            l.pickingMode = PickingMode.Ignore;
            l.style.position = Position.Absolute; l.style.fontSize = size; l.style.color = col;
            mapEl.Add(l);
            return l;
        }

        static Label Text(string t, int size, Color col, bool bold)
        {
            var l = new Label(t);
            l.pickingMode = PickingMode.Ignore;
            l.style.fontSize = size; l.style.color = col;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return l;
        }

        static VisualElement Legend(Color c, string text)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.marginTop = 8;
            var dot = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.style.width = 12; dot.style.height = 12; dot.style.backgroundColor = c; dot.style.marginRight = 8; dot.style.flexShrink = 0;
            dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius = dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = 6;
            row.Add(dot);
            var l = Text(text, 12, new Color(0.85f, 0.9f, 1f), false); l.style.whiteSpace = WhiteSpace.Normal; l.style.flexShrink = 1;
            row.Add(l);
            return row;
        }
    }
}
