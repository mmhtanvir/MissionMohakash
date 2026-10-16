using System.Collections;
using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Outer airlock hatch, spacecraft style: unlock (status lamp blinks amber, lever turns, bolts retract) -> seal break
    /// (hatch pops out a few cm with a puff of residual air and dust) -> swings outward on its hinge with an ease and a small
    /// settle; closing is the exact reverse ending in pull-in and lock. Same API as SlidingDoor (Open, Wait, SetOpen).
    /// </summary>
    public class HingedHatch : MonoBehaviour
    {
        public bool Open;
        public float Duration = 2.8f;                 // full open or close
        const float UnlockEnd = 0.16f, PopEnd = 0.28f, SwingDeg = 100f, PopOut = 0.05f;

        Transform hinge, slab, lever; Transform[] bolts; Vector3[] boltHome;
        Renderer lamp; Material red, amber, green;
        Light recessLight; ParticleSystem puff;
        float k; bool puffedOpen, puffedClose;
        Vector3 hingeHome; float hingeSign;

        public bool Moving => Mathf.Abs(k - (Open ? 1f : 0f)) > 0.0005f;
        public IEnumerator Wait() { while (Moving) yield return null; }
        public IEnumerator SetOpen(bool open) { Open = open; yield return Wait(); }

        /// <param name="center">centre of the closed hatch (world)</param>
        /// <param name="outward">direction the hatch faces / swings toward</param>
        /// <param name="hingeSide">direction from the centre to the hinge edge</param>
        public static HingedHatch Create(Transform parent, string name, Vector3 center, Vector3 outward, Vector3 hingeSide, float width, float height, Vector3 panelOffset)
        {
            var root = new GameObject(name).transform; root.SetParent(parent, true);
            root.position = center; root.rotation = Quaternion.LookRotation(outward, Vector3.up);
            var h = root.gameObject.AddComponent<HingedHatch>();
            float side = Vector3.Dot(root.right, hingeSide) >= 0f ? 1f : -1f;   // hinge on local +X or -X
            h.hingeSign = side;

            var hull = SlidingDoor.LitMat(new Color(0.88f, 0.89f, 0.91f), 0.45f, 0.15f);
            var dark = SlidingDoor.LitMat(new Color(0.16f, 0.17f, 0.2f), 0.4f, 0.4f);
            var steel = SlidingDoor.LitMat(new Color(0.72f, 0.74f, 0.78f), 0.6f, 0.85f);
            var stripe = SlidingDoor.LitMat(new Color(0.95f, 0.45f, 0.08f), 0.3f);
            var glass = SlidingDoor.LitMat(new Color(0.25f, 0.55f, 0.85f), 0.9f, 0f, 0.6f);
            h.red = SlidingDoor.LitMat(new Color(1f, 0.12f, 0.08f), 0.2f, 0f, 2.5f);
            h.amber = SlidingDoor.LitMat(new Color(1f, 0.6f, 0.05f), 0.2f, 0f, 2.5f);
            h.green = SlidingDoor.LitMat(new Color(0.2f, 1f, 0.4f), 0.2f, 0f, 2.5f);

            // hinge line on the chosen edge; the slab hangs off it
            h.hinge = new GameObject("Hinge").transform; h.hinge.SetParent(root, false);
            h.hinge.localPosition = new Vector3(side * width / 2, 0, 0); h.hingeHome = h.hinge.localPosition;
            h.slab = new GameObject("Hatch").transform; h.slab.SetParent(h.hinge, false);
            h.slab.localPosition = new Vector3(-side * width / 2, 0, 0);
            Part(h.slab, "Slab", Vector3.zero, new Vector3(width, height, 0.12f), hull, true);
            Part(h.slab, "Seal rim", new Vector3(0, 0, 0.065f), new Vector3(width - 0.06f, height - 0.06f, 0.01f), dark);
            Part(h.slab, "Face", new Vector3(0, 0, 0.072f), new Vector3(width - 0.2f, height - 0.2f, 0.01f), hull);
            foreach (float y in new[] { height / 2 - 0.12f, -height / 2 + 0.12f }) Part(h.slab, "Stripe", new Vector3(0, y, 0.08f), new Vector3(width - 0.22f, 0.05f, 0.01f), stripe);
            Cyl(h.slab, "Porthole frame", new Vector3(0, height * 0.22f, 0.08f), 0.17f, 0.03f, dark);
            Cyl(h.slab, "Porthole", new Vector3(0, height * 0.22f, 0.095f), 0.12f, 0.01f, glass);
            for (int i = 0; i < 2; i++) Cyl(h.slab, "Hinge knuckle", new Vector3(side * (width / 2 - 0.03f), (i == 0 ? 1 : -1) * height * 0.32f, 0.0f), 0.05f, 0.22f, steel, vertical: true);
            // lever handle near the free edge (turns 90 deg to unlock)
            h.lever = new GameObject("Lever").transform; h.lever.SetParent(h.slab, false);
            h.lever.localPosition = new Vector3(-side * (width / 2 - 0.22f), -0.05f, 0.1f);
            Cyl(h.lever, "Lever hub", Vector3.zero, 0.05f, 0.04f, steel);
            Part(h.lever, "Lever arm", new Vector3(0, -0.12f, 0.02f), new Vector3(0.05f, 0.26f, 0.04f), steel);
            // locking bolts on the free edge (retract into the slab)
            h.bolts = new Transform[3]; h.boltHome = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                var b = new GameObject("Bolt").transform; b.SetParent(h.slab, false);
                b.localPosition = new Vector3(-side * (width / 2 + 0.03f), (i - 1) * height * 0.35f, 0f);
                Part(b, "Bolt body", Vector3.zero, new Vector3(0.08f, 0.07f, 0.07f), steel);
                h.bolts[i] = b; h.boltHome[i] = b.localPosition;
            }
            // status lamp over the doorway and the button panel beside it (fixed to the tunnel, not the hatch)
            var lampGo = Part(root, "Hatch status lamp", new Vector3(0, height / 2 + 0.16f, 0.04f), new Vector3(0.4f, 0.07f, 0.05f), h.red);
            h.lamp = lampGo.GetComponent<Renderer>();
            var panel = Part(root, "Hatch control panel", panelOffset, new Vector3(0.22f, 0.32f, 0.06f), dark);
            Part(panel.transform, "Button", new Vector3(0, 0.12f, 0.6f), new Vector3(0.45f, 0.3f, 0.6f), h.green);
            Part(panel.transform, "Display", new Vector3(0, -0.15f, 0.6f), new Vector3(0.7f, 0.25f, 0.4f), glass);
            // light inside the recess comes up as the hatch opens
            h.recessLight = new GameObject("Recess light").AddComponent<Light>();
            h.recessLight.transform.SetParent(root, false); h.recessLight.transform.localPosition = new Vector3(0, height * 0.35f, -0.6f);
            h.recessLight.type = LightType.Point; h.recessLight.range = 3f; h.recessLight.color = new Color(1f, 0.96f, 0.88f); h.recessLight.intensity = 0f; h.recessLight.shadows = LightShadows.None;
            h.puff = MakePuff(root, height);
            return h;
        }

        static GameObject Part(Transform parent, string n, Vector3 pos, Vector3 size, Material mat, bool collider = false)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n;
            if (!collider) Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        static void Cyl(Transform parent, string n, Vector3 pos, float r, float depth, Material mat, bool vertical = false)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = n; Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false); g.transform.localPosition = pos;
            g.transform.localRotation = vertical ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f);
            g.transform.localScale = vertical ? new Vector3(r * 2, depth / 2, r * 2) : new Vector3(r * 2, depth / 2, r * 2);
            g.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static ParticleSystem MakePuff(Transform root, float height)
        {
            var g = new GameObject("Seal puff"); g.transform.SetParent(root, false); g.transform.localPosition = new Vector3(0, -height / 2 + 0.1f, 0.15f);
            var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.4f); main.gravityModifier = 0.03f; main.maxParticles = 120;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.83f, 0.78f, 0.5f), new Color(0.95f, 0.95f, 0.95f, 0.25f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(1.2f, 0.1f, 0.1f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                                                new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2f));
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = SurfaceKit.DustMaterial();
            return ps;
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        void Update()
        {
            float before = k;
            k = Mathf.MoveTowards(k, Open ? 1f : 0f, Time.deltaTime / Mathf.Max(0.1f, Duration));
            // unlock: lever turns, bolts retract
            float u = Ease(k / UnlockEnd);
            lever.localRotation = Quaternion.Euler(0f, 0f, hingeSign * 90f * u);
            for (int i = 0; i < bolts.Length; i++) bolts[i].localPosition = boltHome[i] + Vector3.right * hingeSign * 0.07f * u;
            // seal break: pop straight out
            float pop = Ease((k - UnlockEnd) / (PopEnd - UnlockEnd));
            hinge.localPosition = hingeHome + Vector3.forward * PopOut * pop;
            // swing with a small settle at the end
            float s = Mathf.Clamp01((k - PopEnd) / (1f - PopEnd));
            float swing = Ease(s) + 0.035f * Mathf.Sin(s * Mathf.PI * 2f) * s * s;
            hinge.localRotation = Quaternion.Euler(0f, hingeSign * SwingDeg * swing, 0f);   // free edge swings outward (+local Z)
            // seal puffs: breaking the seal on the way out, closing thump on the way in
            if (Open && before < UnlockEnd + 0.02f && k >= UnlockEnd + 0.02f && !puffedOpen) { puff.Emit(60); puffedOpen = true; puffedClose = false; }
            if (!Open && before > PopEnd && k <= PopEnd && !puffedClose) { puff.Emit(25); puffedClose = true; puffedOpen = false; }
            // lamp: red closed, amber blinking while moving, green open
            bool moving = Moving;
            if (lamp) lamp.sharedMaterial = moving ? ((Time.time * 4f) % 1f < 0.5f ? amber : red) : (k > 0.99f ? green : red);
            if (recessLight) recessLight.intensity = 2.2f * Ease((k - UnlockEnd) / 0.3f);
        }
    }
}
