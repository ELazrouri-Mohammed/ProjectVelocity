using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectVelocity.EditorTools
{
    /// <summary>
    /// Tools > Project Velocity > Build Vertical Slice.
    /// (Re)creates the vertical slice "FRACTURE" in one go and saves it: the level (nine beats, one continuous run), the hero,
    /// the three enemy roles, the reality-breaking stages, checkpoints, effects, generated sounds, HUD, lighting, sky and post
    /// processing, all wired. Tuning assets are created once and never overwritten; generated textures and sounds are made
    /// once (delete them to regenerate). The level itself is in VerticalSliceBuilder.Level*.cs.
    /// </summary>
    public static partial class VerticalSliceBuilder
    {
        const string Root = "Assets/ProjectVelocity";
        public const string ScenePath = Root + "/Scenes/VerticalSlice.unity";
        const string SliceFolder = Root + "/Generated/Slice";
        const string TextureFolder = SliceFolder + "/Textures";
        const string AudioFolder = SliceFolder + "/Audio";
        const string MovementTuningPath = Root + "/Settings/MovementTuning.asset";
        const string CombatTuningPath = Root + "/Settings/CombatTuning.asset";
        const string TouchControlsTuningPath = Root + "/Settings/TouchControlsTuning.asset";
        const string TetherTuningPath = Root + "/Settings/TetherTuning.asset";
        const string SliceCameraTuningPath = Root + "/Settings/SliceCameraTuning.asset";
        const string SoundBankPath = Root + "/Settings/SoundBank.asset";
        const string PostProfilePath = SliceFolder + "/SlicePostProcessing.asset";
        const string LightingPath = SliceFolder + "/SliceLighting.lighting";

        const string SliceName = "FRACTURE";

        // Built-in "Ignore Raycast" layer: keeps the player out of its own probes.
        const int PlayerLayer = 2;

        [MenuItem("Tools/Project Velocity/Build Vertical Slice", priority = 2)]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Project Velocity] Stop Play Mode before building the vertical slice.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Build();
        }

        [MenuItem("Tools/Project Velocity/Play Vertical Slice", priority = 3)]
        public static void PlayFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                Build();
            else if (SceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/Project Velocity/Select Tether Tuning", priority = 24)]
        public static void SelectTetherTuning()
        {
            MovementTestBuilder.SelectAsset(MovementTestBuilder.LoadOrCreateAsset<TetherTuning>(TetherTuningPath));
        }

        [MenuItem("Tools/Project Velocity/Select Vertical Slice Camera Tuning", priority = 25)]
        public static void SelectSliceCameraTuning()
        {
            MovementTestBuilder.SelectAsset(LoadOrCreateSliceCameraTuning());
        }

        [MenuItem("Tools/Project Velocity/Select Sound Bank", priority = 26)]
        public static void SelectSoundBank()
        {
            MovementTestBuilder.SelectAsset(MovementTestBuilder.LoadOrCreateAsset<SoundBank>(SoundBankPath));
        }

        public static void Build()
        {
            MovementTestBuilder.EnsureFolder(Root + "/Scenes");
            MovementTestBuilder.EnsureFolder(Root + "/Settings");
            MovementTestBuilder.EnsureFolder(TextureFolder);
            MovementTestBuilder.EnsureFolder(AudioFolder);

            MovementTuning movementTuning = MovementTestBuilder.LoadOrCreateAsset<MovementTuning>(MovementTuningPath);
            CombatTuning combatTuning = MovementTestBuilder.LoadOrCreateAsset<CombatTuning>(CombatTuningPath);
            TouchControlsTuning touchTuning = MovementTestBuilder.LoadOrCreateAsset<TouchControlsTuning>(TouchControlsTuningPath);
            TetherTuning tetherTuning = MovementTestBuilder.LoadOrCreateAsset<TetherTuning>(TetherTuningPath);
            CameraTuning cameraTuning = LoadOrCreateSliceCameraTuning();
            SoundBank soundBank = ProceduralAudio.BuildBank(SoundBankPath, AudioFolder);

            // The new scene first: opening it unloads in-memory objects, so everything generated (and above all the meshes, which
            // live only in the scene) is made after it opens (see MovementTestBuilder.Build).
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Art art = CreateArtAssets();
            CreateArtMeshes(art);

            Light sun = SetupLighting(art);
            var level = new LevelContext(art);
            BuildLevel(level);

            TouchControlsView touchControls = MovementTestBuilder.BuildTouchControls("LINK");
            Player player = BuildPlayer(art, level, movementTuning, combatTuning, touchTuning, tetherTuning, touchControls);
            VelocityCamera cameraRig = BuildCamera(player, cameraTuning);
            player.Controller.CameraRig = cameraRig;
            player.Targeting.Viewpoint = cameraRig.transform;
            player.TetherTargeting.Viewpoint = cameraRig.transform;
            player.CombatTargeting.Viewpoint = cameraRig.transform;
            player.Combat.CameraRig = cameraRig;

            BuildSystems(art, level, player, cameraRig, soundBank, sun);
            BuildPostProcessing(cameraRig.GetComponent<Camera>());

            CheckMeshes(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = player.Root;
            Debug.Log($"[Project Velocity] Vertical slice \"{SliceName}\" built at {ScenePath}. Press Play (or Tools > Project Velocity > " +
                      "Play Vertical Slice). In the Editor, set the Game view to a portrait phone resolution (e.g. 1080x2340) or use the " +
                      "Device Simulator; the touch controls appear there, or set Editor Controls to Mobile on the Player's Input Source " +
                      "Selector. Keyboard: WASD move, mouse look, Space jump, Shift boost, LMB/F attack, E link (hold to swing), " +
                      "R restart at the checkpoint. Build for the phone with Tools > Project Velocity > Build Vertical Slice APK (Portrait).");
        }

        static CameraTuning LoadOrCreateSliceCameraTuning()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CameraTuning>(SliceCameraTuningPath);
            if (existing != null)
                return existing;
            var t = ScriptableObject.CreateInstance<CameraTuning>();
            // Portrait-first framing: a little further and higher, the character in the lower half, width kept on tall phones,
            // and auto follow so one thumb can steer at speed. The movement test's own camera tuning is untouched.
            t.distance = 5.8f;
            t.pivotHeight = 2.1f;
            t.startPitch = 9f;
            t.minPitch = -35f;
            t.maxPitch = 70f;
            t.lookSensitivity = 0.35f;
            t.followLag = 0.02f;
            t.verticalFollowLag = 0.08f;
            t.fovMin = 62f;
            t.fovMax = 80f;
            t.fovMinSpeed = 8f;
            t.fovMaxSpeed = 50f;
            t.fovSmoothTime = 0.2f;
            t.portraitHorizontalFovMin = 52f;
            t.portraitHorizontalFovMax = 63f;
            t.portraitVerticalFovLimit = 108f;
            t.autoFollowStrength = 2.4f;
            t.autoFollowDelay = 0.6f;
            t.autoFollowMinSpeed = 8f;
            t.autoFollowFullSpeed = 30f;
            t.autoFollowMaxAngle = 140f;
            t.autoFollowPitch = 9f;
            t.fallLookDown = 0.6f;
            t.maxFallLookDown = 22f;
            t.tetherDistanceBoost = 1.8f;
            t.wallRoll = 5f;
            t.impulseRecover = 0.16f;
            AssetDatabase.CreateAsset(t, SliceCameraTuningPath);
            return t;
        }

        // ------------------------------------------------------------------ Systems

        static void BuildSystems(Art art, LevelContext level, Player player, VelocityCamera cameraRig, SoundBank bank, Light sun)
        {
            Transform systems = MovementTestBuilder.Group("Systems", null);

            var time = new GameObject("Game Time");
            time.transform.SetParent(systems, false);
            time.AddComponent<GameTimeDriver>();

            // Checkpoints and segments, in route order.
            SliceCheckpoint[] checkpoints = level.Checkpoints.OrderBy(c => c.Index).ToArray();
            SliceSegment[] segments = level.Segments.OrderBy(s => s.Index).ToArray();
            var directorObject = new GameObject("Slice Director");
            directorObject.transform.SetParent(systems, false);
            var director = directorObject.AddComponent<SliceDirector>();
            director.Configure(player.Controller, player.Health, checkpoints, segments, SliceName);

            // Everything that needs the player.
            foreach (RealityTrigger trigger in Object.FindObjectsByType<RealityTrigger>(FindObjectsSortMode.None))
            {
                trigger.Player = player.Controller;
                trigger.RearmOnRespawn = false;
            }
            foreach (RealityTransformSequence stage in Object.FindObjectsByType<RealityTransformSequence>(FindObjectsSortMode.None))
                stage.Player = player.Controller;
            foreach (LoopingMotion loop in Object.FindObjectsByType<LoopingMotion>(FindObjectsSortMode.None))
                loop.SetPlayer(player.Controller);

            // Effects, sound, enemy bolts.
            BuildVfx(art, systems);
            BuildEnemyBolts(art, systems, player.Controller);

            var audioObject = new GameObject("Slice Audio");
            audioObject.transform.SetParent(systems, false);
            var audio = audioObject.AddComponent<SliceAudio>();
            audio.Bank = bank;
            audio.Motor = player.Motor;

            var feedbackObject = new GameObject("Player Feedback");
            feedbackObject.transform.SetParent(systems, false);
            var feedback = feedbackObject.AddComponent<PlayerFeedback>();
            feedback.Configure(player.Controller, player.Motor, player.Combat, player.Pulse, player.Health, cameraRig,
                new[] { player.Visual.gameObject, player.Scarf.gameObject, player.BladeTrailObject });

            var atmosphereObject = new GameObject("Atmosphere");
            atmosphereObject.transform.SetParent(systems, false);
            atmosphereObject.AddComponent<AtmosphereZones>().Configure(player.Root.transform, sun, AtmosphereScript(level), 160f);

            BuildHud(art, systems, player, cameraRig.GetComponent<Camera>());
        }

        static void AddSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        static void CheckMeshes(Scene scene)
        {
            int missing = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh != null)
                        continue;
                    missing++;
                    Debug.LogError($"[Project Velocity] '{filter.name}' has no mesh and will be invisible.", filter);
                }
            }
            if (missing > 0)
                Debug.LogError($"[Project Velocity] Vertical slice built with {missing} invisible mesh object(s): see the errors above.");
        }
    }
}
