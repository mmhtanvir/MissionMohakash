using UnityEngine.UIElements;

namespace Mohakash
{
    /// <summary>The full controls list, shown in the main menu, the pause menu and in-game (H / F1).</summary>
    public static class ControlsUi
    {
        public static readonly (string group, (string key, string action)[] rows)[] List =
        {
            ("ON FOOT", new[]
            {
                ("W A S D  /  Arrow keys", "Walk"),
                ("Mouse", "Look around"),
                ("Shift (hold)", "Run (uses suit oxygen faster)"),
                ("Space", "Jump (slow, Moon gravity)"),
                ("Mouse wheel", "Zoom camera in / out"),
                ("E", "Use / pick up / set down (seismometer, samples, airlock, radiation post, clean solar panels)"),
            }),
            ("ROVER", new[]
            {
                ("R", "Get in / get out (stand next to the rover)"),
                ("W / S", "Drive forward / backward"),
                ("A / D", "Steer left / right"),
                ("Shift (hold)", "Full speed"),
            }),
            ("MISSION", new[]
            {
                ("M (hold)", "Full map: terrain, sites and your shrinking walk-back circle"),
                ("Tab", "Power panel: switch machines on/off, oxygen maker mode"),
                ("1 / 2 / 3", "Time speed 1x / 2x / 4x (2x and 4x only inside the habitat)"),
                ("P", "Pause / resume mission time"),
                ("E (inside habitat)", "Go back outside"),
                ("H  or  F1", "Show / hide this controls list"),
                ("Esc", "Pause menu (frees the mouse)"),
                ("Left click", "Click buttons / lock the mouse to the game"),
            }),
        };

        public static void Build(VisualElement parent)
        {
            Ui.Text(parent, "CONTROLS", "h1");
            foreach (var (group, rows) in List)
            {
                Ui.Text(parent, group, "section");
                foreach (var (key, action) in rows)
                {
                    var r = Ui.Box(parent, "row", "ctl-row");
                    Ui.Text(r, key, "ctl-key");
                    Ui.Text(r, action, "text-small").style.flexShrink = 1;
                }
            }
        }
    }
}
