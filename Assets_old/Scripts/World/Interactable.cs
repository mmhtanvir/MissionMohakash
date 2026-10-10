using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mohakash
{
    /// <summary>Something the astronaut can use with E. Prompt/availability are delegates so the mission runner owns the rules.</summary>
    public class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        public float Radius = 3f;
        public Func<string> Prompt = () => "Use";
        public Func<bool> Available = () => true;
        public Action Use = () => { };
        public string Label;                  // HUD marker text (null = no marker)
        public Color MarkerColor = new Color(0.25f, 0.82f, 0.88f);
        public Func<bool> ShowMarker = () => true;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static Interactable Create(string name, Vector3 pos, float radius, Transform parent)
        {
            var go = new GameObject("Interact: " + name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var i = go.AddComponent<Interactable>();
            i.Radius = radius;
            i.Label = name;
            return i;
        }

        public static Interactable Nearest(Vector3 p)
        {
            Interactable best = null; float bestD = float.MaxValue;
            foreach (var i in All)
            {
                if (!i.isActiveAndEnabled || !i.Available()) continue;
                var d = Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(i.transform.position.x, 0, i.transform.position.z));
                if (d <= i.Radius && d < bestD) { best = i; bestD = d; }
            }
            return best;
        }
    }
}
