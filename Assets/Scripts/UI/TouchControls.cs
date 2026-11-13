using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Mohakash
{
    /// <summary>
    /// On-screen controls for phones and tablets (GameInput.Touch). Everything feeds GameInput, so the game code
    /// doesn't know whether a key or a finger pressed it: left stick = move, drag anywhere else = look,
    /// big buttons bottom-right (USE, JUMP, RUN, ROVER), small buttons above them for the panels.
    /// Hidden while a panel or menu has the pointer (Game.CursorFree).
    /// </summary>
    public static class TouchControls
    {
        const float StickR = 110f, KnobR = 45f, LookSpeed = 1.6f;

        /// <param name="hud">the HUD root: the look pad goes underneath every HUD card, the buttons on top</param>
        /// <param name="extra">shown-or-not per small button (e.g. CAM only while driving the robot rover)</param>
        public static void Build(VisualElement hud, Func<Key, bool> extra = null)
        {
            // look pad: whole screen, under the HUD cards (index 0) so their buttons still get taps
            var pad = new VisualElement { name = "touch-look" };
            Fill(pad); pad.pickingMode = PickingMode.Position;
            int lookId = -1;
            pad.RegisterCallback<PointerDownEvent>(e => { lookId = e.pointerId; pad.CapturePointer(e.pointerId); });
            pad.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId == lookId) GameInput.AddTouchLook(new Vector2(e.deltaPosition.x, -e.deltaPosition.y) * LookSpeed); });
            pad.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == lookId) { pad.ReleasePointer(e.pointerId); lookId = -1; } });
            pad.RegisterCallback<PointerCaptureOutEvent>(_ => lookId = -1);
            hud.Insert(0, pad);

            var layer = new VisualElement { name = "touch-controls" };
            Fill(layer); layer.pickingMode = PickingMode.Ignore;
            hud.Add(layer);

            // move stick, bottom-left
            var stick = Circle(layer, StickR, new Color(1f, 1f, 1f, 0.10f), new Color(1f, 1f, 1f, 0.35f));
            stick.style.left = 40; stick.style.bottom = 40;
            var knob = Circle(stick, KnobR, new Color(1f, 1f, 1f, 0.45f), new Color(1f, 1f, 1f, 0.7f));
            knob.pickingMode = PickingMode.Ignore;
            void Knob(Vector2 off) { knob.style.left = StickR - KnobR + off.x; knob.style.top = StickR - KnobR + off.y; }
            Knob(Vector2.zero);
            int stickId = -1;
            void StickAt(Vector2 local)
            {
                var off = Vector2.ClampMagnitude(local - new Vector2(StickR, StickR), StickR);
                Knob(off);
                GameInput.TouchMove = new Vector2(off.x, -off.y) / StickR;
            }
            void StickUp() { stickId = -1; Knob(Vector2.zero); GameInput.TouchMove = Vector2.zero; }
            stick.RegisterCallback<PointerDownEvent>(e => { stickId = e.pointerId; stick.CapturePointer(e.pointerId); StickAt(e.localPosition); e.StopPropagation(); });
            stick.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId == stickId) StickAt(e.localPosition); });
            stick.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == stickId) { stick.ReleasePointer(e.pointerId); StickUp(); } });
            stick.RegisterCallback<PointerCaptureOutEvent>(_ => StickUp());

            // big action buttons, bottom-right
            var big = new VisualElement(); big.pickingMode = PickingMode.Ignore;
            big.style.position = Position.Absolute; big.style.right = 30; big.style.bottom = 30;
            big.style.flexDirection = FlexDirection.Row; big.style.alignItems = Align.FlexEnd;
            layer.Add(big);
            Btn(big, "ROVER", Key.R, 96, false);
            Btn(big, "RUN", Key.LeftShift, 96, true);
            Btn(big, "JUMP", Key.Space, 110, false);
            Btn(big, "USE", Key.E, 140, false, new Color(1f, 0.59f, 0.24f, 0.55f));

            // small panel buttons above them
            var small = new VisualElement(); small.pickingMode = PickingMode.Ignore;
            small.style.position = Position.Absolute; small.style.right = 30; small.style.bottom = 190;
            small.style.flexDirection = FlexDirection.Row; small.style.flexWrap = Wrap.Wrap; small.style.justifyContent = Justify.FlexEnd;
            small.style.maxWidth = 640;
            layer.Add(small);
            var keyed = new System.Collections.Generic.List<(VisualElement b, Key k)>();
            foreach (var (label, key, hold) in new[] { ("BASE", Key.Tab, false), ("MAP", Key.M, true), ("PLAN", Key.J, false), ("BAG", Key.F, false),
                                                     ("WATCH", Key.Q, false), ("LIGHT", Key.T, false), ("ROBOT", Key.V, false), ("CAM", Key.C, false), ("MENU", Key.Escape, false) })
                keyed.Add((Btn(small, label, key, 76, hold), key));

            layer.schedule.Execute(() =>
            {
                bool on = !Game.CursorFree;
                pad.style.display = layer.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                if (!on) { StickUp(); GameInput.TouchHeld.Clear(); }
                if (extra != null) foreach (var (b, k) in keyed) b.style.display = extra(k) ? DisplayStyle.Flex : DisplayStyle.None;
            }).Every(100);
        }

        static void Fill(VisualElement v)
        {
            v.style.position = Position.Absolute; v.style.left = 0; v.style.top = 0; v.style.right = 0; v.style.bottom = 0;
        }

        static VisualElement Circle(VisualElement parent, float r, Color fill, Color border)
        {
            var c = new VisualElement(); c.pickingMode = PickingMode.Position;
            c.style.position = Position.Absolute; c.style.width = c.style.height = r * 2f;
            c.style.borderTopLeftRadius = c.style.borderTopRightRadius = c.style.borderBottomLeftRadius = c.style.borderBottomRightRadius = r;
            c.style.backgroundColor = fill;
            c.style.borderTopWidth = c.style.borderBottomWidth = c.style.borderLeftWidth = c.style.borderRightWidth = 3;
            c.style.borderTopColor = c.style.borderBottomColor = c.style.borderLeftColor = c.style.borderRightColor = border;
            parent.Add(c);
            return c;
        }

        /// <summary>Round button: a tap presses the key once; a hold button keeps it held while the finger is down.</summary>
        static VisualElement Btn(VisualElement parent, string text, Key key, float size, bool hold, Color? fill = null)
        {
            var b = Circle(parent, size / 2f, fill ?? new Color(0.06f, 0.1f, 0.18f, 0.6f), new Color(1f, 1f, 1f, 0.55f));
            b.style.position = Position.Relative; b.style.marginLeft = 12; b.style.marginTop = 10;
            b.style.justifyContent = Justify.Center; b.style.alignItems = Align.Center;
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.color = Color.white; l.style.fontSize = size > 100 ? 26 : 18; l.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.Add(l);
            b.RegisterCallback<PointerDownEvent>(e =>
            {
                b.CapturePointer(e.pointerId);
                if (hold) GameInput.TouchHeld.Add(key); else GameInput.TouchTap(key);
                b.style.opacity = 0.6f; e.StopPropagation();
            });
            void Up() { if (hold) GameInput.TouchHeld.Remove(key); b.style.opacity = 1f; }
            b.RegisterCallback<PointerUpEvent>(e => { b.ReleasePointer(e.pointerId); Up(); });
            b.RegisterCallback<PointerCaptureOutEvent>(_ => Up());
            return b;
        }
    }
}
