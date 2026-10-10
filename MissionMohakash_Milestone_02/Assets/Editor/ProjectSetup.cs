using System.IO;
using System.Linq;
using Mohakash;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Menu: Mission Mohakash > Rebuild Project Setup. Generates materials, textures, UI panel settings, the astronaut
/// Animator Controller and the three scenes (Intro, MainMenu, Mission1), and sets the build scene order.
/// Safe to run again: it overwrites only the files it owns.
/// </summary>
public static class ProjectSetup
{
    const string Mat = "Assets/Resources/Materials", UiDir = "Assets/Resources/UI", AnimDir = "Assets/Resources/Animation", Scenes = "Assets/Scenes";

    [MenuItem("Mission Mohakash/Rebuild Project Setup")]
    public static void Run()
    {
        foreach (var d in new[] { Mat, UiDir, AnimDir, AnimDir + "/Clips", Scenes }) Directory.CreateDirectory(d);
        Textures();
        Materials();
        PanelSettingsAsset();
        AnimatorAsset();
        ScenesAndBuild();
        PlayerSettings.productName = "Mission Mohakash";
        PlayerSettings.companyName = "Mission Mohakash Team";
        PlayerSettings.runInBackground = true;
        PlayerSettings.resizableWindow = true;
        AssetDatabase.SaveAssets();
        Debug.Log("[Setup] Mission Mohakash project setup complete.");
    }

    // ------------------------------------------------------------------ Textures

    static void Textures()
    {
        // Regolith: grey fractal noise with small specks (procedural, original).
        var reg = new Texture2D(512, 512, TextureFormat.RGB24, true);
        var rnd = new System.Random(3);
        for (int y = 0; y < 512; y++)
        for (int x = 0; x < 512; x++)
        {
            float n = 0, a = 0.5f, f = 1f / 64f;
            for (int o = 0; o < 5; o++) { n += a * Tile(x * f, y * f, 512 * f); a *= 0.5f; f *= 2f; }
            float speck = rnd.NextDouble() < 0.01 ? 0.12f : 0f;
            float v = Mathf.Clamp01(0.38f + (n - 0.5f) * 0.35f + speck);
            reg.SetPixel(x, y, new Color(v, v * 0.985f, v * 0.96f));
        }
        Save(reg, Mat + "/RegolithTex.png");

        // Earth: equirectangular, oceans + continents + clouds (stylised, procedural).
        var earth = new Texture2D(1024, 512, TextureFormat.RGB24, true);
        for (int y = 0; y < 512; y++)
        for (int x = 0; x < 1024; x++)
        {
            float lat = (y / 511f - 0.5f) * Mathf.PI, lon = x / 1023f * Mathf.PI * 2f;
            var p = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
            float land = Noise3(p * 1.6f + Vector3.one * 10f, 4);
            float cloud = Noise3(p * 3.2f + Vector3.one * 40f, 4);
            Color c = land > 0.55f ? Color.Lerp(new Color(0.22f, 0.42f, 0.18f), new Color(0.6f, 0.52f, 0.36f), (land - 0.55f) * 4f)
                                   : Color.Lerp(new Color(0.03f, 0.12f, 0.38f), new Color(0.06f, 0.25f, 0.55f), land * 1.8f);
            if (Mathf.Abs(lat) > 1.25f) c = Color.white * 0.92f;
            c = Color.Lerp(c, Color.white, Mathf.Clamp01((cloud - 0.55f) * 3.2f));
            earth.SetPixel(x, y, c);
        }
        Save(earth, Mat + "/EarthTex.png");
        AssetDatabase.Refresh();
    }

    static float Tile(float x, float y, float period)
    {
        // seamless 2D noise by blending four Perlin samples
        float u = x / period, v = y / period;
        float a = Mathf.PerlinNoise(x, y), b = Mathf.PerlinNoise(x - period, y), c = Mathf.PerlinNoise(x, y - period), d = Mathf.PerlinNoise(x - period, y - period);
        u -= Mathf.Floor(u); v -= Mathf.Floor(v);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static float Noise3(Vector3 p, int oct)
    {
        float n = 0, a = 0.5f, f = 1f;
        for (int i = 0; i < oct; i++)
        {
            n += a * (Mathf.PerlinNoise(p.x * f + 1.3f, p.y * f + 7.1f) + Mathf.PerlinNoise(p.y * f + 3.7f, p.z * f + 2.9f) + Mathf.PerlinNoise(p.z * f + 5.1f, p.x * f + 9.3f)) / 3f;
            a *= 0.5f; f *= 2f;
        }
        return n / 0.9375f;
    }

    static void Save(Texture2D t, string path) => File.WriteAllBytes(path, t.EncodeToPNG());

    // ------------------------------------------------------------------ Materials

    static void Materials()
    {
        var lit        = Shader.Find("Universal Render Pipeline/Lit");
        var unlit      = Shader.Find("Universal Render Pipeline/Unlit");
        var particles  = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var terrainLit = Shader.Find("Universal Render Pipeline/Terrain/Lit");

        // ── Terrain ──────────────────────────────────────────────────────
        var regTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Mat + "/RegolithTex.png");
        // Larger tile size (12 m) gives less obvious tiling at distance; very low smoothness for dusty regolith.
        var layer = new TerrainLayer
        {
            diffuseTexture = regTex,
            tileSize        = new Vector2(12f, 12f),
            smoothness      = 0.03f,
            metallic        = 0f,
            specular        = new Color(0.02f, 0.02f, 0.02f)
        };
        Create(layer, Mat + "/Regolith.terrainlayer");

        var terrainMat = new Material(terrainLit);
        // Enable instancing for draw-call batching on Apple Silicon
        terrainMat.enableInstancing = true;
        Create(terrainMat, Mat + "/TerrainLit.mat");

        // ── Moon rock (general scatter rocks) ────────────────────────────
        // Slightly cool grey-brown, very rough, tiny specular only from glancing angles.
        var rock = new Material(lit) { color = new Color(0.38f, 0.37f, 0.35f) };
        rock.SetFloat("_Smoothness", 0.04f);
        rock.SetFloat("_Metallic",   0.00f);
        rock.enableInstancing = true;
        Create(rock, Mat + "/MoonRock.mat");

        // ── Sample rock (science targets) ────────────────────────────────
        // Warm sandy colour, slight gloss (freshly exposed surface), teal emission so it's
        // easy to see from a distance (accessibility §3.1) while not looking garish.
        var sample = new Material(lit) { color = new Color(0.68f, 0.58f, 0.44f) };
        sample.SetFloat("_Smoothness", 0.30f);
        sample.SetFloat("_Metallic",   0.00f);
        sample.EnableKeyword("_EMISSION");
        sample.SetColor("_EmissionColor", new Color(0.04f, 0.22f, 0.24f) * 1.4f);
        Create(sample, Mat + "/SampleRock.mat");

        // ── Earth sphere ─────────────────────────────────────────────────
        var earth = new Material(lit);
        earth.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Mat + "/EarthTex.png"));
        earth.SetFloat("_Smoothness", 0.55f);   // oceans should glint
        earth.SetFloat("_Metallic",   0.00f);
        earth.color = Color.white;
        Create(earth, Mat + "/Earth.mat");

        // ── Sun glow / corona disk ────────────────────────────────────────
        // HDR colour (>1) so bloom fires on it; unlit so it ignores scene lighting.
        var sun = new Material(unlit);
        sun.SetColor("_BaseColor", new Color(12f, 10.5f, 8.0f));   // brighter for HDR bloom
        Create(sun, Mat + "/SunGlow.mat");

        // ── Stars particle sheet ──────────────────────────────────────────
        var starsMat = new Material(particles) { color = Color.white };
        starsMat.enableInstancing = true;
        Create(starsMat, Mat + "/Stars.mat");

        // ── Line (beacon rings) ───────────────────────────────────────────
        Create(new Material(particles) { color = Color.white }, Mat + "/Line.mat");

        // ── Beam (additive: beacons, horizon haze, rover dust) ────────────
        // Additive blending: src=One, dst=One. Slightly warmer base colour for the dust.
        var beam = new Material(particles) { color = new Color(0.40f, 0.80f, 0.90f, 0.45f) };
        beam.SetFloat("_Surface",  1f);
        beam.SetFloat("_Blend",    2f);
        beam.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        beam.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        beam.SetFloat("_ZWrite",   0f);
        beam.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        beam.SetOverrideTag("RenderType", "Transparent");
        beam.renderQueue = 3000;
        Create(beam, Mat + "/Beam.mat");

        // ── Outpost emissive (used on windows/status lights) ──────────────
        // A subtle warm-white emissive for module window faces so the outpost glows
        // faintly when the sun is low, improving readability.
        var emissive = new Material(lit) { color = new Color(0.70f, 0.70f, 0.68f) };
        emissive.SetFloat("_Smoothness", 0.55f);
        emissive.SetFloat("_Metallic",   0.20f);
        emissive.EnableKeyword("_EMISSION");
        emissive.SetColor("_EmissionColor", new Color(0.55f, 0.52f, 0.42f) * 0.6f);
        Create(emissive, Mat + "/OutpostPanel.mat");
    }

    static void Create(Object asset, string path)
    {
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(asset, path);
    }

    // ------------------------------------------------------------------ UI

    static void PanelSettingsAsset()
    {
        string theme = UiDir + "/MohakashTheme.tss";
        File.WriteAllText(theme, "@import url(\"unity-theme://default\");\n");
        AssetDatabase.ImportAsset(theme);
        var ps = ScriptableObject.CreateInstance<PanelSettings>();
        ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(theme);
        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution = new Vector2Int(1920, 1080);
        ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        ps.match = 0.5f;
        Create(ps, UiDir + "/PanelSettings.asset");
    }

    // ------------------------------------------------------------------ Animator

    static void AnimatorAsset()
    {
        const string model = "Assets/Resources/Models/Astronaut.glb";
        var clips = AssetDatabase.LoadAllAssetsAtPath(model).OfType<AnimationClip>().ToList();
        string path = AnimDir + "/Astronaut.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        string[] loop = { "Idle", "Walk", "Run", "Jump_Idle" };
        // state -> clip in the Quaternius space-suit character (CC0). It has no jump clip, so mid-air uses Idle_Neutral.
        var map = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Idle", "Idle" }, { "Walk", "Walk" }, { "Run", "Run" }, { "Jump", "Idle_Neutral" }, { "Jump_Idle", "Idle_Neutral" },
            { "Jump_Land", "Idle" }, { "Death", "Death" }, { "Wave", "Wave" }, { "Interact", "Interact" },
        };
        foreach (var state in map.Keys)
        {
            var src = clips.FirstOrDefault(c => c.name.EndsWith("|" + map[state]) || c.name == map[state]);
            if (src == null) { Debug.LogWarning("[Setup] Missing astronaut clip " + state); continue; }
            var copy = Object.Instantiate(src);
            copy.name = state;
            var s = AnimationUtility.GetAnimationClipSettings(copy);
            s.loopTime = loop.Contains(state);
            AnimationUtility.SetAnimationClipSettings(copy, s);
            string clipPath = $"{AnimDir}/Clips/{state}.anim";
            Create(copy, clipPath);
            var st = sm.AddState(state);
            st.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (state == "Idle") sm.defaultState = st;
        }
        EditorUtility.SetDirty(ctrl);
    }

    // ------------------------------------------------------------------ Scenes

    static void ScenesAndBuild()
    {
        MakeScene(Game.IntroScene, typeof(IntroPlayer));
        MakeScene(Game.MenuScene, typeof(MainMenu));
        MakeScene(Game.Mission1Scene, typeof(Mission1));
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene($"{Scenes}/{Game.IntroScene}.unity", true),
            new EditorBuildSettingsScene($"{Scenes}/{Game.MenuScene}.unity", true),
            new EditorBuildSettingsScene($"{Scenes}/{Game.Mission1Scene}.unity", true),
        };
    }

    static void MakeScene(string name, System.Type component)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject(name + " Bootstrap", component);
        EditorSceneManager.SaveScene(scene, $"{Scenes}/{name}.unity");
    }

    // ------------------------------------------------------------------ Build

    [MenuItem("Mission Mohakash/Build macOS App")]
    public static void BuildMac()
    {
        var r = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes.Select(s => s.path).ToArray(),
            "Builds/Mac/MissionMohakash.app", BuildTarget.StandaloneOSX, BuildOptions.None);
        Debug.Log("[Build] " + r.summary.result + " " + r.summary.totalSize / (1024 * 1024) + " MB, errors " + r.summary.totalErrors);
        if (Application.isBatchMode && r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    /// <summary>Batch entry point: setup + build.</summary>
    public static void SetupAndBuild() { Run(); BuildMac(); }
}
