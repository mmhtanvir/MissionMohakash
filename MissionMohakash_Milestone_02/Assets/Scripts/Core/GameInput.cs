using UnityEngine;
using UnityEngine.InputSystem;

namespace Mohakash
{
    /// <summary>
    /// The only place that reads keyboard/mouse (new Input System; Project Settings > Active Input Handling = "Input System Package").
    /// AutoTest can inject input through the Sim* fields so the game can be driven without a human.
    /// </summary>
    public static class GameInput
    {
        public static Vector2 SimMove;
        public static bool SimSprint;
        static Key? simPress;

        public static void SimTap(Key k) => simPress = k;
        public static void EndFrame() => simPress = null;

        static Keyboard K => Keyboard.current;
        static Mouse M => Mouse.current;

        static bool Down(Key k) => simPress == k || (K != null && K[k].wasPressedThisFrame);
        static bool Held(Key k) => K != null && K[k].isPressed;

        public static Vector2 Move
        {
            get
            {
                if (SimMove != Vector2.zero) return SimMove;
                var v = Vector2.zero;
                if (Held(Key.W) || Held(Key.UpArrow)) v.y += 1;
                if (Held(Key.S) || Held(Key.DownArrow)) v.y -= 1;
                if (Held(Key.D) || Held(Key.RightArrow)) v.x += 1;
                if (Held(Key.A) || Held(Key.LeftArrow)) v.x -= 1;
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        public static Vector2 Look => M != null ? M.delta.ReadValue() : Vector2.zero;
        public static float Zoom => M != null ? M.scroll.ReadValue().y : 0f;
        public static bool Sprint => SimSprint || Held(Key.LeftShift) || Held(Key.RightShift);
        public static bool Jump => Down(Key.Space);
        public static bool Interact => Down(Key.E);
        public static bool Rover => Down(Key.R);
        public static bool Systems => Down(Key.Tab);
        public static bool Pause => Down(Key.Escape);
        public static bool Help => Down(Key.H) || Down(Key.F1);
        public static bool Click => M != null && M.leftButton.wasPressedThisFrame;

        /// <summary>Returns 0 (pause), 1, 2, 4 for the speed keys, or -1 when none pressed.</summary>
        public static int SpeedKey
        {
            get
            {
                if (Down(Key.P)) return 0;
                if (Down(Key.Digit1)) return 1;
                if (Down(Key.Digit2)) return 2;
                if (Down(Key.Digit3)) return 4;
                return -1;
            }
        }
    }
}
