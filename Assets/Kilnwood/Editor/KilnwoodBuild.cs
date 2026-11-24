using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kilnwood.EditorTools
{
    /// <summary>
    /// Build Engineer's entry points. From the command line:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Kilnwood.EditorTools.KilnwoodBuild.Setup -logFile -
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Kilnwood.EditorTools.KilnwoodBuild.BuildWindows -logFile -
    /// The game builds itself at runtime, so the only scene is an empty one.
    /// </summary>
    public static class KilnwoodBuild
    {
        const string ScenePath = "Assets/Kilnwood/Scenes/Main.unity";
        const string BaseMaterialPath = "Assets/Kilnwood/Resources/ND_Base.mat";

        [MenuItem("Kilnwood/Setup project")]
        public static void Setup()
        {
            Directory.CreateDirectory("Assets/Kilnwood/Scenes");
            Directory.CreateDirectory("Assets/Kilnwood/Resources");

            // Base material on whatever render pipeline the project uses, so Look.Mat() works in builds.
            var shader = GraphicsSettings.currentRenderPipeline != null && GraphicsSettings.currentRenderPipeline.defaultMaterial != null
                ? GraphicsSettings.currentRenderPipeline.defaultMaterial.shader
                : Shader.Find("Standard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "ND_Base" };
                AssetDatabase.CreateAsset(mat, BaseMaterialPath);
            }
            else if (mat.shader != shader) mat.shader = shader;
            EditorUtility.SetDirty(mat);

            // Sky and smoke materials live in Resources so their shaders are included in the build.
            MakeMaterial("Assets/Kilnwood/Resources/KW_Sky.mat", "Skybox/Procedural", m =>
            {
                m.SetFloat("_SunDisk", 2f);
                m.SetFloat("_SunSize", 0.035f);
                m.SetFloat("_SunSizeConvergence", 6f);
                m.SetFloat("_AtmosphereThickness", 0.85f);
                m.SetColor("_SkyTint", new Color(0.52f, 0.6f, 0.72f));
                m.SetColor("_GroundColor", new Color(0.42f, 0.45f, 0.4f));
                m.SetFloat("_Exposure", 1.15f);
            });
            MakeMaterial("Assets/Kilnwood/Resources/KW_Smoke.mat", "Legacy Shaders/Particles/Alpha Blended", m => { });
            // World-space pattern shader for building surfaces (Assets/Kilnwood/Shaders/KW_Triplanar.shader).
            MakeMaterial("Assets/Kilnwood/Resources/KW_Tri.mat", "Kilnwood/Triplanar", m => { m.enableInstancing = true; });
            // Camera finishing pass: occlusion, bloom, filmic tone map (Assets/Kilnwood/Shaders/KW_Post.shader).
            MakeMaterial("Assets/Kilnwood/Resources/KW_Post.mat", "Kilnwood/Post", m => { });
            // Instanced grass tufts (Assets/Kilnwood/Shaders/KW_Grass.shader).
            MakeMaterial("Assets/Kilnwood/Resources/KW_Grass.mat", "Kilnwood/Grass", m => { m.enableInstancing = true; });
            // Valley floor with close-up grain, and the river with moving ripples (Shaders/KW_Ground, KW_Water).
            MakeMaterial("Assets/Kilnwood/Resources/KW_Ground.mat", "Kilnwood/Ground", m => { });
            MakeMaterial("Assets/Kilnwood/Resources/KW_Water.mat", "Kilnwood/Water", m => { });

            // Linear colour space: softer, more natural lighting falloff.
            PlayerSettings.colorSpace = ColorSpace.Linear;

            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "LyFe Apps";
            PlayerSettings.productName = "Kilnwood";
            PlayerSettings.bundleVersion = ReadVersion();
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;

            AssetDatabase.SaveAssets();
            Debug.Log("Kilnwood: setup done (version " + PlayerSettings.bundleVersion + ", shader " + shader.name + ")");
        }

        [MenuItem("Kilnwood/Build Windows")]
        public static void BuildWindows()
        {
            Setup();
            string version = ReadVersion();
            string outDir = Path.Combine("Builds", "Kilnwood-" + version);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "Kilnwood.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log("Kilnwood: build " + s.result + " in " + s.totalTime + ", " + (s.totalSize / (1024 * 1024)) + " MB -> " + options.locationPathName);
            if (Application.isBatchMode && s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        static void MakeMaterial(string path, string shaderName, Action<Material> configure)
        {
            var sh = Shader.Find(shaderName);
            if (sh == null) { Debug.LogWarning("Kilnwood: shader not found: " + shaderName); return; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(sh) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != sh) m.shader = sh;
            configure(m);
            EditorUtility.SetDirty(m);
        }

        static string ReadVersion()
        {
            try
            {
                if (File.Exists("VERSION")) return File.ReadAllText("VERSION").Trim();
            }
            catch (Exception) { }
            return "0.1.0";
        }
    }
}
