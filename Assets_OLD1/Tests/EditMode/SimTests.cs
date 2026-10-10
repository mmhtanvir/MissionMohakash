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
        m.Advance(MissionModel.DurationH);
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
        m.Advance(MissionModel.DurationH + 0.1f);
        Assert.IsTrue(m.Ended);
        Assert.IsFalse(m.Failed, m.FailCause);
        Assert.AreEqual(3, m.PrimaryDone);
        var r = new MissionReport(m);
        Assert.GreaterOrEqual(r.Score, 50);
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
