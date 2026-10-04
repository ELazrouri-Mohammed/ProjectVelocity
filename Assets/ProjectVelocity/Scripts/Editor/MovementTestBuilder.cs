using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectVelocity.EditorTools
{
    /// <summary>
    /// Tools > Project Velocity > Build Movement Test.
    /// (Re)creates the graybox playground, the player and the camera, fully wired, and saves the scene.
    /// Tuning assets are created once and never overwritten, so rebuilding keeps your tuning.
    /// </summary>
    public static class MovementTestBuilder
    {
        const string Root = "Assets/ProjectVelocity";
        public const string ScenePath = Root + "/Scenes/MovementTest.unity";
        const string MovementTuningPath = Root + "/Settings/MovementTuning.asset";
        const string CameraTuningPath = Root + "/Settings/CameraTuning.asset";
        const string TouchControlsTuningPath = Root + "/Settings/TouchControlsTuning.asset";
        const string CombatTuningPath = Root + "/Settings/CombatTuning.asset";
        const string GeneratedFolder = Root + "/Generated";
        const string GridTexturePath = GeneratedFolder + "/GrayboxGrid.png";
        const string TouchDiscPath = GeneratedFolder + "/TouchDisc.png";
        const string TouchPadPath = GeneratedFolder + "/TouchPad.png";

        // Built-in "Ignore Raycast" layer: keeps the player out of its own ground and camera probes.
        const int PlayerLayer = 2;
        // Built-in "UI" layer.
        const int UILayer = 5;

        static readonly Vector3 SpawnPoint = new Vector3(0f, 0.1f, 0f);
        static readonly Color SkyColor = new Color(0.70f, 0.77f, 0.85f);

        // Placeholder traversal target: a glowing orb (diameter, m) inside a camera-facing ring (radius, m).
        const float TargetCoreSize = 1.6f;
        const float TargetRingRadius = 1.45f;
        // Extra Upward Bias for the targets that redirect you upward (added to Target Upward Bias in Movement Tuning).
        const float UpwardTargetBias = 0.35f;

        // Placeholder enemy (Sentinel): a glowing diamond (half width, half height, m) inside a tilted ring, chest high
        // when it sits on the ground, plus a few hidden shards for its death burst.
        const float EnemyCoreRadius = 0.55f;
        const float EnemyCoreHalfHeight = 0.8f;
        const float EnemyRingRadius = 0.95f;
        const float GroundEnemyHeight = 1.4f;
        const int EnemyShardCount = 6;
        const float EnemyShardSize = 0.28f;

        // Slash arc the sword leaves (radii in m), at the height the sword arm swings through.
        const float SlashHeight = 1.25f;
        const float SlashInnerRadius = 0.6f;
        const float SlashOuterRadius = 2.4f;
        const float SlashArcDegrees = 150f;

        [MenuItem("Tools/Project Velocity/Build Movement Test", priority = 0)]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Project Velocity] Stop Play Mode before rebuilding the movement test.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Build();
        }

        [MenuItem("Tools/Project Velocity/Play Movement Test", priority = 1)]
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

        [MenuItem("Tools/Project Velocity/Select Movement Tuning", priority = 20)]
        public static void SelectMovementTuning()
        {
            SelectAsset(LoadOrCreateAsset<MovementTuning>(MovementTuningPath));
        }

        [MenuItem("Tools/Project Velocity/Select Camera Tuning", priority = 21)]
        public static void SelectCameraTuning()
        {
            SelectAsset(LoadOrCreateAsset<CameraTuning>(CameraTuningPath));
        }

        [MenuItem("Tools/Project Velocity/Select Touch Controls Tuning", priority = 22)]
        public static void SelectTouchControlsTuning()
        {
            SelectAsset(LoadOrCreateAsset<TouchControlsTuning>(TouchControlsTuningPath));
        }

        [MenuItem("Tools/Project Velocity/Select Combat Tuning", priority = 23)]
        public static void SelectCombatTuning()
        {
            SelectAsset(LoadOrCreateAsset<CombatTuning>(CombatTuningPath));
        }

        public static void Build()
        {
            EnsureFolder(Root + "/Scenes");
            EnsureFolder(Root + "/Settings");
            EnsureFolder(GeneratedFolder);

            MovementTuning movementTuning = LoadOrCreateAsset<MovementTuning>(MovementTuningPath);
            CameraTuning cameraTuning = LoadOrCreateAsset<CameraTuning>(CameraTuningPath);
            TouchControlsTuning touchTuning = LoadOrCreateAsset<TouchControlsTuning>(TouchControlsTuningPath);
            CombatTuning combatTuning = LoadOrCreateAsset<CombatTuning>(CombatTuningPath);
            Palette palette = CreatePalette();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();
            BuildPlayground(palette);

            TouchControlsView touchControls = BuildTouchControls();
            GameObject player = BuildPlayer(palette, movementTuning, touchTuning, touchControls, combatTuning);
            var motor = player.GetComponent<VelocityMotor>();
            VelocityCamera cameraRig = BuildCamera(player, motor, cameraTuning);
            player.GetComponent<VelocityPlayerController>().CameraRig = cameraRig;
            player.GetComponent<TraversalTargeting>().Viewpoint = cameraRig.transform;
            player.GetComponent<CombatTargeting>().Viewpoint = cameraRig.transform;
            player.GetComponent<CombatController>().CameraRig = cameraRig;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = player;
            Debug.Log($"[Project Velocity] Movement test built at {ScenePath}. Press Play to test. " +
                      "The wall traversal section is behind the spawn: turn around and run south. " +
                      "The traversal target section is behind the spawn to the left: turn around and run south-east; " +
                      "E launches you through the selected (pink) target. " +
                      "The combat section is to the left of the spawn: turn left and run west; Left Mouse or F attacks the " +
                      "selected (yellow) enemy. " +
                      "Tune movement and targets on the Player (Velocity Motor), combat on the Player (Combat Controller) and " +
                      "camera on the Main Camera (Velocity Camera). " +
                      "Touch controls appear on Android/iOS and in the Device Simulator; to try them in the Game view, set " +
                      "Editor Controls to Mobile on the Player's Input Source Selector. Tune them on the Player (Mobile Input Source).");
        }

        // ------------------------------------------------------------------ Playground

        static void BuildPlayground(Palette p)
        {
            Transform root = Group("Playground", null);

            // Ground slab, top surface at y = 0. Falling off its edges respawns you.
            // It reaches further south (behind the spawn) to make room for the wall traversal section.
            Box("Ground", root, -130f, 130f, -1f, 0f, -350f, 470f, p.Floor);

            // A: long straight from the spawn, lined with pillars every 10 m (red, taller ones every 50 m).
            Transform straight = Group("A - Long Straight", root);
            for (int z = 10; z <= 240; z += 10)
            {
                bool marker = z % 50 == 0;
                Pillar(straight, new Vector3(-11f, 0f, z), 0.8f, marker ? 8f : 4f, marker ? p.Marker : p.Pillar);
                Pillar(straight, new Vector3(11f, 0f, z), 0.8f, marker ? 8f : 4f, marker ? p.Marker : p.Pillar);
            }

            // A2: rolling hill at the end of the straight, leading into the arena.
            Transform hill = Group("A2 - Rolling Hill", root);
            Ramp("Hill Up", hill, new Vector3(0f, 0f, 248f), 0f, 16f, 14f, 2.5f, p.Ramp);
            Box("Hill Top", hill, -8f, 8f, 0f, 2.5f, 262f, 272f, p.Block);
            Ramp("Hill Down", hill, new Vector3(0f, 0f, 286f), 180f, 16f, 14f, 2.5f, p.Ramp);

            // B: wide open turning arena: ring of pillars, a centre landmark, kick ramps and a few blockers.
            Transform arena = Group("B - Open Turning Arena", root);
            Vector3 centre = new Vector3(0f, 0f, 350f);
            Pillar(arena, centre, 3f, 20f, p.Marker);
            for (int i = 0; i < 18; i++)
            {
                bool tall = i % 3 == 0;
                Pillar(arena, centre + Dir(10f + i * 20f) * 55f, 2f, tall ? 14f : 10f, tall ? p.Marker : p.Pillar);
            }
            for (int i = 0; i < 4; i++)
            {
                float yaw = 45f + i * 90f;
                Ramp("Kick Ramp", arena, centre + Dir(yaw) * 14f, yaw, 6f, 9f, 2.8f, p.Ramp);
            }
            for (int i = 0; i < 6; i++)
            {
                Vector3 position = centre + Dir(i * 60f) * 34f;
                Block("Blocker", arena, position.x, position.z, 3f, 3f, 2.2f, p.Block);
            }

            // C: weave lane (return path on the left): staggered walls that each block half the lane.
            Transform weave = Group("C - Weave Lane", root);
            for (int i = 0; i < 13; i++)
                Block("Weave Wall", weave, i % 2 == 0 ? -66f : -54f, 260f - i * 18f, 12f, 2f, 4f, p.Block);
            for (int z = 30; z <= 270; z += 20)
            {
                Pillar(weave, new Vector3(-76f, 0f, z), 0.8f, 3f, p.Pillar);
                Pillar(weave, new Vector3(-44f, 0f, z), 0.8f, 3f, p.Pillar);
            }

            // F: stepping towers at different heights (up to 7.5 m and back down), zig-zagging.
            Transform towers = Group("F - Stepping Towers", root);
            float[] towerHeights = { 1.5f, 3f, 4.5f, 6f, 7.5f, 6f, 4.5f, 3f, 1.5f };
            for (int i = 0; i < towerHeights.Length; i++)
                Block($"Tower {towerHeights[i]:0.#} m", towers, i % 2 == 0 ? -31f : -25f, 40f + i * 11f, 5f, 5f, towerHeights[i], p.Tower);

            // D: ramp onto platforms separated by growing gaps (6, 9, 12, 16 m), then a ramp up to the elevated path.
            Transform parkour = Group("D - Ramps, Gaps & Platforms", root);
            Ramp("Ramp Up (3 m)", parkour, new Vector3(45f, 0f, 20f), 0f, 10f, 16f, 3f, p.Ramp);
            Box("P1", parkour, 40f, 50f, 0f, 3f, 36f, 60f, p.Block);
            Box("P2 (6 m gap)", parkour, 40f, 50f, 0f, 3f, 66f, 84f, p.Block);
            Box("P3 (9 m gap, 1.5 m up)", parkour, 40f, 50f, 0f, 4.5f, 93f, 110f, p.Block);
            Box("P4 (12 m gap)", parkour, 40f, 50f, 0f, 3f, 122f, 140f, p.Block);
            Box("P5 (16 m gap - boost)", parkour, 40f, 50f, 0f, 3f, 156f, 200f, p.Block);
            Ramp("Ramp Up To Elevated Path", parkour, new Vector3(45f, 3f, 176f), 0f, 10f, 24f, 4f, p.Ramp);

            // E: elevated path at 7 m: north, across a bridge, then south over gaps and a narrow section, and back down.
            Transform elevated = Group("E - Elevated Path", root);
            Slab("E1", elevated, 42f, 48f, 200f, 260f, 7f, p.Elevated);
            Slab("E2 Bridge", elevated, 42f, 93f, 260f, 266f, 7f, p.Elevated);
            Slab("E3", elevated, 87f, 93f, 200f, 260f, 7f, p.Elevated);
            Slab("E4 (8 m gap)", elevated, 87f, 93f, 150f, 192f, 7f, p.Elevated);
            Slab("E5 (12 m gap, 1.5 m up)", elevated, 87f, 93f, 100f, 138f, 8.5f, p.Elevated);
            Slab("E6 (16 m gap - boost)", elevated, 87f, 93f, 50f, 84f, 7f, p.Elevated);
            Slab("E7 Narrow", elevated, 88.5f, 91.5f, 20f, 50f, 7f, p.Elevated);
            Ramp("Ramp Down", elevated, new Vector3(90f, 0f, -5f), 0f, 6f, 25f, 7f, p.Ramp);

            Vector3[] supports =
            {
                new Vector3(45f, 6f, 230f), new Vector3(67f, 6f, 263f), new Vector3(90f, 6f, 230f), new Vector3(90f, 6f, 171f),
                new Vector3(90f, 7.5f, 119f), new Vector3(90f, 6f, 67f), new Vector3(90f, 6f, 35f),
            };
            foreach (Vector3 support in supports)
                Block("Support", elevated, support.x, support.z, 1.2f, 1.2f, support.y, p.Pillar);

            BuildWallSection(root, p);
            BuildTargetSection(root, p);
            BuildCombatSection(root, p);
        }

        /// <summary>
        /// G: wall traversal, behind the spawn (turn around and run south). Five lanes, each starting at a pair of red gate
        /// markers at z = -80. Teal surfaces are built for wall running, but any tall, near-vertical surface works.
        /// </summary>
        static void BuildWallSection(Transform root, Palette p)
        {
            Transform section = Group("G - Wall Traversal", root);

            float[] laneCentres = { 40f, 0f, -40f, -85f, -115f };
            foreach (float x in laneCentres)
            {
                Pillar(section, new Vector3(x - 8f, 0f, -80f), 1f, 6f, p.Marker);
                Pillar(section, new Vector3(x + 8f, 0f, -80f), 1f, 6f, p.Marker);
            }

            // G1: long parallel walls 8 m apart: wall run, wall jump across, repeat for 160 m.
            Transform parallel = Group("G1 - Parallel Walls", section);
            Box("East Wall", parallel, 44f, 45f, 0f, 10f, -260f, -100f, p.Wall);
            Box("West Wall", parallel, 35f, 36f, 0f, 10f, -260f, -100f, p.Wall);

            // G2: the full chain. Kick ramp → jump → boost (wall A floats 4 m up, too high without the boost) →
            // wall run A → wall jump across to B → wall run B → carry off its end onto the finish platform → land.
            Transform route = Group("G2 - Wall Route", section);
            Ramp("Takeoff Ramp", route, new Vector3(0f, 0f, -100f), 180f, 8f, 8f, 2f, p.Ramp);
            Box("Wall A (4-14 m)", route, -7f, -6f, 4f, 14f, -170f, -130f, p.Wall);
            Box("Wall B (3-14 m)", route, 6f, 7f, 3f, 14f, -215f, -180f, p.Wall);
            Slab("Finish Platform (3 m)", route, -6f, 7f, -242f, -217f, 3f, p.Elevated);
            Block("Support", route, 0.5f, -229.5f, 1.2f, 1.2f, 2f, p.Pillar);
            Ramp("Finish Ramp Down", route, new Vector3(0.5f, 0f, -254f), 0f, 13f, 12f, 3f, p.Ramp);

            // G3: angled entries (a glancing 25° hit runs along the wall, a steep 55° hit also carries you up it),
            // then head-on climbs: a 9 m block you can climb onto, and a 24 m wall too tall to climb (jump off or fall).
            Transform climb = Group("G3 - Angled Walls & Climbs", section);
            AngledWall("Angled Wall 25°", climb, -40f, -150f, 25f, 40f, 10f, p.Wall);
            AngledWall("Angled Wall 55°", climb, -40f, -200f, 55f, 30f, 12f, p.Wall);
            Block("Climb Block (9 m)", climb, -47f, -257f, 10f, 10f, 9f, p.Wall);
            Block("Tall Wall (24 m)", climb, -32f, -259f, 12f, 6f, 24f, p.Wall);

            // G4: one wall line broken by growing gaps (10, 15, 20 m): carry speed across, or kick out and steer back in.
            Transform gaps = Group("G4 - Wall-to-Wall Gaps", section);
            Box("Gap Wall 1", gaps, -121f, -120f, 0f, 10f, -140f, -100f, p.Wall);
            Box("Gap Wall 2 (10 m gap)", gaps, -121f, -120f, 0f, 10f, -190f, -150f, p.Wall);
            Box("Gap Wall 3 (15 m gap)", gaps, -121f, -120f, 0f, 10f, -245f, -205f, p.Wall);
            Box("Gap Wall 4 (20 m gap)", gaps, -121f, -120f, 0f, 10f, -305f, -265f, p.Wall);

            // G5: ascending sequence: panels alternate sides 10 m apart, each starting higher; wall jump between them
            // to climb onto the 6 m platform at the end.
            Transform ascending = Group("G5 - Ascending Walls", section);
            Box("Step 1 (0-9 m)", ascending, -91f, -90f, 0f, 9f, -135f, -110f, p.Wall);
            Box("Step 2 (2-12 m)", ascending, -80f, -79f, 2f, 12f, -160f, -135f, p.Wall);
            Box("Step 3 (4-15 m)", ascending, -91f, -90f, 4f, 15f, -185f, -160f, p.Wall);
            Box("Step 4 (6-18 m)", ascending, -80f, -79f, 6f, 18f, -210f, -185f, p.Wall);
            Slab("Top Platform (6 m)", ascending, -91f, -79f, -237f, -212f, 6f, p.Elevated);
            Block("Support", ascending, -85f, -224.5f, 1.2f, 1.2f, 5f, p.Pillar);
            Ramp("Ramp Down", ascending, new Vector3(-85f, 0f, -253f), 0f, 12f, 16f, 6f, p.Ramp);
        }

        /// <summary>
        /// H: traversal targets, behind the spawn to the left (turn around and run south-east). Three lanes, each starting at a
        /// pair of red gate markers at z = -20, then an open field of targets on extra ground east of the main slab.
        /// Violet orbs are traversal targets: the selected one turns pink and pulses, and E launches you through it.
        /// Gaps fall to the ground, never off the level.
        /// </summary>
        static void BuildTargetSection(Transform root, Palette p)
        {
            Transform section = Group("H - Traversal Targets", root);

            // Extra ground east of the main slab, for the full-chain lane and the open field.
            Box("Ground (target field)", section, 130f, 260f, -1f, 0f, -350f, -10f, p.Floor);

            float[] laneCentres = { 64f, 104f, 160f };
            foreach (float x in laneCentres)
            {
                Pillar(section, new Vector3(x - 8f, 0f, -20f), 1f, 6f, p.Marker);
                Pillar(section, new Vector3(x + 8f, 0f, -20f), 1f, 6f, p.Marker);
            }

            // H1: the basics on a 4 m deck. One target over a 34 m gap (too far for jump + boost), two targets in a row over a
            // 60 m gap (the first alone falls short), then a target that redirects you upward onto a 14 m ledge.
            Transform basics = Group("H1 - Target Basics", section);
            Ramp("Ramp Up (4 m)", basics, new Vector3(64f, 0f, -24f), 180f, 10f, 16f, 4f, p.Ramp);
            Box("Runway (4 m)", basics, 59f, 69f, 0f, 4f, -80f, -40f, p.Block);
            Target("Target - Over The Gap", basics, new Vector3(64f, 8f, -90f), p);
            Box("Landing 1 (34 m gap)", basics, 59f, 69f, 0f, 4f, -165f, -114f, p.Block);
            Target("Target - Sequence 1", basics, new Vector3(64f, 8f, -172f), p);
            Target("Target - Sequence 2", basics, new Vector3(64f, 9f, -200f), p);
            Box("Landing 2 (60 m gap - two targets)", basics, 59f, 69f, 0f, 4f, -305f, -225f, p.Block);
            Target("Target - Upward", basics, new Vector3(64f, 6f, -282f), p, UpwardTargetBias);
            Box("High Ledge (14 m)", basics, 59f, 69f, 0f, 14f, -342f, -305f, p.Elevated);

            // H2: run on the wall, wall jump off it, and the target out over the gap carries you onto the 6 m landing.
            Transform wallJump = Group("H2 - Wall Jump Into Target", section);
            Box("Wall (0-12 m)", wallJump, 113f, 114f, 0f, 12f, -95f, -40f, p.Wall);
            Target("Target - After Wall Jump", wallJump, new Vector3(100f, 9f, -115f), p);
            Box("Landing (6 m)", wallJump, 78f, 118f, 0f, 6f, -185f, -132f, p.Block);

            // H3: the full chain. Kick ramp → jump → boost (the wall floats 4 m up, too high without the boost) → wall run →
            // wall jump → target → target → land on the 7 m finish. Skip the second target and you fall short.
            Transform chain = Group("H3 - Full Chain", section);
            Ramp("Takeoff Ramp", chain, new Vector3(160f, 0f, -28f), 180f, 8f, 8f, 2f, p.Ramp);
            Box("Wall (4-14 m)", chain, 167f, 168f, 4f, 14f, -98f, -58f, p.Wall);
            Target("Target - Chain 1", chain, new Vector3(158f, 10f, -120f), p);
            Target("Target - Chain 2", chain, new Vector3(154f, 11f, -148f), p);
            Box("Finish (7 m)", chain, 130f, 172f, 0f, 7f, -220f, -162f, p.Elevated);

            // H4: open field for free experimenting. Staggered rows 26 m apart at 6-14 m (every target is within reach of
            // the next), a few that redirect upward, and two walls to chain off.
            Transform field = Group("H4 - Open Target Field", section);
            for (int row = 0; row < 12; row++)
            {
                float[] columns = row % 2 == 0 ? new[] { 190f, 218f, 246f } : new[] { 204f, 232f };
                for (int col = 0; col < columns.Length; col++)
                {
                    float height = 6f + (row * 5 + col * 3) % 9;
                    bool upward = (row * 3 + col) % 5 == 4;
                    Target(upward ? "Target (upward)" : "Target", field, new Vector3(columns[col], height, -45f - row * 26f), p,
                        upward ? UpwardTargetBias : 0f);
                }
            }
            Box("Field Wall East (0-12 m)", field, 256f, 257f, 0f, 12f, -300f, -60f, p.Wall);
            Box("Field Wall West (0-12 m)", field, 178f, 179f, 0f, 12f, -330f, -240f, p.Wall);
        }

        /// <summary>
        /// I: blade combat, to the left of the spawn (turn left and run west). Two lanes, each starting at a pair of red gate
        /// markers at x = -15, running onto extra ground west of the main slab, clear of the routes to G and H.
        /// Red diamonds are Sentinels: the selected one turns yellow and pulses, and Attack lunges at it and cuts it down.
        /// They come back a few seconds after dying, and all at once when you respawn. Falls land on the ground, never off the level.
        /// </summary>
        static void BuildCombatSection(Transform root, Palette p)
        {
            Transform section = Group("I - Combat", root);

            // Extra ground west of the main slab, for the far end of both lanes.
            Box("Ground (combat)", section, -280f, -130f, -1f, 0f, -60f, 30f, p.Floor);

            float[] laneCentres = { 0f, -32f };
            foreach (float z in laneCentres)
            {
                Pillar(section, new Vector3(-15f, 0f, z - 8f), 1f, 6f, p.Marker);
                Pillar(section, new Vector3(-15f, 0f, z + 8f), 1f, 6f, p.Marker);
            }

            // I1: one test at a time down a straight lane, chest high unless noted.
            // A: straight ahead. B: off to the right, then to the left, while running down the middle.
            // C: above a hurdle, out of reach from the ground: jump the hurdle and cut it on the way over.
            // D: past the end of the wall on the right, too high to reach from the ground or the wall: wall run, wall jump in
            // the last stretch of the wall, cut it.
            Transform basics = Group("I1 - Combat Basics", section);
            Enemy("Sentinel A - Straight Ahead", basics, new Vector3(-50f, GroundEnemyHeight, 0f), p);
            Enemy("Sentinel B1 - Right", basics, new Vector3(-82f, GroundEnemyHeight, 4.5f), p);
            Enemy("Sentinel B2 - Left", basics, new Vector3(-112f, GroundEnemyHeight, -4.5f), p);
            Block("Hurdle (1.2 m)", basics, -145f, 0f, 2f, 12f, 1.2f, p.Block);
            Enemy("Sentinel C - After A Jump (5.4 m)", basics, new Vector3(-145f, 5.4f, 0f), p);
            Box("Wall (0-12 m)", basics, -210f, -180f, 0f, 12f, 7f, 8f, p.Wall);
            Enemy("Sentinel D - After A Wall Jump (7.5 m)", basics, new Vector3(-216f, 7.5f, 1f), p);
            Pillar(basics, new Vector3(-250f, 0f, -8f), 1f, 6f, p.Marker);
            Pillar(basics, new Vector3(-250f, 0f, 8f), 1f, 6f, p.Marker);

            // I2: the movement-combat chain. Kick ramp → jump → boost (the wall floats 4 m up, too high without the boost) →
            // wall run → wall jump off the back half of the wall → cut the Sentinel (jump off earlier and air boost to it: the kill
            // gives the boost back) → boost → traversal target → land on the 7 m finish.
            Transform chain = Group("I2 - Movement-Combat Chain", section);
            Ramp("Takeoff Ramp", chain, new Vector3(-60f, 0f, -32f), 270f, 8f, 8f, 2f, p.Ramp);
            Box("Wall (4-14 m)", chain, -130f, -90f, 4f, 14f, -40f, -39f, p.Wall);
            Enemy("Sentinel - After The Wall Jump (8.5 m)", chain, new Vector3(-140f, 8.5f, -33f), p);
            Target("Target - After The Kill", chain, new Vector3(-185f, 10f, -32f), p);
            Box("Finish (7 m)", chain, -258f, -200f, 0f, 7f, -44f, -20f, p.Elevated);
        }

        /// <summary>
        /// Placeholder enemy (Sentinel): a glowing diamond inside a tilted dark ring, hovering in place, plus hidden shards for
        /// its death burst. No collider, so it never stops the player: the blade measures its reach to the enemy's hurtbox.
        /// </summary>
        static CombatEnemy Enemy(string name, Transform parent, Vector3 position, Palette p)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var enemy = go.AddComponent<CombatEnemy>();

            Transform visual = Group("Visual", go.transform);
            GameObject core = CreateMeshObject("Core", visual, position, Quaternion.identity, p.EnemyCore, p.EnemyIdle);
            CreateMeshObject("Ring", visual, position, Quaternion.Euler(70f, 0f, 0f), p.EnemyRing, p.Pillar);

            Transform shardGroup = Group("Shards", go.transform);
            var shards = new Transform[EnemyShardCount];
            for (int i = 0; i < shards.Length; i++)
            {
                GameObject shard = Primitive(PrimitiveType.Cube, $"Shard {i + 1}", shardGroup, Vector3.zero, Vector3.one * EnemyShardSize, p.EnemyIdle);
                shard.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                shard.SetActive(false);
                shards[i] = shard.transform;
            }

            enemy.SetLook(visual, new[] { core.GetComponent<Renderer>() }, p.EnemyIdle, p.EnemySelected, p.EnemyHit, shards);
            return enemy;
        }

        /// <summary>
        /// Placeholder traversal target: a glowing orb inside a ring that always faces the camera. No collider, so the
        /// player flies straight through it.
        /// </summary>
        static TraversalTarget Target(string name, Transform parent, Vector3 position, Palette p, float extraUpwardBias = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var target = go.AddComponent<TraversalTarget>();
            target.ExtraUpwardBias = extraUpwardBias;

            Transform visual = Group("Visual", go.transform);
            GameObject core = Primitive(PrimitiveType.Sphere, "Core", visual, Vector3.zero, Vector3.one * TargetCoreSize, p.TargetIdle);
            GameObject ring = CreateMeshObject("Ring", visual, position, Quaternion.identity, p.TargetRing, p.TargetIdle);
            target.SetLook(new[] { core.GetComponent<Renderer>(), ring.GetComponent<Renderer>() },
                p.TargetIdle, p.TargetSelected, p.TargetCooldown, ring.transform, visual);
            return target;
        }

        static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go.transform;
        }

        static GameObject Box(string name, Transform parent, float xMin, float xMax, float yMin, float yMax, float zMin, float zMax, Material material)
        {
            var centre = new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, (zMin + zMax) * 0.5f);
            var size = new Vector3(xMax - xMin, yMax - yMin, zMax - zMin);
            GameObject go = CreateMeshObject(name, parent, centre, Quaternion.identity, GrayboxMeshes.Box(size, centre), material);
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>Block standing on the ground, footprint centred on (x, z).</summary>
        static GameObject Block(string name, Transform parent, float x, float z, float width, float depth, float height, Material material)
        {
            return Box(name, parent, x - width * 0.5f, x + width * 0.5f, 0f, height, z - depth * 0.5f, z + depth * 0.5f, material);
        }

        /// <summary>1 m thick floating slab whose top surface is at <paramref name="topY"/>.</summary>
        static GameObject Slab(string name, Transform parent, float xMin, float xMax, float zMin, float zMax, float topY, Material material)
        {
            return Box(name, parent, xMin, xMax, topY - 1f, topY, zMin, zMax, material);
        }

        static GameObject Pillar(Transform parent, Vector3 position, float width, float height, Material material)
        {
            return Block("Pillar", parent, position.x, position.z, width, width, height, material);
        }

        /// <summary>1 m thick wall standing on the ground, centred on (x, z), its length turned <paramref name="yaw"/> degrees from north-south.</summary>
        static GameObject AngledWall(string name, Transform parent, float x, float z, float yaw, float length, float height, Material material)
        {
            var size = new Vector3(1f, height, length);
            var centre = new Vector3(x, height * 0.5f, z);
            GameObject go = CreateMeshObject(name, parent, centre, Quaternion.Euler(0f, yaw, 0f), GrayboxMeshes.Box(size, centre), material);
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>Ramp whose low edge is centred on <paramref name="lowEdge"/>, rising toward <paramref name="facingYaw"/>.</summary>
        static GameObject Ramp(string name, Transform parent, Vector3 lowEdge, float facingYaw, float width, float length, float rise, Material material)
        {
            Mesh mesh = GrayboxMeshes.Ramp(width, length, rise);
            GameObject go = CreateMeshObject(name, parent, lowEdge, Quaternion.Euler(0f, facingYaw, 0f), mesh, material);
            var meshCollider = go.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
            meshCollider.convex = true;
            return go;
        }

        static GameObject CreateMeshObject(string name, Transform parent, Vector3 position, Quaternion rotation, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static Vector3 Dir(float yawDegrees)
        {
            return Quaternion.Euler(0f, yawDegrees, 0f) * Vector3.forward;
        }

        // ------------------------------------------------------------------ Player & camera

        static GameObject BuildPlayer(Palette p, MovementTuning tuning, TouchControlsTuning touchTuning, TouchControlsView touchControls,
            CombatTuning combatTuning)
        {
            var player = new GameObject("Player");
            player.transform.position = SpawnPoint;

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.skinWidth = 0.06f;
            controller.stepOffset = 0.35f;
            controller.slopeLimit = tuning.maxWalkableSlope;
            controller.minMoveDistance = 0f;

            var motor = player.AddComponent<VelocityMotor>();
            motor.Tuning = tuning;

            // Both input sources live on the player; the selector turns on the one for the platform at startup.
            var desktopInput = player.AddComponent<DesktopInputSource>();
            var mobileInput = player.AddComponent<MobileInputSource>();
            mobileInput.Tuning = touchTuning;
            mobileInput.View = touchControls;
            mobileInput.enabled = false;

            var targeting = player.AddComponent<TraversalTargeting>();
            targeting.Motor = motor;

            var combatTargeting = player.AddComponent<CombatTargeting>();
            combatTargeting.Motor = motor;

            var combat = player.AddComponent<CombatController>();
            combat.Tuning = combatTuning;
            combat.Motor = motor;
            combat.Targeting = combatTargeting;

            var playerController = player.AddComponent<VelocityPlayerController>();
            playerController.Motor = motor;
            playerController.InputSource = desktopInput;
            playerController.Targeting = targeting;
            playerController.Combat = combat;

            var selector = player.AddComponent<InputSourceSelector>();
            selector.Player = playerController;
            selector.DesktopInput = desktopInput;
            selector.MobileInput = mobileInput;

            // Placeholder humanoid combat proxy (presentation only): primitive limbs on empty joints, with a dark visor so facing
            // is readable and the sword in the right hand. Character Visual turns and leans it, Humanoid Visual poses its limbs,
            // Blade Visual swings the sword arm.
            Transform visualRoot = Group("Visual", player.transform);
            HumanoidVisual.Rig rig = BuildHumanoid(visualRoot, p);

            var visual = player.AddComponent<CharacterVisual>();
            visual.Motor = motor;
            visual.VisualRoot = visualRoot;

            BladeVisual blade = BuildSwordArm(player.transform, rig.rightShoulderMount, p);
            combat.Blade = blade;

            var humanoid = player.AddComponent<HumanoidVisual>();
            humanoid.Motor = motor;
            humanoid.Blade = blade;
            humanoid.SetRig(rig);

            var hud = player.AddComponent<MovementDebugHUD>();
            hud.Motor = motor;
            hud.Targeting = targeting;
            hud.Player = playerController;
            hud.Combat = combat;

            SetLayerRecursively(player.transform, PlayerLayer);
            return player;
        }

        /// <summary>
        /// Placeholder humanoid, about 1.8 m tall like the character controller, slim and athletic: primitive limbs (yellow suit,
        /// dark hands, feet, shorts and neck) on empty joint pivots that Humanoid Visual poses, limbs hanging along -Y. No
        /// colliders: the controller's capsule still does all the moving. The right shoulder only gets a mount here; the sword
        /// arm hangs from it (see <see cref="BuildSwordArm"/>).
        /// </summary>
        static HumanoidVisual.Rig BuildHumanoid(Transform visualRoot, Palette p)
        {
            var rig = new HumanoidVisual.Rig();

            rig.hips = Joint("Hips", visualRoot, new Vector3(0f, 0.95f, 0f));
            Primitive(PrimitiveType.Cube, "Pelvis", rig.hips, Vector3.zero, new Vector3(0.28f, 0.17f, 0.18f), p.PlayerAccent);

            rig.spine = Joint("Spine", rig.hips, new Vector3(0f, 0.08f, 0f));
            Primitive(PrimitiveType.Capsule, "Waist", rig.spine, new Vector3(0f, 0.1f, 0f), new Vector3(0.24f, 0.12f, 0.17f), p.PlayerBody);
            Primitive(PrimitiveType.Capsule, "Chest", rig.spine, new Vector3(0f, 0.27f, 0f), new Vector3(0.36f, 0.2f, 0.21f), p.PlayerBody);
            Primitive(PrimitiveType.Capsule, "Neck", rig.spine, new Vector3(0f, 0.5f, 0f), new Vector3(0.09f, 0.06f, 0.09f), p.PlayerAccent);

            rig.head = Joint("Head", rig.spine, new Vector3(0f, 0.56f, 0f));
            Primitive(PrimitiveType.Sphere, "Skull", rig.head, new Vector3(0f, 0.12f, 0f), new Vector3(0.21f, 0.25f, 0.23f), p.PlayerBody);
            Primitive(PrimitiveType.Cube, "Visor (front)", rig.head, new Vector3(0f, 0.14f, 0.1f), new Vector3(0.17f, 0.06f, 0.06f), p.PlayerVisor);

            rig.leftShoulder = Joint("Shoulder L", rig.spine, new Vector3(-0.21f, 0.44f, 0f));
            Limb("Upper Arm L", rig.leftShoulder, 0.3f, 0.085f, p.PlayerBody);
            rig.leftElbow = Joint("Elbow L", rig.leftShoulder, new Vector3(0f, -0.3f, 0f));
            Limb("Forearm L", rig.leftElbow, 0.26f, 0.075f, p.PlayerBody);
            Primitive(PrimitiveType.Sphere, "Hand L", rig.leftElbow, new Vector3(0f, -0.3f, 0f), Vector3.one * 0.09f, p.PlayerAccent);

            rig.rightShoulderMount = Joint("Shoulder R", rig.spine, new Vector3(0.21f, 0.44f, 0f));

            BuildLeg("L", -1f, rig.hips, p, out rig.leftHip, out rig.leftKnee, out rig.leftAnkle);
            BuildLeg("R", 1f, rig.hips, p, out rig.rightHip, out rig.rightKnee, out rig.rightAnkle);
            return rig;
        }

        /// <summary>Thigh, shin and foot on hip, knee and ankle joints. <paramref name="side"/> -1 = left, 1 = right.</summary>
        static void BuildLeg(string name, float side, Transform hips, Palette p, out Transform hip, out Transform knee, out Transform ankle)
        {
            hip = Joint($"Hip {name}", hips, new Vector3(0.1f * side, -0.03f, 0f));
            Limb($"Thigh {name}", hip, 0.44f, 0.13f, p.PlayerBody);
            knee = Joint($"Knee {name}", hip, new Vector3(0f, -0.44f, 0f));
            Limb($"Shin {name}", knee, 0.41f, 0.1f, p.PlayerBody);
            ankle = Joint($"Ankle {name}", knee, new Vector3(0f, -0.41f, 0f));
            Primitive(PrimitiveType.Cube, $"Foot {name}", ankle, new Vector3(0f, -0.035f, 0.06f), new Vector3(0.09f, 0.07f, 0.24f), p.PlayerAccent);
        }

        /// <summary>Empty pivot for the humanoid's poses.</summary>
        static Transform Joint(string name, Transform parent, Vector3 localPosition)
        {
            Transform joint = Group(name, parent);
            joint.localPosition = localPosition;
            return joint;
        }

        /// <summary>Capsule limb hanging along -Y from its joint.</summary>
        static void Limb(string name, Transform joint, float length, float thickness, Material material)
        {
            Primitive(PrimitiveType.Capsule, name, joint, new Vector3(0f, -length * 0.5f, 0f), new Vector3(thickness, length * 0.5f, thickness), material);
        }

        /// <summary>
        /// The sword arm: upper arm, forearm, hand and a placeholder sword in one straight line along +Z from the right
        /// shoulder, so Blade Visual swings arm and blade together (the edge leads a horizontal cut). Plus the slash arc it
        /// leaves, aimed along each attack, so it's a child of the player rather than the body.
        /// </summary>
        static BladeVisual BuildSwordArm(Transform player, Transform shoulderMount, Palette p)
        {
            Transform arm = Joint("Sword Arm", shoulderMount, Vector3.zero);
            arm.localRotation = Quaternion.Euler(35f, 160f, 0f); // Blade Visual's rest pose
            Quaternion alongArm = Quaternion.Euler(90f, 0f, 0f); // turns a capsule's length onto +Z
            Primitive(PrimitiveType.Capsule, "Upper Arm R", arm, new Vector3(0f, 0f, 0.15f), new Vector3(0.085f, 0.15f, 0.085f), p.PlayerBody)
                .transform.localRotation = alongArm;
            Primitive(PrimitiveType.Capsule, "Forearm R", arm, new Vector3(0f, 0f, 0.43f), new Vector3(0.075f, 0.13f, 0.075f), p.PlayerBody)
                .transform.localRotation = alongArm;
            Primitive(PrimitiveType.Sphere, "Hand R", arm, new Vector3(0f, 0f, 0.6f), Vector3.one * 0.09f, p.PlayerAccent);
            Primitive(PrimitiveType.Cube, "Grip", arm, new Vector3(0f, 0f, 0.62f), new Vector3(0.035f, 0.035f, 0.16f), p.PlayerVisor);
            Primitive(PrimitiveType.Cube, "Guard", arm, new Vector3(0f, 0f, 0.71f), new Vector3(0.2f, 0.045f, 0.04f), p.PlayerVisor);
            Primitive(PrimitiveType.Cube, "Blade", arm, new Vector3(0f, 0f, 1.255f), new Vector3(0.1f, 0.035f, 1.05f), p.BladeSteel);

            GameObject arc = CreateMeshObject("Slash Arc", player, player.position + Vector3.up * SlashHeight, Quaternion.identity,
                p.SlashArc, p.SlashFx);
            var arcRenderer = arc.GetComponent<MeshRenderer>();
            arcRenderer.shadowCastingMode = ShadowCastingMode.Off;
            arcRenderer.receiveShadows = false;
            arcRenderer.enabled = false;

            var blade = player.gameObject.AddComponent<BladeVisual>();
            blade.SetParts(arm, arcRenderer);
            return blade;
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
                SetLayerRecursively(child, layer);
        }

        static VelocityCamera BuildCamera(GameObject player, VelocityMotor motor, CameraTuning tuning)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";

            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SkyColor;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            cam.fieldOfView = tuning.fovMin;
            go.AddComponent<AudioListener>();

            var rig = go.AddComponent<VelocityCamera>();
            rig.Target = player.transform;
            rig.TargetMotor = motor;
            rig.Tuning = tuning;

            // Rough starting placement for the Scene view; the rig takes over on Play.
            Quaternion rotation = Quaternion.Euler(tuning.startPitch, 0f, 0f);
            Vector3 pivot = player.transform.position + Vector3.up * tuning.pivotHeight;
            go.transform.SetPositionAndRotation(pivot + rotation * Vector3.back * tuning.distance, rotation);
            return rig;
        }

        // ------------------------------------------------------------------ Touch controls

        /// <summary>
        /// Prototype landscape touch controls: a movement stick (left) and JUMP / BOOST / ACTION / ATTACK around the right thumb,
        /// plus a small debug RESET. Hidden until the Mobile Input Source is in use. Positions and sizes come from Touch Controls Tuning
        /// at runtime (inside the safe area), so the placement here is only a preview.
        /// </summary>
        static TouchControlsView BuildTouchControls()
        {
            Sprite disc = LoadOrCreateCircleSprite(TouchDiscPath, false);
            Sprite pad = LoadOrCreateCircleSprite(TouchPadPath, true);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("Touch Controls");
            go.layer = UILayer;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvas.enabled = false;

            RectTransform stick = UIImage("Movement Stick", go.transform, pad, Color.white, new Vector2(300f, 280f), 300f);
            RectTransform knob = UIImage("Knob", stick, disc, Color.white, Vector2.zero, 135f);
            knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 0.5f);
            knob.anchoredPosition = Vector2.zero;

            // Colour-coded: violet ACTION matches the traversal targets it launches through, red ATTACK the enemies it cuts.
            RectTransform jump = UIButton("Jump", go.transform, pad, new Color(0.85f, 0.95f, 1f), "JUMP", 34, font, new Vector2(1670f, 240f), 105f);
            RectTransform boost = UIButton("Boost", go.transform, pad, new Color(1f, 0.72f, 0.35f), "BOOST", 28, font, new Vector2(1400f, 180f), 82f);
            RectTransform action = UIButton("Action", go.transform, pad, new Color(0.82f, 0.55f, 1f), "ACTION", 26, font, new Vector2(1695f, 495f), 82f);
            RectTransform reset = UIButton("Reset (debug)", go.transform, pad, new Color(0.8f, 0.8f, 0.8f), "RESET", 18, font, new Vector2(1830f, 1000f), 46f);
            RectTransform attack = UIButton("Attack", go.transform, pad, new Color(1f, 0.42f, 0.38f), "ATTACK", 26, font, new Vector2(1470f, 400f), 90f);

            var view = go.AddComponent<TouchControlsView>();
            view.SetParts(stick, knob, new[] { jump, boost, action, reset, attack });
            return view;
        }

        static RectTransform UIImage(string name, Transform parent, Sprite sprite, Color color, Vector2 position, float diameter)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = UILayer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(diameter, diameter);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static RectTransform UIButton(string name, Transform parent, Sprite sprite, Color color, string label, int fontSize, Font font,
            Vector2 position, float radius)
        {
            RectTransform button = UIImage(name, parent, sprite, color, position, radius * 2f);

            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.layer = UILayer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(button, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var text = go.GetComponent<Text>();
            text.text = label;
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color(1f, 1f, 1f, 0.9f);
            text.raycastTarget = false;
            return button;
        }

        /// <summary>White anti-aliased circle sprite: solid (stick knob), or a soft fill with a bright rim (stick base, buttons).</summary>
        static Sprite LoadOrCreateCircleSprite(string path, bool rimmed)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
                return existing;

            const int size = 256;
            const float radius = size * 0.5f - 2f;
            const float rimWidth = 10f;
            const float fillAlpha = 0.3f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                    float inside = Mathf.Clamp01(radius - distance + 0.5f);
                    float alpha = inside;
                    if (rimmed)
                    {
                        float rim = Mathf.Clamp01(distance - (radius - rimWidth) + 0.5f);
                        alpha *= Mathf.Lerp(fillAlpha, 1f, rim);
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ------------------------------------------------------------------ Look

        static void SetupLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.80f, 0.86f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.61f, 0.66f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.32f, 0.34f);

            // Distance fog gives depth cues at speed.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = SkyColor;
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 520f;

            var sunObject = new GameObject("Sun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sunObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            RenderSettings.sun = sun;
        }

        sealed class Palette
        {
            public Material Floor;
            public Material Block;
            public Material Ramp;
            public Material Elevated;
            public Material Tower;
            public Material Pillar;
            public Material Marker;
            public Material Wall;
            public Material PlayerBody;
            public Material PlayerVisor;
            public Material PlayerAccent;
            public Material TargetIdle;
            public Material TargetSelected;
            public Material TargetCooldown;
            public Mesh TargetRing;
            public Material EnemyIdle;
            public Material EnemySelected;
            public Material EnemyHit;
            public Mesh EnemyCore;
            public Mesh EnemyRing;
            public Material BladeSteel;
            public Material SlashFx;
            public Mesh SlashArc;
        }

        // Colour-coded for readability only: orange = ramps, blue = elevated path, green = towers, red = markers, teal = wall-run walls,
        // violet = traversal targets (pink while selected, grey while cooling down), glowing red = enemies (yellow while selected).
        static Palette CreatePalette()
        {
            Texture2D grid = LoadOrCreateGridTexture();
            return new Palette
            {
                Floor = Mat("Graybox_Floor", new Color(0.36f, 0.38f, 0.42f), grid),
                Block = Mat("Graybox_Block", new Color(0.66f, 0.68f, 0.72f), grid),
                Ramp = Mat("Graybox_Ramp", new Color(0.95f, 0.58f, 0.25f), grid),
                Elevated = Mat("Graybox_Elevated", new Color(0.33f, 0.60f, 0.90f), grid),
                Tower = Mat("Graybox_Tower", new Color(0.50f, 0.76f, 0.46f), grid),
                Pillar = Mat("Graybox_Pillar", new Color(0.24f, 0.26f, 0.30f), null),
                Marker = Mat("Graybox_Marker", new Color(0.92f, 0.33f, 0.30f), null),
                Wall = Mat("Graybox_Wall", new Color(0.30f, 0.72f, 0.68f), grid),
                PlayerBody = Mat("Player_Body", new Color(1f, 0.82f, 0.2f), null),
                PlayerVisor = Mat("Player_Visor", new Color(0.08f, 0.09f, 0.11f), null),
                PlayerAccent = Mat("Player_Accent", new Color(0.2f, 0.22f, 0.26f), null),
                TargetIdle = GlowMat("Target_Idle", new Color(0.50f, 0.25f, 0.85f), new Color(0.30f, 0.10f, 0.60f)),
                TargetSelected = GlowMat("Target_Selected", new Color(1f, 0.35f, 0.85f), new Color(1f, 0.30f, 0.80f)),
                TargetCooldown = GlowMat("Target_Cooldown", new Color(0.28f, 0.25f, 0.33f), Color.black),
                TargetRing = GrayboxMeshes.Torus(TargetRingRadius, 0.12f, 48, 10),
                EnemyIdle = GlowMat("Enemy_Idle", new Color(0.85f, 0.12f, 0.12f), new Color(0.75f, 0.05f, 0.05f)),
                EnemySelected = GlowMat("Enemy_Selected", new Color(1f, 0.85f, 0.3f), new Color(1f, 0.7f, 0.15f)),
                EnemyHit = GlowMat("Enemy_Hit", Color.white, Color.white),
                EnemyCore = GrayboxMeshes.Octahedron(EnemyCoreRadius, EnemyCoreHalfHeight),
                EnemyRing = GrayboxMeshes.Torus(EnemyRingRadius, 0.06f, 32, 6),
                BladeSteel = GlowMat("Blade_Steel", new Color(0.8f, 0.86f, 0.95f), new Color(0.15f, 0.3f, 0.45f)),
                SlashFx = FxMat("Slash_Arc", new Color(0.65f, 0.92f, 1f, 0.6f)),
                SlashArc = GrayboxMeshes.Crescent(SlashInnerRadius, SlashOuterRadius, SlashArcDegrees, 24),
            };
        }

        /// <summary>
        /// Unlit, transparent, double-sided material for placeholder effects (the slash arc). Its colour and alpha are driven at
        /// runtime through a property block, so the asset only sets the surface up.
        /// </summary>
        static Material FxMat(string name, Color color)
        {
            string path = $"{GeneratedFolder}/{name}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.color = color; // _BaseColor on URP Unlit
            // URP surface options: Transparent, Alpha blend, Both sides, no depth write.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A <see cref="Mat"/> that glows, so targets read from a distance. Black emission = no glow.</summary>
        static Material GlowMat(string name, Color color, Color emission)
        {
            Material material = Mat(name, color, null);
            bool glows = emission.maxColorComponent > 0f;
            material.SetColor("_EmissionColor", emission);
            if (glows)
                material.EnableKeyword("_EMISSION");
            else
                material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = glows ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Mat(string name, Color color, Texture texture)
        {
            string path = $"{GeneratedFolder}/{name}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.color = color;           // _BaseColor on URP Lit
            material.mainTexture = texture;   // _BaseMap on URP Lit
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.15f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Grid texture covering 4 x 4 m: light 1 m cells with a darker line every 4 m.</summary>
        static Texture2D LoadOrCreateGridTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
            if (existing != null)
                return existing;

            const int size = 512;
            const int cell = size / 4;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool majorLine = x < 6 || y < 6;
                    bool minorLine = x % cell < 3 || y % cell < 3;
                    bool lightCell = ((x / cell + y / cell) & 1) == 0;
                    byte v = majorLine ? (byte)110 : minorLine ? (byte)175 : lightCell ? (byte)255 : (byte)232;
                    pixels[y * size + x] = new Color32(v, v, v, 255);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(GridTexturePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(GridTexturePath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(GridTexturePath) is TextureImporter importer)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
        }

        // ------------------------------------------------------------------ Assets & settings

        static void AddSceneToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        static void SelectAsset(UnityEngine.Object asset)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
