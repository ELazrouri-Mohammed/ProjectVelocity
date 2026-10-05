using UnityEngine;

namespace ProjectVelocity.EditorTools
{
    /// <summary>
    /// FRACTURE, beats 1-4. One continuous run north from the origin toward the megastructure on the horizon:
    ///   1 The Causeway: calm and scale. Accelerate along a bridge over the void between colossal monoliths; one gap to jump,
    ///     one to boost.
    ///   2 The Forum: first combat without stopping. Lanterns fire from above the terraces, a Hound wakes and hunts you through
    ///     the colonnade; the way out is a boosted jump.
    ///   3 The Avenue: the world breaks. Slabs erupt from the walls while the floor collapses behind you, the road ahead peels
    ///     up into a wall (wall run it), the corridor crushes shut (boost through). The stages are Section J's, which were
    ///     timed and tested in the movement test, restyled and moved here.
    ///   4 The Well: the tether reveal. A void you can't jump; one anchor hangs from an arch above it. Jump, boost, LINK, arc,
    ///     let go on the upswing. A lower anchor catches an early release.
    /// Beats 5-9 are in VerticalSliceBuilder.Level2.cs.
    /// </summary>
    public static partial class VerticalSliceBuilder
    {
        // Beat 3, the Avenue: Section J's route frame moved here. s = metres along the avenue (z = AvenueZ + s),
        // l = metres to the right of its centre line (x = l), y = height above its floor (world y = AvenueY + y).
        const float AvenueZ = 618f;
        const float AvenueY = -2f;
        // Beat 4: where the bridge ends and the Well begins.
        const float WellEdgeZ = 948f;
        const float WellEdgeY = -2f;

        static void BuildLevel(LevelContext ctx)
        {
            BuildWorld(ctx);
            BuildCauseway(ctx);
            BuildForum(ctx);
            BuildAvenue(ctx);
            BuildWell(ctx);
            BuildTerraces(ctx);
            BuildHall(ctx);
            BuildGauntlet(ctx);
            BuildCollapse(ctx);
            BuildCrown(ctx);

            // Every piece was built where its geometry sits in the built pose; now put each in its starting pose.
            foreach (RealityChunk piece in ctx.Root.GetComponentsInChildren<RealityChunk>(true))
                piece.SnapToInitial();
        }

        // ------------------------------------------------------------------ The world around the route

        static void BuildWorld(LevelContext ctx)
        {
            // The void: a dark floor far below that the fog swallows.
            Deco(ctx, ctx.DecorRoot, "Void Floor", new Vector3(-4000f, -264f, -2000f), new Vector3(4000f, -260f, 5200f), ctx.Art.Void);

            // Colossal monoliths in pairs along the route, well clear of it, some leaning a few degrees: impossible scale.
            for (float z = -60f; z < 2600f; z += 135f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * ctx.Range(105f, 160f);
                    float width = ctx.Range(26f, 44f);
                    float height = ctx.Range(190f, 330f);
                    Monolith(ctx, new Vector3(x, -260f, z + ctx.Range(-30f, 30f)), width, height, ctx.Range(-12f, 12f), ctx.Range(-4f, 4f));
                }
            }

            // A far ring of giants on the horizon, and blocks of architecture drifting in the air.
            for (int i = 0; i < 14; i++)
            {
                float angle = i / 14f * Mathf.PI * 2f;
                float radius = ctx.Range(700f, 1000f);
                var at = new Vector3(Mathf.Sin(angle) * radius, -260f, 1200f + Mathf.Cos(angle) * radius);
                Monolith(ctx, at, ctx.Range(60f, 90f), ctx.Range(380f, 560f), ctx.Range(0f, 90f), ctx.Range(-3f, 3f));
            }
            // Kept in a band of sky above everything on the route (and clear of the Well's arch), so they never cut through it.
            for (int i = 0; i < 26; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var centre = new Vector3(side * ctx.Range(34f, 72f), ctx.Range(58f, 125f), ctx.Range(80f, 2300f));
                if (centre.z > 970f && centre.z < 1025f)
                    centre.z += 70f;
                FloatingBlock(ctx, centre, new Vector3(ctx.Range(6f, 20f), ctx.Range(4f, 10f), ctx.Range(6f, 22f)), ctx.Range(-4f, 4f));
            }

            // Sky bridges: monolith-scale spans high above the route.
            foreach (float z in new[] { 420f, 1150f, 1900f })
            {
                Deco(ctx, ctx.DecorRoot, "Sky Bridge", new Vector3(-150f, 150f, z - 7f), new Vector3(150f, 162f, z + 7f), ctx.Art.StoneShadow);
                Deco(ctx, ctx.DecorRoot, "Sky Bridge Band", new Vector3(-150f, 148f, z - 7.4f), new Vector3(150f, 150f, z + 7.4f), ctx.Art.Graphite);
                Deco(ctx, ctx.DecorRoot, "Sky Bridge Light", new Vector3(-150f, 149f, z - 7.6f), new Vector3(150f, 149.4f, z - 7.4f), ctx.Art.EnergySoft);
            }
        }

        // ------------------------------------------------------------------ Beat 1: The Causeway

        static void BuildCauseway(LevelContext ctx)
        {
            ctx.BeatStart[0] = 0f;
            ctx.Spawn = new Vector3(0f, 0.1f, 0f);
            ctx.SpawnYaw = 0f;
            Transform seg = BeginSegment(ctx, 0, "The Causeway", "THE CAUSEWAY", new Vector3(0f, 0f, -6f), ctx.Spawn, 14f, -60f, false);
            Transform a = MovementTestBuilder.Group("Architecture", seg);

            // The start: a plaza in front of a great stele, the route running out over the void toward the horizon.
            Deck(ctx, a, "Start Plaza", -12f, 12f, -30f, 12f, 0f, 6f);
            Solid(ctx, a, "Stele", new Vector3(-11f, 0f, -34f), new Vector3(11f, 34f, -30f), ctx.Art.Stone);
            Deco(ctx, a, "Stele Band", new Vector3(-11.4f, 22f, -34.4f), new Vector3(11.4f, 24f, -29.6f), ctx.Art.Graphite);
            Deco(ctx, a, "Stele Slit", new Vector3(-0.4f, 2f, -29.95f), new Vector3(0.4f, 30f, -29.7f), ctx.Art.Energy);
            Growth(ctx, a, new Vector3(-6f, 21.8f, -29.9f), Vector3.forward, 6f, 14f);
            Banner(ctx, a, new Vector3(7f, 21.8f, -29.7f), 3f, 15f, Vector3.forward);

            // Three decks: a 10 m gap (jump) and a 20 m gap down 2 m (jump, then boost).
            Deck(ctx, a, "Causeway 1", -7f, 7f, 12f, 118f, 0f);
            Deck(ctx, a, "Causeway 2", -7f, 7f, 128f, 205f, 0f);
            Deck(ctx, a, "Causeway 3", -7f, 7f, 225f, 300f, -2f);
            for (float z = 16f; z < 112f; z += 9f)
                LightLine(ctx, a, new Vector3(-0.12f, 0f, z), new Vector3(0.12f, 0.03f, z + 3.5f));
            // Chevrons before the boost gap: the only "tutorial" in the level.
            for (int i = 0; i < 3; i++)
            {
                float z = 188f + i * 5f;
                DecoRotated(ctx, a, "Boost Chevron", new Vector3(-1.1f, 0.02f, z), new Vector3(0.25f, 0.04f, 3f), Quaternion.Euler(0f, 35f, 0f), ctx.Art.Energy);
                DecoRotated(ctx, a, "Boost Chevron", new Vector3(1.1f, 0.02f, z), new Vector3(0.25f, 0.04f, 3f), Quaternion.Euler(0f, -35f, 0f), ctx.Art.Energy);
            }
            foreach (float z in new[] { 40f, 90f, 160f, 250f, 292f })
                Pier(ctx, a, 0f, z, 6f, z > 220f ? -5f : -3f);

            // The gate into the forum: two pylons and a lintel, banners hanging toward you.
            Solid(ctx, a, "Gate Pylon", new Vector3(-14f, -2f, 282f), new Vector3(-9f, 42f, 292f), ctx.Art.Stone);
            Solid(ctx, a, "Gate Pylon", new Vector3(9f, -2f, 282f), new Vector3(14f, 42f, 292f), ctx.Art.Stone);
            Deco(ctx, a, "Gate Lintel", new Vector3(-16f, 34f, 281f), new Vector3(16f, 42f, 293f), ctx.Art.StoneLight);
            Deco(ctx, a, "Gate Band", new Vector3(-16.3f, 32f, 280.7f), new Vector3(16.3f, 34f, 293.3f), ctx.Art.Graphite);
            LightLine(ctx, a, new Vector3(-9.05f, 2f, 286f), new Vector3(-8.95f, 30f, 288f), true);
            LightLine(ctx, a, new Vector3(8.95f, 2f, 286f), new Vector3(9.05f, 30f, 288f), true);
            Banner(ctx, a, new Vector3(-4.5f, 31.8f, 281f), 3.2f, 15f, Vector3.back);
            Banner(ctx, a, new Vector3(4.5f, 31.8f, 281f), 3.2f, 15f, Vector3.back);
            Growth(ctx, a, new Vector3(-11.5f, 30f, 281.9f), Vector3.back, 4f, 18f);
        }

        // ------------------------------------------------------------------ Beat 2: The Forum

        static void BuildForum(LevelContext ctx)
        {
            ctx.BeatStart[1] = 300f;
            Transform seg = BeginSegment(ctx, 1, "The Forum", "THE FORUM", new Vector3(0f, -2f, 306f), new Vector3(0f, -1.9f, 308f), 14f, -60f);
            Transform a = MovementTestBuilder.Group("Architecture", seg);
            Material stone = ctx.Art.Stone;

            Deck(ctx, a, "Forum Floor", -36f, 36f, 300f, 600f, -2f, 6f, stone, false);
            for (float z = 315f; z < 600f; z += 30f)
                Deco(ctx, a, "Floor Inlay", new Vector3(-36f, -2f, z), new Vector3(36f, -1.98f, z + 0.6f), ctx.Art.GraphiteTrim);
            LightLine(ctx, a, new Vector3(-0.15f, -2f, 310f), new Vector3(0.15f, -1.97f, 596f));

            // Terraces: the left one 4 m up, the right one 7 m up under a tall wall (a wall-run line above the fight).
            Solid(ctx, a, "Left Terrace", new Vector3(-36f, -2f, 340f), new Vector3(-22f, 2f, 560f), ctx.Art.StoneLight);
            MarkStatic(MovementTestBuilder.Ramp("Left Terrace Ramp", a, new Vector3(-29f, -2f, 328f), 0f, 14f, 12f, 4f, ctx.Art.StoneLight));
            Solid(ctx, a, "Right Terrace", new Vector3(22f, -2f, 360f), new Vector3(36f, 5f, 540f), ctx.Art.StoneLight);
            MarkStatic(MovementTestBuilder.Ramp("Right Terrace Ramp", a, new Vector3(29f, -2f, 342f), 0f, 14f, 18f, 7f, ctx.Art.StoneLight));
            Deco(ctx, a, "Terrace Trim", new Vector3(-22.2f, 1.6f, 340f), new Vector3(-21.9f, 2.15f, 560f), ctx.Art.GraphiteTrim);
            Deco(ctx, a, "Terrace Trim", new Vector3(21.9f, 4.6f, 360f), new Vector3(22.2f, 5.15f, 540f), ctx.Art.GraphiteTrim);

            // The colonnade: staggered pillars to weave through (and to break a Lantern's line of fire).
            for (int k = 0; k < 14; k++)
            {
                float z = 336f + 18f * k;
                float[] xs = k % 2 == 0 ? new[] { -14f, 0f, 14f } : new[] { -6f, 6f };
                foreach (float x in xs)
                {
                    Solid(ctx, a, "Pillar", new Vector3(x - 1.3f, -2f, z - 1.3f), new Vector3(x + 1.3f, 13f, z + 1.3f), stone);
                    Deco(ctx, a, "Capital", new Vector3(x - 1.75f, 13f, z - 1.75f), new Vector3(x + 1.75f, 14.2f, z + 1.75f), ctx.Art.Graphite, true);
                    Deco(ctx, a, "Base Band", new Vector3(x - 1.45f, -2f, z - 1.45f), new Vector3(x + 1.45f, -1.2f, z + 1.45f), ctx.Art.GraphiteTrim);
                }
            }

            // Outer walls, the far wall with its one opening, the near walls beside the gate.
            Solid(ctx, a, "Left Wall", new Vector3(-40f, -2f, 300f), new Vector3(-36f, 20f, 600f), stone);
            Solid(ctx, a, "Right Wall", new Vector3(36f, -2f, 300f), new Vector3(40f, 26f, 600f), stone);
            Solid(ctx, a, "Far Wall", new Vector3(-40f, -2f, 597f), new Vector3(-16f, 22f, 600f), stone);
            Solid(ctx, a, "Far Wall", new Vector3(16f, -2f, 597f), new Vector3(40f, 22f, 600f), stone);
            Solid(ctx, a, "Near Wall", new Vector3(-40f, -2f, 300f), new Vector3(-14f, 20f, 303f), stone);
            Solid(ctx, a, "Near Wall", new Vector3(14f, -2f, 300f), new Vector3(40f, 20f, 303f), stone);
            Deco(ctx, a, "Wall Band", new Vector3(-36.2f, 14f, 300f), new Vector3(-35.9f, 15.5f, 597f), ctx.Art.Graphite);
            Deco(ctx, a, "Wall Band", new Vector3(35.9f, 18f, 300f), new Vector3(36.2f, 19.5f, 597f), ctx.Art.Graphite);
            LightLine(ctx, a, new Vector3(35.85f, 8f, 360f), new Vector3(35.95f, 8.3f, 540f));
            Banner(ctx, a, new Vector3(-35.8f, 13.8f, 380f), 4f, 12f, Vector3.right);
            Banner(ctx, a, new Vector3(-35.8f, 13.8f, 500f), 4f, 12f, Vector3.right);
            Banner(ctx, a, new Vector3(35.8f, 17.8f, 570f), 4f, 12f, Vector3.left);
            Growth(ctx, a, new Vector3(-35.9f, 19.8f, 440f), Vector3.right, 10f, 16f);
            Growth(ctx, a, new Vector3(35.9f, 25.8f, 420f), Vector3.left, 8f, 14f);
            Growth(ctx, a, new Vector3(-28f, 21.8f, 596.9f), Vector3.back, 8f, 12f);

            // Enemies: two Lanterns over the terraces, one by the exit, and a Hound perched on a pillar mid-forum.
            Transform enemies = MovementTestBuilder.Group("Enemies", seg);
            Ranged(ctx, enemies, "Lantern (left terrace)", new Vector3(-28f, 9f, 400f));
            Ranged(ctx, enemies, "Lantern (right terrace)", new Vector3(28f, 12.5f, 470f));
            Ranged(ctx, enemies, "Lantern (exit)", new Vector3(-8f, 9f, 578f));
            Hound(ctx, enemies, "Hound (colonnade)", new Vector3(6f, 15.6f, 462f), 42f);
        }

        // ------------------------------------------------------------------ Beat 3: The Avenue

        static Vector3 Av(float s, float l, float y)
        {
            return new Vector3(l, AvenueY + y, AvenueZ + s);
        }

        /// <summary>A world displacement for a move in the avenue frame.</summary>
        static Vector3 AvOffset(float s, float l, float y)
        {
            return new Vector3(l, y, s);
        }

        static GameObject AvBox(LevelContext ctx, Transform parent, string name, float s0, float s1, float l0, float l1, float y0, float y1, Material material)
        {
            return Solid(ctx, parent, name, Av(s0, l0, y0), Av(s1, l1, y1), material);
        }

        static void AvWall(LevelContext ctx, Transform parent, string name, float side, float s0, float s1, float y0, float y1)
        {
            if (side < 0f)
                AvBox(ctx, parent, name, s0, s1, -20f, -14f, y0, y1, ctx.Art.Stone);
            else
                AvBox(ctx, parent, name, s0, s1, 14f, 20f, y0, y1, ctx.Art.Stone);
        }

        static GameObject AvBody(LevelContext ctx, RealityChunk piece, float s0, float s1, float l0, float l1, float y0, float y1, string name = "Body")
        {
            return Body(ctx, piece, Av(s0, l0, y0), Av(s1, l1, y1), null, name);
        }

        static void BuildAvenue(LevelContext ctx)
        {
            ctx.BeatStart[2] = AvenueZ;
            Transform seg = BeginSegment(ctx, 2, "The Avenue", "THE AVENUE", Av(4f, 0f, 0f), Av(7f, 0f, 0.1f), 28f, -45f);
            Transform a = MovementTestBuilder.Group("Architecture", seg);

            // A monumental corridor, 28 m wide between walls 48 m tall that plunge 40 m into the abyss. The gaps in the walls
            // are where pieces come out of them.
            AvBox(ctx, a, "Entrance Lintel", 0f, 4f, -20f, 20f, 36f, 48f, ctx.Art.StoneLight);
            Deco(ctx, a, "Lintel Band", Av(-0.3f, -20.3f, 34f), Av(4.3f, 20.3f, 36f), ctx.Art.Graphite);
            foreach (float s in new[] { 12f, 32f })
            {
                AvBox(ctx, a, "Pilaster", s - 1f, s + 1f, -14f, -12.5f, 0f, 48f, ctx.Art.StoneLight);
                AvBox(ctx, a, "Pilaster", s - 1f, s + 1f, 12.5f, 14f, 0f, 48f, ctx.Art.StoneLight);
            }
            AvWall(ctx, a, "Left Wall", -1f, 0f, 104f, -40f, 48f);
            AvWall(ctx, a, "Left Wall (under the slab)", -1f, 104f, 126f, -40f, 0f);
            AvWall(ctx, a, "Left Wall (over the slab)", -1f, 104f, 126f, 30f, 48f);
            AvWall(ctx, a, "Left Wall", -1f, 126f, 230f, -40f, 48f);
            AvWall(ctx, a, "Left Wall (under the crusher)", -1f, 230f, 302f, -40f, 0f);
            AvWall(ctx, a, "Left Wall (over the crusher)", -1f, 230f, 302f, 16f, 48f);
            AvWall(ctx, a, "Left Wall", -1f, 302f, 306f, -40f, 48f);
            AvWall(ctx, a, "Right Wall", 1f, 0f, 70f, -40f, 48f);
            AvWall(ctx, a, "Right Wall (under the slab)", 1f, 70f, 92f, -40f, 0f);
            AvWall(ctx, a, "Right Wall (over the slab)", 1f, 70f, 92f, 30f, 48f);
            AvWall(ctx, a, "Right Wall", 1f, 92f, 230f, -40f, 48f);
            AvWall(ctx, a, "Right Wall (under the crusher)", 1f, 230f, 302f, -40f, 0f);
            AvWall(ctx, a, "Right Wall (over the crusher)", 1f, 230f, 302f, 16f, 48f);
            AvWall(ctx, a, "Right Wall", 1f, 302f, 306f, -40f, 48f);

            // The first 150 m of floor (stage A collapses it behind you) and the road (stage B peels it up) are reality pieces.
            AvBox(ctx, a, "Floor", 150f, 175f, -14f, 14f, -3f, 0f, ctx.Art.Stone);
            AvBox(ctx, a, "Floor (crusher)", 211f, 306f, -14f, 14f, -3f, 0f, ctx.Art.Stone);
            AvBox(ctx, a, "Bridge To The Well", 306f, 330f, -6f, 6f, -2f, 0f, ctx.Art.StoneLight);
            Deco(ctx, a, "Bridge Trim", Av(306f, -6.25f, -1.2f), Av(330f, -5.95f, 0.12f), ctx.Art.GraphiteTrim);
            Deco(ctx, a, "Bridge Trim", Av(306f, 5.95f, -1.2f), Av(330f, 6.25f, 0.12f), ctx.Art.GraphiteTrim);
            Deco(ctx, a, "Bridge Substructure", Av(306f, -5f, -60f), Av(330f, 5f, -2f), ctx.Art.StoneShadow);

            // Dressing: light lines high on both walls, guiding the eye down the corridor; banners; growth.
            LightLine(ctx, a, Av(4f, -14.05f, 26f), Av(300f, -13.95f, 26.4f));
            LightLine(ctx, a, Av(4f, 13.95f, 26f), Av(300f, 14.05f, 26.4f));
            Banner(ctx, a, Av(-0.4f, -6f, 33.8f), 3.5f, 16f, Vector3.back);
            Banner(ctx, a, Av(-0.4f, 6f, 33.8f), 3.5f, 16f, Vector3.back);
            Growth(ctx, a, Av(40f, -13.95f, 47f), Vector3.right, 12f, 22f);
            Growth(ctx, a, Av(160f, 13.95f, 47f), Vector3.left, 10f, 20f);

            BuildAvenueStages(ctx, seg);
        }

        /// <summary>
        /// Section J's stages A-C, moved into the slice. Timings (delay / duration / anticipation, s) were checked offline in the
        /// movement test against steady runs from 22 to 38 m/s: route pieces are in place before you can reach them and the
        /// crusher needs a boost (a plain 22 m/s run is crushed, about 26 m/s gets through).
        /// </summary>
        static void BuildAvenueStages(LevelContext ctx, Transform seg)
        {
            // ---------------- A: the building wakes. Fires at s 40.
            RealityTransformSequence stageA = Stage(ctx, seg, "Stage A - The Building Wakes", Av(70f, 0f, 0f), 30f, new Vector3(32f, 40f, 6f));

            RealityChunk slabRight = Piece(stageA.transform, "A1 Slab Erupts From The Right Wall [steer left]", Av(81f, 7f, 15f));
            AvBody(ctx, slabRight, 70f, 92f, 0f, 14f, 0f, 30f);
            OutlineSideX(ctx, slabRight.transform, 0f, Av(70f, 0f, 0f), Av(92f, 0f, 30f));
            Animate(ctx, slabRight, AvOffset(0f, 14f, 0f), Vector3.zero, Vector3.zero, Vector3.zero, 0.25f, 0.45f, Eruption(), 0.25f, 0.15f);

            RealityChunk slabLeft = Piece(stageA.transform, "A2 Slab Erupts From The Left Wall [steer right]", Av(115f, -7f, 15f));
            AvBody(ctx, slabLeft, 104f, 126f, -14f, 0f, 0f, 30f);
            OutlineSideX(ctx, slabLeft.transform, 0f, Av(104f, 0f, 0f), Av(126f, 0f, 30f));
            Animate(ctx, slabLeft, AvOffset(0f, -14f, 0f), Vector3.zero, Vector3.zero, Vector3.zero, 0.95f, 0.45f, Eruption(), 0.3f, 0.15f);

            // The corridor floor, in three 50 m plates hinged on the left wall, swings down into the abyss behind you.
            var floorBehind = new RealityChunk[3];
            float[] collapseAt = { 1.9f, 4.2f, 6.6f };
            for (int i = 0; i < 3; i++)
            {
                float s0 = i * 50f;
                RealityChunk plate = Piece(stageA.transform, $"A{3 + i} Floor Collapses Behind You ({i + 1}/3) [don't stop]", Av(s0 + 25f, -14f, -1f));
                AvBody(ctx, plate, s0, s0 + 50f, -14f, 13.9f, -1f, 0f);
                OutlineTop(ctx, plate.transform, Av(s0, -14f, -1f), Av(s0 + 50f, 13.9f, 0f));
                Animate(ctx, plate, Vector3.zero, Vector3.zero, AvOffset(0f, 0f, -40f), new Vector3(0f, 0f, -90f), collapseAt[i], 1.2f, Collapse(), 0.4f, 0.08f);
                floorBehind[i] = plate;
            }
            stageA.SetChunks(new[] { slabRight, slabLeft, floorBehind[0], floorBehind[1], floorBehind[2] });

            // ---------------- B: the road ahead peels up into a wall. Fires at s 130.
            RealityTransformSequence stageB = Stage(ctx, seg, "Stage B - The Road Becomes A Wall", Av(175f, 0f, 0f), 45f, new Vector3(32f, 40f, 6f));
            RealityChunk road = Piece(stageB.transform, "B1 The Road Peels Up Into A Wall [wall run]", Av(193f, -14f, 0f));
            AvBody(ctx, road, 175f, 211f, -14f, 13.9f, -1f, 0f);
            OutlineTop(ctx, road.transform, Av(175f, -14f, -1f), Av(211f, 13.9f, 0f));
            Animate(ctx, road, Vector3.zero, Vector3.zero, Vector3.zero, new Vector3(0f, 0f, 90f), 0.3f, 0.75f, HeavySafe(), 0.3f, 0.08f);
            stageB.SetChunks(new[] { road });

            // ---------------- C: the corridor crushes shut. Fires at s 215.
            RealityTransformSequence stageC = Stage(ctx, seg, "Stage C - The Corridor Crushes Shut", Av(254f, 0f, 0f), 39f, new Vector3(32f, 50f, 6f));
            var crusher = new RealityChunk[6];
            float[] closeAt = { 0.77f, 1.73f, 2.69f };
            for (int k = 0; k < 3; k++)
            {
                float s0 = 230f + 24f * k;
                RealityChunk left = Piece(stageC.transform, $"C{2 * k + 1} Crusher {k + 1} From The Left [boost through]", Av(s0 + 12f, -7f, 8f));
                AvBody(ctx, left, s0, s0 + 24f, -14f, 0f, 0f, 16f);
                OutlineSideX(ctx, left.transform, 0f, Av(s0, 0f, 0f), Av(s0 + 24f, 0f, 16f));
                Animate(ctx, left, AvOffset(0f, -14f, 0f), Vector3.zero, Vector3.zero, Vector3.zero, closeAt[k], 0.9f, AnimationCurve.Linear(0f, 0f, 1f, 1f), 0.35f, 0.1f);
                MakeHazard(ctx, left, false);

                RealityChunk right = Piece(stageC.transform, $"C{2 * k + 2} Crusher {k + 1} From The Right [boost through]", Av(s0 + 12f, 7f, 8f));
                AvBody(ctx, right, s0, s0 + 24f, 0f, 14f, 0f, 16f);
                OutlineSideX(ctx, right.transform, 0f, Av(s0, 0f, 0f), Av(s0 + 24f, 0f, 16f));
                Animate(ctx, right, AvOffset(0f, 14f, 0f), Vector3.zero, Vector3.zero, Vector3.zero, closeAt[k], 0.9f, AnimationCurve.Linear(0f, 0f, 1f, 1f), 0.35f, 0.1f);
                MakeHazard(ctx, right, false);

                crusher[2 * k] = left;
                crusher[2 * k + 1] = right;
            }
            stageC.SetChunks(crusher);
        }

        // ------------------------------------------------------------------ Beat 4: The Well

        static void BuildWell(LevelContext ctx)
        {
            ctx.BeatStart[3] = WellEdgeZ - 24f;
            Transform seg = BeginSegment(ctx, 3, "The Well", "THE WELL", new Vector3(0f, WellEdgeY, WellEdgeZ - 20f),
                new Vector3(0f, WellEdgeY + 0.1f, WellEdgeZ - 18f), 12f, -50f);
            Transform a = MovementTestBuilder.Group("Architecture", seg);

            // The void you can't jump. One anchor hangs from a colossal arch above it: jump, boost, LINK, arc, let go on the
            // upswing (simulated: holding on lands about 85 m out, a good release about 120 m; the landing runs 80-140 m).
            Vector3 edge = new Vector3(0f, WellEdgeY, WellEdgeZ);
            Anchor(ctx, seg, "Anchor - The Well", edge + new Vector3(0f, 26f, 50f), TetherAnchor.AnchorKind.Swing, 1.15f);
            // Caught early? A lower anchor over the landing turns a short release into a second swing.
            Anchor(ctx, seg, "Anchor - Recovery", edge + new Vector3(0f, 10f, 86f), TetherAnchor.AnchorKind.Swing, 0.8f);

            Deco(ctx, a, "Arch Pylon", new Vector3(-50f, -260f, WellEdgeZ + 44f), new Vector3(-40f, 66f, WellEdgeZ + 56f), ctx.Art.Stone);
            Deco(ctx, a, "Arch Pylon", new Vector3(40f, -260f, WellEdgeZ + 44f), new Vector3(50f, 66f, WellEdgeZ + 56f), ctx.Art.Stone);
            Deco(ctx, a, "Arch Beam", new Vector3(-52f, 56f, WellEdgeZ + 45f), new Vector3(52f, 66f, WellEdgeZ + 55f), ctx.Art.StoneLight);
            Deco(ctx, a, "Arch Band", new Vector3(-52.3f, 54f, WellEdgeZ + 44.7f), new Vector3(52.3f, 56f, WellEdgeZ + 55.3f), ctx.Art.Graphite);
            Deco(ctx, a, "Anchor Chain", new Vector3(-0.3f, WellEdgeY + 27.6f, WellEdgeZ + 49.7f), new Vector3(0.3f, 56f, WellEdgeZ + 50.3f), ctx.Art.Graphite);
            LightLine(ctx, a, new Vector3(-40.05f, 0f, WellEdgeZ + 49f), new Vector3(-39.95f, 52f, WellEdgeZ + 51f), true);
            LightLine(ctx, a, new Vector3(39.95f, 0f, WellEdgeZ + 49f), new Vector3(40.05f, 52f, WellEdgeZ + 51f), true);
            Deco(ctx, a, "Recovery Mount", new Vector3(-2f, WellEdgeY + 30f, WellEdgeZ + 84f), new Vector3(2f, WellEdgeY + 33f, WellEdgeZ + 88f), ctx.Art.Graphite);
            Deco(ctx, a, "Recovery Chain", new Vector3(-0.2f, WellEdgeY + 11.6f, WellEdgeZ + 85.8f), new Vector3(0.2f, WellEdgeY + 30f, WellEdgeZ + 86.2f), ctx.Art.Graphite);

            // The canyon: walls of impossible height either side, ribbed with mechanism and split by light.
            for (int side = -1; side <= 1; side += 2)
            {
                float x0 = side < 0 ? -96f : 70f;
                float x1 = side < 0 ? -70f : 96f;
                Deco(ctx, a, "Canyon Wall", new Vector3(x0, -260f, WellEdgeZ - 10f), new Vector3(x1, 52f, WellEdgeZ + 170f), ctx.Art.Stone);
                float face = side < 0 ? -70f : 70f;
                for (float z = WellEdgeZ; z < WellEdgeZ + 170f; z += 16f)
                    Deco(ctx, a, "Canyon Rib", new Vector3(face - (side < 0 ? 0f : 1.5f), -120f, z), new Vector3(face + (side < 0 ? 1.5f : 0f), 50f, z + 2.5f), ctx.Art.Graphite);
                LightLine(ctx, a, new Vector3(face - 0.1f, -200f, WellEdgeZ + 62f), new Vector3(face + 0.1f, 50f, WellEdgeZ + 63f), true);
                Growth(ctx, a, new Vector3(face + side * -0.1f, 50f, WellEdgeZ + 100f), new Vector3(-side, 0f, 0f), 18f, 40f);
            }
            for (int i = 0; i < 9; i++)
                FloatingBlock(ctx, new Vector3(ctx.Range(-50f, 50f), ctx.Range(-140f, -40f), WellEdgeZ + ctx.Range(10f, 150f)),
                    new Vector3(ctx.Range(5f, 14f), ctx.Range(3f, 8f), ctx.Range(5f, 16f)), ctx.Range(-6f, 6f));
        }

        static void MarkStatic(GameObject go)
        {
            UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, UnityEditor.StaticEditorFlags.BatchingStatic);
        }
    }
}
