using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectVelocity.EditorTools
{
    /// <summary>
    /// The level kit: segments and checkpoints, static architecture (colliding, static-batched), decor (no collider), reality
    /// pieces and stages, looping hazards, interactables and the big monumental shapes. Everything is in world space: the route
    /// runs north (+Z) from the spawn at the origin.
    /// </summary>
    public static partial class VerticalSliceBuilder
    {
        // Thickness of the glowing seams on reality pieces (m).
        const float SeamSize = 0.4f;

        sealed class LevelContext
        {
            public readonly Art Art;
            public readonly Transform Root;
            public readonly Transform FxRoot;
            public readonly Transform DecorRoot;
            public readonly List<SliceCheckpoint> Checkpoints = new List<SliceCheckpoint>();
            public readonly List<SliceSegment> Segments = new List<SliceSegment>();
            public readonly float[] BeatStart = new float[9];
            public readonly System.Random Random = new System.Random(20261004);
            public Vector3 Spawn;
            public float SpawnYaw;
            public Transform Segment;

            public LevelContext(Art art)
            {
                Art = art;
                Root = MovementTestBuilder.Group("Level", null);
                FxRoot = MovementTestBuilder.Group("FX", null);
                DecorRoot = MovementTestBuilder.Group("World", Root);
            }

            public float Range(float min, float max)
            {
                return min + (float)Random.NextDouble() * (max - min);
            }
        }

        // ------------------------------------------------------------------ Segments and checkpoints

        /// <summary>
        /// Starts a segment (everything built until the next one belongs to it) with its checkpoint gate at
        /// <paramref name="gate"/>, restarting at <paramref name="restart"/> facing north.
        /// </summary>
        static Transform BeginSegment(LevelContext ctx, int index, string name, string label, Vector3 gate, Vector3 restart, float width,
            float killHeight, bool drawGate = true)
        {
            Transform group = MovementTestBuilder.Group($"{index} - {name}", ctx.Root);
            var segment = group.gameObject.AddComponent<SliceSegment>();
            segment.Index = index;
            ctx.Segments.Add(segment);
            ctx.Segment = group;

            var checkpointObject = new GameObject($"Checkpoint {index} ({label})");
            checkpointObject.transform.SetParent(group, false);
            checkpointObject.transform.position = gate;
            var checkpoint = checkpointObject.AddComponent<SliceCheckpoint>();
            checkpoint.Index = index;
            checkpoint.Label = label;
            checkpoint.KillHeight = killHeight;
            checkpoint.VolumeSize = new Vector3(width + 20f, 60f, 4f);
            var restartPoint = new GameObject("Restart Point");
            restartPoint.transform.SetParent(checkpointObject.transform, false);
            restartPoint.transform.position = restart;
            checkpoint.RestartPoint = restartPoint.transform;
            if (drawGate)
                CheckpointBeacons(ctx, checkpoint, gate, width);
            ctx.Checkpoints.Add(checkpoint);
            return group;
        }

        /// <summary>Two slim graphite posts with energy cores either side of the route; they light up when reached.</summary>
        static void CheckpointBeacons(LevelContext ctx, SliceCheckpoint checkpoint, Vector3 at, float width)
        {
            var glowing = new List<Renderer>();
            for (int side = -1; side <= 1; side += 2)
            {
                float x = at.x + side * (width * 0.5f + 0.8f);
                Deco(ctx, checkpoint.transform, "Beacon Post", new Vector3(x - 0.35f, at.y, at.z - 0.35f), new Vector3(x + 0.35f, at.y + 5.5f, at.z + 0.35f), ctx.Art.Graphite);
                glowing.Add(Glow(checkpoint.transform, "Beacon Core", new Vector3(x - 0.18f, at.y + 1f, at.z - 0.4f),
                    new Vector3(x + 0.18f, at.y + 5.2f, at.z + 0.4f), ctx.Art.BeaconDormant));
            }
            glowing.Add(Glow(checkpoint.transform, "Beacon Line", new Vector3(at.x - width * 0.5f, at.y + 0.01f, at.z - 0.15f),
                new Vector3(at.x + width * 0.5f, at.y + 0.04f, at.z + 0.15f), ctx.Art.BeaconDormant));
            checkpoint.SetLook(glowing.ToArray(), ctx.Art.BeaconDormant, ctx.Art.BeaconLit);
        }

        static SliceCheckpoint Finish(LevelContext ctx, Vector3 at, float width)
        {
            var finishObject = new GameObject("Finish Line");
            finishObject.transform.SetParent(ctx.Segment, false);
            finishObject.transform.position = at;
            var finish = finishObject.AddComponent<SliceCheckpoint>();
            finish.Index = 99;
            finish.Label = "FINISH";
            finish.IsFinish = true;
            finish.KillHeight = -60f;
            finish.VolumeSize = new Vector3(width + 20f, 60f, 6f);
            var restart = new GameObject("Restart Point");
            restart.transform.SetParent(finishObject.transform, false);
            restart.transform.position = at;
            finish.RestartPoint = restart.transform;
            CheckpointBeacons(ctx, finish, at, width);
            return finish;
        }

        // ------------------------------------------------------------------ Static architecture

        /// <summary>A colliding, static-batched block between two corners.</summary>
        static GameObject Solid(LevelContext ctx, Transform parent, string name, Vector3 min, Vector3 max, Material material, bool castShadows = true)
        {
            GameObject go = MovementTestBuilder.Box(name, parent, min.x, max.x, min.y, max.y, min.z, max.z, material);
            go.GetComponent<MeshRenderer>().shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>A walkable deck: top at <paramref name="top"/>, <paramref name="thickness"/> deep, with graphite edge trims.</summary>
        static GameObject Deck(LevelContext ctx, Transform parent, string name, float xMin, float xMax, float zMin, float zMax, float top,
            float thickness = 3f, Material material = null, bool trims = true)
        {
            GameObject deck = Solid(ctx, parent, name, new Vector3(xMin, top - thickness, zMin), new Vector3(xMax, top, zMax), material ?? ctx.Art.Stone);
            if (trims)
            {
                Deco(ctx, parent, "Edge Trim", new Vector3(xMin - 0.25f, top - 1.2f, zMin), new Vector3(xMin + 0.05f, top + 0.12f, zMax), ctx.Art.GraphiteTrim);
                Deco(ctx, parent, "Edge Trim", new Vector3(xMax - 0.05f, top - 1.2f, zMin), new Vector3(xMax + 0.25f, top + 0.12f, zMax), ctx.Art.GraphiteTrim);
            }
            return deck;
        }

        /// <summary>A visual-only block (no collider), static-batched.</summary>
        static GameObject Deco(LevelContext ctx, Transform parent, string name, Vector3 min, Vector3 max, Material material, bool castShadows = false)
        {
            Vector3 centre = (min + max) * 0.5f;
            Vector3 size = max - min;
            GameObject go = MovementTestBuilder.CreateMeshObject(name, parent, centre, Quaternion.identity, GrayboxMeshes.Box(size, centre), material);
            go.GetComponent<MeshRenderer>().shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>A visual-only block whose material changes at runtime (kept out of static batching).</summary>
        static Renderer Glow(Transform parent, string name, Vector3 min, Vector3 max, Material material)
        {
            Vector3 centre = (min + max) * 0.5f;
            GameObject go = MovementTestBuilder.CreateMeshObject(name, parent, centre, Quaternion.identity, GrayboxMeshes.Box(max - min, centre), material);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        /// <summary>A visual-only block rotated about its centre.</summary>
        static GameObject DecoRotated(LevelContext ctx, Transform parent, string name, Vector3 centre, Vector3 size, Quaternion rotation, Material material,
            bool castShadows = false)
        {
            GameObject go = MovementTestBuilder.CreateMeshObject(name, parent, centre, rotation, GrayboxMeshes.Box(size, centre), material);
            go.GetComponent<MeshRenderer>().shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>A colliding block rotated about its centre (static).</summary>
        static GameObject SolidRotated(LevelContext ctx, Transform parent, string name, Vector3 centre, Vector3 size, Quaternion rotation, Material material)
        {
            GameObject go = MovementTestBuilder.CreateMeshObject(name, parent, centre, rotation, GrayboxMeshes.Box(size, centre), material);
            go.AddComponent<BoxCollider>().size = size;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>A pier: a tall square column from deep in the void up to <paramref name="top"/>.</summary>
        static void Pier(LevelContext ctx, Transform parent, float x, float z, float width, float top)
        {
            Deco(ctx, parent, "Pier", new Vector3(x - width * 0.5f, -260f, z - width * 0.5f), new Vector3(x + width * 0.5f, top, z + width * 0.5f), ctx.Art.StoneShadow);
            Deco(ctx, parent, "Pier Band", new Vector3(x - width * 0.5f - 0.2f, top - 6f, z - width * 0.5f - 0.2f),
                new Vector3(x + width * 0.5f + 0.2f, top - 4.5f, z + width * 0.5f + 0.2f), ctx.Art.Graphite);
        }

        /// <summary>A thin glowing line set into a surface (guidance, never decoration for its own sake).</summary>
        static void LightLine(LevelContext ctx, Transform parent, Vector3 min, Vector3 max, bool bright = false)
        {
            Deco(ctx, parent, "Light Line", min, max, bright ? ctx.Art.Energy : ctx.Art.EnergySoft);
        }

        /// <summary>A bone cloth banner hanging from <paramref name="top"/> along -Y, facing along <paramref name="facing"/>; it sways.</summary>
        static void Banner(LevelContext ctx, Transform parent, Vector3 top, float width, float height, Vector3 facing)
        {
            var go = new GameObject("Banner");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(top, Quaternion.LookRotation(-facing, Vector3.up));
            go.AddComponent<MeshFilter>().sharedMesh = SliceMeshes.Strip("Banner", width, height, 3, 10);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ctx.Art.Cloth;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            go.AddComponent<SwayCloth>();
        }

        /// <summary>Verdigris growth spilling down a wall face (thin slabs), the one trace of life in the stone.</summary>
        static void Growth(LevelContext ctx, Transform parent, Vector3 topCentre, Vector3 outward, float width, float height)
        {
            Vector3 side = Vector3.Cross(Vector3.up, outward).normalized;
            int strands = Mathf.Max(2, Mathf.RoundToInt(width / 1.6f));
            for (int i = 0; i < strands; i++)
            {
                float u = strands == 1 ? 0.5f : i / (float)(strands - 1);
                float h = height * ctx.Range(0.45f, 1f);
                Vector3 c = topCentre + side * ((u - 0.5f) * width) + Vector3.down * (h * 0.5f) + outward * 0.08f;
                Vector3 size = new Vector3(ctx.Range(0.6f, 1.4f), h, 0.12f);
                DecoRotated(ctx, parent, "Growth", c, size, Quaternion.LookRotation(outward, Vector3.up), ctx.Art.Verdigris);
            }
        }

        // ------------------------------------------------------------------ Monumental world

        /// <summary>A colossal monolith in the void: banded stone, a dark slit of mechanism, sometimes a faint energy seam.</summary>
        static void Monolith(LevelContext ctx, Vector3 baseCentre, float width, float height, float yaw, float tilt)
        {
            var go = new GameObject("Monolith");
            go.transform.SetParent(ctx.DecorRoot, false);
            go.transform.SetPositionAndRotation(baseCentre, Quaternion.Euler(tilt, yaw, tilt * 0.5f));
            Transform t = go.transform;
            AddLocalBox(t, "Mass", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, width * 0.85f), ctx.Art.Stone);
            AddLocalBox(t, "Band", new Vector3(0f, height * 0.62f, 0f), new Vector3(width + 1.2f, 4f, width * 0.85f + 1.2f), ctx.Art.Graphite);
            AddLocalBox(t, "Band", new Vector3(0f, height * 0.34f, 0f), new Vector3(width + 0.8f, 2f, width * 0.85f + 0.8f), ctx.Art.GraphiteTrim);
            AddLocalBox(t, "Slit", new Vector3(0f, height * 0.5f, -width * 0.43f), new Vector3(width * 0.12f, height * 0.7f, 1f), ctx.Art.Graphite);
            if (ctx.Random.NextDouble() < 0.5)
                AddLocalBox(t, "Seam", new Vector3(0f, height * 0.5f, -width * 0.44f), new Vector3(width * 0.03f, height * 0.62f, 1f), ctx.Art.EnergySoft);
            AddLocalBox(t, "Crown", new Vector3(0f, height + 3f, 0f), new Vector3(width * 0.7f, 6f, width * 0.6f), ctx.Art.StoneShadow);
            foreach (Transform part in t)
                GameObjectUtility.SetStaticEditorFlags(part.gameObject, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>A slab of architecture hanging in the air, turning very slowly.</summary>
        static void FloatingBlock(LevelContext ctx, Vector3 centre, Vector3 size, float spin)
        {
            var go = new GameObject("Floating Block");
            go.transform.SetParent(ctx.DecorRoot, false);
            go.transform.SetPositionAndRotation(centre, Quaternion.Euler(ctx.Range(-20f, 20f), ctx.Range(0f, 360f), ctx.Range(-20f, 20f)));
            AddLocalBox(go.transform, "Mass", Vector3.zero, size, ctx.Art.Stone);
            AddLocalBox(go.transform, "Edge", new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x + 0.4f, 0.6f, size.z + 0.4f), ctx.Art.Graphite);
            go.AddComponent<SpinVisual>().Configure(new Vector3(ctx.Range(-0.3f, 0.3f), 1f, ctx.Range(-0.3f, 0.3f)).normalized, spin);
        }

        static GameObject AddLocalBox(Transform parent, string name, Vector3 localCentre, Vector3 size, Material material, bool castShadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            go.AddComponent<MeshFilter>().sharedMesh = GrayboxMeshes.Box(size, parent.TransformPoint(localCentre));
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------------ Reality pieces and stages

        /// <summary>
        /// One stage: a sequence and the trigger that starts it. The trigger sits at <paramref name="timedAt"/> facing north, its
        /// volume <paramref name="lead"/> metres back along the route (volume = width, height, depth).
        /// </summary>
        static RealityTransformSequence Stage(LevelContext ctx, Transform parent, string name, Vector3 timedAt, float lead, Vector3 volume)
        {
            Transform group = MovementTestBuilder.Group(name, parent);
            var sequence = group.gameObject.AddComponent<RealityTransformSequence>();
            var triggerObject = new GameObject($"Trigger (fires {lead:0} m before this point)");
            triggerObject.transform.SetParent(group, false);
            triggerObject.transform.SetPositionAndRotation(timedAt, Quaternion.identity);
            var trigger = triggerObject.AddComponent<RealityTrigger>();
            trigger.Sequence = sequence;
            trigger.LeadDistance = lead;
            trigger.VolumeSize = volume;
            trigger.RearmOnRespawn = false;
            return sequence;
        }

        /// <summary>A stage with no trigger (set off by a switch).</summary>
        static RealityTransformSequence SwitchStage(LevelContext ctx, Transform parent, string name)
        {
            Transform group = MovementTestBuilder.Group(name, parent);
            return group.gameObject.AddComponent<RealityTransformSequence>();
        }

        /// <summary>An empty reality piece whose object is the pivot. Its parts are added where they sit in the built pose.</summary>
        static RealityChunk Piece(Transform parent, string name, Vector3 pivot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pivot;
            return go.AddComponent<RealityChunk>();
        }

        /// <summary>Solid part of a reality piece (with box collider) between two world corners.</summary>
        static GameObject Body(LevelContext ctx, RealityChunk piece, Vector3 min, Vector3 max, Material material = null, string name = "Body")
        {
            GameObject go = MovementTestBuilder.Box(name, piece.transform, min.x, max.x, min.y, max.y, min.z, max.z, material ?? ctx.Art.RealityBody);
            return go;
        }

        /// <summary>A glowing seam (no collider, no shadow) between two points that differ along one axis.</summary>
        static void Seam(LevelContext ctx, Transform parent, Vector3 a, Vector3 b, Material material = null)
        {
            var size = new Vector3(Mathf.Abs(b.x - a.x) + SeamSize, Mathf.Abs(b.y - a.y) + SeamSize, Mathf.Abs(b.z - a.z) + SeamSize);
            Vector3 centre = (a + b) * 0.5f;
            GameObject seam = MovementTestBuilder.CreateMeshObject("Seam", parent, centre, Quaternion.identity, GrayboxMeshes.Box(size, centre),
                material ?? ctx.Art.RealityDormant);
            seam.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Seams around the top face of a box.</summary>
        static void OutlineTop(LevelContext ctx, Transform parent, Vector3 min, Vector3 max, Material material = null)
        {
            float y = max.y;
            Seam(ctx, parent, new Vector3(min.x, y, min.z), new Vector3(max.x, y, min.z), material);
            Seam(ctx, parent, new Vector3(min.x, y, max.z), new Vector3(max.x, y, max.z), material);
            Seam(ctx, parent, new Vector3(min.x, y, min.z), new Vector3(min.x, y, max.z), material);
            Seam(ctx, parent, new Vector3(max.x, y, min.z), new Vector3(max.x, y, max.z), material);
        }

        /// <summary>Seams around a face across X at <paramref name="x"/> (a face looking east or west).</summary>
        static void OutlineSideX(LevelContext ctx, Transform parent, float x, Vector3 min, Vector3 max, Material material = null)
        {
            Seam(ctx, parent, new Vector3(x, min.y, min.z), new Vector3(x, min.y, max.z), material);
            Seam(ctx, parent, new Vector3(x, max.y, min.z), new Vector3(x, max.y, max.z), material);
            Seam(ctx, parent, new Vector3(x, min.y, min.z), new Vector3(x, max.y, min.z), material);
            Seam(ctx, parent, new Vector3(x, min.y, max.z), new Vector3(x, max.y, max.z), material);
        }

        /// <summary>Seams around a face across Z at <paramref name="z"/> (a face looking north or south).</summary>
        static void OutlineSideZ(LevelContext ctx, Transform parent, float z, Vector3 min, Vector3 max, Material material = null)
        {
            Seam(ctx, parent, new Vector3(min.x, min.y, z), new Vector3(max.x, min.y, z), material);
            Seam(ctx, parent, new Vector3(min.x, max.y, z), new Vector3(max.x, max.y, z), material);
            Seam(ctx, parent, new Vector3(min.x, min.y, z), new Vector3(min.x, max.y, z), material);
            Seam(ctx, parent, new Vector3(max.x, min.y, z), new Vector3(max.x, max.y, z), material);
        }

        /// <summary>
        /// Sets up a piece's move: from (built position + <paramref name="fromOffset"/>, <paramref name="fromRotation"/>) to
        /// (built position + <paramref name="toOffset"/>, <paramref name="toRotation"/>), in its parent's space. Its glowing seams
        /// are the parts made with the dormant material.
        /// </summary>
        static void Animate(LevelContext ctx, RealityChunk piece, Vector3 fromOffset, Vector3 fromRotation, Vector3 toOffset, Vector3 toRotation,
            float delay, float duration, AnimationCurve curve, float anticipation, float tremble)
        {
            Vector3 built = piece.transform.localPosition;
            piece.Configure(built + fromOffset, fromRotation, built + toOffset, toRotation, duration, curve);
            piece.Delay = delay;
            piece.Anticipation = anticipation;
            piece.Tremble = tremble;
            Renderer[] seams = piece.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterial == ctx.Art.RealityDormant).ToArray();
            piece.SetLook(seams, ctx.Art.RealityDormant, ctx.Art.RealityWarning, ctx.Art.RealityShifting, ctx.Art.RealitySettled);
        }

        /// <summary>
        /// The danger look: graphite body, orange seams that flare before it moves. <paramref name="lethal"/>: contact while it
        /// moves kills (anything that pins you crushes you, lethal or not). Call after <see cref="Animate"/>.
        /// </summary>
        static void MakeHazard(LevelContext ctx, RealityChunk piece, bool lethal)
        {
            piece.IsLethal = lethal;
            foreach (Renderer r in piece.GetComponentsInChildren<Renderer>(true))
            {
                if (r.sharedMaterial == ctx.Art.RealityBody)
                    r.sharedMaterial = ctx.Art.HazardBody;
                else if (r.sharedMaterial == ctx.Art.RealityDormant)
                    r.sharedMaterial = ctx.Art.HazardDormant;
            }
            foreach (RealityChunk chunk in piece.GetComponentsInChildren<RealityChunk>(true))
            {
                Renderer[] seams = chunk.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterial == ctx.Art.HazardDormant).ToArray();
                chunk.SetLook(seams, ctx.Art.HazardDormant, ctx.Art.HazardWarning, ctx.Art.HazardMoving, ctx.Art.HazardDormant);
            }
        }

        /// <summary>A looping mover (crusher, turning beam): its object is the pivot; add parts with <see cref="LoopBody"/>.</summary>
        static LoopingMotion Loop(Transform parent, string name, Vector3 pivot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pivot;
            return go.AddComponent<LoopingMotion>();
        }

        static GameObject LoopBody(LevelContext ctx, LoopingMotion loop, Vector3 min, Vector3 max, Material material)
        {
            return MovementTestBuilder.Box("Body", loop.transform, min.x, max.x, min.y, max.y, min.z, max.z, material);
        }

        // ------------------------------------------------------------------ Interactables

        /// <summary>A tether anchor: a graphite clamp holding a bright ring that faces the camera.</summary>
        static TetherAnchor Anchor(LevelContext ctx, Transform parent, string name, Vector3 position, TetherAnchor.AnchorKind kind = TetherAnchor.AnchorKind.Swing,
            float rangeMultiplier = 1f, bool clamp = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var anchor = go.AddComponent<TetherAnchor>();
            anchor.Kind = kind;
            anchor.RangeMultiplier = rangeMultiplier;

            Transform visual = MovementTestBuilder.Group("Visual", go.transform);
            GameObject ring = MovementTestBuilder.CreateMeshObject("Ring", visual, position, Quaternion.identity, ctx.Art.AnchorRing, ctx.Art.AnchorIdle);
            GameObject core = MovementTestBuilder.CreateMeshObject("Core", visual, position, Quaternion.identity, ctx.Art.Octahedron, ctx.Art.AnchorIdle);
            core.transform.localScale = Vector3.one * 0.9f;
            if (clamp)
            {
                MovementTestBuilder.CreateMeshObject("Clamp", go.transform, position + Vector3.up * 1.6f, Quaternion.identity,
                    SliceMeshes.Block("Anchor Clamp", new Vector3(0.7f, 1.6f, 0.7f)), ctx.Art.Graphite);
            }
            anchor.SetLook(new[] { ring.GetComponent<Renderer>(), core.GetComponent<Renderer>() }, ctx.Art.AnchorIdle, ctx.Art.AnchorSelected,
                ring.transform, visual);
            return anchor;
        }

        /// <summary>A traversal target: a cyan orb in a thin camera-facing ring.</summary>
        static TraversalTarget Target(LevelContext ctx, Transform parent, string name, Vector3 position, float extraUpwardBias = 0f, float rangeMultiplier = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var target = go.AddComponent<TraversalTarget>();
            target.ExtraUpwardBias = extraUpwardBias;
            target.RangeMultiplier = rangeMultiplier;
            Transform visual = MovementTestBuilder.Group("Visual", go.transform);
            GameObject core = MovementTestBuilder.CreateMeshObject("Core", visual, position, Quaternion.Euler(0f, 45f, 0f), ctx.Art.Octahedron, ctx.Art.TargetIdle);
            core.transform.localScale = Vector3.one * 1.5f;
            GameObject ring = MovementTestBuilder.CreateMeshObject("Ring", visual, position, Quaternion.identity, ctx.Art.TargetRing, ctx.Art.TargetIdle);
            target.SetLook(new[] { core.GetComponent<Renderer>(), ring.GetComponent<Renderer>() }, ctx.Art.TargetIdle, ctx.Art.TargetSelected,
                ctx.Art.TargetCooldown, ring.transform, visual);
            return target;
        }

        // ------------------------------------------------------------------ Motion personalities (pose 0 → 1 over time 0 → 1)

        static Keyframe K(float time, float value, float inTangent, float outTangent)
        {
            return new Keyframe(time, value, inTangent, outTangent);
        }

        /// <summary>Fast eruption: bursts out, brakes hard, overshoots a little and settles heavily.</summary>
        static AnimationCurve Eruption()
        {
            return new AnimationCurve(K(0f, 0f, 0f, 4.5f), K(0.42f, 1.05f, 0f, 0f), K(0.7f, 0.985f, 0f, 0f), K(1f, 1f, 0f, 0f));
        }

        /// <summary>Slam: falls faster and faster, stops dead, kicks back a little and settles.</summary>
        static AnimationCurve Slam()
        {
            return new AnimationCurve(K(0f, 0f, 0f, 0.3f), K(0.72f, 1f, 2.8f, 0f), K(0.84f, 0.985f, 0f, 0f), K(1f, 1f, 0f, 0f));
        }

        /// <summary>Heavy turn without sway: slow to start, gathers speed, firm stop (for surfaces you may be on).</summary>
        static AnimationCurve HeavySafe()
        {
            return new AnimationCurve(K(0f, 0f, 0f, 0f), K(0.7f, 0.8f, 1.9f, 1.9f), K(1f, 1f, 0.2f, 0f));
        }

        /// <summary>Collapse: hesitates, then drops away faster and faster.</summary>
        static AnimationCurve Collapse()
        {
            return new AnimationCurve(K(0f, 0f, 0f, 0f), K(0.4f, 0.1f, 0.6f, 0.6f), K(1f, 1f, 2.6f, 0f));
        }

        /// <summary>Swing up: fast at first, easing into a firm stop with no bounce (you land on it next).</summary>
        static AnimationCurve SwingUp()
        {
            return new AnimationCurve(K(0f, 0f, 0f, 2f), K(1f, 1f, 0f, 0f));
        }

        /// <summary>Retraction: eases off and pulls away faster and faster.</summary>
        static AnimationCurve Retract()
        {
            return new AnimationCurve(K(0f, 0f, 0f, 0f), K(1f, 1f, 2.2f, 0f));
        }
    }
}
