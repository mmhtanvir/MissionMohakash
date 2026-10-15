using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// A realtime reflection probe that follows the camera. With a black sky there is nothing to reflect, so metal, visors
    /// and glossy panels looked dead. The probe captures the actual scene (sun disk, terrain, habitat) a few times a second
    /// so shiny surfaces pick up real reflections, the single biggest realism win for hard-surface materials.
    /// </summary>
    public class ReflectionRig : MonoBehaviour
    {
        ReflectionProbe probe;
        float timer;

        void Awake()
        {
            var go = new GameObject("Reflection Probe (realtime)");
            go.transform.SetParent(transform);
            probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 128;
            probe.hdr = true;
            probe.size = new Vector3(160f, 80f, 160f);
            probe.boxProjection = false;
            probe.intensity = 1.1f;
            probe.importance = 10;
            probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = Color.black;
            probe.cullingMask = ~(1 << 2);
            probe.nearClipPlane = 0.3f; probe.farClipPlane = 1500f;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || probe == null) return;
            probe.transform.position = cam.transform.position + Vector3.up * 0.5f;
            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f) { timer = 0.35f; probe.RenderProbe(); }
        }
    }
}
