using UnityEngine;

namespace Mohakash
{
    public static class LunarClock
    {
        public const float SynodicDayHours = 708.7f;   // 29.53 Earth days
        public static float StartPhase = 0.46f;        // 0 sunrise, 0.25 noon, 0.5 sunset, 0.75 midnight
        public static float Compression = 1f;          // 1 = real Moon. 9.84 = one full day+night inside the 72 h mission
        public static float PeakElevationDeg = 30f;     // 90 = equator, ~1.5 = true south pole (very dark, long shadows)
        public static float RiseAzimuth = 152f;         // noon lands at 242 deg, where your current sun already is

        public static float Phase(float h) => Mathf.Repeat(StartPhase + h * Compression / SynodicDayHours, 1f);
        public static float ElevationDeg(float h) => PeakElevationDeg * Mathf.Sin(Phase(h) * Mathf.PI * 2f);
        public static float AzimuthDeg(float h) => RiseAzimuth + Phase(h) * 360f;
        public static bool IsNight(float h) => ElevationDeg(h) < -1f;
    }
}
