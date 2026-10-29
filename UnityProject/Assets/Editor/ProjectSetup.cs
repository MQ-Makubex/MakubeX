#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MakubeX.Editor
{
    /// <summary>Reproducible scene and lightweight Mac configuration for phase one.</summary>
    public static class ProjectSetup
    {
        public const string EntryScene = "Assets/Scenes/Entry.unity";
        public const string ChapterScene = "Assets/Scenes/Chapter01_V004_Story.unity";
        public const string LegacyChapterScene = "Assets/Scenes/Chapter01.unity";

        [MenuItem("MakubeX/Generate Phase One Scenes")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before generating scenes.");

            // Resolve first, so an incomplete compilation cannot overwrite working scenes.
            Type appType = typeof(MakubeXApp);
            Type chapterType = typeof(Chapter01Controller);
            Directory.CreateDirectory("Assets/Scenes");
            ConfigureProject();
            ConfigureTextures();

            var entry = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("MakubeX App").AddComponent(appType);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.03f, 0.04f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 30;
            camera.allowHDR = false;
            cameraObject.transform.position = new Vector3(0, 0, -10);
            EditorSceneManager.SaveScene(entry, EntryScene);

            // Keep the old prototype scene as a reference. Integrate the validated v004 copy.
            Chapter01V004StorySetup.Integrate();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(EntryScene, true),
                new EditorBuildSettingsScene(ChapterScene, true)
            };
            EditorSceneManager.OpenScene(EntryScene, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MAKUBEX_SETUP_OK: Entry and Chapter01 scenes generated; built-in renderer, UGUI, legacy input, 1600x900.");
        }

        static void ConfigureProject()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.companyName = "MakubeX";
            PlayerSettings.productName = "MakubeX";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.makubex.chapter01");
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneOSX, new[] { GraphicsDeviceType.Metal });
            ConfigureIntelMacBuild();

            // Unity serializes Active Input Handling in ProjectSettings rather than a stable public API.
            UnityEngine.Object settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (settings != null)
            {
                var serialized = new SerializedObject(settings);
                var inputHandler = serialized.FindProperty("activeInputHandler") ?? serialized.FindProperty("m_ActiveInputHandler");
                if (inputHandler != null)
                {
                    inputHandler.intValue = 0; // Input Manager (Old); no dependency on the Input System package.
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            GraphicsSettings.defaultRenderPipeline = null;
            int originalQuality = QualitySettings.GetQualityLevel();
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = null;
                QualitySettings.pixelLightCount = 1;
                QualitySettings.antiAliasing = 2;
                QualitySettings.vSyncCount = 1;
                QualitySettings.shadows = ShadowQuality.HardOnly;
                QualitySettings.shadowResolution = ShadowResolution.Low;
                QualitySettings.shadowDistance = 20;
                QualitySettings.shadowCascades = 0;
                QualitySettings.realtimeReflectionProbes = false;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                QualitySettings.lodBias = 1;
            }
            QualitySettings.SetQualityLevel(Math.Min(originalQuality, QualitySettings.names.Length - 1), false);
            Time.fixedDeltaTime = 1f / 60f;
        }

        static void ConfigureTextures()
        {
            // The approved 16:9 source images retain their source dimensions and full color.
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (name != "cover" && name != "memory-map" && name != "chapter01-loading") continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Default;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 4096;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }
        }

        [MenuItem("MakubeX/Build Mac Player")]
        public static void BuildMac()
        {
            // macOS Build Profiles use their own Architecture setting. PlayerSettings.SetArchitecture
            // does not select the Mac player variation in Unity 6.6.
            ConfigureIntelMacBuild();
            Directory.CreateDirectory("Builds");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { EntryScene, ChapterScene },
                target = BuildTarget.StandaloneOSX,
                locationPathName = "Builds/MakubeX.app",
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Mac player build failed: " + report.summary.result);
            VerifyIntelExecutable(Path.Combine(report.summary.outputPath, "Contents", "MacOS", PlayerSettings.productName));
            Debug.Log("MAKUBEX_BUILD_OK: " + report.summary.outputPath);
        }

        static void ConfigureIntelMacBuild()
        {
            // Official 6000.6 StandaloneOSX API directs architecture selection to
            // OSXStandalone.UserBuildSettings.architecture. The Mono Mac postprocessor reads it.
            // CoreCLR is ARM64-only in this Editor; keep the installed Mono x64 player backend.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
#pragma warning disable CS0618 // Intel Mac support is deprecated by Unity 6.6, but required for this Intel host.
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = OSArchitecture.x64;
            UnityEditor.OSXStandalone.UserBuildSettings.createXcodeProject = false;
            if (UnityEditor.OSXStandalone.UserBuildSettings.architecture != OSArchitecture.x64 ||
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.Mono2x)
                throw new InvalidOperationException("Unable to select the Mono x64 Mac player build configuration.");
#pragma warning restore CS0618
            Debug.Log("MAKUBEX_MAC_BUILD_CONFIGURATION: OSXStandalone.UserBuildSettings.architecture=x64; scriptingBackend=Mono2x.");
        }

        static void VerifyIntelExecutable(string path)
        {
            // Verify the emitted Mach-O header, so a successful ARM build cannot be reported as Intel.
            using (var binary = new BinaryReader(File.OpenRead(path)))
            {
                uint magic = binary.ReadUInt32();
                uint cpu = binary.ReadUInt32();
                if (magic != 0xfeedfacf || cpu != 0x01000007) // MH_MAGIC_64, CPU_TYPE_X86_64
                    throw new InvalidOperationException("Built Mac executable is not thin Intel x86_64: " + path +
                        " (Mach-O magic 0x" + magic.ToString("x8") + ", CPU 0x" + cpu.ToString("x8") + ").");
            }
            Debug.Log("MAKUBEX_MAC_BINARY_VERIFIED_X86_64: " + path);
        }
    }
}
#endif
