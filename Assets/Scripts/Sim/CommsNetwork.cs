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
        /// <summary>The outpost starts with a faulty antenna and no link: fix the fault, then establish the link. Needed to reach Earth and Gateway.</summary>
        public bool Connected => LinkEstablished && Fault == Fault.None && m.Load("COM")?.Powered == true;
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

            Nodes.Add(new CommsNode { Id = "EARTH", Name = "Earth (Mission Control)", Kind = NodeKind.Earth, Type = "The people helping you from Earth", Services = "Talking, base messages, science" });
            Nodes.Add(new CommsNode { Id = "R1", Name = "Satellite R-1", Kind = NodeKind.Relay, Type = "A satellite flying around the Moon", Services = "Passes your messages on to Earth" });
            Nodes.Add(new CommsNode { Id = "GW", Name = "Space station", Kind = NodeKind.Station, Type = "A space station around the Moon", Services = "Sends supply drones (Tab > Call for supplies)" });
            Nodes.Add(new CommsNode { Id = "OUT", Name = "Your base", Kind = NodeKind.Outpost, Type = "Your home on the surface", Services = "Talking, base messages, science" });
            if (rover() != null) Nodes.Add(new CommsNode { Id = "ROVER", Name = "Rover", Kind = NodeKind.Rover, Type = "Your space car", Services = "Short-range radio: works only if no hill is in the way" });
            for (int i = 0; i < sites.Length; i++)
                Nodes.Add(new CommsNode { Id = "SITE" + i, Name = "Site " + siteNames[i] + (i == CraterIndex ? " (low ground)" : ""), Kind = NodeKind.Site, Type = "A place to explore", Services = "Short-range radio: works only if no hill is in the way" });
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
                if (!powered) { s.State = LinkState.Unavailable; s.Reason = "The radio has no power."; }
                else if (c < 0f)
                {
                    s.State = LinkState.Unavailable; s.HasBlock = true;
                    s.Reason = $"A hill is in the way, {Vector3.Distance(pa, s.Block):0} m from the antenna.";
                }
                else
                {
                    s.RateMbps = c < 3f ? 0.256f : 2f; s.LatencyS = Vector3.Distance(pa, pb) / 299792458f;
                    s.State = c < 3f ? LinkState.Weak : LinkState.Verified;
                    s.Reason = c < 3f ? "Only just clear of the hill: the signal is weak." : "Nothing in the way.";
                }
            }

            // Outpost <-> Earth, direct (S-band)
            var e1 = Add("OUT", "EARTH", "Base straight to Earth");
            float cl = Ray(ant, MoonWorld.EarthAzimuth, MoonWorld.EarthElevation, 500f, out e1.Block);
            if (!powered)
            {
                e1.State = LinkState.Unavailable; e1.Reason = "The radio has no power.";
            }
            else if (cl < 0f)
            {
                e1.State = LinkState.Unavailable; e1.HasBlock = true;
                e1.Reason = $"A hill blocks the way to Earth, {Vector3.Distance(ant, e1.Block):0} m from the antenna.";
            }
            else
            {
                float loss = 12f * Mathf.Pow(PointingErrDeg / 9.5f, 2f) + CableLossDb + (cl < 3f ? 6f : 0f);
                Pick(e1, 22f + 30f - Fspl(384400f, 2200f) + 228.6f - 60f - loss, 10f);
                e1.LatencyS = 1.3f; Grade(e1, 1f, cl);
                e1.Reason = e1.RateMbps <= 0f ? "The signal is too weak to reach Earth." :
                    loss < 1f ? "Nothing in the way. Strong signal!" :
                    "The signal is weak: " + (PointingErrDeg > 1f ? "the antenna is not pointing at Earth. Press 'Fix the aim'." :
                    CableLossDb > 1f ? "the antenna cable is loose. Press 'Fix the cable'." : "a hill is almost in the way.");
            }

            // Outpost <-> relay R-1 (window-limited)
            var r1 = Add("OUT", "R1", "Base to satellite R-1");
            float rel = RelayEl(h), rc = rel > 3f ? Ray(ant, RelayAz(h), rel, 500f, out r1.Block) : -99f;
            if (!powered)
            {
                r1.State = LinkState.Unavailable; r1.Reason = "The radio has no power.";
            }
            else if (rc < 0f)
            {
                r1.HasBlock = rel > 3f; r1.NextWindowH = NextWindow(ant, h);
                r1.State = r1.NextWindowH >= 0f ? LinkState.Waiting : LinkState.Unavailable;
                string win = r1.NextWindowH >= 0f ? $" It comes back in {r1.NextWindowH:0.0} hours." : " It won't come back today.";
                r1.Reason = (rel <= 3f ? "R-1 is below the horizon right now." : "A hill hides R-1.") + win;
            }
            else
            {
                Pick(r1, 22f + 8f - Fspl(RelayKm(h), 2200f) + 228.6f - 60f - CableLossDb - (rc < 3f ? 6f : 0f), 10f);
                r1.LatencyS = 0.02f; Grade(r1, 1f, rc);
                r1.Reason = $"R-1 is up in the sky, {RelayKm(h):0} km away. It can hear you!";
            }

            var tr = Add("R1", "EARTH", "Satellite R-1 to Earth");
            tr.State = LinkState.Verified; tr.RateMbps = 4f; tr.LatencyS = 1.3f;
            tr.Reason = "The satellite can always talk to Earth.";

            var gw = Add("GW", "OUT", "Space station");
            gw.State = LinkState.Unavailable;
            gw.Reason = "This space station is not built yet.";

            for (int i = 0; i < SitePos.Length; i++)
                Surface("OUT", "SITE" + i, "Base to site " + SiteName[i], ant, SitePos[i] + Vector3.up * SiteAntH);

            var rv = RoverPos();
            if (rv.HasValue) Surface("OUT", "ROVER", "Base to rover", ant, rv.Value + Vector3.up * SiteAntH);

            if (CraterIndex >= 0)
            {
                var sp = SitePos[CraterIndex] + Vector3.up * SiteAntH;
                var cr = Add("SITE" + CraterIndex, "R1", "Low site to satellite R-1");
                float cc = rel > 3f ? Ray(sp, RelayAz(h), rel, 300f, out cr.Block) : -99f;
                if (cc < 0f)
                {
                    cr.HasBlock = rel > 3f; cr.NextWindowH = NextWindow(sp, h);
                    cr.State = cr.NextWindowH >= 0f ? LinkState.Waiting : LinkState.Unavailable;
                    cr.Reason = (rel <= 3f ? "R-1 is below the horizon." : "The crater wall hides R-1.") +
                                (cr.NextWindowH >= 0f ? $" It comes back in {cr.NextWindowH:0.0} hours." : " Put a relay radio on the rim.");
                }
                else { cr.RateMbps = 1f; cr.LatencyS = 0.02f; cr.State = LinkState.Verified; cr.Reason = "It can see R-1!"; }
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
            return $"Radio check: power {(m.Load("COM")?.Powered == true ? "OK" : "OFF (turn it on with Tab)")}, " +
                   $"aim {(PointingErrDeg > 1.5f ? "WRONG (press Fix the aim)" : "OK")}, " +
                   $"cable {(CableLossDb > 1f ? "LOOSE (press Fix the cable)" : "OK")}, " +
                   $"view of Earth {(e != null && !e.HasBlock ? "clear" : "BLOCKED by a hill")}.";
        }

        public string Rehome()
        {
            if (Fault != Fault.Tracker) return "The aim was already fine.";
            Fault = Fault.None; PointingErrDeg = 0f; AntennaCondition = 1f; Evaluate(); Persist();
            return "Fixed! The antenna points at Earth again.";
        }

        public string Reseat()
        {
            if (Fault != Fault.Connector) return "The cable was already fine.";
            Fault = Fault.None; CableLossDb = 0f; AntennaCondition = 1f; Evaluate(); Persist();
            return "Fixed! The cable is plugged in tight.";
        }

        public string Establish()
        {
            if (m.Load("COM")?.Powered != true) return "Can't connect: the radio has no power. Turn it on with Tab.";
            if (Fault != Fault.None) return "Can't connect: something is wrong with the antenna. Press 'Check the radio' to find out what.";
            if (BestRateMbps <= 0f) return "There is no way to reach Earth right now. Look at 'Ways to reach Earth' to see why.";
            LinkEstablished = true;
            return $"Connected to Mission Control {(ViaDirect ? "straight to Earth" : "through satellite R-1")}!";
        }

        public string SendPacket()
        {
            if (!LinkEstablished) return "Connect to Earth first.";
            if (BestRateMbps <= 0f) return "No connection right now. Your message will wait and go later.";
            PacketAcked = true; EarthLinkVerified = true; Persist();
            return $"Mission Control says hello back! (It took {2f * LatencyBest + 0.4f:0.0} seconds there and back.)";
        }

        public string Check(string svc)
        {
            if (!LinkEstablished || BestRateMbps <= 0f) return "Connect to Earth first.";
            float need = svc == "voice" ? 0.032f : svc == "tlm" ? 0.256f : 1f;
            string nm = svc == "voice" ? "Talking" : svc == "tlm" ? "Base messages" : "Science data";
            if (BestRateMbps < need)
                return $"{nm} did NOT work: the signal is too weak. Press 'Check the radio'.";
            if (svc == "voice") VoiceOk = true; else if (svc == "tlm") TlmOk = true; else SciOk = true;
            return $"{nm} works!";
        }

        public string FindRelaySite()
        {
            if (CraterIndex < 0) return "There are no places to check.";
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
                ? "Found a good spot for a relay radio! It can see the base, the low site and R-1."
                : "No perfect spot yet. Visit more places or try again later.";
        }

        public string SaveResults()
        {
            if (!EarthLinkVerified || !VoiceOk) return "Not saved yet: send a hello message and test talking first.";
            CommunicationsTestComplete = true; Persist();
            return $"Saved! {Links.Count(l => l.State == LinkState.Verified)} ways work, " +
                   $"{Links.Count(l => l.State == LinkState.Unavailable)} are blocked" +
                   $"{(RelaySiteIdentified ? ", and you found a relay spot" : "")}.";
        }

        /// <summary>Call from the sample-site interaction: a radio check at that location.</summary>
        public void SurveyAt(string site)
        {
            int i = Array.IndexOf(SiteName, site); if (i < 0 || !Enabled) return;
            Evaluate(); var l = Link("OUT", "SITE" + i); surveyed.Add(site);
            TerrainSurveyComplete = surveyed.Count >= 2;
            m.Log(l.State == LinkState.Verified ? Severity.Good : Severity.Warning,
                $"Radio check at site {site}: " + (l.State == LinkState.Verified ? "you can talk to the base from here. " : "the base can't hear you well from here. ") + l.Reason);
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
