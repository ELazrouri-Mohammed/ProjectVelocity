using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ProjectVelocity.EditorTools
{
    /// <summary>
    /// The slice's enemies (three roles, each with a silhouette readable at phone size), the switch, the enemy bolts, the
    /// effect systems and the HUD. Enemies share one language: bone-white ceramic shells over graphite cores, and orange only
    /// where they can hurt you (eyes, blade edges, the Warden's seams). The Warden's weak spot glows cyan on its back.
    /// </summary>
    public static partial class VerticalSliceBuilder
    {
        // ------------------------------------------------------------------ Enemy 1: the Lantern (ranged pressure)

        static RangedDrone Ranged(LevelContext ctx, Transform parent, string name, Vector3 position)
        {
            Art art = ctx.Art;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var body = root.AddComponent<CombatEnemy>();
            body.HurtboxRadius = 0.9f;
            body.RespawnDelay = 0f;
            body.ReviveWithPlayer = false;
            body.IsLight = true;
            body.AnimateIdle = false;

            Transform visual = MovementTestBuilder.Group("Visual", root.transform);
            Vector2[] oct = SliceMeshes.Polygon(8, 22.5f);
            Part("Shell", visual, SliceMeshes.Loft("Lantern Shell", oct, new[]
            {
                new SliceMeshes.Ring(-1.3f, 0.02f, 0.02f), new SliceMeshes.Ring(-0.8f, 0.3f, 0.3f), new SliceMeshes.Ring(0f, 0.44f, 0.44f),
                new SliceMeshes.Ring(0.6f, 0.3f, 0.3f), new SliceMeshes.Ring(1.15f, 0.05f, 0.05f),
            }, 1), art.EnemyShell, Vector3.zero);
            Part("Collar", visual, SliceMeshes.Loft("Lantern Collar", oct, new[]
            {
                new SliceMeshes.Ring(-0.13f, 0.52f, 0.52f), new SliceMeshes.Ring(0.13f, 0.52f, 0.52f),
            }, 1), art.EnemyCore, Vector3.zero);
            GameObject eye = Part("Eye", visual, art.Octahedron, art.EnemyEye, new Vector3(0f, 0f, 0.5f), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.5f, 0.35f, 0.5f), false);
            for (int i = 0; i < 4; i++)
            {
                Quaternion turn = Quaternion.Euler(0f, 45f + i * 90f, 0f);
                Part("Fin", visual, SliceMeshes.Block("Lantern Fin", new Vector3(0.05f, 0.7f, 0.35f)), art.EnemyCore,
                    turn * new Vector3(0f, -0.95f, 0.3f), turn * Quaternion.Euler(-25f, 0f, 0f));
            }
            GameObject halo = Part("Halo", visual, art.ThinTorus, art.EnemyHalo, new Vector3(0f, 1.4f, 0f), Quaternion.Euler(90f, 0f, 0f),
                Vector3.one * 0.75f, false);
            Transform muzzle = Joint("Muzzle", visual, new Vector3(0f, 0f, 0.75f));
            LineRenderer aim = AimLine(art, root.transform, art.FxAimLine);

            body.SetLook(visual, new[] { halo.GetComponent<Renderer>() }, art.EnemyHalo, art.EnemySelected, art.EnemyHit, new Transform[0]);
            var drone = root.AddComponent<RangedDrone>();
            drone.SetParts(visual, muzzle, eye.GetComponent<Renderer>(), art.EnemyEye, art.EnemyEyeCharge, aim);
            drone.WakeRange = 60f;
            StrikeAnchor(root, body);
            return drone;
        }

        // ------------------------------------------------------------------ Enemy 2: the Hound (fast pursuer)

        static PursuerDrone Hound(LevelContext ctx, Transform parent, string name, Vector3 position, float wakeRange)
        {
            Art art = ctx.Art;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var body = root.AddComponent<CombatEnemy>();
            body.HurtboxRadius = 0.85f;
            body.RespawnDelay = 0f;
            body.ReviveWithPlayer = false;
            body.IsLight = true;
            body.AnimateIdle = false;

            Transform visual = MovementTestBuilder.Group("Visual", root.transform);
            visual.rotation = Quaternion.Euler(0f, 180f, 0f);
            Vector2[] diamond = SliceMeshes.Polygon(4, 0f);
            Part("Hull", visual, SliceMeshes.Loft("Hound Hull", diamond, new[]
            {
                new SliceMeshes.Ring(-1.1f, 0.04f, 0.04f), new SliceMeshes.Ring(-0.5f, 0.3f, 0.2f), new SliceMeshes.Ring(0.25f, 0.34f, 0.22f),
                new SliceMeshes.Ring(1f, 0.03f, 0.03f),
            }, 2), art.EnemyCore, Vector3.zero);
            Part("Spine Plate", visual, SliceMeshes.Loft("Hound Plate", diamond, new[]
            {
                new SliceMeshes.Ring(-0.7f, 0.08f, 0.05f, 0f, 0.17f), new SliceMeshes.Ring(0.5f, 0.12f, 0.06f, 0f, 0.17f),
                new SliceMeshes.Ring(0.85f, 0.02f, 0.02f, 0f, 0.1f),
            }, 2), art.EnemyShell, Vector3.zero);
            Part("Eye", visual, SliceMeshes.Block("Hound Eye", new Vector3(0.32f, 0.045f, 0.08f)), art.EnemyEye, new Vector3(0f, 0.1f, 0.62f),
                Quaternion.identity, Vector3.one, false);
            var blades = new Transform[2];
            var glow = new Renderer[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1f : -1f;
                Transform pivot = Joint(i == 0 ? "Blade R" : "Blade L", visual, new Vector3(side * 0.28f, 0f, 0.05f));
                Part("Blade", pivot, SliceMeshes.Loft("Hound Blade", diamond, new[]
                {
                    new SliceMeshes.Ring(0f, 0.03f, 0.14f), new SliceMeshes.Ring(side * 1.2f, 0.012f, 0.06f, 0f, -0.1f),
                    new SliceMeshes.Ring(side * 1.65f, 0.002f, 0.01f, 0f, -0.2f),
                }, 0), art.EnemyShell, Vector3.zero);
                glow[i] = Part("Blade Edge", pivot, SliceMeshes.Loft("Hound Blade Edge", diamond, new[]
                {
                    new SliceMeshes.Ring(side * 0.1f, 0.006f, 0.006f, 0f, 0.13f), new SliceMeshes.Ring(side * 1.2f, 0.005f, 0.005f, 0f, -0.04f),
                    new SliceMeshes.Ring(side * 1.62f, 0.001f, 0.001f, 0f, -0.19f),
                }, 0), art.DangerSoft, Vector3.zero, Quaternion.identity, Vector3.one, false).GetComponent<Renderer>();
                blades[i] = pivot;
            }
            GameObject halo = Part("Halo", visual, art.ThinTorus, art.EnemyHalo, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), Vector3.one * 0.95f, false);
            LineRenderer aim = AimLine(art, root.transform, art.FxAimLine);

            body.SetLook(visual, new[] { halo.GetComponent<Renderer>() }, art.EnemyHalo, art.EnemySelected, art.EnemyHit, new Transform[0]);
            var hound = root.AddComponent<PursuerDrone>();
            hound.SetParts(visual, blades, glow, art.DangerSoft, art.Danger, aim);
            hound.WakeRange = wakeRange;
            StrikeAnchor(root, body);
            return hound;
        }

        // ------------------------------------------------------------------ Enemy 3: the Warden (space controller)

        static HeavyWarden Warden(LevelContext ctx, Transform parent, string name, Vector3 feet, float yaw)
        {
            Art art = ctx.Art;
            const float feetDepth = 2.3f;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(feet + Vector3.up * feetDepth, Quaternion.Euler(0f, yaw, 0f));
            var body = root.AddComponent<CombatEnemy>();
            body.HurtboxRadius = 1.7f;
            body.RespawnDelay = 0f;
            body.ReviveWithPlayer = false;
            body.IsLight = false;
            body.AnimateIdle = false;

            Transform visual = MovementTestBuilder.Group("Visual", root.transform);
            visual.localRotation = Quaternion.identity;
            Vector2[] oct = SliceMeshes.Polygon(8, 22.5f);
            for (int side = -1; side <= 1; side += 2)
                Part("Leg", visual, SliceMeshes.Block("Warden Leg", new Vector3(0.9f, 1.9f, 1.1f)), art.EnemyCore, new Vector3(side * 0.75f, -1.35f, 0f));
            Part("Torso", visual, SliceMeshes.Loft("Warden Torso", oct, new[]
            {
                new SliceMeshes.Ring(-0.45f, 1.15f, 0.8f), new SliceMeshes.Ring(0.9f, 1.4f, 0.95f), new SliceMeshes.Ring(1.65f, 0.95f, 0.7f),
            }, 1), art.EnemyShell, Vector3.zero);
            Part("Head", visual, SliceMeshes.Block("Warden Head", new Vector3(0.65f, 0.5f, 0.65f)), art.EnemyCore, new Vector3(0f, 1.95f, 0.15f));
            Part("Eye", visual, SliceMeshes.Block("Warden Eye", new Vector3(0.48f, 0.07f, 0.06f)), art.EnemyEye, new Vector3(0f, 2f, 0.49f),
                Quaternion.identity, Vector3.one, false);
            Part("Weak Spot", visual, SliceMeshes.Block("Warden Core", new Vector3(1.05f, 1.3f, 0.12f)), art.EnemyWeakSpot, new Vector3(0f, 0.6f, -0.97f),
                Quaternion.identity, Vector3.one, false);

            Transform arms = Joint("Arms", visual, new Vector3(0f, 1.15f, 0f));
            var glow = new System.Collections.Generic.List<Renderer>();
            for (int side = -1; side <= 1; side += 2)
            {
                Part("Forearm", arms, SliceMeshes.Block("Warden Forearm", new Vector3(0.65f, 1.9f, 0.75f)), art.EnemyCore, new Vector3(side * 1.35f, -0.6f, 0.25f));
                glow.Add(Part("Forearm Seam", arms, SliceMeshes.Block("Warden Seam", new Vector3(0.68f, 0.1f, 0.78f)), art.DangerSoft,
                    new Vector3(side * 1.35f, -1.2f, 0.25f), Quaternion.identity, Vector3.one, false).GetComponent<Renderer>());
            }

            // The tower shield: blocks the route and turns every frontal hit away.
            GameObject shield = Part("Shield", visual, SliceMeshes.Block("Warden Shield", new Vector3(5.4f, 4.6f, 0.5f)), art.StoneLight, new Vector3(0f, -0.05f, 1.55f));
            shield.AddComponent<BoxCollider>().size = new Vector3(5.4f, 4.6f, 0.5f);
            Part("Shield Frame", visual, SliceMeshes.Block("Warden Shield Frame", new Vector3(5.6f, 0.3f, 0.6f)), art.EnemyCore, new Vector3(0f, 2.2f, 1.55f));
            for (int i = -1; i <= 1; i++)
            {
                glow.Add(Part("Shield Seam", visual, SliceMeshes.Block("Warden Shield Seam", new Vector3(0.08f, 3.8f, 0.06f)), art.DangerSoft,
                    new Vector3(i * 1.6f, -0.1f, 1.82f), Quaternion.identity, Vector3.one, false).GetComponent<Renderer>());
            }
            var bodyCollider = visual.gameObject.AddComponent<BoxCollider>();
            bodyCollider.center = new Vector3(0f, -0.15f, 0f);
            bodyCollider.size = new Vector3(2.2f, 4.4f, 1.9f);

            GameObject halo = Part("Halo", visual, art.ThinTorus, art.EnemyHalo, new Vector3(0f, 0.2f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one * 1.9f, false);

            GameObject ring = MovementTestBuilder.CreateMeshObject("Shockwave", root.transform, feet + Vector3.up * 0.05f, Quaternion.identity, art.FlatRing, art.FxShockwave);
            ring.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            ring.SetActive(false);

            body.SetLook(visual, new[] { halo.GetComponent<Renderer>() }, art.EnemyHalo, art.EnemySelected, art.EnemyHit, new Transform[0]);
            var warden = root.AddComponent<HeavyWarden>();
            warden.SetParts(visual, arms, ring.transform, glow.ToArray(), art.DangerSoft, art.Danger,
                new Collider[] { shield.GetComponent<BoxCollider>(), bodyCollider }, feetDepth);
            warden.WakeRange = 40f;
            return warden;
        }

        // ------------------------------------------------------------------ Switch

        static EnemySwitch Switch(LevelContext ctx, Transform parent, string name, Vector3 position, RealityTransformSequence sequence)
        {
            Art art = ctx.Art;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var body = root.AddComponent<CombatEnemy>();
            body.HurtboxRadius = 1f;
            body.RespawnDelay = 0f;
            body.ReviveWithPlayer = false;
            body.IsLight = true;
            body.AnimateIdle = true;
            Transform visual = MovementTestBuilder.Group("Visual", root.transform);
            GameObject core = Part("Core", visual, art.Octahedron, art.Energy, Vector3.zero, Quaternion.identity, new Vector3(1.4f, 1.6f, 1.4f), false);
            Part("Ring", visual, art.ThinTorus, art.Graphite, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), Vector3.one * 1.1f, false);
            body.SetLook(visual, new[] { core.GetComponent<Renderer>() }, art.Energy, art.EnemySelected, art.EnemyHit, new Transform[0]);
            var trigger = root.AddComponent<EnemySwitch>();
            trigger.Sequence = sequence;
            return trigger;
        }

        /// <summary>A light enemy can be struck with the tether: LINK at it zips you in and the blade cuts it on arrival.</summary>
        static void StrikeAnchor(GameObject root, CombatEnemy body)
        {
            var anchor = root.AddComponent<TetherAnchor>();
            anchor.Kind = TetherAnchor.AnchorKind.EnemyStrike;
            anchor.StrikeTarget = body;
            anchor.RangeMultiplier = 0.7f;
            anchor.Cooldown = 0.3f;
        }

        static LineRenderer AimLine(Art art, Transform parent, Material material)
        {
            var go = new GameObject("Aim Line");
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.06f;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.colorGradient = Gradient(new Color(1f, 0.55f, 0.2f, 0.9f), new Color(1f, 0.4f, 0.1f, 0.15f));
            line.enabled = false;
            return line;
        }

        // ------------------------------------------------------------------ Enemy bolts

        static EnemyProjectiles BuildEnemyBolts(Art art, Transform parent, VelocityPlayerController player)
        {
            var go = new GameObject("Enemy Bolts");
            go.transform.SetParent(parent, false);
            var bolts = new Transform[10];
            for (int i = 0; i < bolts.Length; i++)
            {
                var bolt = new GameObject($"Bolt {i + 1}");
                bolt.transform.SetParent(go.transform, false);
                Part("Core", bolt.transform, art.Octahedron, art.Bolt, Vector3.zero, Quaternion.identity, Vector3.one * 0.7f, false);
                var trail = bolt.AddComponent<TrailRenderer>();
                trail.time = 0.18f;
                trail.widthMultiplier = 0.45f;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                trail.colorGradient = Gradient(new Color(1f, 0.75f, 0.4f, 1f), new Color(1f, 0.35f, 0.1f, 0f));
                trail.sharedMaterial = art.FxSpark;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                bolt.SetActive(false);
                bolts[i] = bolt.transform;
            }
            var projectiles = go.AddComponent<EnemyProjectiles>();
            projectiles.SetBolts(bolts, player);
            return projectiles;
        }

        // ------------------------------------------------------------------ Effects

        static VfxLibrary BuildVfx(Art art, Transform parent)
        {
            Transform root = MovementTestBuilder.Group("Effects", parent);
            Gradient fade = Gradient(new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0f));

            ParticleSystem dust = Particles(root, "Dust", art.FxDust, ParticleSystemRenderMode.Billboard, 0.9f, 1.5f, 0f, 160,
                Gradient(new Color(1f, 1f, 1f, 0.7f), new Color(1f, 1f, 1f, 0f)), AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            var dustDrag = dust.limitVelocityOverLifetime;
            dustDrag.enabled = true;
            dustDrag.drag = 2.5f;
            ParticleSystem sparks = Particles(root, "Sparks", art.FxSpark, ParticleSystemRenderMode.Stretch, 0.22f, 0.45f, 1.2f, 240, fade,
                AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            var sparkMain = sparks.main;
            sparkMain.startSize = 0.11f;
            var sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.velocityScale = 0.035f;
            sparkRenderer.lengthScale = 1.2f;
            ParticleSystem flash = Particles(root, "Flash", art.FxFlash, ParticleSystemRenderMode.Billboard, 0.12f, 0.14f, 0f, 24,
                Gradient(new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0f)), AnimationCurve.Linear(0f, 0.6f, 1f, 1.5f));
            ParticleSystem shards = Particles(root, "Shards", art.FxShard, ParticleSystemRenderMode.Mesh, 0.9f, 1.4f, 1.6f, 120,
                Gradient(Color.white, Color.white), new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f)));
            var shardMain = shards.main;
            shardMain.startRotation3D = true;
            shardMain.startSize = 0.22f;
            var shardRenderer = shards.GetComponent<ParticleSystemRenderer>();
            shardRenderer.mesh = SliceMeshes.Block("Shard", new Vector3(1f, 0.6f, 0.4f));
            var shardSpin = shards.rotationOverLifetime;
            shardSpin.enabled = true;
            shardSpin.separateAxes = true;
            shardSpin.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
            shardSpin.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
            shardSpin.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            ParticleSystem rings = Particles(root, "Rings", art.FxRing, ParticleSystemRenderMode.HorizontalBillboard, 0.45f, 0.5f, 0f, 24,
                Gradient(new Color(1f, 1f, 1f, 0.9f), new Color(1f, 1f, 1f, 0f)), AnimationCurve.Linear(0f, 0.25f, 1f, 1f));
            ParticleSystem streaks = Particles(root, "Streaks", art.FxStreak, ParticleSystemRenderMode.Stretch, 0.18f, 0.3f, 0f, 120, fade,
                AnimationCurve.Linear(0f, 1f, 1f, 1f));
            var streakMain = streaks.main;
            streakMain.startSize = 0.06f;
            var streakRenderer = streaks.GetComponent<ParticleSystemRenderer>();
            streakRenderer.velocityScale = 0.07f;
            streakRenderer.lengthScale = 2f;

            // Physical debris.
            Transform debrisRoot = MovementTestBuilder.Group("Debris", root);
            var chunks = new Rigidbody[28];
            Mesh debrisMesh = SliceMeshes.Block("Debris", Vector3.one);
            for (int i = 0; i < chunks.Length; i++)
            {
                var chunk = new GameObject($"Debris {i + 1}");
                chunk.transform.SetParent(debrisRoot, false);
                chunk.AddComponent<MeshFilter>().sharedMesh = debrisMesh;
                var renderer = chunk.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = i % 4 == 0 ? art.GraphiteTrim : art.StoneShadow;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                chunk.AddComponent<BoxCollider>().size = Vector3.one;
                var body = chunk.AddComponent<Rigidbody>();
                body.mass = 40f;
                body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                chunk.layer = PlayerLayer;
                chunk.SetActive(false);
                chunks[i] = body;
            }
            var debris = debrisRoot.gameObject.AddComponent<DebrisPool>();
            debris.SetChunks(chunks);

            var library = root.gameObject.AddComponent<VfxLibrary>();
            library.SetSystems(dust, sparks, flash, shards, rings, streaks, debris);
            return library;
        }

        static ParticleSystem Particles(Transform parent, string name, Material material, ParticleSystemRenderMode mode, float lifeMin, float lifeMax,
            float gravity, int max, Gradient colour, AnimationCurve size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.startColor = Color.white;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(colour);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, size);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // ------------------------------------------------------------------ HUD

        static void BuildHud(Art art, Transform parent, Player player, Camera camera)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("HUD");
            go.transform.SetParent(parent, false);
            go.layer = 5;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            var rect = (RectTransform)go.transform;

            Image vignette = FullScreen(go.transform, "Shield Glow", art.Vignette, new Color(1f, 1f, 1f, 0f));
            Image flash = FullScreen(go.transform, "Flash", art.White, new Color(1f, 1f, 1f, 0f));

            Text timer = Label(go.transform, "Timer", font, 44, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Color(1f, 1f, 1f, 0.85f), FontStyle.Bold);
            Text fps = Label(go.transform, "FPS", font, 26, new Vector2(0f, 1f), new Vector2(110f, -64f), new Color(1f, 1f, 1f, 0.6f), FontStyle.Normal);
            Text toast = Label(go.transform, "Toast", font, 38, new Vector2(0.5f, 0.74f), Vector2.zero, new Color(0.75f, 0.97f, 1f, 1f), FontStyle.Bold);
            Text title = Label(go.transform, "Title", font, 112, new Vector2(0.5f, 0.64f), Vector2.zero, new Color(1f, 1f, 1f, 0f), FontStyle.Bold);
            title.text = "F R A C T U R E";
            Text subtitle = Label(go.transform, "Subtitle", font, 30, new Vector2(0.5f, 0.64f), new Vector2(0f, -96f), new Color(0.8f, 0.95f, 1f, 0f), FontStyle.Normal);
            subtitle.text = "PROJECT VELOCITY  ·  VERTICAL SLICE";
            Text hint = Label(go.transform, "Hint", font, 34, new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0.8f, 0.97f, 1f, 0f), FontStyle.Bold);
            Text death = Label(go.transform, "Death Word", font, 96, new Vector2(0.5f, 0.58f), Vector2.zero, new Color(1f, 0.82f, 0.78f, 0f), FontStyle.Bold);

            RectTransform anchorReticle = Reticle(go.transform, "Anchor Reticle", art.Diamond, new Color(0.45f, 0.95f, 1f, 0.95f), 120f);
            RectTransform pulseReticle = Reticle(go.transform, "Pulse Reticle", art.Brackets, new Color(1f, 0.6f, 0.25f, 0.95f), 110f);

            Image panel = FullScreen(go.transform, "Results", art.White, new Color(0.03f, 0.04f, 0.05f, 0.72f));
            Text resultsTitle = Label(panel.transform, "Results Title", font, 60, new Vector2(0.5f, 0.64f), Vector2.zero, new Color(0.8f, 0.97f, 1f, 1f), FontStyle.Bold);
            Text resultsBody = Label(panel.transform, "Results Body", font, 44, new Vector2(0.5f, 0.52f), Vector2.zero, Color.white, FontStyle.Normal);
            resultsBody.rectTransform.sizeDelta = new Vector2(1000f, 260f);
            Text resultsPrompt = Label(panel.transform, "Results Prompt", font, 34, new Vector2(0.5f, 0.38f), Vector2.zero, new Color(1f, 1f, 1f, 0.8f), FontStyle.Bold);
            panel.gameObject.SetActive(false);

            var hud = go.AddComponent<SliceHUD>();
            hud.SetSources(player.Controller, player.Motor, player.Health, player.TetherTargeting, player.Combat, camera);
            hud.SetElements(rect, vignette, flash, timer, toast, title, subtitle, hint, death, fps, anchorReticle, pulseReticle,
                panel.gameObject, resultsTitle, resultsBody, resultsPrompt);
        }

        static Image FullScreen(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = 5;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Text Label(Transform parent, string name, Font font, int size, Vector2 anchor, Vector2 position, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.layer = 5;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(1000f, size * 1.6f);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            text.raycastTarget = false;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        static RectTransform Reticle(Transform parent, string name, Sprite sprite, Color color, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = 5;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            go.SetActive(false);
            return rect;
        }
    }
}
