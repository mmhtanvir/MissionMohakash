using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Fixes the "everything is magenta" problem. Every material in this project uses a URP shader, but the project's
/// Quality Settings pointed at URP pipeline assets that are not in the project. With no active pipeline asset Unity
/// draws URP shaders as magenta (terrain, Earth, sun, rocks, stars...).
///
/// This runs automatically when the editor loads / scripts recompile. If any Quality level (or the Graphics default)
/// has no valid pipeline asset, it creates one in Assets/Settings and assigns it. Manual run:
/// Mission Mohakash > Fix Magenta (Ensure URP Asset).
/// </summary>
[InitializeOnLoad]
public static class EnsureRenderPipeline
{
    const string Dir = "Assets/Settings";
    const string RpPath = Dir + "/MohakashURP.asset";
    const string RdPath = Dir + "/MohakashURP_Renderer.asset";

    static EnsureRenderPipeline()
    {
        EditorApplication.delayCall += () => Run(false);
    }

    [MenuItem("Mission Mohakash/Fix Magenta (Ensure URP Asset)")]
    static void Manual() => Run(true);

    static void Run(bool verbose)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        bool graphicsOk = GraphicsSettings.defaultRenderPipeline != null;
        bool qualityOk = true;
        int current = QualitySettings.GetQualityLevel();
        int count = QualitySettings.names.Length;
        for (int i = 0; i < count; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            if (QualitySettings.renderPipeline == null) { qualityOk = false; break; }
        }
        QualitySettings.SetQualityLevel(current, false);

        if (graphicsOk && qualityOk)
        {
            if (verbose) Debug.Log("[Fix] URP pipeline asset already assigned everywhere - nothing to do.");
            return;
        }

        Directory.CreateDirectory(Dir);
        var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RpPath);
        if (rp == null)
        {
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RdPath);
            if (rd == null)
            {
                rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rd, RdPath);
            }
            rp = UniversalRenderPipelineAsset.Create(rd);
            rp.supportsHDR = true;                 // sun glow / bloom need HDR
            rp.shadowDistance = 500f;              // cover full 1000m play area
            AssetDatabase.CreateAsset(rp, RpPath);
            AssetDatabase.SaveAssets();
        }

        GraphicsSettings.defaultRenderPipeline = rp;
        for (int i = 0; i < count; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = rp;
        }
        QualitySettings.SetQualityLevel(current, false);
        AssetDatabase.SaveAssets();
        Debug.Log("[Fix] Created/assigned URP pipeline asset '" + RpPath + "' to Graphics + all Quality levels. Magenta materials should now render.");
    }
}
