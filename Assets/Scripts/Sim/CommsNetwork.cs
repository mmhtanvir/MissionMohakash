using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mohakash
{
    public enum NodeKind { Earth, Relay, Station, Outpost, Rover, Site }
    public enum LinkState { Verified, Weak, Unavailable, Waiting }
    public enum Fault { None, Tracker, Connector }

    public class CommsNode { public string Id, Name, Type, Services; public NodeKind Kind; public bool InService = true; }

    public class CommsLink
    {
        public string A, B, Name, Reason = ""; public LinkState State;
        public float SnrDb, MarginDb, RateMbps, LatencyS, NextWindowH = -1f;
        public Vector3 Block; public bool HasBlock;
    }

    public class QueueItem { public string Name, Kind; public float Kb; }

    /// <summary>Earth / relay / surface network. Illustrative link budgets, real terrain line-of-sight. Not live spacecraft tracking.</summary>
    public class CommsNetwork
    {
        public bool Enabled;
        // ---- persisted for Mission 2 (see CommsSave)
        public bool EarthLinkVerified, CommunicationsTestComplete, TerrainSurveyComplete, RelaySiteIdentified;
        public float AntennaCondition = 1f;
        public float CommsSystemHealth => Mathf.Clamp01(0.5f * AntennaCondition + 0.3f * (CableLossDb < 1f ? 1f : 0.4f) + 0.2f * (m.Load("COM")?.Powered == true ? 1f : 0f));
        public Vector3 RelaySite;
        // ---- scenario fault and test progress
        public Fault Fault; public float PointingErrDeg, CableLossDb;
        public bool Inspected, PathsReviewed, QualityViewed, MapOpened, LinkEstablished, PacketAcked, VoiceOk, TlmOk, SciOk;
        // ---- network state
        public readonly List<CommsNode> Nodes = new List<CommsNode>();
        public readonly List<CommsLink> Links = new List<CommsLink>();
        public readonly List<QueueItem> Queue = new List<QueueItem>();
        public readonly List<string> Log = new List<string>();
        public int Delivered, Version, CraterIndex = -1;
        public float BestRateMbps, LatencyBest = 1.3f; public bool ViaDirect;
        public Vector3 Antenna; public Vector3[] SitePos = new Vector3[0]; public string[] SiteName = new string[0];
        public Func<Vector3, float> Height = p => 0f;
        public Func<Vector3?> RoverPos = () => null;

        readonly MissionModel m; readonly HashSet<string> surveyed = new HashSet<string>();
        float evalT, telemAcc, lastHour; int seenData;
        const float RelayPeriodH = 12f, MastH = 5f, SiteAntH = 1.5f;
        static readonly float[] Rates = { 0.032f, 0.256f, 1f, 4f, 10f };

        public CommsNetwork(MissionModel model) { m = model; }

        public static float RelayAz(float h) => 60f + 100f * Mathf.Sin(h / RelayPeriodH * Mathf.PI * 2f);
        public static float RelayEl(float h) => 30f * Mathf.Sin(h / RelayPeriodH * Mathf.PI * 2f + 0.6f);
        static float RelayKm(float h) => 2500f + 2000f * (1f + Mathf.Sin(h / RelayPeriodH * Mathf.PI * 2f + 1.5f));
        static float Fspl(float km, float mhz) => 20f * Mathf.Log10(km) + 20f * Mathf.Log10(mhz) + 32.44f;
        public CommsLink Link(string a, string b) => Links.Find(l => (l.A == a && l.B == b) || (l.A == b && l.B == a));

        public void Init(Vector3 antennaBase, Func<Vector3, float> height, string[] siteNames, Vector3[] sites, Func<Vector3?> rover)
        {
            Antenna = antennaBase; Height = height; SiteName = siteNames; SitePos = sites; RoverPos = rover;
            CraterIndex = 0;
            for (int i = 1; i < sites.Length; i++) if (height(sites[i]) < height(sites[CraterIndex])) CraterIndex = i;

            Fault = UnityEngine.Random.value < 0.5f ? Fault.Tracker : Fault.Connector;
            if (Fault == Fault.Tracker) { PointingErrDeg = UnityEngine.Random.Range(5f, 9f); AntennaCondition = 0.6f; }
            else { CableLossDb = 5f; AntennaCondition = 0.7f; }

            Nodes.Add(new CommsNode { Id = "EARTH", Name = "Earth / Mission Control", Kind = NodeKind.Earth, Type = "Earth ground network", Services = "Voice, status telemetry, science-data routing" });
            Nodes.Add(new CommsNode { Id = "R1", Name = "Relay R-1", Kind = NodeKind.Relay, Type = "Lunar relay spacecraft (elliptical orbit)", Services = "Store-and-forward relay, S-band to the surface, X-band trunk to Earth" });
            Nodes.Add(new CommsNode { Id = "GW", Name = "Gateway-class station", Kind = NodeKind.Station, Type = "Lunar-orbiting station (planned)", Services = "None in this scenario", InService = false });
            Nodes.Add(new CommsNode { Id = "OUT", Name = "First Light Outpost", Kind = NodeKind.Outpost, Type = "Surface station", Services = "Voice, telemetry, science upload; surface UHF" });
            if (rover() != null) Nodes.Add(new CommsNode { Id = "ROVER", Name = "Rover", Kind = NodeKind.Rover, Type = "Surface station (mobile)", Services = "UHF to the outpost, line of sight only" });
            for (int i = 0; i < sites.Length; i++)
                Nodes.Add(new CommsNode { Id = "SITE" + i, Name = "Site " + siteNames[i] + (i == CraterIndex ? " (crater proxy)" : ""), Kind = NodeKind.Site, Type = "Survey point", Services = "UHF line of sight only" });
            Enabled = true; lastHour = m.Hour; Evaluate();
        }

        // ---- terrain line of sight: minimum clearance above terrain along the ray ----
        float Ray(Vector3 from, float az, float el, float range, out Vector3 worst)
        {
            var dir = Quaternion.Euler(-el, az, 0f) * Vector3.forward; float min = float.MaxValue; worst = from;
            for (float d = 8f; d <= range; d += 8f)
            {
                var p = from + dir * d; float g = Height(p), c = p.y - g;
                if (c < min) { min = c; worst = new Vector3(p.x, g, p.z); }
            }
            return min;
        }

        float Seg(Vector3 a, Vector3 b, out Vector3 worst)
        {
            float len = Vector3.Distance(a, b), min = float.MaxValue; worst = a;
            for (float d = 8f; d < len; d += 8f)
            {
                var p = Vector3.Lerp(a, b, d / len); float g = Height(p), c = p.y - g;
                if (c < min) { min = c; worst = new Vector3(p.x, g, p.z); }
            }
            return min;
        }

        float NextWindow(Vector3 from, float h)
        {
            for (float t = 0.25f; t <= 24f; t += 0.25f)
            {
                float e = RelayEl(h + t);
                if (e > 3f && Ray(from, RelayAz(h + t), e, 500f, out _) >= 0f) return t;
            }
            return -1f;
        }

        // ---- link budget ----
        static void Pick(CommsLink L, float ebn0At1Mbps, float maxRate)
        {
            L.RateMbps = 0f;
            foreach (var r in Rates)
            {
                if (r > maxRate) break;
                float e = ebn0At1Mbps - 10f * Mathf.Log10(r);
                if (e - 2.5f >= 3f) { L.RateMbps = r; L.SnrDb = e; }
            }
            if (L.RateMbps == 0f)
            {
                float e = ebn0At1Mbps - 10f * Mathf.Log10(Rates[0]); L.SnrDb = e;
                if (e >= 2.5f) L.RateMbps = Rates[0];
            }
            L.MarginDb = L.SnrDb - 2.5f;
        }

        public void Evaluate()
        {
            Links.Clear(); Version++;
            float h = m.Hour; bool powered = m.Load("COM")?.Powered == true;
            var ant = Antenna + Vector3.up * MastH;

            CommsLink Add(string a, string b, string nm) { var l = new CommsLink { A = a, B = b, Name = nm }; Links.Add(l); return l; }
            void Grade(CommsLink l, float goodRate, float clear) =>
                l.State = l.RateMbps <= 0f ? LinkState.Unavailable : (l.RateMbps < goodRate || clear < 3f) ? LinkState.Weak : LinkState.Verified;
            void Surface(string a, string b, string nm, Vector3 pa, Vector3 pb)
            {
                var s = Add(a, b, nm); float c = Seg(pa, pb, out s.Block);
                if (!powered) { s.State = LinkState.Unavailable; s.Reason = "COM terminal has no power."; }
                else if (c < 0f)
                {
                    s.State = LinkState.Unavailable; s.HasBlock = true;
                    s.Reason = $"Terrain blocks the path: a ridge rises {-c:0.0} m above the line of sight, {Vector3.Distance(pa, s.Block):0} m from the antenna.";
                }
                else
                {
                    s.RateMbps = c < 3f ? 0.256f : 2f; s.LatencyS = Vector3.Distance(pa, pb) / 299792458f;
                    s.State = c < 3f ? LinkState.Weak : LinkState.Verified;
                    s.Reason = c < 3f ? "Grazing clearance: reduced rate." : "Clear line of sight.";
                }
            }

            // Outpost <-> Earth, direct (S-band)
            var e1 = Add("OUT", "EARTH", "Direct to Earth (S-band)");
            float cl = Ray(ant, MoonWorld.EarthAzimuth, MoonWorld.EarthElevation, 500f, out e1.Block);
            if (!powered)
            {
                e1.State = LinkState.Unavailable; e1.Reason = "COM terminal has no power.";
            }
            else if (cl < 0f)
            {
                e1.State = LinkState.Unavailable; e1.HasBlock = true;
                e1.Reason = $"Terrain blocks the path to Earth: a ridge rises {-cl:0.0} m above the line of sight, {Vector3.Distance(ant, e1.Block):0} m from the antenna.";
            }
            else
            {
                float loss = 12f * Mathf.Pow(PointingErrDeg / 9.5f, 2f) + CableLossDb + (cl < 3f ? 6f : 0f);
                Pick(e1, 22f + 30f - Fspl(384400f, 2200f) + 228.6f - 60f - loss, 10f);
                e1.LatencyS = 1.3f; Grade(e1, 1f, cl);
                e1.Reason = e1.RateMbps <= 0f ? "Signal below threshold." :
                    loss < 1f ? "Clear line of sight, nominal link budget." :
                    $"{loss:0.0} dB of loss: " + (PointingErrDeg > 1f ? $"antenna {PointingErrDeg:0.0} deg off target." :
                    CableLossDb > 1f ? $"feed connector loss {CableLossDb:0.0} dB." : "grazing terrain clearance.");
            }

            // Outpost <-> relay R-1 (window-limited)
            var r1 = Add("OUT", "R1", "Outpost to relay R-1 (S-band)");
            float rel = RelayEl(h), rc = rel > 3f ? Ray(ant, RelayAz(h), rel, 500f, out r1.Block) : -99f;
            if (!powered)
            {
                r1.State = LinkState.Unavailable; r1.Reason = "COM terminal has no power.";
            }
            else if (rc < 0f)
            {
                r1.HasBlock = rel > 3f; r1.NextWindowH = NextWindow(ant, h);
                r1.State = r1.NextWindowH >= 0f ? LinkState.Waiting : LinkState.Unavailable;
                string win = r1.NextWindowH >= 0f ? $" Next window in {r1.NextWindowH:0.0} h." : " No window in the next 24 h.";
                r1.Reason = (rel <= 3f ? "R-1 is below the local horizon (orbit geometry)." : $"Terrain blocks R-1 ({-rc:0.0} m above the line of sight).") + win;
            }
            else
            {
                Pick(r1, 22f + 8f - Fspl(RelayKm(h), 2200f) + 228.6f - 60f - CableLossDb - (rc < 3f ? 6f : 0f), 10f);
                r1.LatencyS = 0.02f; Grade(r1, 1f, rc);
                r1.Reason = $"R-1 at {rel:0} deg elevation, {RelayKm(h):0} km: link up.";
            }

            var tr = Add("R1", "EARTH", "Relay trunk to Earth (X-band)");
            tr.State = LinkState.Verified; tr.RateMbps = 4f; tr.LatencyS = 1.3f;
            tr.Reason = "The relay spacecraft keeps its own Earth link.";

            var gw = Add("GW", "OUT", "Gateway-class station");
            gw.State = LinkState.Unavailable;
            gw.Reason = "Planned lunar-orbiting station: not in service in this scenario.";

            for (int i = 0; i < SitePos.Length; i++)
                Surface("OUT", "SITE" + i, "Outpost to site " + SiteName[i] + " (UHF)", ant, SitePos[i] + Vector3.up * SiteAntH);

            var rv = RoverPos();
            if (rv.HasValue) Surface("OUT", "ROVER", "Outpost to rover (UHF)", ant, rv.Value + Vector3.up * SiteAntH);

            if (CraterIndex >= 0)
            {
                var sp = SitePos[CraterIndex] + Vector3.up * SiteAntH;
                var cr = Add("SITE" + CraterIndex, "R1", "Crater site to relay R-1 (direct)");
                float cc = rel > 3f ? Ray(sp, RelayAz(h), rel, 300f, out cr.Block) : -99f;
                if (cc < 0f)
                {
                    cr.HasBlock = rel > 3f; cr.NextWindowH = NextWindow(sp, h);
                    cr.State = cr.NextWindowH >= 0f ? LinkState.Waiting : LinkState.Unavailable;
                    cr.Reason = (rel <= 3f ? "R-1 is below the horizon." : "The crater wall blocks R-1.") +
                                (cr.NextWindowH >= 0f ? $" Next window in {cr.NextWindowH:0.0} h." : " Deploy a relay on the rim.");
                }
                else { cr.RateMbps = 1f; cr.LatencyS = 0.02f; cr.State = LinkState.Verified; cr.Reason = "Direct view of R-1."; }
            }

            var rl = Link("OUT", "R1");
            float rd = e1.State == LinkState.Verified || e1.State == LinkState.Weak ? e1.RateMbps : 0f;
            float rr = rl != null && (rl.State == LinkState.Verified || rl.State == LinkState.Weak) ? Mathf.Min(rl.RateMbps, 4f) : 0f;
            ViaDirect = rd >= rr; BestRateMbps = Mathf.Max(rd, rr); LatencyBest = ViaDirect ? 1.3f : 1.32f;
        }

        // ---- per-frame: link refresh + store-and-forward data queue ----
        public void Update(float dtReal)
        {
            if (!Enabled) return;
            float dtH = Mathf.Max(0f, m.Hour - lastHour); lastHour = m.Hour;
            evalT += dtReal; if (evalT >= 0.5f) { evalT = 0f; Evaluate(); }
            telemAcc += dtH; if (telemAcc >= 0.1f) { telemAcc = 0f; Queue.Add(new QueueItem { Name = "Status telemetry", Kind = "TLM", Kb = 40f }); }
            while (seenData < m.Data.Count) { var d = m.Data[seenData++]; Queue.Add(new QueueItem { Name = d.Instrument + ": " + d.What, Kind = "SCI", Kb = 500f }); }
            if (BestRateMbps > 0f && Queue.Count > 0)
            {
                float kb = BestRateMbps * 125f * 3600f * dtH * 0.5f;
                while (Queue.Count > 0 && kb >= Queue[0].Kb) { kb -= Queue[0].Kb; Queue.RemoveAt(0); Delivered++; }
                if (Queue.Count > 0) Queue[0].Kb -= kb;
            }
        }

        // ---- workstation actions ----
        public string Inspect()
        {
            Inspected = true; var e = Link("OUT", "EARTH");
            return $"Terminal {(m.Load("COM")?.Powered == true ? "powered" : "NO POWER")}; antenna {AntennaCondition:0%}; " +
                   $"feed loss {CableLossDb:0.0} dB; tracker error {PointingErrDeg:0.0} deg; " +
                   $"line of sight {(e != null && !e.HasBlock ? "clear" : "OBSTRUCTED")}; " +
                   $"SNR {e?.SnrDb:0.0} dB, margin {e?.MarginDb:0.0} dB, {e?.RateMbps:0.###} Mbps.";
        }

        public string Rehome()
        {
            if (Fault != Fault.Tracker) return "Tracker re-homed: no change, no pointing fault found.";
            Fault = Fault.None; PointingErrDeg = 0f; AntennaCondition = 1f; Evaluate(); Persist();
            return "Tracker re-homed: pointing error 0.0 deg. Auto-tracking nominal.";
        }

        public string Reseat()
        {
            if (Fault != Fault.Connector) return "Feed connector reseated: no change, cable loss was already nominal.";
            Fault = Fault.None; CableLossDb = 0f; AntennaCondition = 1f; Evaluate(); Persist();
            return "Feed connector reseated: loss 0.0 dB.";
        }

        public string Establish()
        {
            if (m.Load("COM")?.Powered != true) return "Cannot establish: the COM terminal has no power.";
            if (BestRateMbps <= 0f) return "No path to Earth right now. See the paths view for the reason and next window.";
            LinkEstablished = true;
            return $"Link established with Mission Control via {(ViaDirect ? "direct-to-Earth" : "relay R-1")}.";
        }

        public string SendPacket()
        {
            if (!LinkEstablished) return "Establish the link first.";
            if (BestRateMbps <= 0f) return "Link is down: packet queued for the next contact.";
            PacketAcked = true; EarthLinkVerified = true; Persist();
            return $"Telemetry packet acknowledged by Mission Control (round trip {2f * LatencyBest + 0.4f:0.0} s).";
        }

        public string Check(string svc)
        {
            if (!LinkEstablished || BestRateMbps <= 0f) return "No established link: cannot test.";
            float need = svc == "voice" ? 0.032f : svc == "tlm" ? 0.256f : 1f;
            string nm = svc == "voice" ? "Two-way voice" : svc == "tlm" ? "Status telemetry" : "Science-data upload";
            if (BestRateMbps < need)
                return $"{nm} FAILED: link supports {BestRateMbps:0.###} Mbps, needs {need:0.###}. Diagnose the link (pointing, connector, terrain).";
            if (svc == "voice") VoiceOk = true; else if (svc == "tlm") TlmOk = true; else SciOk = true;
            return $"{nm} verified at {BestRateMbps:0.###} Mbps.";
        }

        public string FindRelaySite()
        {
            if (CraterIndex < 0) return "No survey sites.";
            var cp = SitePos[CraterIndex]; var ant = Antenna + Vector3.up * MastH;
            Vector3 best = cp; int bestScore = -1;
            for (int ix = -6; ix <= 6; ix++)
                for (int iz = -6; iz <= 6; iz++)
                {
                    var p = cp + new Vector3(ix * 15f, 0f, iz * 15f); p.y = Height(p);
                    var top = p + Vector3.up * 3f; int score = 0;
                    if (Seg(ant, top, out _) >= 0f) score++;
                    if (Seg(cp + Vector3.up * SiteAntH, top, out _) >= 0f) score++;
                    for (float t = 0f; t < RelayPeriodH; t += 1f)
                    {
                        float e = RelayEl(m.Hour + t);
                        if (e > 3f && Ray(top, RelayAz(m.Hour + t), e, 400f, out _) >= 0f) { score++; break; }
                    }
                    score = score * 100 - (int)(Vector3.Distance(p, cp) / 10f);
                    if (score > bestScore) { bestScore = score; best = p; }
                }
            RelaySite = best; RelaySiteIdentified = bestScore >= 250; Persist();
            return RelaySiteIdentified
                ? $"Relay site identified at x {best.x:0}, z {best.z:0}: sees the outpost, the crater floor and R-1."
                : $"Best candidate x {best.x:0}, z {best.z:0} still misses a link. Survey more sites or wait for a window.";
        }

        public string SaveResults()
        {
            if (!EarthLinkVerified || !VoiceOk) return "Not saved: verify the Earth link and two-way voice first.";
            CommunicationsTestComplete = true; Persist();
            return $"Link map saved for Mission 2: {Links.Count(l => l.State == LinkState.Verified)} verified, " +
                   $"{Links.Count(l => l.State == LinkState.Unavailable)} unavailable, " +
                   $"relay site {(RelaySiteIdentified ? "identified" : "NOT identified")}.";
        }

        /// <summary>Call from the sample-site interaction: a radio check at that location.</summary>
        public void SurveyAt(string site)
        {
            int i = Array.IndexOf(SiteName, site); if (i < 0 || !Enabled) return;
            Evaluate(); var l = Link("OUT", "SITE" + i); surveyed.Add(site);
            TerrainSurveyComplete = surveyed.Count >= 2;
            m.Log(l.State == LinkState.Verified ? Severity.Good : Severity.Warning,
                $"Radio check at site {site}: direct link to the outpost is {l.State.ToString().ToUpper()}. {l.Reason}" +
                (i == CraterIndex ? " (lowest ground: Mission 2 crater proxy)" : ""));
            Persist();
        }

        void Persist() => CommsSave.Save(this);
    }

    /// <summary>Campaign state for Mission 2 (PlayerPrefs, same store as your scores).</summary>
    public static class CommsSave
    {
        public static void Save(CommsNetwork n)
        {
            PlayerPrefs.SetInt("c_earthLinkVerified", n.EarthLinkVerified ? 1 : 0);
            PlayerPrefs.SetInt("c_commsTestComplete", n.CommunicationsTestComplete ? 1 : 0);
            PlayerPrefs.SetInt("c_terrainSurveyComplete", n.TerrainSurveyComplete ? 1 : 0);
            PlayerPrefs.SetInt("c_relaySiteIdentified", n.RelaySiteIdentified ? 1 : 0);
            PlayerPrefs.SetFloat("c_antennaCondition", n.AntennaCondition);
            PlayerPrefs.SetFloat("c_commsSystemHealth", n.CommsSystemHealth);
            PlayerPrefs.SetFloat("c_relayX", n.RelaySite.x);
            PlayerPrefs.SetFloat("c_relayZ", n.RelaySite.z);
            PlayerPrefs.Save();
        }

        public static bool EarthLinkVerified => PlayerPrefs.GetInt("c_earthLinkVerified", 0) == 1;
        public static bool CommunicationsTestComplete => PlayerPrefs.GetInt("c_commsTestComplete", 0) == 1;
        public static bool TerrainSurveyComplete => PlayerPrefs.GetInt("c_terrainSurveyComplete", 0) == 1;
        public static bool RelaySiteIdentified => PlayerPrefs.GetInt("c_relaySiteIdentified", 0) == 1;
        public static float AntennaCondition => PlayerPrefs.GetFloat("c_antennaCondition", 1f);
        public static float CommsSystemHealth => PlayerPrefs.GetFloat("c_commsSystemHealth", 1f);
        public static Vector2 RelaySiteXZ => new Vector2(PlayerPrefs.GetFloat("c_relayX", 0f), PlayerPrefs.GetFloat("c_relayZ", 0f));
    }
}
