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
            if (GameInput.Touch)
            {
                // phones/tablets: a copy (never edit the asset) with a smaller reference size, so text and buttons come out ~1.2x bigger
                var ps = UnityEngine.Object.Instantiate(doc.panelSettings);
                ps.referenceResolution = new Vector2Int(1600, 900);
                doc.panelSettings = ps;
            }
            go.SetActive(true);
            var root = doc.rootVisualElement;
            var sheet = Resources.Load<StyleSheet>("UI/Mohakash");
            if (sheet) root.styleSheets.Add(sheet);
            if (sheet && root.panel != null) root.panel.visualTree.styleSheets.Add(sheet);   // dropdown menus open outside root
            root.AddToClassList("root");
            root.pickingMode = PickingMode.Ignore;
            if (GameInput.Touch)
            {
                // keep everything inside the safe area (notch, rounded corners, home bar). Re-checked while running: right after
                // launch Android can report the screen and the safe area in different orientations, so each inset is capped at 10%.
                root.style.position = Position.Absolute;
                void Safe()
                {
                    float w = Screen.width, h = Screen.height; var s = Screen.safeArea;
                    bool ok = w > 0 && h > 0 && s.width > 0 && s.height > 0 && s.xMax <= w + 1 && s.yMax <= h + 1;
                    float P(float px, float of) => ok ? Mathf.Clamp(100f * px / of, 0f, 10f) : 0f;
                    root.style.left = Length.Percent(P(s.xMin, w)); root.style.right = Length.Percent(P(w - s.xMax, w));
                    root.style.bottom = Length.Percent(P(s.yMin, h)); root.style.top = Length.Percent(P(h - s.yMax, h));
                }
                Safe(); root.schedule.Execute(Safe).Every(500);
            }
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
            var v = Add(parent, new VisualElement(), classes); v.AddToClassList("panel");
            v.style.maxWidth = Length.Percent(98); v.style.maxHeight = Length.Percent(98);   // big fixed-size screens shrink to fit phones / 4:3 tablets
            return v;
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

        public static void SetBorder(VisualElement el, float width, Color color, float radius = 0)
        {
            el.style.borderTopWidth = el.style.borderBottomWidth = el.style.borderLeftWidth = el.style.borderRightWidth = width;
            el.style.borderTopColor = el.style.borderBottomColor = el.style.borderLeftColor = el.style.borderRightColor = color;
            if (radius > 0) SetRadius(el, radius);
        }

        public static void SetBorderWidth(VisualElement el, float width)
        {
            el.style.borderTopWidth = el.style.borderBottomWidth = el.style.borderLeftWidth = el.style.borderRightWidth = width;
        }

        public static void SetBorderColor(VisualElement el, Color color)
        {
            el.style.borderTopColor = el.style.borderBottomColor = el.style.borderLeftColor = el.style.borderRightColor = color;
        }

        public static void SetRadius(VisualElement el, float radius)
        {
            el.style.borderTopLeftRadius = el.style.borderTopRightRadius = el.style.borderBottomLeftRadius = el.style.borderBottomRightRadius = radius;
        }

        public static Severity Level(float value, float warnBelow, float critBelow) =>
            value < critBelow ? Severity.Critical : value < warnBelow ? Severity.Warning : Severity.Good;
    }
}
