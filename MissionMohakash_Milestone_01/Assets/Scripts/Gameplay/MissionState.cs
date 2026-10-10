using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Mohakash
{
    public enum MissionPhase { Playing, Success, Failed }

    public class Objective
    {
        public string id;
        public string label;
        public bool done;
        public bool optional;
    }

    public class DataEntry
    {
        public string source;
        public float units;
        public float time;
        public Vector3 position;
    }

    /// <summary>
    /// Holds resources, objectives, the science-data log (with provenance: source, time, location)
    /// and the end-of-mission report. ALL numbers are fictional GAME UNITS, not real engineering specs.
    /// </summary>
    public class MissionState : MonoBehaviour
    {
        public static MissionState Instance { get; private set; }

        public const float MaxOxygen = 100f;
        public const float MaxPower = 100f;
        public const float IdleOxygenDrain = 0.45f;   // GAME UNITS per second
        public const float SprintOxygenDrain = 0.9f;  // GAME UNITS per second

        public float oxygen = MaxOxygen;
        public float power = 80f;
        public float science;
        public float elapsed;
        public bool playerSprinting;
        public MissionPhase Phase = MissionPhase.Playing;
        public string failReason = "";

        public readonly List<Objective> Objectives = new List<Objective>();
        public readonly List<DataEntry> Log = new List<DataEntry>();

        string toast = "";
        float toastUntil;
        public string Toast { get { return Time.time < toastUntil ? toast : ""; } }

        void Awake()
        {
            Instance = this;
            Objectives.Add(new Objective { id = "seismo", label = "Deploy the seismometer" });
            Objectives.Add(new Objective { id = "sampleA", label = "Drill core sample A" });
            Objectives.Add(new Objective { id = "sampleB", label = "Drill core sample B" });
            Objectives.Add(new Objective { id = "relay", label = "Optional: set up the comms relay", optional = true });
            Objectives.Add(new Objective { id = "return", label = "Return to the lander airlock" });
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Say(string msg) { toast = msg; toastUntil = Time.time + 3.5f; }

        Objective Find(string id)
        {
            foreach (var o in Objectives) if (o.id == id) return o;
            return null;
        }

        public bool ReadyToReturn
        {
            get
            {
                foreach (var o in Objectives)
                    if (!o.optional && o.id != "return" && !o.done) return false;
                return true;
            }
        }

        void Update()
        {
            if (Phase != MissionPhase.Playing) return;
            elapsed += Time.deltaTime;
            oxygen -= (playerSprinting ? SprintOxygenDrain : IdleOxygenDrain) * Time.deltaTime;
            if (oxygen <= 0f) Fail("Oxygen reached 0 before you got back inside the lander.");
        }

        void Fail(string reason)
        {
            oxygen = Mathf.Max(0f, oxygen);
            failReason = reason;
            Phase = MissionPhase.Failed;
        }

        public void Interact(Interactable it)
        {
            if (Phase != MissionPhase.Playing || it == null) return;
            if (it.Used) { Say("Already used."); return; }

            foreach (var id in it.requires)
            {
                var o = Find(id);
                if (o != null && !o.done) { Say("Not ready - first: " + o.label + "."); return; }
            }
            if (power < it.powerCost)
            {
                Say("Not enough power: need " + it.powerCost.ToString("0") + " GAME UNITS, have " + power.ToString("0") + ".");
                return;
            }

            power -= it.powerCost;
            oxygen -= it.oxygenCost;
            power = Mathf.Min(MaxPower, power + it.powerGain);

            if (it.scienceGain > 0f)
            {
                science += it.scienceGain;
                Log.Add(new DataEntry { source = it.displayName, units = it.scienceGain, time = elapsed, position = it.transform.position });
            }
            var obj = Find(it.completesObjective);
            if (obj != null) obj.done = true;

            Say(it.displayName + ": done" + it.CostText());
            it.MarkUsed();

            if (oxygen <= 0f) { Fail("Oxygen ran out while working at " + it.displayName + "."); return; }
            if (it.isFinish)
            {
                var ret = Find("return");
                if (ret != null) ret.done = true;
                Phase = MissionPhase.Success;
            }
        }

        public string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Phase == MissionPhase.Success ? "MISSION COMPLETE - EVA SUCCESS" : "MISSION FAILED");
            if (Phase == MissionPhase.Failed) sb.AppendLine(failReason);
            sb.AppendLine();
            sb.AppendLine("Time on EVA: " + Mathf.FloorToInt(elapsed / 60f) + "m " + Mathf.FloorToInt(elapsed % 60f) + "s");
            sb.AppendLine("Oxygen left: " + oxygen.ToString("0") + " / " + MaxOxygen.ToString("0") + " GAME UNITS");
            sb.AppendLine("Power left: " + power.ToString("0") + " / " + MaxPower.ToString("0") + " GAME UNITS");
            sb.AppendLine("Science data collected: " + science.ToString("0") + " GAME UNITS");
            sb.AppendLine();
            sb.AppendLine("Objectives:");
            foreach (var o in Objectives) sb.AppendLine("  [" + (o.done ? "x" : " ") + "] " + o.label);
            sb.AppendLine();
            sb.AppendLine("Science data log (provenance: source / time / map position):");
            if (Log.Count == 0) sb.AppendLine("  (no data recorded)");
            foreach (var d in Log)
                sb.AppendLine("  +" + d.units.ToString("0") + "  " + d.source + "  @ " + d.time.ToString("0") + "s  (x "
                              + d.position.x.ToString("0") + ", z " + d.position.z.ToString("0") + ")");
            sb.AppendLine();
            sb.AppendLine("Trade-off recap: sprinting burns oxygen twice as fast; the instruments cost power; the solar charger");
            sb.AppendLine("restores power but costs oxygen and can only be used once.");
            sb.AppendLine();
            sb.AppendLine("NOTE: all values are fictional GAME UNITS, not real NASA/engineering numbers. Placeholder prototype.");
            return sb.ToString();
        }
    }
}
