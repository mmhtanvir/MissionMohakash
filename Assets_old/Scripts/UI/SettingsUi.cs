using UnityEngine.UIElements;

namespace Mohakash
{
    /// <summary>Settings controls shared by the main menu and the pause menu.</summary>
    public static class SettingsUi
    {
        public static void Build(VisualElement parent)
        {
            var box = Ui.Box(parent, "col"); box.style.marginTop = 8; box.style.marginBottom = 8;
            var sens = Ui.Add(box, new Slider("Mouse sensitivity", 0.2f, 3f) { value = Game.MouseSensitivity });
            sens.RegisterValueChangedCallback(e => Game.MouseSensitivity = e.newValue);
            var vol = Ui.Add(box, new Slider("Volume", 0f, 1f) { value = Game.Volume });
            vol.RegisterValueChangedCallback(e => Game.Volume = e.newValue);
            var inv = Ui.Add(box, new Toggle("Invert mouse Y") { value = Game.InvertY });
            inv.RegisterValueChangedCallback(e => Game.InvertY = e.newValue);
            var hints = Ui.Add(box, new Toggle("Beginner hints") { value = Game.Hints });
            hints.RegisterValueChangedCallback(e => Game.Hints = e.newValue);
        }
    }
}
