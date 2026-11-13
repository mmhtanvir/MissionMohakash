using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mohakash
{
    /// <summary>Mission-control style workstation: orbital view, local terrain view, link diagnostics, data queue, event log.</summary>
    public class CommsWorkstation : MonoBehaviour
    {
        const float W = 680f, H = 500f;
        Mission1 m; CommsNetwork n;
        VisualElement overlay, map; Label info, queueLbl, logLbl, scaleLbl;
        readonly Dictionary<string, Button> btn = new Dictionary<string, Button>();
        bool local, showPaths; string sel = "OUT"; float tick;
        public bool Open { get; private set; }

        static readonly Color Green = new Color(0.3f,  0.95f, 0.55f);
        static readonly Color Amber = new Color(1f,    0.75f, 0.2f);
        static readonly Color Red   = new Color(1f,    0.3f,  0.3f);
        static readonly Color Grey  = new Color(0.6f,  0.65f, 0.7f);

        public void Init(VisualElement root, Mission1 mission)
        {
            m = mission; n = m.Model.Net;

            overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0; overlay.style.top = 0; overlay.style.right = 0; overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0.01f, 0.02f, 0.04f, 0.93f);
            overlay.style.flexDirection = FlexDirection.Row;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.alignItems = Align.Center;
            overlay.style.display = DisplayStyle.None;
            root.Add(overlay);

            var left = Ui.Box(overlay, "col");
            Ui.Text(left, "RADIO DESK: TALK TO EARTH", "h1");
            Ui.Text(left, "A pretend map that shows how your radio signal gets to Earth.", "small", "muted");
            var tabs = Ui.Box(left, "row");
            Ui.Button(tabs, "Space map",          () => { local = false; showPaths = false; n.MapOpened = true; Refresh(); Check(); });
            Ui.Button(tabs, "Map around the base",         () => { local = true;  showPaths = false; Refresh(); });
            Ui.Button(tabs, "Ways to reach Earth",       () => { showPaths = true; n.PathsReviewed = true; Refresh(); Check(); });
            Ui.Button(tabs, "Places to explore",    () => LunarIntelligenceUi.Show(root));
            Ui.Button(tabs, "Close (Esc)", Close);

            map = new VisualElement();
            map.style.width = W; map.style.height = H;
            map.style.backgroundColor = new Color(0.03f, 0.05f, 0.09f);
            map.generateVisualContent += Draw;
            left.Add(map);
            scaleLbl = Ui.Text(left, "", "small", "muted");

            foreach (var nd in n.Nodes)
            {
                var id = nd.Id;
                var b = new Button(() =>
                {
                    sel = id; showPaths = false;
                    if (id == "EARTH") n.QualityViewed = true;
                    Refresh(); Check();
                }) { text = nd.Name };
                b.style.position = Position.Absolute; b.style.fontSize = 11;
                b.style.paddingLeft = 4; b.style.paddingRight = 4;
                b.style.paddingTop = 1; b.style.paddingBottom = 1;
                map.Add(b); btn[id] = b;
            }

            var right = Ui.Box(overlay, "col");
            right.style.width = 450; right.style.marginLeft = 14;

            info = Ui.Text(right, "", "small");
            info.enableRichText = true; info.style.whiteSpace = WhiteSpace.Normal;

            var acts = Ui.Box(right, "row"); acts.style.flexWrap = Wrap.Wrap;
            Act(acts, "1. Check the radio",                () => n.Inspect());
            Act(acts, "Fix the aim (15 min)",       () => { m.Model.Advance(0.25f); return n.Rehome(); });
            Act(acts, "Fix the cable (15 min)", () => { m.Model.Advance(0.25f); return n.Reseat(); });
            Act(acts, "2. Connect to Earth",                 () => n.Establish());
            Act(acts, "3. Send a hello message",               () => n.SendPacket());
            Act(acts, "4. Test talking",                    () => n.Check("voice"));
            Act(acts, "5. Test base messages",                () => n.Check("tlm"));
            Act(acts, "6. Test science data",             () => n.Check("sci"));
            Act(acts, "Find a relay spot (30 min)",        () => { m.Model.Advance(0.5f); return n.FindRelaySite(); });
            Act(acts, "7. Save the results",              () => n.SaveResults());

            queueLbl = Ui.Text(right, "", "small"); queueLbl.style.whiteSpace = WhiteSpace.Normal;
            logLbl   = Ui.Text(right, "", "small", "muted"); logLbl.style.whiteSpace = WhiteSpace.Normal;
        }

        void Act(VisualElement parent, string text, System.Func<string> f) =>
            Ui.Button(parent, text, () => { n.Log.Add($"h{m.Model.Hour:0.0}  {f()}"); Refresh(); Check(); });

        void Check() => m.Model.Tasks.Do(Spot.Comms, m.Ctx());

        public void OpenUi()
        {
            Open = true;
            overlay.style.display = DisplayStyle.Flex;
            Game.SetCursorFree(true);
            if (!local) n.MapOpened = true;
            Refresh(); Check();
        }

        public void Close()
        {
            Open = false;
            overlay.style.display = DisplayStyle.None;
            Game.SetCursorFree(false);
        }

        void Update()
        {
            if (!Open) return;
            tick += Time.unscaledDeltaTime;
            if (tick > 0.3f) { tick = 0f; Refresh(); }
        }

        // ---- node positions on the canvas ----
        Vector2 Pos(string id)
        {
            float h = m.Model.Hour;
            if (!local)
            {
                switch (id)
                {
                    case "EARTH": return new Vector2(600, 60);
                    case "OUT":   return new Vector2(330, 375);
                    case "R1":    { float a = h / 12f * Mathf.PI * 2f; return new Vector2(330 + 200 * Mathf.Cos(a), 300 + 125 * Mathf.Sin(a)); }
                    case "GW":    return new Vector2(330 + 120 * Mathf.Cos(2.2f), 300 + 215 * Mathf.Sin(2.2f));
                    default:      return new Vector2(-999, -999);
                }
            }
            var c = new Vector2(340, 250); const float s = 1.1f; var ant = n.Antenna;
            Vector2 ToPx(Vector3 w)         => c + new Vector2((w.x - ant.x) * s, -(w.z - ant.z) * s);
            Vector2 Bearing(float az, float r) => c + new Vector2(Mathf.Sin(az * Mathf.Deg2Rad), -Mathf.Cos(az * Mathf.Deg2Rad)) * r;
            if (id == "OUT")   return c;
            if (id == "EARTH") return Bearing(MoonWorld.EarthAzimuth, 270f);
            if (id == "R1")    return CommsNetwork.RelayEl(h) > 0f ? Bearing(CommsNetwork.RelayAz(h), 210f) : new Vector2(-999, -999);
            if (id == "ROVER") { var rv = n.RoverPos(); return rv.HasValue ? ToPx(rv.Value) : new Vector2(-999, -999); }
            if (id.StartsWith("SITE")) return ToPx(n.SitePos[int.Parse(id.Substring(4))]);
            return new Vector2(-999, -999);
        }

        void Refresh()
        {
            if (n == null) return;
            foreach (var nd in n.Nodes)
            {
                var b = btn[nd.Id]; var p = Pos(nd.Id); bool vis = p.x > -900f;
                b.style.display = vis ? DisplayStyle.Flex : DisplayStyle.None;
                b.style.left = p.x - 45; b.style.top = p.y + 8;
                b.style.backgroundColor = sel == nd.Id ? new Color(0.1f, 0.45f, 0.55f) : new Color(0.12f, 0.15f, 0.2f);
                b.style.color = nd.InService ? Color.white : Grey;
            }
            info.text = showPaths ? PathsText() : NodeText();

            var sb = new StringBuilder();
            sb.Append($"<b>MESSAGES</b>: {n.Queue.Count} waiting, {n.Delivered} sent. ");
            sb.Append(n.BestRateMbps > 0f ? "Sending now.\n" : "Can't reach Earth right now. Messages wait, they are not lost.\n");
            queueLbl.enableRichText = true; queueLbl.text = sb.ToString();

            var lg = new StringBuilder("<b>WHAT HAPPENED</b>\n");
            for (int i = Mathf.Max(0, n.Log.Count - 6); i < n.Log.Count; i++) lg.Append(n.Log[i]).Append('\n');
            logLbl.enableRichText = true; logLbl.text = lg.ToString();

            scaleLbl.text = local
                ? "Map around the base, seen from above. North is up. A red X means a hill is in the way."
                : "Space map, not to scale. Earth is really 384,400 km away!";
            map.MarkDirtyRepaint();
        }

        // ---- text panels ----
        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
        static Color StateColor(LinkState s) =>
            s == LinkState.Verified ? Green : s == LinkState.Weak ? Amber : s == LinkState.Waiting ? Grey : Red;

        string LinkText(CommsLink l)
        {
            string word = l.State == LinkState.Verified ? "WORKS" : l.State == LinkState.Weak ? "WEAK" : l.State == LinkState.Waiting ? "WAIT" : "BLOCKED";
            return $"<color=#{Hex(StateColor(l.State))}>{word}</color>  {l.Name}\n    {l.Reason}\n";
        }

        string NodeText()
        {
            var nd = n.Nodes.Find(x => x.Id == sel);
            if (nd == null) return "";
            var sb = new StringBuilder();
            sb.Append($"<b>{nd.Name}</b>\n{nd.Type}{(nd.InService ? "" : "  (not built yet)")}\nWhat it does: {nd.Services}\n\n");
            foreach (var l in n.Links) if (l.A == sel || l.B == sel) sb.Append(LinkText(l));
            return sb.ToString();
        }

        string PathsText()
        {
            var sb = new StringBuilder("<b>WAYS TO REACH EARTH</b>\n");
            foreach (var l in n.Links) sb.Append(LinkText(l));
            return sb.ToString();
        }

        // ---- drawing ----
        static void Circle(Painter2D pt, Vector2 c, float r, bool fill)
        {
            pt.BeginPath(); pt.Arc(c, r, 0f, 360f);
            if (fill) pt.Fill(); pt.Stroke();
        }

        static void Poly(Painter2D pt, System.Func<float, Vector2> f)
        {
            pt.BeginPath();
            for (int i = 0; i <= 72; i++) { var p = f(i / 72f * Mathf.PI * 2f); if (i == 0) pt.MoveTo(p); else pt.LineTo(p); }
            pt.Stroke();
        }

        static void Line(Painter2D pt, Vector2 a, Vector2 b, LinkState s)
        {
            pt.lineWidth = s == LinkState.Verified ? 3f : 2f; pt.strokeColor = StateColor(s);
            float len = Vector2.Distance(a, b); var d = (b - a) / Mathf.Max(len, 1f);
            if (s == LinkState.Verified || s == LinkState.Weak)
            {
                pt.BeginPath(); pt.MoveTo(a); pt.LineTo(b); pt.Stroke(); return;
            }
            // dotted = waiting for window, dashed = unavailable
            float dash = s == LinkState.Waiting ? 3f : 9f, gap = s == LinkState.Waiting ? 7f : 6f;
            for (float t = 0f; t < len; t += dash + gap)
            {
                pt.BeginPath(); pt.MoveTo(a + d * t); pt.LineTo(a + d * Mathf.Min(t + dash, len)); pt.Stroke();
            }
        }

        void Draw(MeshGenerationContext mgc)
        {
            if (n == null) return;
            var pt = mgc.painter2D; float h = m.Model.Hour;

            if (!local)
            {
                pt.lineWidth = 1f; pt.strokeColor = new Color(0.3f, 0.7f, 0.9f, 0.6f);
                Poly(pt, a => new Vector2(330 + 200 * Mathf.Cos(a), 300 + 125 * Mathf.Sin(a)));  // R-1 orbit
                pt.strokeColor = new Color(0.5f, 0.5f, 0.55f, 0.5f);
                Poly(pt, a => new Vector2(330 + 120 * Mathf.Cos(a), 300 + 215 * Mathf.Sin(a)));  // planned station orbit
                pt.fillColor = new Color(0.55f, 0.55f, 0.58f); pt.strokeColor = new Color(0.8f, 0.8f, 0.85f); pt.lineWidth = 2f;
                Circle(pt, new Vector2(330, 300), 75f, true);                                    // Moon disc
            }
            else
            {
                pt.lineWidth = 1f; pt.strokeColor = new Color(1f, 1f, 1f, 0.15f);
                Circle(pt, Pos("OUT"), 110f, false); Circle(pt, Pos("OUT"), 220f, false);
            }

            foreach (var l in n.Links)
            {
                var a = Pos(l.A); var b = Pos(l.B);
                if (a.x < -900f || b.x < -900f) continue;
                Line(pt, a + new Vector2(0, 4), b + new Vector2(0, 4), l.State);
                if (local && l.HasBlock)
                {
                    var bp = Pos("OUT") + new Vector2((l.Block.x - n.Antenna.x) * 1.1f, -(l.Block.z - n.Antenna.z) * 1.1f);
                    pt.lineWidth = 3f; pt.strokeColor = Red;
                    pt.BeginPath(); pt.MoveTo(bp + new Vector2(-6, -6)); pt.LineTo(bp + new Vector2(6, 6));   pt.Stroke();
                    pt.BeginPath(); pt.MoveTo(bp + new Vector2(-6,  6)); pt.LineTo(bp + new Vector2(6, -6));  pt.Stroke();
                }
            }

            if (local && n.RelaySiteIdentified)
            {
                var rp = Pos("OUT") + new Vector2((n.RelaySite.x - n.Antenna.x) * 1.1f, -(n.RelaySite.z - n.Antenna.z) * 1.1f);
                pt.lineWidth = 2.5f; pt.strokeColor = Green; pt.fillColor = new Color(0.3f, 0.95f, 0.55f, 0.3f);
                Circle(pt, rp, 9f, true);
            }

            if (local && n.CraterIndex >= 0)
            {
                pt.lineWidth = 2f; pt.strokeColor = Amber;
                Circle(pt, Pos("SITE" + n.CraterIndex), 16f, false);
            }
        }
    }
}
