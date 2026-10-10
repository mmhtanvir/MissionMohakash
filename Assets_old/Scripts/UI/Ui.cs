using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace Mohakash
{
    /// <summary>Tiny helpers for building UI Toolkit screens in code. Styles live in Resources/UI/Mohakash.uss.</summary>
    public static class Ui
    {
        /// <summary>Creates the scene's UIDocument (one per scene) and returns its root.</summary>
        public static VisualElement CreateRoot()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }
            var go = new GameObject("UI");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
            go.SetActive(true);
            var root = doc.rootVisualElement;
            var sheet = Resources.Load<StyleSheet>("UI/Mohakash");
            if (sheet) root.styleSheets.Add(sheet);
            root.AddToClassList("root");
            root.pickingMode = PickingMode.Ignore;
            return root;
        }

        public static T Add<T>(VisualElement parent, T child, params string[] classes) where T : VisualElement
        {
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) child.AddToClassList(c);
            parent?.Add(child);
            return child;
        }

        public static VisualElement Box(VisualElement parent, params string[] classes) => Add(parent, new VisualElement { pickingMode = PickingMode.Ignore }, classes);
        public static VisualElement Panel(VisualElement parent, params string[] classes)
        {
            var v = Add(parent, new VisualElement(), classes); v.AddToClassList("panel"); return v;
        }
        public static Label Text(VisualElement parent, string text, params string[] classes) => Add(parent, new Label(text) { pickingMode = PickingMode.Ignore }, classes);

        public static Button Button(VisualElement parent, string text, Action onClick, params string[] classes)
        {
            var b = Add(parent, new Button(onClick) { text = text }, classes);
            b.AddToClassList("btn");
            return b;
        }

        /// <summary>Status marker: green circle / yellow diamond / red square (shape + colour, GDD §31).</summary>
        public static VisualElement Dot(VisualElement parent, Severity s)
        {
            var d = Box(parent, "dot");
            SetDot(d, s);
            return d;
        }

        public static void SetDot(VisualElement d, Severity s)
        {
            d.RemoveFromClassList("ok"); d.RemoveFromClassList("warn"); d.RemoveFromClassList("crit"); d.RemoveFromClassList("info");
            d.AddToClassList(s == Severity.Good ? "ok" : s == Severity.Warning ? "warn" : s == Severity.Critical ? "crit" : "info");
        }

        public static (VisualElement bar, VisualElement fill) Bar(VisualElement parent, params string[] classes)
        {
            var bar = Box(parent, "bar"); foreach (var c in classes) bar.AddToClassList(c);
            var fill = Box(bar, "bar-fill");
            return (bar, fill);
        }

        public static void SetBar(VisualElement fill, float t, Severity s = Severity.Good)
        {
            fill.style.width = Length.Percent(Mathf.Clamp01(t) * 100f);
            SetDot(fill, s);
        }

        public static void Show(VisualElement v, bool show) => v.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        public static bool Visible(VisualElement v) => v.resolvedStyle.display != DisplayStyle.None && v.style.display != DisplayStyle.None;

        public static Severity Level(float value, float warnBelow, float critBelow) =>
            value < critBelow ? Severity.Critical : value < warnBelow ? Severity.Warning : Severity.Good;
    }
}
