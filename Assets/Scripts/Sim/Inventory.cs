using System.Collections.Generic;
using System.Linq;

namespace Mohakash
{
    public class ItemDef { public string Id, Name; public float Kg; }

    /// <summary>What the astronaut carries. 4 pocket slots, 20 with the EVA bag. One item type per slot, max 10 per slot, max 20 kg in total.</summary>
    public class Inventory
    {
        public const int PocketSlots = 4, BagSlots = 20, PerSlot = 10;
        public const float MaxKg = 20f;
        public static readonly ItemDef Sample = new ItemDef { Id = "SAMPLE", Name = "Regolith sample", Kg = 1f };

        public class Slot { public ItemDef Item; public int Count; }
        public readonly List<Slot> Slots = new List<Slot>();
        public bool HasBag;

        public int Capacity => HasBag ? BagSlots : PocketSlots;
        public float Kg => Slots.Sum(s => s.Item.Kg * s.Count);
        public int Count(ItemDef d) => Slots.Where(s => s.Item == d).Sum(s => s.Count);

        /// <summary>Adds one item: tops up a slot of the same item, else opens a free slot. Returns the reason on failure.</summary>
        public string Add(ItemDef d)
        {
            if (Kg + d.Kg > MaxKg) return $"Too heavy: {Kg:0.#} + {d.Kg:0.#} kg is over the {MaxKg:0} kg limit.";
            var s = Slots.Find(x => x.Item == d && x.Count < PerSlot);
            if (s == null)
            {
                if (Slots.Count >= Capacity) return HasBag ? "Bag full: all 20 slots are used." : "Pockets full (4 slots). Take the EVA bag from the cargo bags in the habitat for 20 slots.";
                Slots.Add(s = new Slot { Item = d });
            }
            s.Count++;
            return null;
        }

        /// <summary>Removes up to n of an item, emptying the last slots first. Returns how many were removed.</summary>
        public int Remove(ItemDef d, int n = int.MaxValue)
        {
            int got = 0;
            for (int i = Slots.Count - 1; i >= 0 && got < n; i--)
            {
                if (Slots[i].Item != d) continue;
                int k = System.Math.Min(n - got, Slots[i].Count);
                Slots[i].Count -= k; got += k;
                if (Slots[i].Count == 0) Slots.RemoveAt(i);
            }
            return got;
        }

        /// <summary>Leaving the bag behind only works if what it holds fits in the 4 pockets.</summary>
        public string DropBag()
        {
            if (Slots.Count > PocketSlots) return $"Can't leave the bag: {Slots.Count} slots in use, pockets hold {PocketSlots}.";
            HasBag = false;
            return null;
        }
    }
}
