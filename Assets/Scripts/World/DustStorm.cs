using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Mars dust storm look: thick orange haze instead of darkness, wind-blown dust streaks and rolling dust clouds
    /// around the camera, gusting. Tick after MoonWorld.SetSunNatural (it overrides fog/ambient/sun each frame).
    /// </summary>
    public class DustStorm : MonoBehaviour
    {
        static readonly Color DustAir = new Color(0.62f, 0.40f, 0.24f);
        ParticleSystem streaks, clouds, grit;
        Vector3 wind = new Vector3(1f, 0f, 0.35f).normalized;

        public static DustStorm Create(Transform parent)
        {
            var ds = new GameObject("Dust storm").AddComponent<DustStorm>();
            ds.transform.SetParent(parent);
            ds.streaks = ds.Make("Streaks", 1400, new Vector2(0.05f, 0.14f), new Vector2(0.6f, 1.4f), 0.55f, true);
            ds.clouds  = ds.Make("Clouds", 160, new Vector2(6f, 14f), new Vector2(2.5f, 4.5f), 0.22f, false);
            ds.grit    = ds.Make("Grit", 500, new Vector2(0.4f, 1.2f), new Vector2(1.0f, 2.0f), 0.35f, false);
            return ds;
        }

        ParticleSystem Make(string name, int max, Vector2 size, Vector2 life, float alpha, bool stretch)
        {
            var g = new GameObject(name); g.transform.SetParent(transform);
            var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = true; m.maxParticles = max; m.playOnAwake = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            m.startSpeed = 0f;
            m.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.52f, 0.32f, alpha), new Color(0.55f, 0.34f, 0.2f, alpha * 0.7f));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = stretch ? new Vector3(40f, 8f, 40f) : new Vector3(70f, 6f, 70f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = SurfaceKit.DustMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            if (stretch) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.12f; r.lengthScale = 2f; }
            return ps;
        }

        /// <summary>k: 0 = calm, 1 = full storm. inside: in the base, no dust in the room, light haze only.</summary>
        public void Tick(float k, bool inside, Light sun)
        {
            var cam = Camera.main;
            bool on = k > 0.01f && !inside && cam != null;
            foreach (var ps in new[] { streaks, clouds, grit })
            {
                if (on && !ps.isPlaying) ps.Play();
                else if (!on && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (k <= 0.01f) return;

            // gusts: slow swell + fast flutter, wind direction wanders a little
            float t = Time.time;
            float gust = Mathf.Clamp01(0.55f + 0.35f * (Mathf.PerlinNoise(t * 0.25f, 0.3f) * 2f - 1f) + 0.15f * Mathf.Sin(t * 3.1f));
            Vector3 w = Quaternion.Euler(0f, (Mathf.PerlinNoise(0.7f, t * 0.05f) - 0.5f) * 50f, 0f) * wind;
            float speed = Mathf.Lerp(14f, 30f, gust) * k;

            if (on)
            {
                Vector3 c = cam.transform.position, up = c - w * 14f;   // emit upwind so the dust blows past the player
                Drive(streaks, up + Vector3.up * 1f, w, speed, 1400f * k * (0.6f + gust), 0.25f);
                Drive(clouds, c - w * 30f + Vector3.up * 1.5f, w, speed * 0.45f, 55f * k * (0.6f + gust), 0.12f);
                Drive(grit, up + Vector3.down * 0.5f, w, speed * 0.7f, 260f * k, 0.5f);
            }

            // haze, not night: orange air swallows the distance, ambient lifts toward the dust colour
            float haze = k * Mathf.Lerp(0.8f, 1.15f, gust);
            Color air = DustAir * Mathf.Lerp(1f, 0.55f, k);
            RenderSettings.fogDensity = Mathf.Lerp(0.0025f, inside ? 0.006f : 0.045f, haze);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, air, k);
            if (cam) cam.backgroundColor = RenderSettings.fogColor;
            Color amb = air * 0.9f;
            RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, amb, k * 0.8f);
            RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, amb * 0.85f, k * 0.8f);
            RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor, amb * 0.5f, k * 0.8f);
            if (sun)
            {
                sun.intensity *= Mathf.Lerp(1f, 0.4f, haze);
                sun.shadowStrength *= Mathf.Lerp(1f, 0.25f, k);
                sun.color = Color.Lerp(sun.color, new Color(1f, 0.7f, 0.45f), k);
            }
        }

        static void Drive(ParticleSystem ps, Vector3 pos, Vector3 w, float speed, float rate, float turbulence)
        {
            ps.transform.position = pos;
            ps.transform.rotation = Quaternion.LookRotation(w);
            var em = ps.emission; em.rateOverTime = rate;
            var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
            float tu = speed * turbulence;
            v.x = new ParticleSystem.MinMaxCurve(w.x * speed - tu, w.x * speed + tu);
            v.y = new ParticleSystem.MinMaxCurve(-tu * 0.3f, tu * 0.5f);
            v.z = new ParticleSystem.MinMaxCurve(w.z * speed - tu, w.z * speed + tu);
        }
    }
}
