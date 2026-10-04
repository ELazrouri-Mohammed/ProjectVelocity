using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectVelocity.EditorTools
{
    /// <summary>
    /// Tools > Project Velocity > Build Android Development APK (and Build And Run On Android Device).
    /// Builds only the Movement Test as a debug-signed development APK in Builds/Android/ (ignored by git), landscape only.
    /// No release signing, no store bundle.
    /// </summary>
    public static class AndroidDevBuild
    {
        const string OutputFolder = "Builds/Android";
        const string ApkName = "ProjectVelocity-MovementTest-dev.apk";

        [MenuItem("Tools/Project Velocity/Build Android Development APK", priority = 40)]
        public static void BuildApk()
        {
            Build(false);
        }

        [MenuItem("Tools/Project Velocity/Build And Run On Android Device (USB)", priority = 41)]
        public static void BuildAndRun()
        {
            Build(true);
        }

        /// <summary>Landscape only, either way up. Also set in Project Settings; re-applied here so a build can't come out portrait.</summary>
        public static void ApplyLandscapeOrientation()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        }

        static void Build(bool run)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                Debug.LogWarning("[Project Velocity] Stop Play Mode (and let scripts finish compiling) before building.");
                return;
            }

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Android Build Support missing",
                    "Install the Android Build Support module (with OpenJDK and Android SDK & NDK Tools) for this Unity version " +
                    "from Unity Hub > Installs > (this version) > Add modules, then restart Unity.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // A scene built before the touch controls existed would come out with keyboard/mouse only.
            if (!SceneHasTouchControls())
            {
                Debug.Log("[Project Velocity] Movement test scene is missing or predates the touch controls: rebuilding it first.");
                MovementTestBuilder.Build();
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (!EditorUtility.DisplayDialog("Switch to Android?",
                        "The active platform isn't Android. Switch now? The first switch re-imports assets and can take a few minutes.",
                        "Switch and build", "Cancel"))
                    return;
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    Debug.LogError("[Project Velocity] Couldn't switch the active platform to Android. Switch it in File > Build Profiles, then try again.");
                    return;
                }
            }

            ApplyLandscapeOrientation();
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

            Directory.CreateDirectory(OutputFolder);
            string apkPath = $"{OutputFolder}/{ApkName}";
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MovementTestBuilder.ScenePath },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development | (run ? BuildOptions.AutoRunPlayer : BuildOptions.None),
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Project Velocity] Android build {summary.result.ToString().ToLowerInvariant()} " +
                               $"with {summary.totalErrors} error(s). See the Console above for details.");
                return;
            }

            string fullPath = Path.GetFullPath(apkPath);
            Debug.Log($"[Project Velocity] Android development APK built ({summary.totalSize / (1024f * 1024f):0.0} MB, " +
                      $"{summary.totalTime.TotalMinutes:0.0} min): {fullPath}" +
                      (run ? "  Installed and launched on the connected device (if one is connected with USB debugging on)."
                           : "  Install it with: adb install -r \"" + fullPath + "\""));
            if (!run)
                EditorUtility.RevealInFinder(fullPath);
        }

        static bool SceneHasTouchControls()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MovementTestBuilder.ScenePath) == null)
                return false;
            return AssetDatabase.GetDependencies(MovementTestBuilder.ScenePath, false)
                .Any(path => path.EndsWith("/" + nameof(MobileInputSource) + ".cs"));
        }
    }
}
