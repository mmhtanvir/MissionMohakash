using UnityEngine;
using UnityEngine.UIElements;

namespace Mohakash
{
    /// <summary>
    /// Compact circular HUD widget that shows the current position inside the lunar day.
    /// The ring is split into a bright arc (sun above horizon) and a dark arc (night).
    /// A dot marks where the mission started. A second dot marks "now".
    /// Drawn entirely with UIElements mesh generation (no extra assets needed).
    /// </summary>
    public class LunarDayWidget : VisualElement
    {
        // ---- layout ----
        const float Size = 110f;       // widget width = height
        const float CX = Size / 2f, CY = Size / 2f;
        const float RingOuter = 46f, RingInner = 36f;
        const float DotR = 5f;

        // ---- colours ----
        static readonly Color32 NightArc   = new Color32(18,  30,  70, 220);
        static readonly Color32 DayArc     = new Color32(255, 200, 80, 220);
        static readonly Color32 SunsetArc  = new Color32(255, 130, 40, 200);
        static readonly Color32 NowDot     = new Color32(255, 255, 255, 255);
        static readonly Color32 StartDot   = new Color32(100, 220, 255, 180);
        static readonly Color32 BgCircle   = new Color32(9,   18,  38, 210);
        static readonly Color32 NightTint  = new Color32(30,  50, 120, 100);

        // ---- data ----
        float missionHour;
        bool  isNight;
        Label centerLabel, subLabel;

        public LunarDayWidget()
        {
            style.width  = Size;
            style.height = Size;
            style.flexShrink = 0;

            // Center label: shows time-to-next-event
            centerLabel = new Label { pickingMode = PickingMode.Ignore };
            centerLabel.style.position   = Position.Absolute;
            centerLabel.style.left       = 0; centerLabel.style.right = 0;
            centerLabel.style.top        = Length.Percent(32);
            centerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            centerLabel.style.fontSize   = 13;
            centerLabel.style.color      = new Color(1f, 1f, 1f, 0.92f);
            centerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            Add(centerLabel);

            subLabel = new Label { pickingMode = PickingMode.Ignore };
            subLabel.style.position   = Position.Absolute;
            subLabel.style.left       = 0; subLabel.style.right = 0;
            subLabel.style.top        = Length.Percent(52);
            subLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            subLabel.style.fontSize   = 11;
            subLabel.style.color      = new Color(0.6f, 0.75f, 0.95f, 0.85f);
            Add(subLabel);

            generateVisualContent += Draw;
        }

        /// <summary>Call from MissionUi.Update every frame.</summary>
        public void Tick(float hour)
        {
            missionHour = hour;
            isNight     = LunarClock.IsNight(hour);

            // next sunrise / sunset within a reasonable lookahead
            float nextEvent = NextPhaseChange(hour, 200f);
            float hoursAway  = nextEvent - hour;

            if (isNight)
            {
                centerLabel.text = $"{hoursAway:0.0}h";
                subLabel.text    = "to sunrise";
                centerLabel.style.color = new Color(0.5f, 0.7f, 1f, 0.95f);
            }
            else
            {
                centerLabel.text = $"{hoursAway:0.0}h";
                subLabel.text    = "to night";
                centerLabel.style.color = new Color(1f, 0.88f, 0.4f, 0.95f);
            }

            MarkDirtyRepaint();
        }

        // ------------------------------------------------------------------ drawing

        void Draw(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;

            // background filled circle
            painter.fillColor = BgCircle;
            painter.BeginPath();
            painter.Arc(new Vector2(CX, CY), RingOuter + 4f, 0f, 360f);
            painter.Fill();

            // ring: draw the full ring as night first, then overlay the day arc
            DrawArc(painter, 0f, 360f, NightArc, RingInner, RingOuter);
            DrawDayArcs(painter);

            // night-zone centre tint
            if (isNight)
            {
                painter.fillColor = NightTint;
                painter.BeginPath();
                painter.Arc(new Vector2(CX, CY), RingInner - 1f, 0f, 360f);
                painter.Fill();
            }

            // "start of mission" tick (small dot on ring)
            DrawRingDot(painter, 0f, StartDot, 3.5f);

            // "now" dot
            float nowAngle = PhaseToAngleDeg(LunarClock.Phase(missionHour));
            DrawRingDot(painter, nowAngle, NowDot, DotR);
        }

        // Paints the day arc in two tones: a golden core and warm orange shoulders (dawn/dusk)
        void DrawDayArcs(Painter2D painter)
        {
            // find the angular span of "above horizon"
            const int steps = 360;
            float startA = -1f, endA = -1f;
            for (int i = 0; i <= steps; i++)
            {
                float frac  = i / (float)steps;
                float phase = Mathf.Repeat(LunarClock.StartPhase + frac, 1f);   // phase at mission start + frac of a day
                // We only care about the cycle shape, not the mission clock, for the ring layout
                float elev  = LunarClock.PeakElevationDeg * Mathf.Sin(phase * Mathf.PI * 2f);
                if (elev > -1f)
                {
                    float a = frac * 360f;
                    if (startA < 0f) startA = a;
                    endA = a;
                }
            }

            if (startA < 0f) return;   // always night (very rare config)

            // main golden arc
            float span   = endA - startA;
            float flare  = span * 0.18f;    // dawn/dusk shoulders

            DrawArc(painter, startA, startA + flare, SunsetArc, RingInner, RingOuter);
            DrawArc(painter, startA + flare, endA - flare, DayArc, RingInner, RingOuter);
            DrawArc(painter, endA - flare, endA, SunsetArc, RingInner, RingOuter);
        }

        static void DrawArc(Painter2D painter, float fromDeg, float toDeg, Color32 col, float inner, float outer)
        {
            if (toDeg <= fromDeg) return;
            const int segsPerDeg = 6;
            int n = Mathf.Max(2, Mathf.CeilToInt((toDeg - fromDeg) / segsPerDeg));

            painter.fillColor = col;
            for (int i = 0; i < n; i++)
            {
                float a0 = Deg2Rad(Mathf.Lerp(fromDeg, toDeg, i / (float)n));
                float a1 = Deg2Rad(Mathf.Lerp(fromDeg, toDeg, (i + 1) / (float)n));
                painter.BeginPath();
                painter.MoveTo(new Vector2(CX + Mathf.Cos(a0) * inner, CY + Mathf.Sin(a0) * inner));
                painter.LineTo(new Vector2(CX + Mathf.Cos(a0) * outer, CY + Mathf.Sin(a0) * outer));
                painter.LineTo(new Vector2(CX + Mathf.Cos(a1) * outer, CY + Mathf.Sin(a1) * outer));
                painter.LineTo(new Vector2(CX + Mathf.Cos(a1) * inner, CY + Mathf.Sin(a1) * inner));
                painter.ClosePath();
                painter.Fill();
            }
        }

        static void DrawRingDot(Painter2D painter, float angleDeg, Color32 col, float r)
        {
            float mid  = (RingInner + RingOuter) * 0.5f;
            float rad  = Deg2Rad(angleDeg);
            var   pos  = new Vector2(CX + Mathf.Cos(rad) * mid, CY + Mathf.Sin(rad) * mid);
            painter.fillColor = col;
            painter.BeginPath();
            painter.Arc(pos, r, 0f, 360f);
            painter.Fill();
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Angle on the widget ring for a given lunar phase (0-1). 0 = top, clockwise.</summary>
        static float PhaseToAngleDeg(float phase)
            => phase * 360f - 90f;   // -90 so phase 0 is at the top

        /// <summary>Scan forward in small steps to find the next horizon crossing (night<->day).</summary>
        static float NextPhaseChange(float startHour, float maxHours)
        {
            const float step = 0.25f;
            bool curNight = LunarClock.IsNight(startHour);
            for (float h = startHour + step; h < startHour + maxHours; h += step)
                if (LunarClock.IsNight(h) != curNight) return h;
            return startHour + maxHours;
        }

        static float Deg2Rad(float d) => d * Mathf.PI / 180f;
    }
}
