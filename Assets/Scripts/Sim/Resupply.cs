using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mohakash
{
    public class SupplyItem
    {
        public string Name; public float Kg;
        public Func<MissionModel, string> Stock;    // current level, shown next to the item
        public Func<MissionModel, bool> Low;        // highlights the item and triggers the "call Gateway" hint
        public Func<MissionModel, string> Blocked;  // reason it can't be ordered right now, or null
        public Action<MissionModel> Deliver;
    }

    /// <summary>Backup from Lunar Gateway (station in lunar orbit): a cargo drone brings forgotten or running-out items to the habitat stores.
    /// Limited calls, each one costs score (self-reliance), and the drone takes time to arrive.</summary>
    public class Resupply
    {
        public const float DelayH = 6f, DroneKg = 30f;
        public const int MaxCalls = 3;
        public int Calls;
        public readonly List<(float due, List<SupplyItem> items)> Pending = new List<(float, List<SupplyItem>)>();

        public static readonly SupplyItem[] Items =
        {
            new SupplyItem { Name = "Water (10 L)", Kg = 10f, Stock = m => $"{m.Water:0} / {m.WaterCap:0} L", Low = m => m.Water < 12f,
                Blocked = m => m.Water > m.WaterCap - 10f ? "the water tanks are nearly full" : null,
                Deliver = m => m.Water = Mathf.Min(m.WaterCap, m.Water + 10f) },
            new SupplyItem { Name = "Food (3 days)", Kg = 5f, Stock = m => $"{m.FoodDaysLeft:0.0} days", Low = m => m.FoodDaysLeft < 1f,
                Deliver = m => m.Food += 3f * 104f * 24f },
            new SupplyItem { Name = "Oxygen bottle (4 kg)", Kg = 8f, Stock = m => $"{m.CabinO2:0.0} / {m.CabinO2Cap:0} kg", Low = m => m.CabinO2 < 1.5f,
                Deliver = m => m.CabinO2 = Mathf.Min(m.CabinO2Cap, m.CabinO2 + 4f) },
            new SupplyItem { Name = "Scrubber cartridge", Kg = 3f, Stock = m => $"{m.CartridgeKg:0.0} kg left", Low = m => m.Kit.Has("SCR") && m.CartridgeKg < 1f,
                Blocked = m => m.Kit.Has("SCR") ? null : "no CO2 scrubber to put it in",
                Deliver = m => m.CartridgeKg += 3f },
            new SupplyItem { Name = "Spare parts kit", Kg = 5f, Stock = m => $"{m.Spares} kit(s)", Low = m => m.Spares == 0 && m.SuitIntegrity < 60f,
                Deliver = m => { m.Spares++; m.Kit.Set("SPARE", m.Kit.Count("SPARE") + 1); } },
            new SupplyItem { Name = "Sample container (5)", Kg = 2f, Stock = m => $"room for {m.SampleCapacity}", Low = m => false,
                Blocked = m => m.SampleCapacity > 0 ? "you already have one" : null,
                Deliver = m => { m.SampleCapacity = 5; m.Kit.Set("SAMP", 1); } },
            new SupplyItem { Name = "Battery (+10 PWh)", Kg = 25f, Stock = m => $"{m.BatteryCap:0} PWh", Low = m => false,
                Deliver = m => { m.Kit.Set("BAT", m.Kit.Count("BAT") + 1); m.BatteryCap += 10f; m.MaxDischarge += 2f; } },
            new SupplyItem { Name = "Solar panel (+1 PW)", Kg = 20f, Stock = m => $"{m.SolarPeak:0} PW peak", Low = m => false,
                Deliver = m => m.Kit.Set("SOL-S", m.Kit.Count("SOL-S") + 1) },
        };

        /// <summary>Sends one call. Returns the refusal reason, or null when the drone is on its way.</summary>
        public string Request(MissionModel m, List<SupplyItem> cart)
        {
            if (cart.Count == 0) return "Pick at least one item.";
            if (Calls >= MaxCalls) return $"Gateway has no more drones for you ({MaxCalls} calls used).";
            float kg = cart.Sum(i => i.Kg);
            if (kg > DroneKg) return $"Too heavy for the drone: {kg:0} / {DroneKg:0} kg.";
            var no = cart.FirstOrDefault(i => i.Blocked?.Invoke(m) != null);
            if (no != null) return $"{no.Name}: {no.Blocked(m)}.";
            if (m.Hour + DelayH > MissionModel.DurationH) return "Too late: the drone would arrive after the mission ends.";
            Calls++;
            Pending.Add((m.Hour + DelayH, cart.ToList()));
            m.Log(Severity.Info, $"Gateway: cargo drone launched with {string.Join(", ", cart.Select(i => i.Name))} ({kg:0} kg). Arrival at hour {m.Hour + DelayH:0}.");
            return null;
        }

        public void Tick(MissionModel m)
        {
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                if (m.Hour < Pending[i].due) continue;
                foreach (var it in Pending[i].items) it.Deliver(m);
                m.Log(Severity.Good, $"Gateway drone landed: {string.Join(", ", Pending[i].items.Select(x => x.Name))} added to the habitat stores.");
                Pending.RemoveAt(i);
            }
        }
    }
}
