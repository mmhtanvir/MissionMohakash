using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Mohakash
{
    /// <summary>
    /// The ONLY place in the project that reads input. Uses the new Input System when
    /// Active Input Handling is "Input System Package (New)" or "Both"; falls back to the
    /// legacy Input Manager only when the project is set to "Input Manager (Old)".
    /// No other script may touch Keyboard/Mouse/Input directly.
    /// </summary>
    public static class InputReader
    {
#if ENABLE_INPUT_SYSTEM
        public static Vector2 Move
        {
            get
            {
                var k = Keyboard.current;
                if (k == null) return Vector2.zero;
                float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
                float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
                var v = new Vector2(x, y);
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }
        public static Vector2 LookDelta
        {
            get { var m = Mouse.current; return m == null ? Vector2.zero : m.delta.ReadValue() * 0.09f; }
        }
        public static bool Sprint { get { var k = Keyboard.current; return k != null && k.leftShiftKey.isPressed; } }
        public static bool JumpPressed { get { var k = Keyboard.current; return k != null && k.spaceKey.wasPressedThisFrame; } }
        public static bool InteractPressed { get { var k = Keyboard.current; return k != null && k.eKey.wasPressedThisFrame; } }
        public static bool EscapePressed { get { var k = Keyboard.current; return k != null && k.escapeKey.wasPressedThisFrame; } }
        public static bool RestartPressed { get { var k = Keyboard.current; return k != null && k.rKey.wasPressedThisFrame; } }
        public static bool ClickPressed { get { var m = Mouse.current; return m != null && m.leftButton.wasPressedThisFrame; } }
#else
        public static Vector2 Move
        {
            get
            {
                var v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }
        public static Vector2 LookDelta { get { return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 3f; } }
        public static bool Sprint { get { return Input.GetKey(KeyCode.LeftShift); } }
        public static bool JumpPressed { get { return Input.GetKeyDown(KeyCode.Space); } }
        public static bool InteractPressed { get { return Input.GetKeyDown(KeyCode.E); } }
        public static bool EscapePressed { get { return Input.GetKeyDown(KeyCode.Escape); } }
        public static bool RestartPressed { get { return Input.GetKeyDown(KeyCode.R); } }
        public static bool ClickPressed { get { return Input.GetMouseButtonDown(0); } }
#endif
    }
}
