using System.Collections;
using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Two-leaf airlock door: the leaves slide apart into the frame with an ease-in/ease-out motion and close the same way.
    /// Open = true/false sets the target; Wait() yields until the motion has finished.
    /// </summary>
    public class SlidingDoor : MonoBehaviour
    {
        public bool Open;
        public float Duration = 1.1f;          // seconds for a full open or close
        Transform left, right; Vector3 axis; float travel; float k;   // k: 0 closed .. 1 open
        Renderer statusLight; Material red, green;

        public bool Moving => Mathf.Abs(k - (Open ? 1f : 0f)) > 0.001f;
        public IEnumerator Wait() { while (Moving) yield return null; }
        public IEnumerator SetOpen(bool open) { Open = open; yield return Wait(); }

        /// <param name="center">centre of the closed doorway (world, bottom at center.y - height/2)</param>
        /// <param name="slide">direction the right leaf moves (world)</param>
        /// <param name="normal">direction the door faces (toward the side the player sees it from)</param>
        public static SlidingDoor Create(Transform parent, string name, Vector3 center, Vector3 slide, Vector3 normal, float width, float height,
                                         Material leaf, Material trim, Material redGlow, Material greenGlow)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, true);
            root.position = center; root.rotation = Quaternion.LookRotation(normal, Vector3.up);
            var d = root.gameObject.AddComponent<SlidingDoor>();
            d.axis = root.InverseTransformDirection(slide).normalized; d.travel = width * 0.5f + 0.02f;
            float w = width * 0.5f;
            d.left = Leaf(root, "Leaf L", new Vector3(-w / 2, 0, 0), new Vector3(w, height, 0.08f), leaf, trim);
            d.right = Leaf(root, "Leaf R", new Vector3(w / 2, 0, 0), new Vector3(w, height, 0.08f), leaf, trim);
            // status lamp above the doorway: red while closed or moving, green when open
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube); lamp.name = "Door status";
            Destroy(lamp.GetComponent<Collider>());
            lamp.transform.SetParent(root, false); lamp.transform.localPosition = new Vector3(0, height / 2 + 0.12f, 0.06f); lamp.transform.localScale = new Vector3(0.5f, 0.08f, 0.03f);
            d.statusLight = lamp.GetComponent<Renderer>(); d.red = redGlow; d.green = greenGlow; d.statusLight.sharedMaterial = redGlow;
            return d;
        }

        static Transform Leaf(Transform root, string n, Vector3 pos, Vector3 size, Material leaf, Material trim)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n;
            g.transform.SetParent(root, false); g.transform.localPosition = pos; g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = leaf;
            // window slot + kick plate so the motion reads clearly
            foreach (var (p, s) in new[] { (new Vector3(0, 0.22f, 0.55f), new Vector3(0.35f, 0.18f, 0.2f)), (new Vector3(0, -0.42f, 0.55f), new Vector3(0.9f, 0.08f, 0.2f)) })
            {
                var t = GameObject.CreatePrimitive(PrimitiveType.Cube); t.name = "trim"; Destroy(t.GetComponent<Collider>());
                t.transform.SetParent(g.transform, false); t.transform.localPosition = p; t.transform.localScale = s;
                t.GetComponent<Renderer>().sharedMaterial = trim;
            }
            return g.transform;
        }

        /// <summary>URP Lit material (same shader as the moon rock, so it is always in the build).</summary>
        public static Material LitMat(Color c, float smooth = 0.4f, float metal = 0f, float emit = 0f)
        {
            var rock = Resources.Load<Material>("Materials/MoonRock");
            var m = new Material(rock != null ? rock.shader : Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            if (emit > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * emit); }
            return m;
        }

        Vector3 leftHome, rightHome; bool homed;

        void Update()
        {
            if (!homed) { leftHome = left.localPosition; rightHome = right.localPosition; homed = true; }
            k = Mathf.MoveTowards(k, Open ? 1f : 0f, Time.deltaTime / Mathf.Max(0.05f, Duration));
            float e = k * k * (3f - 2f * k);                      // ease in / ease out
            left.localPosition = leftHome - axis * travel * e;
            right.localPosition = rightHome + axis * travel * e;
            if (statusLight) statusLight.sharedMaterial = (Open && !Moving) ? green : red;
        }
    }
}
