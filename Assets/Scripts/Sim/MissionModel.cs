using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mohakash
{
    public enum Severity { Info, Good, Warning, Critical }
    public enum OgsMode { Off, Auto, On }

    public class LogEntry
    {
        public float Hour; public Severity Sev; public string Text;
        public override string ToString() => $"h{Hour:00.0}  {Text}";
    }

    /// <summary>One science record with provenance: what, when, where, which instrument, under which conditions.</summary>
    public class DataRecord
    {
        public float Hour; public string Instrument, What, Where, Conditions;
    }

    public class PowerLoad
    {
        public string Id, Name; public float Pw; public int Priority;
        public bool Enabled = true, Wanted, Powered, Locked;
        public string Why;   // plain-language state reason for the systems panel
    }

    /// <summary>
    /// Mission 1 "First Light" simulation (GDD §8, §13-§18, §23, §25). Pure C#: no scene objects, so it is unit-testable.
    /// One fixed tick = 1 mission minute. All numbers are GAME UNITS unless labelled otherwise in Docs/DECISIONS.md.
    /// </summary>
    public class MissionModel
    {
        // ---- Mission constants (GDD §10 Mission 1) ----
        public const float DurationH = 72f;
        public const float ShadowStart = 40f, ShadowEnd = 46f;   // scripted 6 h shadow window
        public const float SpeStart = 22f, SpeEnd = 25f;         // minor solar particle event
        public const float QuakeHour = 52f;                     // scheduled moonquake window (Apollo-inspired timing, Dataset TBD)
        public const float SeisMinDistance = 80f;               // GDD "8 cells"; 1 cell = 10 m in the 3D world (DECISIONS D3)
        public const float SeisNeededHours = 24f;
        public const float DoseAllowance = 20f;                 // game-mSv per mission (GDD §17)
        public const float RoverPwhPerMeter = 0.012f;           // GDD 0.12 PWh per 10 m cell

        public readonly Loadout Kit;

        // ---- State ----
        public float Hour;
        public float Sun = 1f, Dust;
        public float Supply, Demand;
        public float Battery, BatteryCap, MaxDischarge;
        bool batteryLockout;
        public OgsMode Ogs = OgsMode.Auto;
        public readonly List<PowerLoad> Loads = new List<PowerLoad>();

        public float CabinO2, CabinO2Cap, Water, WaterCap, Food, CO2, CartridgeKg;
        public float O2Produced, O2Consumed, O2Vented, O2ZeroHours;
        public float Health = 100f, Fatigue, Dose;
        public float Hunger = 100f, Hydration = 100f;   // 100 = fed / hydrated, 0 = starving / dehydrated
        public bool Inside = true, Sprinting, Resting, Driving;

        public const float SuitO2Max = 4.5f, SuitBattMax = 4.2f, SuitCO2Max = 4.0f;   // Suit A (GDD §18)
        public float SuitO2 = SuitO2Max, SuitBatt = SuitBattMax, SuitCO2 = SuitCO2Max, SuitIntegrity = 100f, MinIntegrity = 100f;
        public float EvaLeft => Mathf.Min(SuitO2, SuitBatt, SuitCO2);
        public int Spares;

        public float RoverBattery, RoverCap;
        public bool RoverNearGarage;

        public bool SeisCarried, SeisDeployed;
        public Vector3 SeisPos;   // world position of the deployed seismometer (set by DeploySeismometer)
        public float SeisDistance, SeisPoweredHours, SeisGoodHours;
        public bool QuakeRecorded, QuakeMissed;
        public int RadReadings, SamplesDelivered, SampleCapacity;
        public readonly Inventory Inv = new Inventory();
        public readonly Resupply Gateway = new Resupply();
        public int SamplesCarried => Inv.Count(Inventory.Sample);

        public int CriticalEvents, NearFailures;
        public float FarthestFromBase, PrimaryDoneHour = -1f;

        public bool Ended, Failed;

        // ---- MissionTasks ----
        public MissionTasks Tasks;
        public bool SeisInstalled = true;        // false from deployment until the self-test passes
        public float SeisQuality = 1f;           // placement, orientation and slope reduce data quality
        public bool Sheltering;                  // in the shielded corner during a solar particle event
        public float CabinPressureKpa => Mathf.Lerp(40f, 101.3f, Mathf.Clamp01(CabinO2 / 3f));   // derived from cabin O2
        public CommsNetwork Net;
        public string FailCause = "";
        public readonly List<LogEntry> Events = new List<LogEntry>();
        public readonly List<DataRecord> Data = new List<DataRecord>();
        public readonly List<string> CauseChain = new List<string>();
        public System.Action<LogEntry> OnEvent;

        readonly HashSet<string> latched = new HashSet<string>();

        public MissionModel(Loadout kit)
        {
            Kit = kit;
            BatteryCap = 10f * kit.Count("BAT");
            MaxDischarge = 2f * kit.Count("BAT");
            Battery = BatteryCap * 0.6f;
            CabinO2Cap = 4f + 8f * kit.Count("TNK-O");
            CabinO2 = 4.0f;
            WaterCap = 40f + 40f * kit.Count("TNK-W");
            Water = Mathf.Min(WaterCap, 15f + 10f * kit.Count("WATER"));
            Food = 9000f;
            CartridgeKg = kit.Has("SCR") ? 3f * kit.Count("CART") : 0f;
            Spares = kit.Count("SPARE");
            RoverCap = kit.Has("ROV-SC") ? 6f : 0f;
            RoverBattery = RoverCap;
            SampleCapacity = kit.Has("SAMP") && kit.Has("STO") ? 5 : 0;
            Tasks = new MissionTasks(this);
            Net = new CommsNetwork(this);

            // GDD §14 power priority: 1 life support, 2 suit, 3 comms, 4 science, 6 rover charging.
            AddLoad("HAB", "Habitat", 0.6f, 1, true);
            if (kit.Has("SCR")) AddLoad("SCR", "CO2 Scrubber", 0.2f, 1, true);
            if (kit.Has("OGS")) AddLoad("OGS", "Oxygen Generator", 1.0f, 1, false);
            AddLoad("SUI", "Suit Station", 0.3f, 2, true);
            if (kit.Has("COM")) AddLoad("COM", "Comms Console", 0.2f, 3, false);
            if (kit.Has("RAD")) AddLoad("RAD", "Radiation Monitor", 0.02f, 4, false);
            if (kit.Has("SEIS")) AddLoad("SEIS", "Seismometer", 0.1f, 4, false);
            if (kit.Has("GAR")) AddLoad("GAR", "Garage (rover charging)", 0.4f, 6, false);

            Log(Severity.Info, "Landed at Connecting Ridge, lunar south pole. Mission clock started.");
        }

        void AddLoad(string id, string name, float pw, int prio, bool locked) =>
            Loads.Add(new PowerLoad { Id = id, Name = name, Pw = pw, Priority = prio, Locked = locked });

        public PowerLoad Load(string id) => Loads.FirstOrDefault(l => l.Id == id);
        bool Powered(string id) { var l = Load(id); return l != null && l.Powered; }

        public float SolarPeak => 1.0f * Kit.Count("SOL-S");
        public float BatteryPct => BatteryCap > 0 ? Battery / BatteryCap : 0f;
        public bool InShadow => Hour >= ShadowStart && Hour < ShadowEnd;
        public bool SpeActive => Hour >= SpeStart && Hour < SpeEnd;
        public float ShadowEnergyNeeded => Loads.Where(l => l.Wanted && l.Id != "OGS" && l.Id != "GAR").Sum(l => l.Pw) * (ShadowEnd - ShadowStart);

        public bool P1Seismometer => SeisGoodHours >= SeisNeededHours;
        public bool P2Radiation => RadReadings >= 1;
        public bool P3Survive => Hour >= DurationH && !Failed;
        public int PrimaryDone => (P1Seismometer ? 1 : 0) + (P2Radiation ? 1 : 0) + (P3Survive ? 1 : 0);
        public bool S1Samples => SamplesDelivered >= 2;
        public bool S2Oxygen => CabinO2 >= 3f;
        public bool S3SuitOk => MinIntegrity >= 50f;

        /// <summary>Illumination curve (0-1). Polar sun skims the horizon; scripted 6 h shadow window (GDD §10 M1).</summary>
        public static float SunAt(float h)
        {
            if (h >= ShadowStart - 0.5f && h < ShadowStart) return Mathf.Lerp(0.9f, 0f, (h - (ShadowStart - 0.5f)) / 0.5f);
            if (h >= ShadowStart && h < ShadowEnd) return 0f;
            if (h >= ShadowEnd && h < ShadowEnd + 0.5f) return Mathf.Lerp(0f, 0.9f, (h - ShadowEnd) / 0.5f);
            return 0.9f + 0.1f * Mathf.Sin(h * 0.26f);
        }

        // ---------------- Player actions ----------------

        public void Log(Severity s, string text)
        {
            var e = new LogEntry { Hour = Hour, Sev = s, Text = text };
            Events.Add(e);
            if (s == Severity.Critical) CriticalEvents++;
            OnEvent?.Invoke(e);
        }

        void Cause(string text) { if (!CauseChain.Contains(text)) CauseChain.Add(text); }

        public void PickUpSeismometer()
        {
            Tasks.ResetSeis();
            if (SeisDeployed) Log(Severity.Info, "Seismometer picked up for repositioning.");
            SeisDeployed = false; SeisCarried = true;
        }

        public void DeploySeismometer(float distanceFromHab, Vector3 pos)
        {
            SeisCarried = false; SeisDeployed = true; SeisDistance = distanceFromHab; SeisPos = pos;
            var l = Load("SEIS"); if (l != null) l.Enabled = true;
            if (distanceFromHab >= SeisMinDistance)
                Log(Severity.Good, $"Seismometer deployed {distanceFromHab:0} m from the habitat: quiet site. Keep it powered for {SeisNeededHours:0} h.");
            else
                Log(Severity.Warning, $"Seismometer deployed only {distanceFromHab:0} m away. Base vibration will mask quakes: move it beyond {SeisMinDistance:0} m.");
            Data.Add(new DataRecord { Hour = Hour, Instrument = "SEIS", What = "Deployment",
                Where = $"{distanceFromHab:0} m from HAB (x {pos.x:0}, z {pos.z:0})",
                Conditions = distanceFromHab >= SeisMinDistance ? "Outside base noise radius" : "INSIDE base noise radius (noisy)" });
            if (Tasks.Enabled && !Tasks.AutoPass) Tasks.OnSeisDeployed();
        }

        public bool LogRadiationReading()
        {
            if (!Powered("RAD")) { Log(Severity.Warning, "Radiation Monitor has no power: no reading taken."); return false; }
            RadReadings++;
            float rate = Inside ? 0.01f : 0.04f;
            if (SpeActive) rate += Inside ? 0.2f : 6f;
            Data.Add(new DataRecord { Hour = Hour, Instrument = "RAD", What = $"Dose rate {rate:0.00} game-mSv/h, mission dose {Dose:0.0}",
                Where = "Habitat exterior post", Conditions = SpeActive ? "During solar particle event" : "Quiet sun" });
            Log(Severity.Good, $"Radiation reading logged ({rate:0.00} game-mSv/h).");
            return true;
        }

        public bool CollectSample(string site, float distance)
        {
            if (SampleCapacity == 0) { Log(Severity.Warning, "No sample container in your loadout: cannot collect."); return false; }
            if (SamplesCarried + SamplesDelivered >= SampleCapacity) { Log(Severity.Warning, "Sample container full."); return false; }
            var why = Inv.Add(Inventory.Sample);
            if (why != null) { Log(Severity.Warning, why); return false; }
            Data.Add(new DataRecord { Hour = Hour, Instrument = "EVA", What = $"Regolith sample {site}", Where = $"{distance:0} m from HAB",
                Conditions = $"Suit integrity {SuitIntegrity:0}%" });
            Log(Severity.Good, $"Regolith sample {site} bagged. Bring it back inside to deliver it.");
            return true;
        }

        public void EnterHabitat()
        {
            Inside = true; Driving = false;
            if (SamplesCarried > 0) { int n = Inv.Remove(Inventory.Sample); SamplesDelivered += n; Log(Severity.Good, $"{n} sample(s) stored. Delivered: {SamplesDelivered}."); }
            Log(Severity.Info, "Entered the habitat. Suit refilling at the Suit Station.");
        }

        public bool ExitHabitat()
        {
            if (EvaLeft < 0.6f) { Log(Severity.Warning, $"Suit not ready: {EvaLeft:0.0} h left. Wait for the refill."); return false; }
            Inside = false; Resting = false;
            Log(Severity.Info, $"EVA started. Suit time {EvaLeft:0.0} h.");
            return true;
        }

        public bool RepairSuit()
        {
            if (Spares <= 0 || !Inside) return false;
            Spares--; SuitIntegrity = Mathf.Min(100f, SuitIntegrity + 50f);
            Log(Severity.Good, $"Suit repaired with a spare kit ({Spares} left).");
            return true;
        }

        public void CleanArrays() { Dust = 0f; Log(Severity.Good, "Solar arrays cleaned of dust."); }

        // ---- Galley and wash corner: the food / water stores finally have a player-facing use ----
        public const float MealGapH = 6f, WashGapH = 12f;
        public float LastMealHour = -99f, LastWashHour = -99f;
        public int MealsEaten, WashesDone;
        public float FoodDaysLeft => Food / (104f * 24f);

        /// <summary>Eat at the galley. The meal is part of the normal food burn (no extra food used); it takes the edge off fatigue.</summary>
        public bool EatMeal(out string refusal)
        {
            refusal = null;
            if (!Inside) { refusal = "The galley is inside the habitat."; return false; }
            if (Food <= 0f) { refusal = "The galley is empty: no food left."; return false; }
            float wait = MealGapH - (Hour - LastMealHour);
            if (wait > 0f) { refusal = $"You ate recently. Next meal in {wait:0.0} h. Food left: {FoodDaysLeft:0.0} days, water {Water:0} L."; return false; }
            LastMealHour = Hour; MealsEaten++;
            Hunger = Mathf.Min(100f, Hunger + MealHunger); Hydration = Mathf.Min(100f, Hydration + 10f);
            Fatigue = Mathf.Max(0f, Fatigue - 6f);
            Log(Severity.Good, $"Meal eaten (rehydrated ration). Fatigue eased. Food left: {FoodDaysLeft:0.0} days.");
            return true;
        }

        public const float MealHunger = 40f, DrinkHydration = 35f;

        /// <summary>Drink at the galley water dispenser. Like meals, it is part of the normal 3 L/day crew water burn (no extra water used).</summary>
        public bool Drink(out string refusal)
        {
            refusal = null;
            if (!Inside) { refusal = "The water dispenser is inside the habitat."; return false; }
            if (Hydration >= 95f) { refusal = "You are not thirsty."; return false; }
            if (Water <= 0f) { refusal = "The water tank is empty."; return false; }
            Hydration = Mathf.Min(100f, Hydration + DrinkHydration);
            Log(Severity.Good, $"Drank water. Tank: {Water:0} L.");
            return true;
        }

        /// <summary>Wash up at the hygiene corner: costs 1 L of water, so it is a real trade-off when water is short.</summary>
        public bool WashUp(out string refusal)
        {
            refusal = null;
            if (!Inside) { refusal = "The wash basin is inside the habitat."; return false; }
            float wait = WashGapH - (Hour - LastWashHour);
            if (wait > 0f) { refusal = $"You washed recently. Next wash in {wait:0.0} h."; return false; }
            if (Water < 4f) { refusal = $"Water is too low to spare ({Water:0.0} L). Keep it for drinking and the oxygen generator."; return false; }
            LastWashHour = Hour; WashesDone++;
            Water -= 1f; Fatigue = Mathf.Max(0f, Fatigue - 3f);
            Log(Severity.Good, "Washed up (1 L of water used). Dust from the suit liner rinsed off; fatigue eased.");
            return true;
        }

        /// <summary>Rover energy for a distance driven. Returns false when the battery is empty.</summary>
        public bool UseRoverEnergy(float meters)
        {
            float cost = meters * RoverPwhPerMeter * (Sun < 0.05f ? 1.3f : 1f);
            if (RoverBattery <= 0f) return false;
            RoverBattery = Mathf.Max(0f, RoverBattery - cost);
            if (RoverBattery <= 0f) { Log(Severity.Critical, "Rover battery empty: rover stranded. Walk back if you are inside the walk-back circle."); Cause("Rover battery ran out away from the garage"); }
            return true;
        }

        // ---------------- Simulation ----------------

        public void Advance(float hours)
        {
            int ticks = Mathf.FloorToInt((hours + carry) * 60f);
            carry = (hours + carry) - ticks / 60f;
            for (int i = 0; i < ticks && !Ended; i++) Tick();
        }
        float carry;

        public void Tick()
        {
            const float dt = 1f / 60f;
            float prevHour = Hour;
            Hour += dt;

            // --- Power (GDD §14) ---
            Sun = SunAt(Hour);
            Dust = Mathf.Min(0.5f, Dust + 0.02f / 24f * dt);   // ~2% per mission day (GDD §14: 1-3%/day)
            Supply = SolarPeak * Sun * (1f - Dust);

            foreach (var l in Loads)
            {
                switch (l.Id)
                {
                    case "OGS":
                        l.Wanted = Ogs == OgsMode.On || (Ogs == OgsMode.Auto && BatteryPct > 0.6f && Sun > 0.05f && CabinO2 < CabinO2Cap - 0.05f);
                        l.Why = Ogs == OgsMode.Off ? "Switched off" : !l.Wanted ? "Auto: waiting (battery <60%, dark, or cabin full)" : "";
                        break;
                    case "SEIS": l.Wanted = l.Enabled && SeisDeployed; l.Why = !SeisDeployed ? "Not deployed" : l.Enabled ? "" : "Switched off"; break;
                    case "GAR": l.Wanted = l.Enabled && RoverNearGarage && RoverBattery < RoverCap - 0.01f; l.Why = l.Wanted ? "" : "Idle (rover not parked / full)"; break;
                    default: l.Wanted = l.Locked || l.Enabled; l.Why = l.Wanted ? "" : "Switched off"; break;
                }
            }

            if (Battery <= BatteryCap * 0.01f) batteryLockout = true;
            if (Battery >= BatteryCap * 0.05f) batteryLockout = false;
            float fromBattery = batteryLockout ? 0f : Mathf.Min(MaxDischarge, Battery / dt);
            float available = Supply + fromBattery;
            Demand = 0f;
            int shortAt = int.MaxValue;   // GDD §14: once a priority level is short, every lower priority is shed too
            foreach (var l in Loads.OrderBy(x => x.Priority))
            {
                bool was = l.Powered;
                l.Powered = l.Wanted && l.Priority <= shortAt && available >= l.Pw - 0.0001f;
                bool deferrable = l.Id == "OGS" && Ogs == OgsMode.Auto;   // AUTO only uses spare power
                if (deferrable && l.Wanted && !l.Powered) { l.Why = "Auto: waiting for spare power"; continue; }
                if (l.Wanted && !l.Powered && shortAt == int.MaxValue) shortAt = l.Priority;
                if (l.Powered) { available -= l.Pw; Demand += l.Pw; }
                if (was && l.Wanted && !l.Powered)
                {
                    string why = Sun < 0.05f ? "shadow window, battery empty" : "not enough power";
                    l.Why = "SHED: " + why;
                    Log(l.Priority <= 2 ? Severity.Critical : Severity.Warning, $"{l.Name} lost power ({why}).");
                    if (l.Priority <= 1) Cause($"{l.Name} lost power ({why})");
                }
                else if (l.Wanted && !l.Powered) l.Why = "SHED: " + (Sun < 0.05f ? "shadow window, battery empty" : "not enough power");
            }
            Battery = Mathf.Clamp(Battery + (Supply - Demand) * dt, 0f, BatteryCap);

            // --- Oxygen / CO2 / water / food (GDD §13, §15) ---
            if (Powered("OGS"))
            {
                float need = 0.1125f * dt;
                if (Water >= need)
                {
                    Water -= need;
                    float made = 0.10f * dt;
                    O2Produced += made;
                    float room = CabinO2Cap - CabinO2;
                    if (made > room) { O2Vented += made - room; made = room; }
                    CabinO2 += made;
                }
                else Latch("ogs-dry", Severity.Warning, "OGS has no water to split. Oxygen production stopped.");
            }
            CabinO2 = Mathf.Max(0f, CabinO2 - 0.002f * dt);   // habitat leak

            if (Inside)
            {
                float use = Mathf.Min(CabinO2, 0.035f * dt);
                CabinO2 -= use; O2Consumed += 0.035f * dt;
                CO2 += 0.042f * dt;
                if (Powered("SUI"))
                {
                    float refill = 3f * dt;
                    float before = SuitO2;
                    SuitO2 = Mathf.Min(SuitO2Max, SuitO2 + refill);
                    SuitBatt = Mathf.Min(SuitBattMax, SuitBatt + refill);
                    SuitCO2 = Mathf.Min(SuitCO2Max, SuitCO2 + refill);
                    float o2 = (SuitO2 - before) * 0.06f;
                    CabinO2 = Mathf.Max(0f, CabinO2 - o2);
                }
            }
            else
            {
                float rate = Sprinting ? 1.6f : 1f;
                SuitO2 = Mathf.Max(0f, SuitO2 - rate * dt - (SuitIntegrity < 50f ? 0.8f * dt : 0f));
                SuitBatt = Mathf.Max(0f, SuitBatt - dt);
                SuitCO2 = Mathf.Max(0f, SuitCO2 - rate * dt);
                O2Consumed += 0.06f * rate * dt;
                if (!Driving) SuitIntegrity = Mathf.Max(0f, SuitIntegrity - (Sprinting ? 2f : 0.8f) * dt);
                MinIntegrity = Mathf.Min(MinIntegrity, SuitIntegrity);
            }
            if (Powered("SCR") && CartridgeKg > 0f)
            {
                float removed = Mathf.Min(0.06f * dt, CO2, CartridgeKg);
                CO2 -= removed; CartridgeKg -= removed;
                if (CartridgeKg <= 0f) { Log(Severity.Critical, "Last scrubber cartridge is saturated. CO2 will now build up."); Cause("Scrubber cartridges ran out"); }
            }
            Water = Mathf.Max(0f, Water - 0.125f * dt);
            Food = Mathf.Max(0f, Food - 104f * dt);

            // --- Radiation (GDD §17, §23) ---
            float doseRate = Inside ? 0.01f : 0.04f;
            if (SpeActive) doseRate += Inside ? (Sheltering ? 0.03f : 0.2f) : 6f;
            Dose += doseRate * dt;

            // --- Rover charging ---
            if (Powered("GAR")) RoverBattery = Mathf.Min(RoverCap, RoverBattery + 0.4f * dt);

            // --- Seismometer + quake window ---
            if (SeisDeployed && Powered("SEIS"))
            {
                if (SeisInstalled)
                {
                    SeisPoweredHours += dt;
                    if (SeisDistance >= SeisMinDistance) SeisGoodHours += dt * SeisQuality;
                }
            }            if (prevHour < QuakeHour && Hour >= QuakeHour)
            {
                if (SeisDeployed && Powered("SEIS") && SeisDistance >= SeisMinDistance)
                {
                    QuakeRecorded = true;
                    Data.Add(new DataRecord { Hour = Hour, Instrument = "SEIS", What = "Shallow moonquake recorded (event window from Apollo-era timing, dataset TBD)",
                        Where = $"Seismometer {SeisDistance:0} m from HAB", Conditions = "Powered, outside base noise radius" });
                    Log(Severity.Good, "MOONQUAKE RECORDED by the seismometer. Bonus science!");
                }
                else
                {
                    QuakeMissed = true;
                    Log(Severity.Warning, SeisDeployed ? (Powered("SEIS") ? "A moonquake happened, but base noise masked it (seismometer too close)." : "A moonquake happened while the seismometer had no power.")
                                                       : "A moonquake happened. No seismometer was listening.");
                }
            }

            // --- Health / fatigue (GDD §17) ---
            bool hypoxic = Inside ? CabinO2 <= 0f : EvaLeft <= 0f;
            if (hypoxic) Health -= (Inside ? 25f : 60f) * dt;
            if (Inside && CO2 > 0.6f) { Health -= 4f * dt; Fatigue += 4f * dt; }
            if (Water <= 0f) Health -= 2f * dt;
            if (Food <= 0f) Health -= 1f * dt;
            if (Dose > DoseAllowance) Health -= 5f * dt;
            // hunger empties in ~24 h, thirst in ~16 h (faster in the suit, slower asleep); at 0 they hurt health
            float burn = Resting ? 0.5f : Inside ? 1f : 1.5f;
            Hunger = Mathf.Clamp(Hunger - 100f / 24f * burn * dt, 0f, 100f);
            Hydration = Mathf.Clamp(Hydration - 100f / 16f * burn * dt, 0f, 100f);
            if (Hunger <= 0f) Health -= 2f * dt;
            if (Hydration <= 0f) Health -= 4f * dt;
            if (Inside && !hypoxic && CO2 <= 0.6f && Water > 0f && Food > 0f && Hunger > 20f && Hydration > 20f) Health += 2f * dt;
            Health = Mathf.Clamp(Health, 0f, 100f);
            Fatigue += (Inside ? (Resting ? -12f : 0.3f) : (Sprinting ? 4f : 2f)) * dt;
            Fatigue = Mathf.Clamp(Fatigue, 0f, 100f);

            if (CabinO2 <= 0f) O2ZeroHours += dt; else O2ZeroHours = 0f;

            Gateway.Tick(this);
            CheckWarnings();

            if (PrimaryDoneHour < 0f && P1Seismometer && P2Radiation) PrimaryDoneHour = Hour;

            // --- End conditions (GDD §25) ---
            if (Health <= 0f) Fail(hypoxic ? (Inside ? "Cabin oxygen ran out" : "Suit life support ran out during EVA") : "Crew health reached 0");
            else if (O2ZeroHours >= 2f) Fail("Cabin oxygen was 0 kg for 2 mission hours");
            else if (Hour >= DurationH)
            {
                Ended = true;
                Log(PrimaryDone == 3 ? Severity.Good : Severity.Warning, PrimaryDone == 3 ? "Mission complete: all primary objectives met." : "Mission timer ended. Some primary objectives were not met.");
            }
        }

        void Fail(string cause)
        {
            Ended = true; Failed = true; FailCause = cause;
            Cause(cause);
            Log(Severity.Critical, "MISSION FAILED: " + cause + ".");
        }

        void Latch(string key, Severity s, string text) { if (latched.Add(key)) Log(s, text); }

        void Check(bool cond, string key, Severity s, string text, string cause = null)
        {
            if (cond) { if (latched.Add(key)) { Log(s, text); if (cause != null) Cause(cause); } }
            else latched.Remove(key);
        }

        void CheckWarnings()
        {
            if (BatteryCap > 0) Check(BatteryPct < 0.25f, "bat25", Severity.Warning, $"Battery below 25% ({Battery:0.0} PWh). Shed loads or wait for sun.");
            Check(CabinO2 < 2f, "o2-2", Severity.Warning, "Cabin oxygen low (< 2 kg). Is the OGS running?");
            Check(CabinO2 < 1f, "o2-1", Severity.Critical, "Cabin oxygen critical (< 1 kg).", "Cabin oxygen fell below 1 kg");
            if (CabinO2 < 1f && latched.Add("near-o2")) NearFailures++;
            Check(CabinO2 <= 0f, "o2-0", Severity.Critical, "CABIN OXYGEN 0 kg. 2-hour countdown to mission failure.", "Cabin oxygen reached 0 kg");
            Check(CO2 > 0.4f, "co2-4", Severity.Warning, "CO2 rising. Check the scrubber and cartridges.");
            Check(CO2 > 0.6f, "co2-6", Severity.Critical, "CO2 harmful: health falling, fatigue rising.", "CO2 built up above the safe level");
            Check(Water < 3f, "water", Severity.Warning, "Water below 3 L.");
            Check(Water <= 0f, "water0", Severity.Critical, "Out of water.", "Water ran out");
            Check(!Inside && EvaLeft < 1f, "eva1", Severity.Warning, $"Suit: under 1 h left. Head back now.");
            Check(!Inside && EvaLeft < 0.5f, "eva05", Severity.Critical, "Suit reserve (30 min). Return to the airlock immediately!", "Stayed outside into the suit reserve");
            Check(!Inside && SuitIntegrity < 50f, "suit50", Severity.Warning, "Suit integrity below 50%: slow O2 leak. Repair with a spare inside.");
            Check(Health < 40f, "hp40", Severity.Critical, "Health below 40%.");
            if (Health < 30f && latched.Add("near-hp")) NearFailures++;
            Check(Dose > 15f, "dose15", Severity.Warning, "Radiation dose high (> 15 of 20 game-mSv). Stay inside.");
            Check(Dose > DoseAllowance, "dose20", Severity.Critical, "Dose over the mission allowance: health falling.", "Radiation dose exceeded the allowance");

            // Scripted hazard warnings
            if (Hour >= ShadowStart - 2f && Hour < ShadowStart)
                Latch("shadow-warn", Severity.Warning, $"Shadow window in {ShadowStart - Hour:0.0} h (6 h without sun). Load needs ~{ShadowEnergyNeeded:0.0} PWh; battery holds {Battery:0.0} PWh.");
            if (Hour >= ShadowStart && latched.Add("shadow-start"))
            {
                Log(Severity.Warning, "Shadow window: the ridge is in shadow. Solar output 0 for 6 h.");
                if (Battery < ShadowEnergyNeeded) Cause($"Entered the 6 h shadow window with {Battery:0.0} PWh stored but ~{ShadowEnergyNeeded:0.0} PWh needed");
            }
            Check(Gateway.Calls < Resupply.MaxCalls && Resupply.Items.Any(i => i.Low(this)), "gateway-hint", Severity.Info,
                "Supplies running low: you can call Lunar Gateway for a cargo drone (Tab > Call Gateway).");
            Check(Hydration < 25f, "thirsty", Severity.Warning, "You are getting dehydrated. Drink at the water dispenser in the galley.");
            Check(Hunger < 25f, "hungry", Severity.Warning, "You are getting hungry. Eat a meal at the galley.");
            if (Hour >= ShadowEnd) Latch("shadow-end", Severity.Good, "Sunlight is back on the ridge.");
            if (Hour >= SpeStart - 1f / 6f && Hour < SpeStart && Powered("RAD"))
                Latch("spe-warn", Severity.Critical, "RAD ALERT: solar particle event in 10 minutes. Get inside the habitat!");
            if (Hour >= SpeStart && latched.Add("spe-start"))
            {
                Log(Inside ? Severity.Warning : Severity.Critical, Kit.Has("RAD") ? "Solar particle event in progress (3 h). Shelter inside." : "Dose rising fast! No radiation monitor, so no early warning.");
                if (!Inside) Cause("Caught outside during the solar particle event");
            }
            if (Hour >= SpeEnd) Latch("spe-end", Severity.Good, "Solar particle event over.");
        }
    }
}
