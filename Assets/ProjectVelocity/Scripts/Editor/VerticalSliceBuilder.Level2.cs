using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectVelocity.EditorTools
{
    /// <summary>
    /// FRACTURE, beats 5-9:
    ///   5 The Terraces: combat during traversal. Broken islands over the void; a Lantern mid-gap to cut down in the air (the
    ///     kill gives the boost back for the rest of the gap), Hounds on the chase, a Warden blocking a narrow neck (go over on
    ///     the anchor, around on the wall, or behind it), and a gap you cross either on a bridge raised by a switch (cut it or
    ///     pulse it) or, faster, through a traversal target.
    ///   6 The Turning Hall: reality escalates. The hall rolls 90° around you (its wall becomes the floor, its window a pit to
    ///     wall run across), then its pieces fly out and lock into an ascending wall-run garden; a two-anchor swing chain
    ///     carries you out over the void.
    ///   7 The Gauntlet: hard platforming. A turning beam to run while its faces tilt, three crushers on a rhythm, then a chain:
    ///     jump, cut the Lantern for your boost, boost to the wall, wall run, wall jump, target.
    ///   8 The Collapse: the causeway falls away behind you faster than you can run (boost to stay ahead) while pillars topple
    ///     across your path, ending on a ramp that throws you up onto the crown.
    ///   9 The Crown: the payoff. The megastructure's crown opens around a blazing anomaly; run into the light.
    /// </summary>
    public static partial class VerticalSliceBuilder
    {
        // ------------------------------------------------------------------ Beat 5: The Terraces

        static void BuildTerraces(LevelContext ctx)
        {
            ctx.BeatStart[4] = 1028f;
            Transform seg = BeginSegment(ctx, 4, "The Terraces", "THE TERRACES", new Vector3(0f, -4f, 1046f), new Vector3(0f, -3.9f, 1048f), 32f, -60f);
            Transform a = MovementTestBuilder.Group("Architecture", seg);
            Transform enemies = MovementTestBuilder.Group("Enemies", seg);

            // I1: the Well's landing. A 26 m gap, 2 m down, to I2: a jump and a boost just make it; cut the Lantern hanging in
            // the gap on the way and the kill gives the boost back.
            Island(ctx, a, "Island 1 (the landing)", -16f, 16f, 1028f, 1092f, -4f);
            Ranged(ctx, enemies, "Lantern (mid-gap)", new Vector3(0f, -1f, 1105f));

            // I2, Hounds perched on two spires.
            Island(ctx, a, "Island 2", -18f, 18f, 1118f, 1160f, -6f);
            Solid(ctx, a, "Spire", new Vector3(-15.5f, -6f, 1147.5f), new Vector3(-12.5f, 0f, 1150.5f), ctx.Art.StoneShadow);
            Solid(ctx, a, "Spire", new Vector3(12.5f, -6f, 1153.5f), new Vector3(15.5f, 1f, 1156.5f), ctx.Art.StoneShadow);
            Hound(ctx, enemies, "Hound (left spire)", new Vector3(-14f, 2f, 1149f), 45f);
            Hound(ctx, enemies, "Hound (right spire)", new Vector3(14f, 3f, 1155f), 45f);

            // The neck: 6 m wide, and a Warden in the middle of it facing you. Over it on the zip anchor (its straight line clears
            // the Warden's head by about 2.5 m from anywhere on the island), along the wall slab beside it, or past its slow turn:
            // then cut it from behind (or don't; it only blocks).
            Deck(ctx, a, "The Neck", -3f, 3f, 1160f, 1206f, -6f, 3f, ctx.Art.StoneLight);
            Warden(ctx, enemies, "Warden (the neck)", new Vector3(0f, -6f, 1186f), 180f);
            Solid(ctx, a, "Wall Slab Beside The Neck", new Vector3(5.5f, -14f, 1154f), new Vector3(6.5f, 6f, 1214f), ctx.Art.Stone);
            LightLine(ctx, a, new Vector3(5.45f, 5.6f, 1154f), new Vector3(5.5f, 5.9f, 1214f));
            Anchor(ctx, seg, "Zip Anchor - Over The Warden", new Vector3(0f, 5f, 1197f), TetherAnchor.AnchorKind.Zip, 0.9f);
            Deco(ctx, a, "Anchor Chain", new Vector3(-0.25f, 6.6f, 1196.75f), new Vector3(0.25f, 40f, 1197.25f), ctx.Art.Graphite);
            FloatingBlock(ctx, new Vector3(0f, 46f, 1197f), new Vector3(14f, 6f, 10f), 0f);

            // I3: a 40 m gap to I4. Cut or pulse the switch to raise the bridge, or take the target straight across.
            Island(ctx, a, "Island 3", -14f, 14f, 1206f, 1256f, -6f);
            RealityTransformSequence bridgeStage = SwitchStage(ctx, seg, "Stage - The Bridge Rises (switch)");
            RealityChunk bridge = Piece(bridgeStage.transform, "Bridge Rises From The Abyss", new Vector3(0f, -7.5f, 1276f));
            Body(ctx, bridge, new Vector3(-4f, -9f, 1256f), new Vector3(4f, -6f, 1296f));
            OutlineTop(ctx, bridge.transform, new Vector3(-4f, -9f, 1256f), new Vector3(4f, -6f, 1296f));
            Animate(ctx, bridge, new Vector3(0f, -70f, 0f), new Vector3(0f, 0f, 12f), Vector3.zero, Vector3.zero, 0f, 1.4f, SwingUp(), 0.25f, 0f);
            bridgeStage.SetChunks(new[] { bridge });
            Solid(ctx, a, "Switch Pylon", new Vector3(7.5f, -6f, 1247.5f), new Vector3(10.5f, -3f, 1250.5f), ctx.Art.Graphite);
            Switch(ctx, enemies, "Switch (raises the bridge)", new Vector3(9f, -1.6f, 1249f), bridgeStage);
            Target(ctx, seg, "Target - Across The Gap", new Vector3(0f, 0f, 1276f), 0.1f, 1.1f);

            Island(ctx, a, "Island 4", -16f, 16f, 1296f, 1340f, -6f);
        }

        /// <summary>A broken island of architecture: a deck with a jagged mass hanging under it.</summary>
        static void Island(LevelContext ctx, Transform parent, string name, float xMin, float xMax, float zMin, float zMax, float top)
        {
            Deck(ctx, parent, name, xMin, xMax, zMin, zMax, top, 5f, ctx.Art.Stone);
            float w = xMax - xMin;
            float d = zMax - zMin;
            Deco(ctx, parent, "Underside", new Vector3(xMin + w * 0.1f, top - 22f, zMin + d * 0.1f), new Vector3(xMax - w * 0.1f, top - 5f, zMax - d * 0.1f), ctx.Art.StoneShadow);
            Deco(ctx, parent, "Underside", new Vector3(xMin + w * 0.3f, top - 46f, zMin + d * 0.3f), new Vector3(xMax - w * 0.35f, top - 22f, zMax - d * 0.25f), ctx.Art.StoneShadow);
            Deco(ctx, parent, "Underside Band", new Vector3(xMin + w * 0.1f - 0.2f, top - 9f, zMin + d * 0.1f - 0.2f),
                new Vector3(xMax - w * 0.1f + 0.2f, top - 7.5f, zMax - d * 0.1f + 0.2f), ctx.Art.Graphite);
        }

        // ------------------------------------------------------------------ Beat 6: The Turning Hall

        static void BuildHall(LevelContext ctx)
        {
            ctx.BeatStart[5] = 1300f;
            Transform seg = BeginSegment(ctx, 5, "The Turning Hall", "THE TURNING HALL", new Vector3(0f, -6f, 1306f), new Vector3(0f, -5.9f, 1308f), 32f, -60f);
            Transform a = MovementTestBuilder.Group("Architecture", seg);

            // The hall: 22 x 22 m inside, 85 m long. As you run in it rolls 90° around you: the left wall becomes the floor (at
            // the same height), the floor becomes the right wall, and the window in the left wall becomes a 30 m pit to wall
            // run across. (Section J's rolling room, at the slice's scale; you are carried until the floor gets too steep.)
            RealityTransformSequence roll = Stage(ctx, seg, "Stage - The Hall Turns", new Vector3(0f, -6f, 1342f), 8f, new Vector3(26f, 30f, 6f));
            RealityChunk room = Piece(roll.transform, "The Hall Rolls 90° Around You [wall run the pit]", new Vector3(0f, 5f, 1382.5f));
            Body(ctx, room, new Vector3(-13f, -8f, 1340f), new Vector3(13f, -6f, 1425f), ctx.Art.RealityBody, "Floor");
            Body(ctx, room, new Vector3(-13f, 16f, 1340f), new Vector3(13f, 18f, 1425f), ctx.Art.RealityBody, "Ceiling");
            Body(ctx, room, new Vector3(-13f, -6f, 1340f), new Vector3(-11f, 16f, 1388f), ctx.Art.StoneLight, "Left Wall");
            Body(ctx, room, new Vector3(-13f, -6f, 1418f), new Vector3(-11f, 16f, 1425f), ctx.Art.StoneLight, "Left Wall");
            Body(ctx, room, new Vector3(11f, -6f, 1340f), new Vector3(13f, 16f, 1425f), ctx.Art.RealityBody, "Right Wall");
            Seam(ctx, room.transform, new Vector3(-11f, -6f, 1340f), new Vector3(-11f, -6f, 1425f));
            Seam(ctx, room.transform, new Vector3(11f, -6f, 1340f), new Vector3(11f, -6f, 1425f));
            Seam(ctx, room.transform, new Vector3(-11f, 16f, 1340f), new Vector3(-11f, 16f, 1425f));
            Seam(ctx, room.transform, new Vector3(11f, 16f, 1340f), new Vector3(11f, 16f, 1425f));
            Seam(ctx, room.transform, new Vector3(-11f, -6f, 1388f), new Vector3(-11f, 16f, 1388f));
            Seam(ctx, room.transform, new Vector3(-11f, -6f, 1418f), new Vector3(-11f, 16f, 1418f));
            for (float z = 1350f; z < 1425f; z += 15f)
            {
                Seam(ctx, room.transform, new Vector3(-11f, 16f, z), new Vector3(11f, 16f, z), ctx.Art.RealityDormant);
                MovingDeco(room.transform, "Rib", new Vector3(10.6f, -6f, z - 0.6f), new Vector3(11f, 16f, z + 0.6f), ctx.Art.Graphite);
            }
            Animate(ctx, room, Vector3.zero, Vector3.zero, Vector3.zero, new Vector3(0f, 0f, 90f), 0.2f, 0.85f, HeavySafe(), 0.2f, 0.1f);
            roll.SetChunks(new[] { room });

            Deck(ctx, a, "Hall Exit", -11f, 11f, 1425f, 1446f, -6f, 3f, ctx.Art.StoneLight);
            Solid(ctx, a, "Hall Portal", new Vector3(-16f, -6f, 1336f), new Vector3(-13.2f, 22f, 1340f), ctx.Art.StoneLight);
            Solid(ctx, a, "Hall Portal", new Vector3(13.2f, -6f, 1336f), new Vector3(16f, 22f, 1340f), ctx.Art.StoneLight);
            Deco(ctx, a, "Hall Lintel", new Vector3(-16f, 19f, 1336f), new Vector3(16f, 24f, 1340f), ctx.Art.Stone);
            LightLine(ctx, a, new Vector3(-16f, 18.6f, 1335.9f), new Vector3(16f, 18.9f, 1336f), true);

            // The garden: as you leave, the hall's pieces fly out ahead and lock into four alternating wall-run panels 10 m apart,
            // each higher than the last (the movement test's ascending walls), then a platform. Each is in place before a fast
            // runner can reach it.
            RealityTransformSequence garden = Stage(ctx, seg, "Stage - The Hall Breaks Into A Garden", new Vector3(0f, -6f, 1430f), 35f, new Vector3(30f, 40f, 6f));
            var gardenPieces = new List<RealityChunk>();
            float[,] panels =
            {
                { -6f, -5f, 1450f, 1475f, -6f, 3f },
                { 5f, 6f, 1475f, 1500f, -4f, 6f },
                { -6f, -5f, 1500f, 1525f, -2f, 9f },
                { 5f, 6f, 1525f, 1550f, 0f, 12f },
            };
            for (int i = 0; i < 4; i++)
            {
                var min = new Vector3(panels[i, 0], panels[i, 4], panels[i, 2]);
                var max = new Vector3(panels[i, 1], panels[i, 5], panels[i, 3]);
                float side = min.x < 0f ? -1f : 1f;
                RealityChunk panel = Piece(garden.transform, $"Garden Panel {i + 1} [wall run, wall jump]", (min + max) * 0.5f);
                Body(ctx, panel, min, max, ctx.Art.StoneLight);
                float face = side < 0f ? max.x : min.x;
                OutlineSideX(ctx, panel.transform, face, min, max);
                Animate(ctx, panel, new Vector3(side * 34f, 28f + i * 5f, 40f), new Vector3(35f * side, 80f, 20f), Vector3.zero, Vector3.zero,
                    0.3f + 0.3f * i, 0.9f, SwingUp(), 0.15f, 0f);
                gardenPieces.Add(panel);
            }
            RealityChunk platform = Piece(garden.transform, "Garden Platform", new Vector3(0f, -1f, 1571f));
            Body(ctx, platform, new Vector3(-7f, -2f, 1552f), new Vector3(7f, 0f, 1590f), ctx.Art.Stone);
            OutlineTop(ctx, platform.transform, new Vector3(-7f, -2f, 1552f), new Vector3(7f, 0f, 1590f));
            Animate(ctx, platform, new Vector3(0f, -55f, 30f), new Vector3(-25f, 0f, 0f), Vector3.zero, Vector3.zero, 1.4f, 0.9f, SwingUp(), 0.15f, 0f);
            gardenPieces.Add(platform);
            garden.SetChunks(gardenPieces.ToArray());

            // A checkpoint on the way out of the hall. The garden belongs to the hall's segment, so restarting here finds it
            // already assembled (its trigger is behind this point and would never fire again).
            BeginSegment(ctx, 6, "The Garden", "THE GARDEN", new Vector3(0f, -6f, 1432f), new Vector3(0f, -5.9f, 1434f), 22f, -60f);

            // Off the platform, two anchors in a row out over the void (simulated: holding both lands about 142 m out, good
            // releases about 195 m; letting go of the second too early falls short). Checkpoint first: this is a commitment.
            ctx.BeatStart[6] = 1556f;
            Transform ascent = BeginSegment(ctx, 7, "The Ascent", "THE ASCENT", new Vector3(0f, 0f, 1558f), new Vector3(0f, 0.1f, 1560f), 14f, -60f);
            Anchor(ctx, ascent, "Anchor - Ascent 1", new Vector3(0f, 20f, 1628f), TetherAnchor.AnchorKind.Swing, 1f);
            Anchor(ctx, ascent, "Anchor - Ascent 2", new Vector3(0f, 24f, 1702f), TetherAnchor.AnchorKind.Swing, 1.05f);
            Transform ascentDecor = MovementTestBuilder.Group("Architecture", ascent);
            foreach (Vector3 anchor in new[] { new Vector3(0f, 20f, 1628f), new Vector3(0f, 24f, 1702f) })
            {
                Deco(ctx, ascentDecor, "Anchor Chain", anchor + new Vector3(-0.25f, 1.6f, -0.25f), anchor + new Vector3(0.25f, 52f, 0.25f), ctx.Art.Graphite);
                FloatingBlock(ctx, anchor + new Vector3(0f, 58f, 0f), new Vector3(18f, 8f, 14f), 0f);
            }
        }

        static void MovingDeco(Transform parent, string name, Vector3 min, Vector3 max, Material material)
        {
            Vector3 centre = (min + max) * 0.5f;
            GameObject go = MovementTestBuilder.CreateMeshObject(name, parent, centre, Quaternion.identity, GrayboxMeshes.Box(max - min, centre), material);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ Beat 7: The Gauntlet

        static void BuildGauntlet(LevelContext ctx)
        {
            Transform seg = BeginSegment(ctx, 8, "The Gauntlet", "THE GAUNTLET", new Vector3(0f, -2f, 1734f), new Vector3(0f, -1.9f, 1736f), 24f, -60f);
            Transform a = MovementTestBuilder.Group("Architecture", seg);
            Transform enemies = MovementTestBuilder.Group("Enemies", seg);

            Island(ctx, a, "Gauntlet Landing", -12f, 12f, 1725f, 1797f, -2f);

            // 7a: a square beam turning slowly around its length. One face is always roughly up, but it tilts as you run: enter
            // while the face is level or rising (or boost across), or slide off as it passes 50°.
            LoopingMotion beam = Loop(seg, "Turning Beam", new Vector3(0f, -5.5f, 1828f));
            LoopBody(ctx, beam, new Vector3(-3.5f, -9f, 1797.5f), new Vector3(3.5f, -2f, 1859f), ctx.Art.StoneLight);
            var beamSeams = new List<Renderer>();
            foreach (Vector2 corner in new[] { new Vector2(-3.5f, -9f), new Vector2(3.5f, -9f), new Vector2(-3.5f, -2f), new Vector2(3.5f, -2f) })
                beamSeams.Add(LoopSeam(ctx, beam, new Vector3(corner.x, corner.y, 1797f), new Vector3(corner.x, corner.y, 1859f)));
            beam.ConfigureRotate(Vector3.forward, 15f, 0f);
            beam.SetLook(beamSeams.ToArray(), ctx.Art.EnergySoft, ctx.Art.Energy, ctx.Art.EnergySoft);
            Deco(ctx, a, "Beam Bearing", new Vector3(-4.5f, -12f, 1794f), new Vector3(4.5f, -9.5f, 1797f), ctx.Art.Graphite);
            Deco(ctx, a, "Beam Bearing", new Vector3(-4.5f, -12f, 1859f), new Vector3(4.5f, -9.5f, 1862f), ctx.Art.Graphite);

            // 7b: three crushers on one rhythm along a narrow walkway. Each glows orange and trembles before it slams.
            Deck(ctx, a, "Crusher Walkway", -4f, 4f, 1864f, 1932f, -2f, 3f, ctx.Art.StoneLight);
            for (int k = 0; k < 3; k++)
            {
                float zc = 1878f + 20f * k;
                LoopingMotion crusher = Loop(seg, $"Crusher {k + 1} [time it]", new Vector3(0f, 7f, zc));
                LoopBody(ctx, crusher, new Vector3(-4.3f, 4f, zc - 4f), new Vector3(4.3f, 10f, zc + 4f), ctx.Art.HazardBody);
                var seams = new List<Renderer>
                {
                    LoopSeam(ctx, crusher, new Vector3(-4.3f, 4f, zc - 4f), new Vector3(4.3f, 4f, zc - 4f), ctx.Art.HazardDormant),
                    LoopSeam(ctx, crusher, new Vector3(-4.3f, 4f, zc + 4f), new Vector3(4.3f, 4f, zc + 4f), ctx.Art.HazardDormant),
                    LoopSeam(ctx, crusher, new Vector3(-4.3f, 4f, zc - 4f), new Vector3(-4.3f, 4f, zc + 4f), ctx.Art.HazardDormant),
                    LoopSeam(ctx, crusher, new Vector3(4.3f, 4f, zc - 4f), new Vector3(4.3f, 4f, zc + 4f), ctx.Art.HazardDormant),
                };
                Vector3 up = crusher.transform.localPosition;
                crusher.ConfigurePingPong(up, Vector3.zero, up + Vector3.down * 6f, Vector3.zero, 1.25f, 0.16f, 0.35f, 0.6f,
                    new AnimationCurve(K(0f, 0f, 0f, 0f), K(1f, 1f, 2.5f, 0f)), k * 0.55f);
                crusher.SetDanger(true, 0.45f, 0.08f);
                crusher.SetLook(seams.ToArray(), ctx.Art.HazardDormant, ctx.Art.HazardWarning, ctx.Art.HazardMoving);
                for (int side = -1; side <= 1; side += 2)
                    Deco(ctx, a, "Crusher Guide", new Vector3(side * 5f - 0.5f, -14f, zc - 0.5f), new Vector3(side * 5f + 0.5f, 16f, zc + 0.5f), ctx.Art.Graphite);
                Deco(ctx, a, "Crusher Gantry", new Vector3(-5.5f, 15f, zc - 0.6f), new Vector3(5.5f, 16.2f, zc + 0.6f), ctx.Art.Graphite);
            }

            // 7c: the chain. Jump off the walkway, cut the Lantern (the kill gives your boost back), boost to the wall, wall run,
            // wall jump, launch through the target onto the causeway. A zip anchor catches you if the chain breaks early.
            Ranged(ctx, enemies, "Lantern (the chain)", new Vector3(0f, 1.5f, 1947f));
            Anchor(ctx, seg, "Zip Anchor - Safety", new Vector3(0f, 5f, 1953f), TetherAnchor.AnchorKind.Zip, 0.6f, false);
            Solid(ctx, a, "Chain Wall", new Vector3(6f, -12f, 1955f), new Vector3(7f, 8f, 1992f), ctx.Art.Stone);
            LightLine(ctx, a, new Vector3(5.95f, 7.6f, 1955f), new Vector3(6f, 7.9f, 1992f), true);
            Target(ctx, seg, "Target - Off The Wall", new Vector3(0f, 3f, 2010f), 0f, 1.2f);
        }

        static Renderer LoopSeam(LevelContext ctx, LoopingMotion loop, Vector3 a, Vector3 b, Material material = null)
        {
            Seam(ctx, loop.transform, a, b, material ?? ctx.Art.EnergySoft);
            Transform last = loop.transform.GetChild(loop.transform.childCount - 1);
            return last.GetComponent<Renderer>();
        }

        // ------------------------------------------------------------------ Beat 8: The Collapse

        static void BuildCollapse(LevelContext ctx)
        {
            ctx.BeatStart[7] = 2030f;
            Transform seg = BeginSegment(ctx, 9, "The Collapse", "THE COLLAPSE", new Vector3(0f, -2f, 2038f), new Vector3(0f, -1.9f, 2040f), 18f, -60f);
            Transform a = MovementTestBuilder.Group("Architecture", seg);

            Deck(ctx, a, "Collapse Start", -9f, 9f, 2030f, 2045f, -2f, 4f, ctx.Art.StoneLight);

            // The causeway falls away behind you in a wave at 24 m/s, faster than a plain run (22): keep boosting to stay ahead.
            RealityTransformSequence wave = Stage(ctx, seg, "Stage - The Causeway Falls", new Vector3(0f, -2f, 2058f), 8f, new Vector3(30f, 70f, 8f));
            wave.StartDelay = 1f;
            wave.Stagger = 0.5f;
            const int plateCount = 22;
            var plates = new RealityChunk[plateCount];
            for (int k = 0; k < plateCount; k++)
            {
                float z0 = 2045f + 12f * k;
                RealityChunk plate = Piece(wave.transform, $"Falling Plate {k + 1} [keep moving]", new Vector3(0f, -3.5f, z0 + 6f));
                Body(ctx, plate, new Vector3(-9f, -5f, z0), new Vector3(9f, -2f, z0 + 11.95f), ctx.Art.Stone);
                OutlineTop(ctx, plate.transform, new Vector3(-9f, -5f, z0), new Vector3(9f, -2f, z0 + 11.95f));
                Animate(ctx, plate, Vector3.zero, Vector3.zero, new Vector3(0f, -70f, 0f), new Vector3(ctx.Range(-14f, 14f), 0f, ctx.Range(-12f, 12f)),
                    0f, 1.5f, Collapse(), 0.35f, 0.06f);
                plates[k] = plate;
            }
            wave.SetChunks(plates);

            // Pillars topple across the path ahead (lethal while they fall). Each falls well before even a boosted runner gets
            // there, then lies across the causeway 2.2 m high: jump it. They ride on the plate under them, so they fall with it.
            float[] pillarZ = { 2100f, 2170f, 2240f };
            for (int i = 0; i < pillarZ.Length; i++)
            {
                float zc = pillarZ[i];
                float side = i % 2 == 0 ? 1f : -1f;
                int plateIndex = Mathf.FloorToInt((zc - 2045f) / 12f);
                RealityTransformSequence topple = Stage(ctx, seg, $"Stage - Pillar {i + 1} Topples", new Vector3(0f, -2f, zc), 80f, new Vector3(24f, 40f, 6f));
                RealityChunk pillar = Piece(plates[plateIndex].transform, $"Pillar {i + 1} Topples Across [LETHAL while falling, then jump it]",
                    new Vector3(side * 10.5f, -2f, zc));
                float inner = side * 10.5f;
                float outer = side * 12.7f;
                Body(ctx, pillar, new Vector3(Mathf.Min(inner, outer), -2f, zc - 1.1f), new Vector3(Mathf.Max(inner, outer), 24f, zc + 1.1f), ctx.Art.RealityBody);
                Seam(ctx, pillar.transform, new Vector3(Mathf.Min(inner, outer), 24f, zc), new Vector3(Mathf.Max(inner, outer), 24f, zc));
                Seam(ctx, pillar.transform, new Vector3(inner, -2f, zc), new Vector3(inner, 24f, zc));
                Animate(ctx, pillar, Vector3.zero, Vector3.zero, Vector3.zero, new Vector3(0f, 0f, side * 90f), 0.3f, 0.8f, Slam(), 0.5f, 0.1f);
                MakeHazard(ctx, pillar, true);
                topple.SetChunks(new[] { pillar });
                Solid(ctx, a, "Pillar Pedestal", new Vector3(Mathf.Min(inner, side * 13.5f), -8f, zc - 2f), new Vector3(Mathf.Max(inner, side * 13.5f), -2.05f, zc + 2f), ctx.Art.Graphite);
            }

            // The ramp at the end throws you up onto the crown.
            GameObject ramp = MovementTestBuilder.Ramp("Launch Ramp", a, new Vector3(0f, -2f, 2309f), 0f, 18f, 34f, 16f, ctx.Art.StoneLight);
            MarkStatic(ramp);
            Deco(ctx, a, "Ramp Support", new Vector3(-8f, -60f, 2312f), new Vector3(8f, -2f, 2343f), ctx.Art.StoneShadow);
            LightLine(ctx, a, new Vector3(-0.15f, 14f, 2342.6f), new Vector3(0.15f, 14.05f, 2343f), true);
            for (float z = 2050f; z < 2300f; z += 40f)
                Pier(ctx, a, 0f, z, 5f, -5.1f);
        }

        // ------------------------------------------------------------------ Beat 9: The Crown

        static void BuildCrown(LevelContext ctx)
        {
            ctx.BeatStart[8] = 2346f;
            Transform seg = BeginSegment(ctx, 10, "The Crown", "THE CROWN", new Vector3(0f, 12f, 2364f), new Vector3(0f, 12.1f, 2366f), 40f, -60f);
            Transform a = MovementTestBuilder.Group("Architecture", seg);

            // The plinth: an enormous terrace on top of the megastructure's base, the whole world far below.
            Solid(ctx, a, "Crown Terrace", new Vector3(-70f, -4f, 2346f), new Vector3(70f, 12f, 2525f), ctx.Art.StoneLight);
            Deco(ctx, a, "Crown Base", new Vector3(-90f, -260f, 2340f), new Vector3(90f, -4f, 2560f), ctx.Art.StoneShadow);
            Deco(ctx, a, "Crown Tier", new Vector3(-80f, -30f, 2348f), new Vector3(80f, -4f, 2540f), ctx.Art.Stone);
            Deco(ctx, a, "Crown Band", new Vector3(-70.3f, 6f, 2345.7f), new Vector3(70.3f, 8f, 2525.3f), ctx.Art.Graphite);
            for (int i = -3; i <= 3; i++)
                LightLine(ctx, a, new Vector3(i * 9f - 0.12f, 12f, 2372f), new Vector3(i * 9f + 0.12f, 12.03f, 2465f));
            Banner(ctx, a, new Vector3(-30f, 40f, 2440f), 5f, 22f, Vector3.back);
            Banner(ctx, a, new Vector3(30f, 40f, 2440f), 5f, 22f, Vector3.back);
            for (int side = -1; side <= 1; side += 2)
            {
                Solid(ctx, a, "Crown Pylon", new Vector3(side * 30f - 3f, 12f, 2436f), new Vector3(side * 30f + 3f, 44f, 2442f), ctx.Art.Stone);
                LightLine(ctx, a, new Vector3(side * 30f - 0.15f, 14f, 2435.9f), new Vector3(side * 30f + 0.15f, 42f, 2436f), true);
                Growth(ctx, a, new Vector3(side * 30f, 43.8f, 2435.9f), Vector3.back, 5f, 16f);
            }

            // The megastructure's heart: a black spire wrapped in turning rings, its crown closed around a blazing anomaly. As
            // you arrive the crown opens and the light pours out.
            var core = new Vector3(0f, 12f, 2640f);
            GameObject spire = MovementTestBuilder.CreateMeshObject("Spire", a, core + Vector3.up * 300f, Quaternion.Euler(0f, 45f, 0f), ctx.Art.Octahedron, ctx.Art.Graphite);
            spire.transform.localScale = new Vector3(110f, 400f, 110f);
            spire.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            GameObject anomaly = MovementTestBuilder.CreateMeshObject("Anomaly", a, core + Vector3.up * 70f, Quaternion.identity, ctx.Art.Octahedron, ctx.Art.Beam);
            anomaly.transform.localScale = new Vector3(46f, 56f, 46f);
            anomaly.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            anomaly.AddComponent<SpinVisual>().Configure(Vector3.up, 12f);
            GameObject beam = MovementTestBuilder.CreateMeshObject("Light Beam", a, Vector3.zero, Quaternion.identity,
                SliceMeshes.Loft("Light Beam", SliceMeshes.Polygon(12), new[]
                {
                    new SliceMeshes.Ring(core.y + 70f, 9f, 9f, core.x, core.z), new SliceMeshes.Ring(core.y + 1800f, 14f, 14f, core.x, core.z),
                }, 1, false, false), ctx.Art.Beam);
            beam.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            RealityTransformSequence opening = Stage(ctx, seg, "Stage - The Crown Opens", new Vector3(0f, 12f, 2380f), 18f, new Vector3(60f, 40f, 6f));
            var petals = new List<RealityChunk>();
            for (int i = 0; i < 8; i++)
            {
                float yaw = i * 45f + 22.5f;
                Vector3 outward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                RealityChunk petal = Piece(opening.transform, $"Crown Petal {i + 1}", core + outward * 36f);
                petal.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                AddLocalBox(petal.transform, "Petal", new Vector3(0f, 55f, 0f), new Vector3(40f, 110f, 7f), ctx.Art.Stone);
                AddLocalBox(petal.transform, "Petal Edge", new Vector3(0f, 110.5f, 0f), new Vector3(40.6f, 1.6f, 7.6f), ctx.Art.Graphite);
                GameObject light = AddLocalBox(petal.transform, "Petal Seam", new Vector3(0f, 55f, -3.6f), new Vector3(1.2f, 100f, 0.4f), ctx.Art.RealityDormant);
                petal.Configure(petal.transform.localPosition, new Vector3(-24f, yaw, 0f), petal.transform.localPosition, new Vector3(58f, yaw, 0f), 3.2f, HeavySafe());
                petal.Delay = 0.2f + i * 0.08f;
                petal.Anticipation = 0.4f;
                petal.Tremble = 0.4f;
                petal.SetLook(new[] { light.GetComponent<Renderer>() }, ctx.Art.RealityDormant, ctx.Art.RealityWarning, ctx.Art.Energy, ctx.Art.Energy);
                petals.Add(petal);
            }
            opening.SetChunks(petals.ToArray());

            // The rings, visible from the very start of the run: the megastructure turning on the horizon.
            float[] radii = { 330f, 240f, 160f };
            Vector3[] tilts = { new Vector3(72f, 0f, 10f), new Vector3(-58f, 30f, 0f), new Vector3(18f, -20f, 40f) };
            float[] spins = { 3.5f, -5f, 8f };
            for (int i = 0; i < radii.Length; i++)
            {
                var holder = new GameObject($"Ring {i + 1}");
                holder.transform.SetParent(a, false);
                holder.transform.SetPositionAndRotation(core + Vector3.up * 200f, Quaternion.Euler(tilts[i]));
                GameObject ring = MovementTestBuilder.CreateMeshObject("Ring", holder.transform, holder.transform.position, holder.transform.rotation,
                    ctx.Art.Torus, i == 1 ? ctx.Art.StoneLight : ctx.Art.Graphite);
                ring.transform.localScale = Vector3.one * radii[i];
                ring.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                GameObject glow = MovementTestBuilder.CreateMeshObject("Ring Light", holder.transform, holder.transform.position, holder.transform.rotation,
                    ctx.Art.ThinTorus, ctx.Art.EnergySoft);
                glow.transform.localScale = Vector3.one * (radii[i] * 1.06f);
                glow.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                holder.AddComponent<SpinVisual>().Configure(Vector3.forward, spins[i]);
            }

            // Into the light.
            Finish(ctx, new Vector3(0f, 12f, 2470f), 30f);
            Deco(ctx, a, "Finish Arch", new Vector3(-17f, 12f, 2468f), new Vector3(-14f, 36f, 2472f), ctx.Art.Stone);
            Deco(ctx, a, "Finish Arch", new Vector3(14f, 12f, 2468f), new Vector3(17f, 36f, 2472f), ctx.Art.Stone);
            Deco(ctx, a, "Finish Lintel", new Vector3(-17f, 33f, 2468f), new Vector3(17f, 37f, 2472f), ctx.Art.StoneLight);
            LightLine(ctx, a, new Vector3(-14f, 32.6f, 2467.9f), new Vector3(14f, 32.9f, 2468f), true);
        }
    }
}
