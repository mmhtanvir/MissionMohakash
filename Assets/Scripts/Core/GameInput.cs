using System.Collections.Generic;
using System.Linq;
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

        // ---- touch (UI/TouchControls.cs feeds these; phones/tablets, or "-touch" on desktop to try it) ----
        public static readonly bool Touch = Application.isMobilePlatform || System.Environment.GetCommandLineArgs().Contains("-touch");
        public static Vector2 TouchMove;                                  // virtual stick, -1..1
        public static readonly HashSet<Key> TouchHeld = new HashSet<Key>();   // hold buttons (RUN, MAP)
        static readonly HashSet<Key> tapPending = new HashSet<Key>(), tapNow = new HashSet<Key>();
        static Vector2 lookPending, lookNow; static int tapFrame = -1, lookFrame = -1;
        public static void TouchTap(Key k) => tapPending.Add(k);
        public static void AddTouchLook(Vector2 d) => lookPending += d;
        // a tap or drag that arrives during frame N is seen by every reader for exactly one frame (no matter the script order)
        static bool Tapped(Key k)
        {
            if (tapFrame != Time.frameCount) { tapFrame = Time.frameCount; tapNow.Clear(); tapNow.UnionWith(tapPending); tapPending.Clear(); }
            return tapNow.Contains(k);
        }
        static Vector2 TouchLook
        {
            get { if (lookFrame != Time.frameCount) { lookFrame = Time.frameCount; lookNow = lookPending; lookPending = Vector2.zero; } return lookNow; }
        }
        /// <summary>On touch devices, key names in hints and messages become the on-screen button names.</summary>
        public static string Words(string s)
        {
            if (!Touch || string.IsNullOrEmpty(s)) return s;
            foreach (var (a, b) in new[] { ("(E)", "(USE)"), ("press E", "press USE"), ("Press E", "Press USE"), ("E at the", "USE at the"),
                                          ("(Tab)", "(BASE)"), ("press Tab", "press BASE"), ("Press Tab", "Press BASE"), ("Tab,", "BASE,"), ("(Tab ", "(BASE "),
                                          ("(R)", "(ROVER)"), ("(2/3)", "(2x / 4x at the top)"), ("[C]", "[CAM]"), ("[V]", "[ROBOT]"), (" (O)", ""), ("[Q]", "[WATCH]") })
                s = s.Replace(a, b);
            return s;
        }

        public static bool AnyTap => Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;

        static bool Down(Key k) => simPress == k || Tapped(k) || (K != null && K[k].wasPressedThisFrame);
        static bool Held(Key k) => TouchHeld.Contains(k) || (K != null && K[k].isPressed);

        public static Vector2 Move
        {
            get
            {
                if (SimMove != Vector2.zero) return SimMove;
                if (TouchMove != Vector2.zero) return Vector2.ClampMagnitude(TouchMove, 1f);
                var v = Vector2.zero;
                if (Held(Key.W) || Held(Key.UpArrow)) v.y += 1;
                if (Held(Key.S) || Held(Key.DownArrow)) v.y -= 1;
                if (Held(Key.D) || Held(Key.RightArrow)) v.x += 1;
                if (Held(Key.A) || Held(Key.LeftArrow)) v.x -= 1;
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        public static Vector2 Look => (M != null && !Touch ? M.delta.ReadValue() : Vector2.zero) + TouchLook;
        public static float Zoom => M != null ? M.scroll.ReadValue().y : 0f;
        public static bool Sprint => SimSprint || Held(Key.LeftShift) || Held(Key.RightShift);
        public static bool Jump => Down(Key.Space);
        public static bool Interact => Down(Key.E);
        public static bool Rover => Down(Key.R);
        public static bool Systems => Down(Key.Tab);
        public static bool Inventory => Down(Key.F);
        public static bool Torch => Down(Key.T);
        public static bool Watch => Down(Key.Q);
        public static bool RemoteRover => Down(Key.V);
        public static bool RemoteCamera => Down(Key.C);
        public static bool MapHeld => Held(Key.M);
        public static bool Mark => Down(Key.G);
        public static bool Plan => Down(Key.J);
        public static bool Objectives => Down(Key.O);
        public static bool RightClick => M != null && M.rightButton.wasPressedThisFrame;
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
