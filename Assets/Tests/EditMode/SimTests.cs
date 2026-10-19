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
}
