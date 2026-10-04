using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

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
        const string GeneratedFolder = Root + "/Generated";
        const string GridTexturePath = GeneratedFolder + "/GrayboxGrid.png";

        // Built-in "Ignore Raycast" layer: keeps the player out of its own ground and camera probes.
        const int PlayerLayer = 2;

        static readonly Vector3 SpawnPoint = new Vector3(0f, 0.1f, 0f);
        static readonly Color SkyColor = new Color(0.70f, 0.77f, 0.85f);

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

        public static void Build()
        {
            EnsureFolder(Root + "/Scenes");
            EnsureFolder(Root + "/Settings");
            EnsureFolder(GeneratedFolder);

            MovementTuning movementTuning = LoadOrCreateAsset<MovementTuning>(MovementTuningPath);
            CameraTuning cameraTuning = LoadOrCreateAsset<CameraTuning>(CameraTuningPath);
            Palette palette = CreatePalette();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();
            BuildPlayground(palette);

            GameObject player = BuildPlayer(palette, movementTuning);
            var motor = player.GetComponent<VelocityMotor>();
            VelocityCamera cameraRig = BuildCamera(player, motor, cameraTuning);
            player.GetComponent<VelocityPlayerController>().CameraRig = cameraRig;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = player;
            Debug.Log($"[Project Velocity] Movement test built at {ScenePath}. Press Play to test. " +
                      "The wall traversal section is behind the spawn: turn around and run south. " +
                      "Tune movement on the Player (Velocity Motor) and camera on the Main Camera (Velocity Camera).");
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

        static GameObject BuildPlayer(Palette p, MovementTuning tuning)
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

            var input = player.AddComponent<DesktopInputSource>();

            var playerController = player.AddComponent<VelocityPlayerController>();
            playerController.Motor = motor;
            playerController.InputSource = input;

            // Placeholder body: a capsule with a dark visor so facing direction is readable.
            Transform visualRoot = Group("Visual", player.transform);
            Primitive(PrimitiveType.Capsule, "Body", visualRoot, new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 0.9f, 0.8f), p.PlayerBody);
            Primitive(PrimitiveType.Cube, "Visor (front)", visualRoot, new Vector3(0f, 1.45f, 0.3f), new Vector3(0.55f, 0.16f, 0.25f), p.PlayerVisor);

            var visual = player.AddComponent<CharacterVisual>();
            visual.Motor = motor;
            visual.VisualRoot = visualRoot;

            player.AddComponent<MovementDebugHUD>().Motor = motor;

            SetLayerRecursively(player.transform, PlayerLayer);
            return player;
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
        }

        // Colour-coded for readability only: orange = ramps, blue = elevated path, green = towers, red = markers, teal = wall-run walls.
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
            };
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
