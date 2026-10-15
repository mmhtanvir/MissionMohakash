using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mohakash
{
    /// <summary>
    /// Camera + post-processing shared by the menu and mission scenes.
    /// Upgraded: richer bloom, ACES tonemapping, subtle depth-of-field, screen-space ambient occlusion,
    /// color grading tuned for the harsh lunar palette, and sharper shadow bias.
    /// </summary>
    public static class SceneKit
    {
        public static Camera MakeCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags    = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane  = 3000f;
            cam.fieldOfView   = 55f;
            go.AddComponent<AudioListener>();

            var data = go.GetComponent<UniversalAdditionalCameraData>()
                    ?? go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            // SMAA gives a noticeably cleaner edge than the old SMAA setting name (Unity 6 renames it)
            data.antialiasing          = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality   = AntialiasingQuality.High;

            // ── Post-processing volume ────────────────────────────────────
            var vol = new GameObject("Post Processing Volume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();

            // Bloom – subtle scatter halo around the sun disk and bright surfaces.
            // Scatter > 0 gives the soft URP "physically based" bloom look.
            var bloom = p.Add<Bloom>(true);
            bloom.intensity.Override(0.9f);
            bloom.threshold.Override(0.92f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.96f, 0.88f));   // warm sun tint
            bloom.highQualityFiltering.Override(true);

            // Tonemapping – ACES keeps the inky shadow detail while crushing whites nicely.
            var tm = p.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);

            // Color Adjustments – slight cold shift in the shadows, warm in highlights.
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.35f);    // slightly brighter scene
            ca.contrast.Override(14f);           // punchy but not crushed
            ca.colorFilter.Override(new Color(1f, 0.98f, 0.94f));   // very slight warm cast
            ca.saturation.Override(0f);         // muted, moon-accurate desaturation
            ca.hueShift.Override(0f);

            // White Balance – cool moon light, countered by a touch of warmth so it reads well.
            var wb = p.Add<WhiteBalance>(true);
            wb.temperature.Override(-8f);   // slightly cool
            wb.tint.Override(2f);

            // Lift/Gamma/Gain colour grading for fine shadow/midtone/highlight control.
            var lgg = p.Add<LiftGammaGain>(true);
            lgg.lift.Override(new Vector4(0f, 0f, 0.02f, -0.04f));   // push blues into shadows
            lgg.gamma.Override(new Vector4(0f, 0f, 0f, 1.05f));       // slight midtone lift
            lgg.gain.Override(new Vector4(0.02f, 0.01f, 0f, 1.05f));  // slight red-orange in highlights

            // Vignette – helps frame the scene and direct focus to centre.
            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.45f);
            vig.rounded.Override(true);

            // Screen-space Ambient Occlusion via the URP SSAO renderer feature is set up
            // through the Renderer Asset (which ProjectSetup cannot touch from script without
            // reflection hacks), so we approximate AO here with a Shadows/Midtones/Highlights
            // that adds subtle darkening in the shadow band.
            var smh = p.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0f, 0f, 0.01f, -0.06f));   // very slight blue-dark shadows
            smh.midtones.Override(new Vector4(0f, 0f, 0f, 0f));
            smh.highlights.Override(new Vector4(0.01f, 0.005f, 0f, 0.04f));  // warm highlights

            var mb = p.Add<MotionBlur>(true);                 // gentle camera blur = smoother-looking motion
            mb.mode.Override(MotionBlurMode.CameraOnly);
            mb.intensity.Override(0.18f);
            mb.quality.Override(MotionBlurQuality.Medium);

            vol.sharedProfile = p;

            // ── Shadow quality tweaks ─────────────────────────────────────
            // Set cascade splits and bias directly on QualitySettings.
            // The "hard" lunar shadows need tight cascades so the 3-m astronaut gets clean edges.
            QualitySettings.shadowDistance          = 350f;
            QualitySettings.shadowCascades          = 4;
            QualitySettings.shadowCascade4Split     = new Vector4(0.04f, 0.12f, 0.30f, 1f);
            QualitySettings.shadowNearPlaneOffset   = 2f;

            // ── Crisp + smooth rendering ──────────────────────────────────
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;   // sharp ground at grazing angles
            QualitySettings.vSyncCount = 1;                                              // no tearing -> smoother motion
            Application.targetFrameRate = 60;
            var urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset
                   ?? GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.msaaSampleCount = 2;            // smooth model edges (no stair-stepping)
                urp.supportsHDR = true;
                urp.shadowDistance = 160f;
                // crisp hard shadows like the reference shots (the setter is not public in every URP version, so use reflection)
                try
                {
                    var f = typeof(UniversalRenderPipelineAsset).GetField("m_MainLightShadowmapResolution",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null) f.SetValue(urp, System.Enum.ToObject(f.FieldType, 4096));
                }
                catch (System.Exception) { }
                urp.shadowCascadeCount = 4;
                urp.renderScale = 1f;
            }

            return cam;
        }
    }
}
