using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectVelocity.EditorTools
{
    /// <summary>
    /// The player for the slice: the same movement, combat and input stack as the movement test, plus the tether, health and
    /// pulse, and a new hero. The hero is a slender biomechanical runner made of faceted lofts on the existing animation rig:
    /// a forward-swept masked helmet with a cyan visor slit, a narrow torso with an asymmetric bone-white pauldron, powerful
    /// legs on blade feet, a back thruster, a long monoblade-spear, a teal scarf and a weapon trail. Graphite body against the
    /// pale world, cyan energy accents: readable as a silhouette on a small portrait screen. Replace the geometry under
    /// "Visual" with an authored model later; gameplay never touches it.
    /// </summary>
    public static partial class VerticalSliceBuilder
    {
        sealed class Player
        {
            public GameObject Root;
            public VelocityPlayerController Controller;
            public VelocityMotor Motor;
            public TraversalTargeting Targeting;
            public TetherTargeting TetherTargeting;
            public CombatTargeting CombatTargeting;
            public CombatController Combat;
            public TetherController Tether;
            public PlayerHealth Health;
            public PulseLauncher Pulse;
            public Transform Visual;
            public RibbonTrail Scarf;
            public GameObject BladeTrailObject;
        }

        static Player BuildPlayer(Art art, LevelContext level, MovementTuning movementTuning, CombatTuning combatTuning,
            TouchControlsTuning touchTuning, TetherTuning tetherTuning, TouchControlsView touchControls)
        {
            var p = new Player();
            var root = new GameObject("Player");
            root.transform.SetPositionAndRotation(level.Spawn, Quaternion.Euler(0f, level.SpawnYaw, 0f));
            p.Root = root;

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.skinWidth = 0.06f;
            controller.stepOffset = 0.35f;
            controller.slopeLimit = movementTuning.maxWalkableSlope;
            controller.minMoveDistance = 0f;

            p.Motor = root.AddComponent<VelocityMotor>();
            p.Motor.Tuning = movementTuning;

            var desktopInput = root.AddComponent<DesktopInputSource>();
            var mobileInput = root.AddComponent<MobileInputSource>();
            mobileInput.Tuning = touchTuning;
            mobileInput.View = touchControls;
            mobileInput.enabled = false;

            p.Targeting = root.AddComponent<TraversalTargeting>();
            p.Targeting.Motor = p.Motor;

            p.TetherTargeting = root.AddComponent<TetherTargeting>();
            p.TetherTargeting.Motor = p.Motor;

            p.CombatTargeting = root.AddComponent<CombatTargeting>();
            p.CombatTargeting.Motor = p.Motor;

            p.Pulse = root.AddComponent<PulseLauncher>();
            p.Pulse.SetBolts(BuildPulseBolts(art, level.FxRoot));

            p.Combat = root.AddComponent<CombatController>();
            p.Combat.Tuning = combatTuning;
            p.Combat.Motor = p.Motor;
            p.Combat.Targeting = p.CombatTargeting;
            p.Combat.Pulse = p.Pulse;

            p.Tether = root.AddComponent<TetherController>();
            p.Tether.Tuning = tetherTuning;
            p.Tether.Motor = p.Motor;
            p.Tether.Targeting = p.TetherTargeting;
            p.Tether.Combat = p.Combat;

            p.Controller = root.AddComponent<VelocityPlayerController>();
            p.Controller.Motor = p.Motor;
            p.Controller.InputSource = desktopInput;
            p.Controller.Targeting = p.Targeting;
            p.Controller.Combat = p.Combat;
            p.Controller.Tether = p.Tether;

            p.Health = root.AddComponent<PlayerHealth>();
            p.Health.Player = p.Controller;
            p.Health.Motor = p.Motor;
            p.Health.Combat = p.Combat;
            p.Controller.Health = p.Health;

            var selector = root.AddComponent<InputSourceSelector>();
            selector.Player = p.Controller;
            selector.DesktopInput = desktopInput;
            selector.MobileInput = mobileInput;

            // The hero.
            Transform visualRoot = MovementTestBuilder.Group("Visual", root.transform);
            p.Visual = visualRoot;
            HumanoidVisual.Rig rig = BuildHero(visualRoot, art, out Transform leftHand, out Transform scarfAnchor);
            p.Tether.Hand = leftHand;

            var bodyVisual = root.AddComponent<CharacterVisual>();
            bodyVisual.Motor = p.Motor;
            bodyVisual.VisualRoot = visualRoot;
            bodyVisual.SetStyle(14f, 40f, 30f, 0.7f);

            BladeVisual blade = BuildHeroBlade(root.transform, rig.rightShoulderMount, art, p.Motor, out GameObject trail);
            p.Combat.Blade = blade;
            p.BladeTrailObject = trail;

            var humanoid = root.AddComponent<HumanoidVisual>();
            humanoid.Motor = p.Motor;
            humanoid.Blade = blade;
            humanoid.Tether = p.Tether;
            humanoid.SetRig(rig);

            // Secondary motion: the scarf and the tether cable.
            var scarfObject = new GameObject("Scarf");
            scarfObject.transform.SetParent(root.transform, false);
            scarfObject.AddComponent<MeshFilter>();
            var scarfRenderer = scarfObject.AddComponent<MeshRenderer>();
            scarfRenderer.sharedMaterial = art.HeroScarf;
            scarfRenderer.shadowCastingMode = ShadowCastingMode.Off;
            p.Scarf = scarfObject.AddComponent<RibbonTrail>();
            p.Scarf.Anchor = scarfAnchor;
            p.Scarf.SetShape(12, 1.9f, 0.17f, 0.04f);

            var ropeObject = new GameObject("Tether Cable");
            ropeObject.transform.SetParent(root.transform, false);
            var line = ropeObject.AddComponent<LineRenderer>();
            line.sharedMaterial = art.FxRope;
            line.widthMultiplier = 0.07f;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.textureMode = LineTextureMode.Stretch;
            line.colorGradient = Gradient(new Color(0.6f, 1f, 1f, 1f), new Color(0.3f, 0.9f, 1f, 1f));
            var rope = ropeObject.AddComponent<TetherRopeVisual>();
            rope.Motor = p.Motor;
            rope.Tether = p.Tether;

            // Developer readout, off: enable the component on the Player to see the numbers.
            var hud = root.AddComponent<MovementDebugHUD>();
            hud.Motor = p.Motor;
            hud.Targeting = p.Targeting;
            hud.Player = p.Controller;
            hud.Combat = p.Combat;
            hud.enabled = false;

            MovementTestBuilder.SetLayerRecursively(root.transform, PlayerLayer);
            return p;
        }

        /// <summary>The hero's body on the humanoid rig (joints are empty pivots; limbs hang along -Y, facing +Z).</summary>
        static HumanoidVisual.Rig BuildHero(Transform visualRoot, Art art, out Transform leftHand, out Transform scarfAnchor)
        {
            var rig = new HumanoidVisual.Rig();
            Vector2[] hex = SliceMeshes.Polygon(6, 30f);
            Vector2[] oct = SliceMeshes.Polygon(8, 22.5f);
            Vector2[] diamond = SliceMeshes.Polygon(4, 0f);

            rig.hips = Joint("Hips", visualRoot, new Vector3(0f, 0.96f, 0f));
            Part("Pelvis", rig.hips, SliceMeshes.Loft("Hero Pelvis", hex, new[]
            {
                new SliceMeshes.Ring(-0.1f, 0.13f, 0.09f), new SliceMeshes.Ring(0.05f, 0.15f, 0.1f), new SliceMeshes.Ring(0.1f, 0.12f, 0.085f),
            }, 1), art.HeroSuit, Vector3.zero);
            Part("Hip Plate L", rig.hips, SliceMeshes.Block("Hero Hip Plate", new Vector3(0.04f, 0.13f, 0.15f)), art.HeroArmor,
                new Vector3(-0.16f, -0.02f, 0f), Quaternion.Euler(0f, 0f, -8f));
            Part("Hip Plate R", rig.hips, SliceMeshes.Block("Hero Hip Plate", new Vector3(0.04f, 0.13f, 0.15f)), art.HeroArmor,
                new Vector3(0.16f, -0.02f, 0f), Quaternion.Euler(0f, 0f, 8f));

            rig.spine = Joint("Spine", rig.hips, new Vector3(0f, 0.08f, 0f));
            Part("Torso", rig.spine, SliceMeshes.Loft("Hero Torso", oct, new[]
            {
                new SliceMeshes.Ring(0f, 0.11f, 0.08f), new SliceMeshes.Ring(0.18f, 0.13f, 0.095f), new SliceMeshes.Ring(0.31f, 0.19f, 0.12f),
                new SliceMeshes.Ring(0.44f, 0.21f, 0.12f), new SliceMeshes.Ring(0.52f, 0.14f, 0.1f),
            }, 1), art.HeroSuit, Vector3.zero);
            Part("Chest Plate", rig.spine, SliceMeshes.Loft("Hero Chest Plate", oct, new[]
            {
                new SliceMeshes.Ring(0.24f, 0.14f, 0.05f, 0f, 0.075f), new SliceMeshes.Ring(0.4f, 0.19f, 0.06f, 0f, 0.085f),
                new SliceMeshes.Ring(0.5f, 0.11f, 0.04f, 0f, 0.07f),
            }, 1), art.HeroArmor, Vector3.zero);
            Part("Chest Core", rig.spine, art.Octahedron, art.HeroEnergy, new Vector3(0f, 0.36f, 0.155f), Quaternion.identity, Vector3.one * 0.07f, false);
            Part("Thruster", rig.spine, SliceMeshes.Block("Hero Thruster", new Vector3(0.22f, 0.24f, 0.1f)), art.HeroSuit,
                new Vector3(0f, 0.34f, -0.14f), Quaternion.Euler(-8f, 0f, 0f));
            for (int side = -1; side <= 1; side += 2)
            {
                Part("Thruster Vent", rig.spine, SliceMeshes.Loft("Hero Vent", hex, new[]
                {
                    new SliceMeshes.Ring(0f, 0.03f, 0.03f), new SliceMeshes.Ring(-0.12f, 0.04f, 0.04f),
                }, 1), art.HeroEnergy, new Vector3(0.07f * side, 0.26f, -0.18f), Quaternion.Euler(-30f, 0f, 0f), Vector3.one, false);
            }
            Part("Neck", rig.spine, SliceMeshes.Limb("Hero Neck", 6, 0.08f, 0.09f, 0.09f, 0.08f, 0.08f), art.HeroSuit, new Vector3(0f, 0.58f, 0f));
            scarfAnchor = Joint("Scarf Anchor", rig.spine, new Vector3(0f, 0.5f, -0.1f));

            rig.head = Joint("Head", rig.spine, new Vector3(0f, 0.56f, 0f));
            Part("Helmet", rig.head, SliceMeshes.Loft("Hero Helmet", oct, new[]
            {
                new SliceMeshes.Ring(-0.14f, 0.085f, 0.1f, 0f, 0.14f), new SliceMeshes.Ring(-0.05f, 0.115f, 0.13f, 0f, 0.14f),
                new SliceMeshes.Ring(0.05f, 0.105f, 0.115f, 0f, 0.13f), new SliceMeshes.Ring(0.11f, 0.075f, 0.075f, 0f, 0.1f),
                new SliceMeshes.Ring(0.17f, 0.02f, 0.025f, 0f, 0.06f),
            }, 2), art.HeroArmor, Vector3.zero);
            Part("Visor", rig.head, SliceMeshes.Block("Hero Visor", new Vector3(0.2f, 0.024f, 0.06f)), art.HeroEnergy,
                new Vector3(0f, 0.15f, 0.118f), Quaternion.identity, Vector3.one, false);
            Part("Crest", rig.head, SliceMeshes.Loft("Hero Crest", diamond, new[]
            {
                new SliceMeshes.Ring(-0.04f, 0.012f, 0.03f, 0f, 0.27f), new SliceMeshes.Ring(-0.2f, 0.008f, 0.06f, 0f, 0.3f),
                new SliceMeshes.Ring(-0.34f, 0.002f, 0.01f, 0f, 0.34f),
            }, 2), art.HeroSuit, Vector3.zero);

            rig.leftShoulder = Joint("Shoulder L", rig.spine, new Vector3(-0.22f, 0.45f, 0f));
            Part("Pauldron", rig.leftShoulder, SliceMeshes.Loft("Hero Pauldron", oct, new[]
            {
                new SliceMeshes.Ring(0.05f, 0.05f, 0.11f), new SliceMeshes.Ring(-0.08f, 0.08f, 0.135f, 0.02f),
                new SliceMeshes.Ring(-0.16f, 0.035f, 0.09f, -0.02f),
            }, 0), art.HeroArmor, new Vector3(0f, 0.03f, 0f));
            Part("Upper Arm L", rig.leftShoulder, SliceMeshes.Limb("Hero Upper Arm", 6, 0.3f, 0.1f, 0.1f, 0.075f, 0.075f), art.HeroSuit, Vector3.zero);
            rig.leftElbow = Joint("Elbow L", rig.leftShoulder, new Vector3(0f, -0.3f, 0f));
            Part("Forearm L", rig.leftElbow, SliceMeshes.Limb("Hero Forearm", 6, 0.27f, 0.08f, 0.08f, 0.06f, 0.06f), art.HeroSuit, Vector3.zero);
            Part("Bracer L", rig.leftElbow, SliceMeshes.Limb("Hero Bracer", 6, 0.16f, 0.1f, 0.1f, 0.085f, 0.085f), art.HeroArmor, new Vector3(0f, -0.06f, 0f));
            Part("Bracer Line L", rig.leftElbow, SliceMeshes.Block("Hero Line", new Vector3(0.012f, 0.14f, 0.02f)), art.HeroEnergy,
                new Vector3(-0.05f, -0.14f, 0f), Quaternion.identity, Vector3.one, false);
            leftHand = Joint("Hand L", rig.leftElbow, new Vector3(0f, -0.3f, 0f));
            Part("Hand", leftHand, SliceMeshes.Block("Hero Hand", new Vector3(0.065f, 0.09f, 0.08f)), art.HeroSuit, Vector3.zero);

            rig.rightShoulderMount = Joint("Shoulder R", rig.spine, new Vector3(0.21f, 0.45f, 0f));

            BuildHeroLeg("L", -1f, rig.hips, art, hex, out rig.leftHip, out rig.leftKnee, out rig.leftAnkle);
            BuildHeroLeg("R", 1f, rig.hips, art, hex, out rig.rightHip, out rig.rightKnee, out rig.rightAnkle);
            return rig;
        }

        static void BuildHeroLeg(string name, float side, Transform hips, Art art, Vector2[] hex, out Transform hip, out Transform knee, out Transform ankle)
        {
            hip = Joint($"Hip {name}", hips, new Vector3(0.1f * side, -0.04f, 0f));
            Part($"Thigh {name}", hip, SliceMeshes.Limb("Hero Thigh", 6, 0.44f, 0.17f, 0.17f, 0.105f, 0.105f, 1.08f), art.HeroSuit, Vector3.zero);
            Part($"Thigh Plate {name}", hip, SliceMeshes.Loft("Hero Thigh Plate", hex, new[]
            {
                new SliceMeshes.Ring(-0.06f, 0.07f, 0.03f, 0f, 0.07f), new SliceMeshes.Ring(-0.3f, 0.055f, 0.025f, 0f, 0.055f),
            }, 1), art.HeroArmor, Vector3.zero);
            Part($"Thigh Line {name}", hip, SliceMeshes.Block("Hero Line", new Vector3(0.014f, 0.26f, 0.02f)), art.HeroEnergy,
                new Vector3(0.075f * side, -0.2f, 0f), Quaternion.identity, Vector3.one, false);

            knee = Joint($"Knee {name}", hip, new Vector3(0f, -0.44f, 0f));
            Part($"Knee Guard {name}", knee, SliceMeshes.Block("Hero Knee", new Vector3(0.08f, 0.1f, 0.05f)), art.HeroArmor,
                new Vector3(0f, -0.02f, 0.06f), Quaternion.Euler(10f, 0f, 0f));
            Part($"Shin {name}", knee, SliceMeshes.Limb("Hero Shin", 6, 0.41f, 0.11f, 0.11f, 0.06f, 0.06f), art.HeroSuit, Vector3.zero);
            Part($"Shin Plate {name}", knee, SliceMeshes.Loft("Hero Shin Plate", hex, new[]
            {
                new SliceMeshes.Ring(-0.05f, 0.045f, 0.025f, 0f, 0.05f), new SliceMeshes.Ring(-0.33f, 0.03f, 0.02f, 0f, 0.035f),
            }, 1), art.HeroArmor, Vector3.zero);
            Part($"Shin Line {name}", knee, SliceMeshes.Block("Hero Line", new Vector3(0.012f, 0.28f, 0.018f)), art.HeroEnergy,
                new Vector3(0.048f * side, -0.2f, -0.01f), Quaternion.identity, Vector3.one, false);

            ankle = Joint($"Ankle {name}", knee, new Vector3(0f, -0.41f, 0f));
            Part($"Blade Foot {name}", ankle, SliceMeshes.Loft("Hero Blade Foot", hex, new[]
            {
                new SliceMeshes.Ring(-0.07f, 0.045f, 0.05f, 0f, -0.035f), new SliceMeshes.Ring(0.08f, 0.04f, 0.032f, 0f, -0.055f),
                new SliceMeshes.Ring(0.21f, 0.008f, 0.01f, 0f, -0.07f),
            }, 2), art.HeroSuit, Vector3.zero);
            Part($"Heel Spur {name}", ankle, SliceMeshes.Loft("Hero Heel", hex, new[]
            {
                new SliceMeshes.Ring(-0.05f, 0.025f, 0.03f, 0f, -0.04f), new SliceMeshes.Ring(-0.14f, 0.005f, 0.008f, 0f, -0.07f),
            }, 2), art.HeroArmor, Vector3.zero);
        }

        /// <summary>
        /// The sword arm and the monoblade-spear, in one line along +Z from the right shoulder (Blade Visual swings them together),
        /// the slash arc, and the weapon trail at the blade tip.
        /// </summary>
        static BladeVisual BuildHeroBlade(Transform player, Transform shoulderMount, Art art, VelocityMotor motor, out GameObject trailObject)
        {
            Vector2[] hex = SliceMeshes.Polygon(6, 30f);
            Vector2[] diamond = SliceMeshes.Polygon(4, 0f);
            Transform arm = Joint("Sword Arm", shoulderMount, Vector3.zero);
            arm.localRotation = Quaternion.Euler(35f, 160f, 0f); // Blade Visual's rest pose

            Part("Upper Arm R", arm, SliceMeshes.Loft("Hero Upper Arm Z", hex, new[]
            {
                new SliceMeshes.Ring(0f, 0.05f, 0.05f), new SliceMeshes.Ring(0.3f, 0.038f, 0.038f),
            }, 2), art.HeroSuit, Vector3.zero);
            Part("Forearm R", arm, SliceMeshes.Loft("Hero Forearm Z", hex, new[]
            {
                new SliceMeshes.Ring(0.3f, 0.04f, 0.04f), new SliceMeshes.Ring(0.57f, 0.03f, 0.03f),
            }, 2), art.HeroSuit, Vector3.zero);
            Part("Bracer R", arm, SliceMeshes.Loft("Hero Bracer Z", hex, new[]
            {
                new SliceMeshes.Ring(0.36f, 0.05f, 0.05f), new SliceMeshes.Ring(0.52f, 0.043f, 0.043f),
            }, 2), art.HeroArmor, Vector3.zero);
            Part("Hand R", arm, SliceMeshes.Block("Hero Hand", new Vector3(0.07f, 0.08f, 0.09f)), art.HeroSuit, new Vector3(0f, 0f, 0.6f));
            Part("Grip", arm, SliceMeshes.Loft("Blade Grip", hex, new[]
            {
                new SliceMeshes.Ring(0.5f, 0.012f, 0.012f), new SliceMeshes.Ring(0.54f, 0.02f, 0.02f), new SliceMeshes.Ring(0.78f, 0.018f, 0.018f),
            }, 2), art.HeroArmor, Vector3.zero);
            Part("Guard", arm, SliceMeshes.Block("Blade Guard", new Vector3(0.11f, 0.026f, 0.03f)), art.HeroSuit, new Vector3(0f, 0f, 0.79f));
            Part("Blade", arm, SliceMeshes.Loft("Monoblade", diamond, new[]
            {
                new SliceMeshes.Ring(0.8f, 0.028f, 0.007f), new SliceMeshes.Ring(1.05f, 0.034f, 0.008f), new SliceMeshes.Ring(1.75f, 0.024f, 0.006f),
                new SliceMeshes.Ring(1.98f, 0.002f, 0.001f),
            }, 2), art.BladeSteel, Vector3.zero);
            Part("Blade Edge", arm, SliceMeshes.Loft("Monoblade Edge", diamond, new[]
            {
                new SliceMeshes.Ring(0.85f, 0.004f, 0.004f, 0.03f), new SliceMeshes.Ring(1.75f, 0.003f, 0.003f, 0.022f),
                new SliceMeshes.Ring(1.95f, 0.001f, 0.001f, 0.002f),
            }, 2), art.HeroEnergy, Vector3.zero, Quaternion.identity, Vector3.one, false);

            Transform tip = Joint("Blade Tip", arm, new Vector3(0f, 0f, 1.9f));
            trailObject = tip.gameObject;
            var trail = trailObject.AddComponent<TrailRenderer>();
            trail.time = 0.13f;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = 0.32f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.colorGradient = Gradient(new Color(0.7f, 1f, 1f, 0.9f), new Color(0.2f, 0.8f, 1f, 0f));
            trail.sharedMaterial = art.FxTrail;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;

            var arcMesh = GrayboxMeshes.Crescent(0.6f, 2.5f, 150f, 24);
            GameObject arc = MovementTestBuilder.CreateMeshObject("Slash Arc", player, player.position + Vector3.up * 1.25f, Quaternion.identity,
                arcMesh, art.FxArc);
            var arcRenderer = arc.GetComponent<MeshRenderer>();
            arcRenderer.shadowCastingMode = ShadowCastingMode.Off;
            arcRenderer.receiveShadows = false;
            arcRenderer.enabled = false;

            var blade = player.gameObject.AddComponent<BladeVisual>();
            blade.SetParts(arm, arcRenderer);
            blade.SetTrail(trail, motor);
            return blade;
        }

        static Transform[] BuildPulseBolts(Art art, Transform parent)
        {
            var bolts = new Transform[3];
            for (int i = 0; i < bolts.Length; i++)
            {
                var bolt = new GameObject($"Pulse Bolt {i + 1}");
                bolt.transform.SetParent(parent, false);
                Part("Core", bolt.transform, art.Octahedron, art.Pulse, Vector3.zero, Quaternion.identity, Vector3.one * 0.45f, false);
                var trail = bolt.AddComponent<TrailRenderer>();
                trail.time = 0.12f;
                trail.widthMultiplier = 0.35f;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                trail.colorGradient = Gradient(new Color(0.7f, 1f, 1f, 1f), new Color(0.2f, 0.8f, 1f, 0f));
                trail.sharedMaterial = art.FxTrail;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                bolt.SetActive(false);
                bolts[i] = bolt.transform;
            }
            return bolts;
        }

        // ------------------------------------------------------------------ Part helpers

        static Transform Joint(string name, Transform parent, Vector3 localPosition)
        {
            Transform joint = MovementTestBuilder.Group(name, parent);
            joint.localPosition = localPosition;
            return joint;
        }

        static GameObject Part(string name, Transform parent, Mesh mesh, Material material, Vector3 localPosition)
        {
            return Part(name, parent, mesh, material, localPosition, Quaternion.identity, Vector3.one, true);
        }

        static GameObject Part(string name, Transform parent, Mesh mesh, Material material, Vector3 localPosition, Quaternion localRotation)
        {
            return Part(name, parent, mesh, material, localPosition, localRotation, Vector3.one, true);
        }

        static GameObject Part(string name, Transform parent, Mesh mesh, Material material, Vector3 localPosition, Quaternion localRotation,
            Vector3 localScale, bool castShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        static Gradient Gradient(Color start, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(end.a, 1f) });
            return gradient;
        }
    }
}
