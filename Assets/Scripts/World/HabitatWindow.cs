using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mohakash
{
    /// <summary>
    /// The habitat interior is built far from the lunar surface (HabitatInterior.Origin), so its window would show empty
    /// space. This makes the window a portal: a camera standing just outside the real HAB dome renders the actual terrain,
    /// outpost, Earth and sky with an off-axis projection that matches the player's eye through the window, so the view has
    /// correct parallax (not a flat picture). The glass shows that render as emission.
    /// </summary>
    public class HabitatWindow : MonoBehaviour
    {
        public MoonWorld World;
        public Func<bool> Active;            // true while the player is inside
        float outsideY, outsideZ;            // where this window is on the real HAB (height above its ground, z offset)

        Vector3 inCenter; float width, height;
        Camera cam; RenderTexture rt; Renderer glass;
        float habRadius = -1f;

        /// <param name="center">window centre in world space (interior); the window faces -X, looking out toward +X</param>
        /// <param name="outsideY">height of the matching window on the real HAB above its ground</param>
        /// <param name="outsideZ">z of the matching window on the real HAB (relative to the HAB)</param>
        /// <param name="round">round porthole (disc) instead of a rectangle; width = height = diameter</param>
        public static HabitatWindow Create(Transform parent, Vector3 center, float width, float height, MoonWorld world, Func<bool> active, Material frameShader,
                                           float outsideY, float outsideZ, bool round = false)
        {
            GameObject go;
            if (round) { go = new GameObject("Porthole view (portal)"); go.AddComponent<MeshFilter>().sharedMesh = Disc(); go.AddComponent<MeshRenderer>(); }
            else { go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = "Window view (portal)"; Destroy(go.GetComponent<Collider>()); }
            go.transform.SetParent(parent, true);
            go.transform.position = center;
            go.transform.rotation = Quaternion.LookRotation(Vector3.right);   // visible face toward -X (the room)
            go.transform.localScale = new Vector3(width, height, 1f);
            var w = go.AddComponent<HabitatWindow>();
            w.World = world; w.Active = active; w.inCenter = center; w.width = width; w.height = height;
            w.outsideY = outsideY; w.outsideZ = outsideZ;

            w.rt = round ? new RenderTexture(512, 512, 24) { name = "HabitatPortholeRT", antiAliasing = 2 }
                         : new RenderTexture(1280, 512, 24) { name = "HabitatWindowRT", antiAliasing = 2 };
            // Lit shader (always in the build): black glossy base for glass reflections, the outside view as emission
            var mat = new Material(frameShader) { name = "Window glass" };
            mat.SetColor("_BaseColor", Color.black);
            mat.SetFloat("_Smoothness", 0.92f); mat.SetFloat("_Metallic", 0f);
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", w.rt); mat.SetColor("_EmissionColor", Color.white);
            w.glass = go.GetComponent<Renderer>(); w.glass.sharedMaterial = mat;
            w.glass.shadowCastingMode = ShadowCastingMode.Off;

            var cg = new GameObject("Window camera");
            cg.transform.SetParent(go.transform.parent, false);
            w.cam = cg.AddComponent<Camera>();
            w.cam.targetTexture = w.rt;
            w.cam.depth = -10;
            w.cam.clearFlags = CameraClearFlags.SolidColor; w.cam.backgroundColor = Color.black;
            w.cam.cullingMask = ~(1 << 2);   // no player / ignore-raycast helpers
            w.cam.farClipPlane = 3000f;
            w.cam.enabled = false;
            return w;
        }

        /// <summary>unit-diameter disc in the XY plane facing -Z (like Unity's Quad), UVs over its bounding square</summary>
        static Mesh disc;
        static Mesh Disc()
        {
            if (disc != null) return disc;
            const int n = 48; var v = new Vector3[n + 1]; var uv = new Vector2[n + 1]; var t = new int[n * 3];
            v[0] = Vector3.zero; uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n; v[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f);
                uv[i + 1] = new Vector2(v[i + 1].x + 0.5f, v[i + 1].y + 0.5f);
                t[i * 3] = 0; t[i * 3 + 1] = 1 + (i + 1) % n; t[i * 3 + 2] = 1 + i;      // clockwise seen from -Z = front face
            }
            disc = new Mesh { name = "PortholeDisc", vertices = v, uv = uv, triangles = t };
            disc.RecalculateNormals(); disc.RecalculateBounds();
            return disc;
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += Begin;
            RenderPipelineManager.endCameraRendering += End;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Begin;
            RenderPipelineManager.endCameraRendering -= End;
        }

        void OnDestroy() { if (rt != null) rt.Release(); }

        void Begin(ScriptableRenderContext ctx, Camera c) { if (c == cam && World != null) World.CenterSky(c.transform.position); }
        void End(ScriptableRenderContext ctx, Camera c) { if (c == cam && World != null && Camera.main != null) World.CenterSky(Camera.main.transform.position); }

        Vector3 OutsideCenter()
        {
            var hab = World.Hab;
            if (habRadius < 0f)
            {
                var b = new Bounds(hab.position, Vector3.zero);
                foreach (var r in hab.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                habRadius = Mathf.Max(b.extents.x, 3f);
            }
            // window plane just outside the dome wall, so everything between the eye and the glass (the dome itself) is clipped
            return hab.position + new Vector3(habRadius + 0.3f, outsideY, outsideZ);
        }

        void LateUpdate()
        {
            var main = Camera.main;
            bool on = World != null && World.Hab != null && main != null && (Active == null || Active()) && glass.isVisible;
            cam.enabled = on;
            if (!on) return;

            Vector3 outC = OutsideCenter();
            Vector3 eye = outC + (main.transform.position - inCenter);
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(Vector3.right));

            // window corners (viewer's left = +Z when looking toward +X), in camera space
            Vector3 bl = cam.transform.InverseTransformPoint(outC + new Vector3(0f, -height / 2, width / 2));
            Vector3 tr = cam.transform.InverseTransformPoint(outC + new Vector3(0f, height / 2, -width / 2));
            float d = bl.z;
            if (d < 0.05f) { cam.enabled = false; return; }
            float n = d * 0.995f, k = n / d;
            cam.nearClipPlane = n;
            cam.projectionMatrix = Matrix4x4.Frustum(bl.x * k, tr.x * k, bl.y * k, tr.y * k, n, cam.farClipPlane);
        }
    }
}
