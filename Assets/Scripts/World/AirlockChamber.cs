using System;
using System.Collections;
using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// The airlock capsule on the surface habitat is a walk-in chamber: the astronaut steps in through the outer
    /// hatch, the chamber cycles here (vents, warning beacon, pressure panel, air shower), and the inner door at the HAB end
    /// leads into the habitat. Transform: floor centre of the chamber, local +Z toward the outer hatch.
    /// </summary>
    public class AirlockChamber : MonoBehaviour
    {
        public SlidingDoor InnerDoor { get; private set; }
        public float HalfLength = 1.55f, HalfWidth = 0.95f, Height = 2.23f;

        static readonly Color CalmLight = new Color(0.9f, 0.95f, 1f), AlarmLight = new Color(1f, 0.28f, 0.12f);
        Light lamp; GameObject beacon; ParticleSystem ventL, ventR, shower;
        TextMesh panelText; Renderer panelBack; Material panelRed, panelAmber, panelGreen;

        public static AirlockChamber Create(Transform parent, Vector3 floorCenter, Vector3 towardHatch, float halfLength = 1.55f, float halfWidth = 0.95f, float height = 2.23f)
        {
            var go = new GameObject("Airlock chamber");
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(floorCenter, Quaternion.LookRotation(towardHatch, Vector3.up));
            var c = go.AddComponent<AirlockChamber>();
            c.HalfLength = halfLength; c.HalfWidth = halfWidth; c.Height = height;
            c.Build();
            return c;
        }

        Vector3 W(Vector3 local) => transform.TransformPoint(local);

        void Build()
        {
            var dark = SlidingDoor.LitMat(new Color(0.18f, 0.2f, 0.24f), 0.4f, 0.3f);
            var steel = SlidingDoor.LitMat(new Color(0.75f, 0.77f, 0.8f), 0.6f, 0.8f);
            var red = SlidingDoor.LitMat(new Color(1f, 0.12f, 0.08f), 0.2f, 0f, 2.5f);
            var green = SlidingDoor.LitMat(new Color(0.2f, 1f, 0.4f), 0.2f, 0f, 2.5f);
            // inner door to the HAB (two leaves sliding into the end wall), facing into the chamber
            InnerDoor = SlidingDoor.Create(transform, "Airlock inner door", W(new Vector3(0, 0.96f, -HalfLength - 0.02f)), transform.right, transform.forward,
                                           1.25f, 1.92f, dark, steel, red, green);
            lamp = new GameObject("Chamber lamp").AddComponent<Light>();
            lamp.transform.SetParent(transform, false); lamp.transform.localPosition = new Vector3(0, Height - 0.35f, 0);
            lamp.type = LightType.Point; lamp.range = 4.5f; lamp.intensity = 2.4f; lamp.color = CalmLight; lamp.shadows = LightShadows.None;
            beacon = Part("Warning beacon", new Vector3(0.55f, Height - 0.08f, -0.9f), new Vector3(0.16f, 0.09f, 0.16f), SlidingDoor.LitMat(new Color(1f, 0.25f, 0.05f), 0.2f, 0f, 3f));
            beacon.SetActive(false);
            BuildPanel(new Vector3(-HalfWidth + 0.03f, 1.55f, -0.75f));
            ventL = Mist(new Vector3(-HalfWidth + 0.06f, 1.15f, 0f), Vector3.right);
            ventR = Mist(new Vector3(HalfWidth - 0.06f, 1.15f, 0f), Vector3.left);
            shower = Mist(new Vector3(0f, Height - 0.08f, 0f), Vector3.down);
        }

        GameObject Part(string n, Vector3 local, Vector3 size, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n; Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(transform, false); g.transform.localPosition = local; g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        ParticleSystem Mist(Vector3 local, Vector3 dir)
        {
            var g = new GameObject("Airlock mist"); g.transform.SetParent(transform, false);
            g.transform.localPosition = local; g.transform.localRotation = Quaternion.LookRotation(dir);
            var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.4f); main.maxParticles = 300;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.95f, 1f, 0.35f), new Color(1f, 1f, 1f, 0.15f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18f; sh.radius = 0.25f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                                                new[] { new GradientAlphaKey(0.35f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = SurfaceKit.DustMaterial();
            ps.Play();
            return ps;
        }

        static void Rate(ParticleSystem ps, float r) { if (ps == null) return; var em = ps.emission; em.rateOverTime = r; }

        void BuildPanel(Vector3 local)
        {
            panelRed = SlidingDoor.LitMat(new Color(0.35f, 0.03f, 0.02f), 0.6f, 0f, 1.2f);
            panelAmber = SlidingDoor.LitMat(new Color(0.35f, 0.22f, 0.02f), 0.6f, 0f, 1.2f);
            panelGreen = SlidingDoor.LitMat(new Color(0.03f, 0.25f, 0.08f), 0.6f, 0f, 1.2f);
            panelBack = Part("Airlock status panel", local, new Vector3(0.03f, 0.42f, 0.9f), panelGreen).GetComponent<Renderer>();
            var tg = new GameObject("Airlock status text"); tg.transform.SetParent(transform, false);
            tg.transform.localPosition = local + new Vector3(0.02f, 0f, 0f); tg.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);   // readable from inside the chamber
            panelText = tg.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panelText.font = font; tg.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            panelText.fontSize = 64; panelText.characterSize = 0.017f; panelText.anchor = TextAnchor.MiddleCenter; panelText.alignment = TextAlignment.Center;
            panelText.color = Color.white;
            SetPanel("AIRLOCK\nVACUUM 0 kPa", panelGreen);
        }

        void SetPanel(string text, Material back) { if (panelText) panelText.text = text; if (panelBack) panelBack.sharedMaterial = back; }

        void Alarm(bool on)
        {
            if (beacon) beacon.SetActive(on);
            if (lamp) lamp.color = on ? AlarmLight : CalmLight;
        }

        void Update()
        {
            if (beacon != null && beacon.activeSelf)
            {
                beacon.transform.Rotate(0f, 360f * Time.deltaTime, 0f, Space.Self);
                if (lamp) lamp.intensity = 1.7f + 0.9f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
            }
            else if (lamp) lamp.intensity = 2.4f;
        }

        /// <summary>Coming in: fill from vacuum to cabin pressure, then the air shower blows the dust off while the astronaut turns.</summary>
        public IEnumerator Repressurize(Astronaut who, Action<string> say)
        {
            Alarm(true);
            say?.Invoke("Airlock: outer hatch sealed. Repressurising to cabin pressure.");
            Rate(ventL, 60f); Rate(ventR, 60f);
            for (float t = 0f; t < 4.5f; t += Time.deltaTime)
            {
                SetPanel($"AIRLOCK\nREPRESSURISING\n{Mathf.SmoothStep(0f, 101.3f, t / 4.5f),5:0.0} kPa", panelRed);
                yield return null;
            }
            Rate(ventL, 0f); Rate(ventR, 0f);
            say?.Invoke("Dust removal: air shower on. Lunar dust is sharp and clings to the suit - keep it out of the cabin.");
            SetPanel("AIRLOCK\nDUST REMOVAL\n101.3 kPa", panelAmber);
            Rate(shower, 120f);
            if (who != null) yield return who.Spin(360f, 3.0f); else yield return new WaitForSeconds(3f);
            Rate(shower, 0f);
            Alarm(false);
            SetPanel("AIRLOCK\nPRESSURE OK\n101.3 kPa", panelGreen);
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>Going out: suit leak check, then pump the chamber down to vacuum.</summary>
        public IEnumerator Depressurize(Action<string> say)
        {
            Alarm(true);
            say?.Invoke("Airlock: inner door sealed. Suit leak check...");
            SetPanel("AIRLOCK\nSUIT CHECK\n29.6 kPa  SUIT OK", panelAmber);
            yield return new WaitForSeconds(1.4f);
            say?.Invoke("Depressurising. The air is pumped back into the habitat - nothing is wasted.");
            for (float t = 0f; t < 4.5f; t += Time.deltaTime)
            {
                float kpa = 101.3f * (1f - Mathf.SmoothStep(0f, 1f, t / 4.5f));
                Rate(ventL, 45f * kpa / 101.3f); Rate(ventR, 45f * kpa / 101.3f);
                SetPanel($"AIRLOCK\nDEPRESSURISING\n{kpa,5:0.0} kPa", panelRed);
                yield return null;
            }
            Rate(ventL, 0f); Rate(ventR, 0f);
            Alarm(false);
            SetPanel("AIRLOCK\nVACUUM 0 kPa\nOPENING HATCH", panelGreen);
            yield return new WaitForSeconds(0.5f);
        }

        public void ShowIdle(bool pressurised) => SetPanel(pressurised ? "AIRLOCK\nPRESSURE OK\n101.3 kPa" : "AIRLOCK\nVACUUM 0 kPa", panelGreen);
    }
}
