using System;
using UnityEngine;

namespace Mohakash
{
    /// <summary>Shoulder-mounted EVA torch. Aims where the camera looks; refuses to work indoors.</summary>
    public class Torch : MonoBehaviour
    {
        public bool On { get; private set; }
        public float Intensity = 70f;          // tune to taste
        Func<bool> blocked;
        Light lamp; Transform camT; Material lensMat; float level;
        Renderer bodyRenderer;
        static readonly Color Warm = new Color(1f, 0.96f, 0.88f);

        public void Init(Transform camera, Func<bool> isBlocked, CrewRig rig = null)
        {
            camT = camera; blocked = isBlocked;

            // Prefer the helmet socket baked into the EVA rig so the torch sits on the helmet.
            // Fall back to a fixed local offset if the socket isn't present (e.g. casual model).
            Transform attachPoint = rig != null ? rig.Socket("Socket_Helmet") : null;

            var go = new GameObject("Suit Torch");
            if (attachPoint != null)
            {
                go.transform.SetParent(attachPoint, false);
                go.transform.localPosition = new Vector3(0.09f, 0f, 0.04f);   // left side of helmet
                go.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);  // aimed slightly down
            }
            else
            {
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0.28f, 1.55f, 0.08f);   // right shoulder fallback
            }

            lamp = go.AddComponent<Light>();
            lamp.type = LightType.Spot; lamp.range = 45f; lamp.spotAngle = 60f; lamp.innerSpotAngle = 25f;
            lamp.color = Warm; lamp.intensity = 0f; lamp.shadows = LightShadows.None; lamp.enabled = false;

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);   // small visible lamp body
            body.name = "Torch body";
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(go.transform, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.09f, 0.06f, 0.09f);
            lensMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.2f, 0.2f, 0.22f) };
            lensMat.EnableKeyword("_EMISSION");
            bodyRenderer = body.GetComponent<Renderer>();
            bodyRenderer.sharedMaterial = lensMat;
            bodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bodyRenderer.enabled = false;   // hidden until outside
        }

        public bool Toggle()
        {
            if (blocked != null && blocked()) { On = false; return false; }
            On = !On; return true;
        }

        void LateUpdate()
        {
            if (lamp == null) return;
            bool isBlocked = blocked != null && blocked();
            if (On && isBlocked) On = false;

            // body mesh: only visible outside (i.e. not blocked)
            if (bodyRenderer != null) bodyRenderer.enabled = !isBlocked;

            level = Mathf.MoveTowards(level, On ? 1f : 0f, Time.deltaTime * 6f);
            lamp.enabled = level > 0.01f;
            lamp.intensity = Intensity * level;
            lensMat.SetColor("_EmissionColor", Warm * (3f * level));
            if (camT != null)
                lamp.transform.rotation = Quaternion.Slerp(lamp.transform.rotation,
                    Quaternion.LookRotation(camT.forward, Vector3.up), 1f - Mathf.Exp(-14f * Time.deltaTime));
        }
    }
}
