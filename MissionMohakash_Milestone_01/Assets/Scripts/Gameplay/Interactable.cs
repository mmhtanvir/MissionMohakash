using System.Collections.Generic;
using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// A piece of equipment the player can use with E. Costs/gains are GAME UNITS (fictional abstractions).
    /// Rules are applied by MissionState.Interact().
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        public string displayName = "Equipment";
        public string prompt = "Use";
        [Header("Costs / gains (GAME UNITS)")]
        public float powerCost;
        public float oxygenCost;
        public float powerGain;
        public float scienceGain;
        [Header("Mission logic")]
        public string completesObjective = "";
        public string[] requires = new string[0];
        public bool singleUse = true;
        public bool isFinish;
        public bool isOptional;
        [Header("Visuals")]
        public Renderer statusLight;
        public Transform marker;
        public float markerHeight = 2.2f;

        public bool Used { get; private set; }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Update()
        {
            if (marker == null) return;
            marker.gameObject.SetActive(!Used);
            marker.localPosition = new Vector3(0f, markerHeight + Mathf.Sin(Time.time * 2f) * 0.12f, 0f);
            marker.Rotate(0f, 90f * Time.deltaTime, 0f, Space.Self);
        }

        public void MarkUsed()
        {
            if (!singleUse) return;
            Used = true;
            if (statusLight != null) statusLight.sharedMaterial = Placeholder.Unlit(new Color(0.2f, 1f, 0.35f));
        }

        public string CostText()
        {
            var parts = new List<string>();
            if (powerCost > 0) parts.Add("power -" + powerCost.ToString("0"));
            if (oxygenCost > 0) parts.Add("O2 -" + oxygenCost.ToString("0"));
            if (powerGain > 0) parts.Add("power +" + powerGain.ToString("0"));
            if (scienceGain > 0) parts.Add("data +" + scienceGain.ToString("0"));
            return parts.Count == 0 ? "" : " (" + string.Join(", ", parts.ToArray()) + ")";
        }
    }
}
