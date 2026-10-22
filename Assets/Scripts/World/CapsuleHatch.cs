using System.Collections;
using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Outer hatch of the airlock capsule on SurfaceHabitat.glb (tools/habitat_v3): the folded ladder swings down onto the
    /// landing pad, then the round door unlocks and swings outward to the side on its hinge, clear of the doorway and the
    /// chamber. Closing is the exact reverse (door shut, then the ladder folds up against it). Drives the GLB's own
    /// "hatch_door" and "stairs" nodes, whose rest pose is closed / stowed. API: Open, Wait, SetOpen.
    /// </summary>
    public class CapsuleHatch : MonoBehaviour
    {
        public bool Open;
        public float Duration = 5f;                   // full open or close
        // from tools/habitat_v3/anchors.json, converted for glTFast's X mirror (glTF +Y by +100 -> Unity +Y by -100; glTF -Z by +132 -> Unity +Z by +132)
        const float StairsEnd = 0.45f, UnlockEnd = 0.55f, DoorDeg = -100f, StairsDeg = 132.0f;   // ladder 0..0.45, door 0.55..1

        Transform door, stairs; Quaternion door0, stairs0;
        Renderer lamp; Material red, amber, green; Light recess;
        float k;

        public bool Moving => Mathf.Abs(k - (Open ? 1f : 0f)) > 0.0005f;
        public IEnumerator Wait() { while (Moving) yield return null; }
        public IEnumerator SetOpen(bool open) { Open = open; yield return Wait(); }

        /// <param name="hab">the spawned SurfaceHabitat</param>
        /// <param name="lampPos">status lamp above the doorway (world)</param>
        /// <param name="inside">a point inside the capsule, for the light that comes up as the door opens (world)</param>
        public static CapsuleHatch Attach(Transform hab, Vector3 lampPos, Vector3 inside)
        {
            var h = hab.gameObject.AddComponent<CapsuleHatch>();
            h.door = Find(hab, "hatch_door"); h.stairs = Find(hab, "stairs");
            if (h.door == null || h.stairs == null) { Debug.LogWarning("SurfaceHabitat.glb: hatch_door / stairs node missing"); Destroy(h); return null; }
            h.door0 = h.door.localRotation; h.stairs0 = h.stairs.localRotation;
            h.red = SlidingDoor.LitMat(new Color(1f, 0.12f, 0.08f), 0.2f, 0f, 2.5f);
            h.amber = SlidingDoor.LitMat(new Color(1f, 0.6f, 0.05f), 0.2f, 0f, 2.5f);
            h.green = SlidingDoor.LitMat(new Color(0.2f, 1f, 0.4f), 0.2f, 0f, 2.5f);
            var l = GameObject.CreatePrimitive(PrimitiveType.Cube); l.name = "Hatch status lamp"; Destroy(l.GetComponent<Collider>());
            l.transform.SetParent(hab, true); l.transform.position = lampPos; l.transform.localScale = new Vector3(0.07f, 0.07f, 0.4f);
            h.lamp = l.GetComponent<Renderer>(); h.lamp.sharedMaterial = h.red;
            h.recess = new GameObject("Capsule light").AddComponent<Light>();
            h.recess.transform.SetParent(hab, true); h.recess.transform.position = inside;
            h.recess.type = LightType.Point; h.recess.range = 4f; h.recess.color = new Color(1f, 0.96f, 0.88f); h.recess.intensity = 0f; h.recess.shadows = LightShadows.None;
            return h;
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Find(c, name); if (f != null) return f; }
            return null;
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        void Update()
        {
            k = Mathf.MoveTowards(k, Open ? 1f : 0f, Time.deltaTime / Mathf.Max(0.1f, Duration));
            stairs.localRotation = stairs0 * Quaternion.AngleAxis(StairsDeg * Ease(k / StairsEnd), Vector3.forward);
            door.localRotation = door0 * Quaternion.AngleAxis(DoorDeg * Ease((k - UnlockEnd) / (1f - UnlockEnd)), Vector3.up);
            // lamp: red closed, amber blinking while moving, green open
            lamp.sharedMaterial = Moving ? ((Time.time * 4f) % 1f < 0.5f ? amber : red) : (k > 0.99f ? green : red);
            recess.intensity = 2.2f * Ease((k - UnlockEnd) / 0.2f);
        }
    }
}
