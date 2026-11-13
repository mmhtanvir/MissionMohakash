using System.Collections.Generic;
using System.Linq;
using Mohakash;
using NUnit.Framework;

public class SimTests
{
    [Test]
    public void RecommendedLoadoutMatchesGdd()
    {
        var k = Catalog.Recommended();
        Assert.AreEqual(522f, k.Mass, 0.01f);   // GDD §11.1
        Assert.AreEqual(125f, k.Cost, 0.01f);
        Assert.IsTrue(k.WithinLimits);
    }

    [Test]
    public void SimpleLoadoutDefaultFitsAndCanWin()
    {
        var k = Catalog.Simple(new[] { "rover" });
        Assert.IsTrue(k.WithinLimits, $"{k.Mass} kg / {k.Cost} Cr");
        var m = new MissionModel(k);
        m.DeploySeismometer(100f, UnityEngine.Vector3.zero);
        m.Advance(0.1f);
        Assert.IsTrue(m.LogRadiationReading());
        Live(m, MissionModel.DurationH);
        Assert.IsFalse(m.Failed, m.FailCause);
        Assert.AreEqual(3, m.PrimaryDone);
    }

    [Test]
    public void EveryExtraFitsOnItsOwn()
    {
        foreach (var e in Catalog.Extras) Assert.IsTrue(Catalog.Simple(new[] { e.Key }).WithinLimits, e.Name);
    }

    [Test]
    public void RequiredItemsCannotBeRemoved()
    {
        var k = Catalog.Recommended();
        k.Set("HAB", 0);
        Assert.AreEqual(1, k.Count("HAB"));
    }

    [Test]
    public void OgsTurnsWaterIntoOxygenAtGddRatio()
    {
        var m = new MissionModel(Catalog.Recommended()) { Ogs = OgsMode.On };
        m.Battery = m.BatteryCap; m.CabinO2 = 1f;
        float w0 = m.Water;
        m.Advance(1f);
        Assert.AreEqual(0.1125f + 0.125f, w0 - m.Water, 0.01f);   // OGS 1.125 L/kg * 0.1 kg/h + crew 0.125 L/h
        Assert.AreEqual(0.10f, m.O2Produced, 0.005f);
    }

    [Test]
    public void PowerShortageShedsLowestPriorityFirst()
    {
        var k = Catalog.Recommended(); k.Set("SOL-S", 1);
        var m = new MissionModel(k) { Ogs = OgsMode.Off };
        m.SeisDeployed = true; m.SeisDistance = 100f;
        m.Hour = 10f; m.Battery = 0f;          // ~0.95 PW of sun, empty battery
        m.Advance(0.2f);
        Assert.IsTrue(m.Load("HAB").Powered);
        Assert.IsTrue(m.Load("SCR").Powered);
        Assert.IsFalse(m.Load("SUI").Powered);  // priority 2 does not fit
        Assert.IsFalse(m.Load("COM").Powered, "a lower priority must not run while a higher one is shed");
        Assert.IsFalse(m.Load("SEIS").Powered);
    }

    [Test]
    public void ShadowWindowHasNoSun()
    {
        Assert.AreEqual(0f, MissionModel.SunAt(MissionModel.ShadowStart + 1f));
        Assert.Greater(MissionModel.SunAt(10f), 0.8f);
    }

    [Test]
    public void NoScrubberLetsCo2BuildUp()
    {
        var k = Catalog.RequiredOnly();
        var m = new MissionModel(k);
        m.Advance(20f);
        Assert.Greater(m.CO2, 0.6f);
        Assert.Less(m.Health, 100f);
    }

    [Test]
    public void SuitDrainsOutsideAndRefillsInside()
    {
        var m = new MissionModel(Catalog.Recommended());
        m.Inside = false;
        m.Advance(1f);
        Assert.AreEqual(MissionModel.SuitCO2Max - 1f, m.SuitCO2, 0.02f);
        m.EnterHabitat();
        m.Advance(1f);
        Assert.AreEqual(MissionModel.SuitCO2Max, m.SuitCO2, 0.01f);
    }

    [Test]
    public void SeismometerNeedsDistanceAndPowerFor24h()
    {
        var m = new MissionModel(Catalog.Recommended());
        m.DeploySeismometer(50f, UnityEngine.Vector3.zero);
        m.Advance(5f);
        Assert.AreEqual(0f, m.SeisGoodHours, 0.001f);   // too close: does not count
        m.PickUpSeismometer();
        m.DeploySeismometer(95f, UnityEngine.Vector3.zero);
        m.Advance(5f);
        Assert.Greater(m.SeisGoodHours, 4.9f);
    }

    [Test]
    public void RecommendedPlanSurvivesFullMissionWhenPlayerStaysInside()
    {
        var m = new MissionModel(Catalog.Recommended());
        m.DeploySeismometer(100f, UnityEngine.Vector3.zero);
        m.Inside = true;
        m.Advance(0.1f);
        Assert.IsTrue(m.LogRadiationReading());
        Live(m, MissionModel.DurationH + 0.1f);
        Assert.IsTrue(m.Ended);
        Assert.IsFalse(m.Failed, m.FailCause);
        Assert.AreEqual(3, m.PrimaryDone);
        var r = new MissionReport(m);
        Assert.GreaterOrEqual(r.Score, 50);
    }

    [Test]
    public void InventorySlotsStackAndWeighLimits()
    {
        var inv = new Inventory();
        var rock = Inventory.Sample;
        var a = new ItemDef { Id = "A", Kg = 0.1f }; var b = new ItemDef { Id = "B", Kg = 0.1f }; var c = new ItemDef { Id = "C", Kg = 0.1f };
        for (int i = 0; i < 12; i++) Assert.IsNull(inv.Add(rock));                  // 10 + 2: same item opens a second slot
        Assert.AreEqual(2, inv.Slots.Count);
        Assert.IsNull(inv.Add(a)); Assert.IsNull(inv.Add(b));                        // 4 pocket slots used
        Assert.IsNotNull(inv.Add(c));                                                // no 5th slot without the bag
        Assert.IsNull(inv.DropBag());                                                // pockets fit, no bag to drop is fine
        inv.HasBag = true;
        Assert.IsNull(inv.Add(c));
        for (int i = 0; i < 7; i++) inv.Add(rock);                                   // 19 kg + 0.3 kg
        Assert.IsNotNull(inv.Add(rock));                                             // 20 kg limit
        Assert.IsNotNull(inv.DropBag());                                             // 5 slots don't fit in 4 pockets
        Assert.AreEqual(19, inv.Remove(rock));
    }

    [Test]
    public void GatewayDeliversAfterDelayAndLimitsCalls()
    {
        var m = new MissionModel(Catalog.Recommended());
        var water = Resupply.Items[0]; var battery = Resupply.Items.First(i => i.Name.StartsWith("Battery"));
        m.Water = 5f; float cap = m.BatteryCap;
        Assert.IsNotNull(m.Gateway.Request(m, new List<SupplyItem> { water }));            // comms start disconnected
        m.Net.LinkEstablished = true;                                                       // (fault is only rolled by Net.Init)
        m.Load("COM").Enabled = true; m.Advance(0.1f);                                     // COM terminal powered, as from the power panel
        Assert.IsNotNull(m.Gateway.Request(m, new List<SupplyItem> { battery, water }));   // 35 kg > drone
        Assert.IsNull(m.Gateway.Request(m, new List<SupplyItem> { water }));
        m.Advance(Resupply.DelayH - 0.5f);
        Assert.Less(m.Water, 10f);
        m.Advance(1f);
        Assert.Greater(m.Water, 10f);
        Assert.IsNull(m.Gateway.Request(m, new List<SupplyItem> { battery }));
        m.Advance(Resupply.DelayH + 0.1f);
        Assert.AreEqual(cap + 10f, m.BatteryCap);
        Assert.IsNull(m.Gateway.Request(m, new List<SupplyItem> { water }));
        Assert.IsNotNull(m.Gateway.Request(m, new List<SupplyItem> { water }));            // 4th call refused
    }

    /// <summary>Advances like a player who eats and drinks when allowed (hunger/hydration otherwise kill by ~h40).</summary>
    static void Live(MissionModel m, float hours)
    {
        for (float t = 0f; t < hours; t += 1f)
        {
            m.Advance(UnityEngine.Mathf.Min(1f, hours - t));
            m.EatMeal(out _); m.Drink(out _);
        }
    }

    [Test]
    public void FailedMissionScoreIsCappedAt40()
    {
        var m = new MissionModel(Catalog.RequiredOnly());
        m.Advance(MissionModel.DurationH);
        Assert.IsTrue(m.Failed);
        Assert.LessOrEqual(new MissionReport(m).Score, 40);
    }

    [Test]
    public void OutdoorO2PostRefillsSuitFromCabin()
    {
        var m = new MissionModel(Catalog.Recommended());
        Assert.IsFalse(m.RefillSuitOutside(out _), "inside: refused");
        Assert.IsTrue(m.ExitHabitat());
        m.SuitO2 = 1f; m.SuitBatt = 1f; m.SuitCO2 = 1f;
        float cabin = m.CabinO2;
        Assert.IsTrue(m.RefillSuitOutside(out _));
        Assert.AreEqual(MissionModel.SuitO2Max, m.SuitO2, 0.001f);
        Assert.AreEqual(cabin - (MissionModel.SuitO2Max - 1f) * 0.06f, m.CabinO2, 0.001f);
        m.SuitO2 = 0f; m.CabinO2 = 0.6f;
        Assert.IsFalse(m.RefillSuitOutside(out _), "cabin keeps its reserve");
    }

    [Test]
    public void RemoteRoverDrainsByDistanceAndChargesOnDock()
    {
        var m = new MissionModel(Catalog.Recommended());
        Assert.IsTrue(m.UseRemoteEnergy(1000f));
        Assert.AreEqual(100f - 1000f * MissionModel.RemotePctPerMeter, m.RemoteBattery, 0.01f);
        m.UseRemoteEnergy(5000f);
        Assert.AreEqual(0f, m.RemoteBattery);
        Assert.IsFalse(m.UseRemoteEnergy(1f), "empty: cannot move");
        m.RemoteDocked = true;
        m.Advance(4f);
        Assert.AreEqual(100f, m.RemoteBattery, 0.5f, "full in ~4 h on the dock");
    }

    /// <summary>Every level runs start to finish with the default loadout when the player stays inside and fixes what breaks.</summary>
    [Test]
    public void EveryLevelIsSurvivableStayingInside([NUnit.Framework.Range(1, 6)] int level)
    {
        Game.Level = level;
        try
        {
            var lv = Levels.Current;
            var m = new MissionModel(Catalog.Simple(new[] { "rover" })) { Inside = true };
            if (lv.Mars) m.Ogs = OgsMode.On;
            for (float t = 0f; t < MissionModel.DurationH + 0.1f && !m.Ended; t += 1f)
            {
                m.Advance(1f);
                m.EatMeal(out _); m.Drink(out _);
                foreach (var id in m.Broken.Keys.ToList()) m.StartRepair(id);
                if (m.Dust > 0.2f) m.CleanArrays();
            }
            Assert.IsTrue(m.Ended, $"level {level} never ended");
            Assert.IsFalse(m.Failed, $"level {level}: {m.FailCause}");
            Assert.AreEqual(lv.Faults.Length, m.FaultsFixed, "every storm fault repaired");
            Assert.GreaterOrEqual(Levels.PrimaryTotal, 1);
        }
        finally { Game.Level = 1; }
    }

    /// <summary>Every level has its own sample / seismometer spots: gentle ground, a real walk from base, never shared or crowded.</summary>
    [Test]
    public void LevelSitesAreUniqueAndReachable()
    {
        var all = new List<(string who, UnityEngine.Vector2 xz)>();
        foreach (var lv in Levels.All)
        {
            foreach (var (n, xz) in lv.SampleSites) all.Add(($"L{lv.N} sample {n}", xz));
            if (lv.SeisSite is UnityEngine.Vector2 ss) all.Add(($"L{lv.N} seismometer site", ss));
        }
        var bad = new List<string>();
        try
        {
            foreach (var (who, xz) in all)
            {
                Game.Level = int.Parse(who.Substring(1, 1));   // slope on that level's own landing site
                float slope = SiteRules.SlopeDeg(xz.x, xz.y, 6f);
                if (slope >= SiteRules.MaxSlopeDeg) bad.Add($"{who} at ({xz.x},{xz.y}) slope {slope:0.0}");
                Assert.That(xz.magnitude, Is.InRange(SiteRules.MinFromBase, SiteRules.MaxFromBase), who + " distance from base");
            }
            foreach (var lv in Levels.All)
            {
                Assert.LessOrEqual(lv.Lander.magnitude, 38f, $"L{lv.N} lander must sit in the flattened outpost zone");
                for (int k = 0; k < lv.N - 1; k++)
                    Assert.IsFalse(Levels.All[k].LandOffset == lv.LandOffset && Levels.All[k].LandYaw == lv.LandYaw, $"L{lv.N} reuses L{k + 1}'s landing site");
            }
        }
        finally { Game.Level = 1; }
        Assert.IsEmpty(bad, "steep sites:\n" + string.Join("\n", bad));
        for (int i = 0; i < all.Count; i++)
            for (int j = i + 1; j < all.Count; j++)
                Assert.GreaterOrEqual(UnityEngine.Vector2.Distance(all[i].xz, all[j].xz), SiteRules.MinSpacing, $"{all[i].who} and {all[j].who} overlap");
    }

    /// <summary>Each level's outpost yard: nothing overlaps the habitat cross, the airlock approach, the lander pad or each other,
    /// and no two levels share the same yard.</summary>
    [Test]
    public void OutpostYardsDontOverlapAndDiffer()
    {
        var seen = new List<string>();
        foreach (var lv in Levels.All)
        {
            float pz(float z) => lv.PowerNorth ? -z : z;
            float gz(float z) => lv.GarageSouth ? -z : z;
            var items = new List<(string name, UnityEngine.Vector2 at, float r)>
            {
                ("OGS", new UnityEngine.Vector2(-14, pz(-6)), 3.5f), ("COM", new UnityEngine.Vector2(-6, pz(-8)), 2f), ("RAD", new UnityEngine.Vector2(6, pz(-8)), 1.5f),
                ("TNK-W", new UnityEngine.Vector2(-16, pz(8)), 1.2f),
                ("garage", new UnityEngine.Vector2(2, gz(26)), 6.5f), ("rover", new UnityEngine.Vector2(9, gz(17)), 3f), ("RC-1 dock", new UnityEngine.Vector2(14, gz(17)), 2f),
                ("storage", lv.Storage, 3f), ("lander pad", lv.Lander, 10f),
                ("seismometer crate", new UnityEngine.Vector2(9, 5), 1f), ("O2 tanker", new UnityEngine.Vector2(19.2f, 4.5f), 1.5f), ("light mast", new UnityEngine.Vector2(14, 12), 0.5f),
            };
            for (int k = 0; k < 3; k++) items.Add(($"battery {k + 1}", new UnityEngine.Vector2(-13 - k * 2.2f, pz(-12)), 1.2f));
            for (int k = 0; k < 2; k++) items.Add(($"O2 tank {k + 1}", new UnityEngine.Vector2(-16, pz(3 + k * 1.8f)), 1f));
            for (int k = 0; k < 4; k++) items.Add(($"solar array {k + 1}", lv.SolarStart + k * lv.SolarStep, 2f));

            foreach (var (n, at, r) in items)
            {
                if (n == "O2 tanker" || n == "seismometer crate") continue;   // these two sit beside the habitat on purpose
                bool inCross = (UnityEngine.Mathf.Abs(at.y) < 2.4f + r && at.x > -11.3f - r && at.x < 10.4f + r) || (UnityEngine.Mathf.Abs(at.x) < 2.4f + r && UnityEngine.Mathf.Abs(at.y) < 11.2f + r);
                bool inAirlock = at.x > 10f - r && at.x < 21f + r && UnityEngine.Mathf.Abs(at.y) < 4f + r;
                Assert.IsFalse(inCross || inAirlock, $"L{lv.N} {n} at {at} is on the habitat or the airlock approach");
            }
            for (int a = 0; a < items.Count; a++)
                for (int b = a + 1; b < items.Count; b++)
                    if (items[a].name.TrimEnd(' ', '1', '2', '3', '4') != items[b].name.TrimEnd(' ', '1', '2', '3', '4'))   // a bank's own modules sit side by side
                    Assert.GreaterOrEqual(UnityEngine.Vector2.Distance(items[a].at, items[b].at), items[a].r + items[b].r,
                        $"L{lv.N} {items[a].name} {items[a].at} overlaps {items[b].name} {items[b].at}");

            string key = $"{lv.PowerNorth}{lv.GarageSouth}{lv.Storage}{lv.SolarStart}{lv.SolarStep}";
            Assert.IsFalse(seen.Contains(key), $"L{lv.N} repeats another level's yard");
            seen.Add(key);
        }
    }
}
