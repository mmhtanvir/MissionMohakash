using System.Collections.Generic;
using System.Linq;

namespace Mohakash
{
    public class EquipmentDef
    {
        public string Id, Name, Category, Purpose;
        public float Mass, Cost, Power;   // GAME UNITS (GDD §40). Power: + generates, − consumes (PW).
        public int MaxQty = 1;
        public bool Required;             // cannot be removed (mission cannot run without it)
        public string[] Needs = new string[0];
        public string PowerText;
    }

    /// <summary>Mission 1 "First Light" catalogue. Every number is a GDD gameplay value (GAME UNITS), not a NASA specification.</summary>
    public static class Catalog
    {
        public const float M1Budget = 125f, M1Cargo = 540f;

        public static readonly List<EquipmentDef> Mission1 = new List<EquipmentDef>
        {
            new EquipmentDef { Id = "HAB", Name = "Habitat Module", Category = "Structure", Mass = 120, Cost = 20, Power = -0.6f, Required = true,
                Purpose = "Pressurized home. Holds 4 kg O2 and 40 L water." },
            new EquipmentDef { Id = "SUI", Name = "Suit Station (airlock)", Category = "Structure", Mass = 30, Cost = 8, Power = -0.3f, Required = true,
                Purpose = "Enter/exit the habitat; refills and recharges the suit." },
            new EquipmentDef { Id = "SUIT-A", Name = "Suit A (Trainer)", Category = "Suit", Mass = 40, Cost = 10, Required = true, PowerText = "own battery",
                Purpose = "Basic EVA suit: about 4 h outside (shortest timer wins)." },
            new EquipmentDef { Id = "SOL-S", Name = "Solar Array S", Category = "Power", Mass = 20, Cost = 6, Power = 1.0f, MaxQty = 4, PowerText = "+1.0 peak",
                Purpose = "Makes power from sunlight. Zero in shadow; dust lowers it." },
            new EquipmentDef { Id = "BAT", Name = "Battery", Category = "Power", Mass = 25, Cost = 5, MaxQty = 3, PowerText = "stores 10 PWh",
                Purpose = "Stores 10 PWh for the shadow window. Max output 2 PW." },
            new EquipmentDef { Id = "OGS", Name = "Oxygen Generator", Category = "Life support", Mass = 40, Cost = 10, Power = -1.0f,
                Purpose = "Electrolyzer: water + power -> 0.10 kg O2/h (1.125 L water per kg O2).", Needs = new[] { "water" } },
            new EquipmentDef { Id = "SCR", Name = "CO2 Scrubber (cartridge)", Category = "Life support", Mass = 20, Cost = 5, Power = -0.2f,
                Purpose = "Removes 0.06 kg CO2/h. Useless without cartridges.", Needs = new[] { "CART" } },
            new EquipmentDef { Id = "CART", Name = "Scrubber Cartridge", Category = "Consumable", Mass = 3, Cost = 1, MaxQty = 4,
                Purpose = "Holds 3 kg CO2 (about 3 crew-days).", Needs = new[] { "SCR" } },
            new EquipmentDef { Id = "TNK-O", Name = "Oxygen Tank (8 kg)", Category = "Storage", Mass = 15, Cost = 3, MaxQty = 2,
                Purpose = "+8 kg O2 storage. Generation is not storage: a full cabin vents extra O2." },
            new EquipmentDef { Id = "TNK-W", Name = "Water Tank (40 L)", Category = "Storage", Mass = 12, Cost = 3,
                Purpose = "+40 L water storage." },
            new EquipmentDef { Id = "WATER", Name = "Extra water (10 L)", Category = "Supply", Mass = 10, Cost = 1, MaxQty = 4,
                Purpose = "Drinking water and OGS feed. 1 kg per litre." },
            new EquipmentDef { Id = "COM", Name = "Communications Console", Category = "Comms", Mass = 15, Cost = 6, Power = -0.2f,
                Purpose = "Earth link (~1.3 s from the Moon). Uplinks your science data." },
            new EquipmentDef { Id = "STO", Name = "Storage Module", Category = "Structure", Mass = 40, Cost = 8,
                Purpose = "+20 storage slots for samples, parts and food." },
            new EquipmentDef { Id = "GAR", Name = "Garage", Category = "Structure", Mass = 50, Cost = 12, Power = -0.4f, PowerText = "-0.4 while charging",
                Purpose = "Rover charging bay." },
            new EquipmentDef { Id = "ROV-SC", Name = "Scout Rover", Category = "Rover", Mass = 60, Cost = 10, PowerText = "battery 6 PWh",
                Purpose = "Fast and light. 0.12 PWh per 10 m cell (x1.3 in shadow).", Needs = new[] { "GAR" } },
            new EquipmentDef { Id = "SEIS", Name = "Seismometer", Category = "Instrument", Mass = 15, Cost = 9, Power = -0.1f,
                Purpose = "Listens for moonquakes. Must be >= 80 m from base noise." },
            new EquipmentDef { Id = "RAD", Name = "Radiation Monitor", Category = "Instrument", Mass = 4, Cost = 4, Power = -0.02f,
                Purpose = "Dose readings and a 10-minute early warning of solar particle events." },
            new EquipmentDef { Id = "SAMP", Name = "Sample Container (5)", Category = "Consumable", Mass = 2, Cost = 1,
                Purpose = "Holds 5 regolith samples.", Needs = new[] { "STO" } },
            new EquipmentDef { Id = "SPARE", Name = "Spare Parts Kit", Category = "Consumable", Mass = 5, Cost = 2, MaxQty = 3,
                Purpose = "One kit = one repair (suit integrity +50)." },
        };

        public static EquipmentDef Get(string id) => Mission1.First(e => e.Id == id);

        /// <summary>GDD §11.1 sample plan: 522 kg / 125 Cr.</summary>
        public static Loadout Recommended()
        {
            var l = new Loadout();
            foreach (var id in new[] { "HAB", "SUI", "COM", "STO", "GAR", "BAT", "OGS", "SCR", "SUIT-A", "SEIS", "RAD", "ROV-SC", "SAMP", "SPARE", "WATER" })
                l.Set(id, 1);
            l.Set("SOL-S", 2);
            l.Set("CART", 2);
            return l;
        }

        // ---- Simple loadout: standard gear is always packed; extras are ticked until the rocket is full ----

        /// <summary>Always packed in the simple loadout (422 kg / 94 Cr): everything Mission 1's objectives need.</summary>
        public static readonly (string id, int qty, string label)[] Standard =
        {
            ("HAB", 1, "Habitat"), ("SUI", 1, "Airlock"), ("SUIT-A", 1, "Spacesuit"), ("SOL-S", 2, "2 Solar arrays"), ("BAT", 1, "Battery"),
            ("OGS", 1, "Oxygen maker"), ("SCR", 1, "CO2 scrubber"), ("CART", 2, "2 Scrubber cartridges"), ("COM", 1, "Comms console"),
            ("SEIS", 1, "Seismometer"), ("RAD", 1, "Radiation monitor"), ("STO", 1, "Storage"), ("SAMP", 1, "Sample box"),
        };

        public class Extra { public string Key, Name, Benefit; public (string id, int qty)[] Items; }

        public static readonly Extra[] Extras =
        {
            new Extra { Key = "rover", Name = "Scout rover + garage", Benefit = "drive 3x faster than walking", Items = new[] { ("ROV-SC", 1), ("GAR", 1) } },
            new Extra { Key = "bat2", Name = "Second battery", Benefit = "+10 PWh: no shutdowns in the 6 h shadow", Items = new[] { ("BAT", 2) } },
            new Extra { Key = "sol3", Name = "Third solar array", Benefit = "+1 PW while the sun shines", Items = new[] { ("SOL-S", 3) } },
            new Extra { Key = "tank", Name = "Oxygen tank", Benefit = "stores +8 kg O2 instead of venting it", Items = new[] { ("TNK-O", 1) } },
            new Extra { Key = "water", Name = "Extra water (10 L)", Benefit = "more to drink and to turn into oxygen", Items = new[] { ("WATER", 1) } },
            new Extra { Key = "wtank", Name = "Water tank (40 L)", Benefit = "room for the extra water; the habitat alone holds 40 L", Items = new[] { ("TNK-W", 1) } },
            new Extra { Key = "spare", Name = "Spare parts kit", Benefit = "repair your suit once", Items = new[] { ("SPARE", 1) } },
            new Extra { Key = "cart", Name = "Extra CO2 cartridge", Benefit = "+3 kg CO2 capacity (safety margin)", Items = new[] { ("CART", 3) } },
        };

        public static Loadout Simple(IEnumerable<string> extraKeys)
        {
            var l = new Loadout();
            foreach (var (id, q, _) in Standard) l.Set(id, q);
            foreach (var key in extraKeys)
                foreach (var (id, q) in Extras.First(e => e.Key == key).Items) l.Set(id, System.Math.Max(l.Count(id), q));
            return l;
        }

        public static Loadout RequiredOnly()
        {
            var l = new Loadout();
            foreach (var e in Mission1) if (e.Required) l.Set(e.Id, 1);
            return l;
        }
    }

    public class Loadout
    {
        readonly Dictionary<string, int> qty = new Dictionary<string, int>();

        public int Count(string id) => qty.TryGetValue(id, out var n) ? n : 0;
        public bool Has(string id) => Count(id) > 0;

        public void Set(string id, int n)
        {
            var def = Catalog.Get(id);
            n = System.Math.Max(def.Required ? 1 : 0, System.Math.Min(def.MaxQty, n));
            if (n == 0) qty.Remove(id); else qty[id] = n;
        }

        public float Mass => qty.Sum(p => Catalog.Get(p.Key).Mass * p.Value);
        public float Cost => qty.Sum(p => Catalog.Get(p.Key).Cost * p.Value);
        public bool WithinLimits => Mass <= Catalog.M1Cargo + 0.01f && Cost <= Catalog.M1Budget + 0.01f;

        /// <summary>Plain-language dependency and objective warnings shown on the loadout screen.</summary>
        public List<string> Warnings()
        {
            var w = new List<string>();
            if (Mass > Catalog.M1Cargo) w.Add($"Too heavy: {Mass:0} kg of {Catalog.M1Cargo:0} kg cargo limit.");
            if (Cost > Catalog.M1Budget) w.Add($"Over budget: {Cost:0} Cr of {Catalog.M1Budget:0} Cr.");
            if (!Has("SEIS")) w.Add("No seismometer: the PRIMARY objective cannot be completed.");
            if (!Has("RAD")) w.Add("No radiation monitor: you cannot log a reading (primary objective) and get no solar-storm warning.");
            if (!Has("SOL-S")) w.Add("No solar arrays: nothing recharges the battery.");
            if (!Has("BAT")) w.Add("No battery: every load shuts off the moment the sun is blocked (6 h shadow window).");
            if (!Has("OGS")) w.Add("No oxygen generator: the cabin's 4 kg of O2 must last 72 h (crew uses ~2.5 kg + leaks).");
            if (Has("SCR") && !Has("CART")) w.Add("Scrubber has no cartridges: CO2 will build up.");
            if (!Has("SCR")) w.Add("No CO2 scrubber: CO2 reaches a harmful level in about 14 h.");
            if (Has("CART") && !Has("SCR")) w.Add("Cartridges without a scrubber are dead weight.");
            if (Has("ROV-SC") && !Has("GAR")) w.Add("Rover without a garage cannot recharge.");
            if (Has("SAMP") && !Has("STO")) w.Add("Samples need the Storage Module.");
            if (Has("OGS") && Count("WATER") == 0) w.Add("OGS uses water (1.125 L per kg O2). Only 15 L on board: consider extra water.");
            if (Count("CART") == 1) w.Add("One cartridge holds 3 kg CO2; the crew makes ~3.0 kg in 72 h. No margin.");
            if (Count("BAT") == 1 && Count("SOL-S") >= 1) w.Add("One battery (10 PWh) barely covers the 6 h shadow (~9-10 PWh). Shed loads or bring a second battery.");
            return w;
        }

        public Loadout Clone()
        {
            var l = new Loadout();
            foreach (var p in qty) l.qty[p.Key] = p.Value;
            return l;
        }

        public IEnumerable<KeyValuePair<string, int>> Items => qty;
    }
}
