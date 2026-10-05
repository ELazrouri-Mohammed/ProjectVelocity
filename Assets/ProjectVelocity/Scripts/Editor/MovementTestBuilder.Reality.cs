using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectVelocity.EditorTools
{
    /// <summary>
    /// Section J: the reality-breaking run. A building that keeps rewriting itself around you, getting more unstable and
    /// more dangerous as you go. See <see cref="BuildRealitySection"/>.
    /// </summary>
    public static partial class MovementTestBuilder
    {
        // J runs west from the main slab's west edge. Its route frame: s = metres along the route (x = JStartX - s),
        // l = metres to the right of its centre line when running along it (z = JCentreZ + l), y = height.
        const float JStartX = -130f;
        const float JCentreZ = -90f;
        // Thickness of the glowing seams on reality pieces (m).
        const float SeamSize = 0.45f;

        // Triggers and the restart point face the way the route runs (west).
        static readonly Quaternion JFacing = Quaternion.Euler(0f, 270f, 0f);

        /// <summary>
        /// J: reality-breaking traversal, behind the spawn to the right (turn around and run south-west to the pair of red
        /// gate markers at x = -60, then west into the giant corridor). About 30-45 s, escalating. Dark violet-grey pieces
        /// with glowing seams rebuild the route (seams turn violet and the piece trembles just before it moves); dark red
        /// pieces with red seams are dangerous (they flash orange before they move): red that crushes or hits you resets you.
        /// Eight automatic stages fire as you cross volumes on the route:
        ///   A  slabs erupt out of the right, then the left wall (slalom), while the corridor floor collapses behind you.
        ///   B  the road ahead peels up off the ground and slams against the left wall: no floor, wall run it.
        ///   C  the corridor crushes shut, segment after segment, from both walls: boost through or be crushed.
        ///   D  the bridge splits into drifting pieces (jump between them) and a red wall sweeps across it (time it).
        ///   E  the landing ahead drops away; arms swing two traversal targets in and a new route rises far to the right.
        ///   F  the room you run into rolls 90° around you: the left wall becomes the floor, its window becomes a pit
        ///      (wall run across).
        ///   G  the plaza floor opens under you: fall, as debris assembles into a landing beneath you and a red block
        ///      crashes onto it (steer around).
        ///   H  the landing falls away behind you as you jump to the finish.
        /// Failing anywhere in J (falling, being hit or crushed, or R / RESET) restarts J from its entrance, with every piece
        /// back in place and every stage re-armed.
        /// </summary>
        static void BuildRealitySection(Transform root, Palette p)
        {
            Transform section = Group("J - Reality Transformation", root);
            Pillar(section, new Vector3(-60f, 0f, JCentreZ + 8f), 1f, 6f, p.Marker);
            Pillar(section, new Vector3(-60f, 0f, JCentreZ - 8f), 1f, 6f, p.Marker);

            // Failing inside J restarts J: at the corridor entrance, facing in. Two zones, so the first one stays clear of
            // section I's ground to the north.
            var restart = new GameObject("J Restart Point");
            restart.transform.SetParent(section, false);
            restart.transform.SetPositionAndRotation(J(-5f, 0f, 0.1f), JFacing);
            Zone("Respawn Zone (corridor)", section, -8f, 160f, -150f, 28f, restart.transform);
            Zone("Respawn Zone (beyond)", section, 160f, 1000f, -150f, 150f, restart.transform);

            BuildRealityArchitecture(section, p);
            BuildRealityStages(section, p);

            // Every piece was built where its geometry sits in the built pose; now put each in its starting pose.
            foreach (RealityChunk piece in section.GetComponentsInChildren<RealityChunk>(true))
                piece.SnapToInitial();
        }

        /// <summary>Everything in J that never moves.</summary>
        static void BuildRealityArchitecture(Transform section, Palette p)
        {
            // J1: monumental corridor, 28 m wide between 6 m thick walls that stand 48 m tall (and drop 40 m into the abyss).
            // The gaps in the walls are where pieces come out of them.
            Transform corridor = Group("J1 - Corridor", section);
            JBox("Entrance Lintel", corridor, 0f, 4f, -14f, 14f, 36f, 48f, p.Block);
            foreach (float s in new[] { 12f, 32f })
            {
                JBox("Pilaster", corridor, s - 1f, s + 1f, -14f, -12.5f, 0f, 48f, p.Block);
                JBox("Pilaster", corridor, s - 1f, s + 1f, 12.5f, 14f, 0f, 48f, p.Block);
            }

            JWall("Left Wall", corridor, -1f, 0f, 104f, -40f, 48f, p);
            JWall("Left Wall (under the slab)", corridor, -1f, 104f, 126f, -40f, 0f, p);
            JWall("Left Wall (over the slab)", corridor, -1f, 104f, 126f, 30f, 48f, p);
            JWall("Left Wall", corridor, -1f, 126f, 230f, -40f, 48f, p);
            JWall("Left Wall (under the crusher)", corridor, -1f, 230f, 302f, -40f, 0f, p);
            JWall("Left Wall (over the crusher)", corridor, -1f, 230f, 302f, 16f, 48f, p);
            JWall("Left Wall", corridor, -1f, 302f, 306f, -40f, 48f, p);

            JWall("Right Wall", corridor, 1f, 0f, 70f, -40f, 48f, p);
            JWall("Right Wall (under the slab)", corridor, 1f, 70f, 92f, -40f, 0f, p);
            JWall("Right Wall (over the slab)", corridor, 1f, 70f, 92f, 30f, 48f, p);
            JWall("Right Wall", corridor, 1f, 92f, 230f, -40f, 48f, p);
            JWall("Right Wall (under the crusher)", corridor, 1f, 230f, 302f, -40f, 0f, p);
            JWall("Right Wall (over the crusher)", corridor, 1f, 230f, 302f, 16f, 48f, p);
            JWall("Right Wall", corridor, 1f, 302f, 306f, -40f, 48f, p);

            // The first 150 m of floor (stage A collapses it behind you) and the road (stage B peels it up) are reality pieces.
            JBox("Floor", corridor, 150f, 175f, -14f, 14f, -3f, 0f, p.Floor);
            JBox("Floor (crusher)", corridor, 211f, 306f, -14f, 14f, -3f, 0f, p.Floor);

            // J2: the bridge out of the corridor (its first piece; stage D splits the rest), the arms' hubs on the right.
            Transform bridge = Group("J2 - Bridge", section);
            JBox("Bridge (first piece)", bridge, 306f, 330f, -6f, 6f, -2f, 0f, p.Block);
            JBox("Arm 1 Hub Support", bridge, 443f, 447f, 44f, 48f, -40f, 5f, p.Pillar);
            JBox("Arm 2 Hub Support", bridge, 483f, 487f, 68f, 72f, -40f, 9f, p.Pillar);

            // J3: out of the rolling room onto a walled plaza whose floor opens (stage G), and the finish far below it.
            Transform plaza = Group("J3 - Plaza And Finish", section);
            JBox("Exit Of The Room", plaza, 665f, 700f, 41f, 63f, 10f, 20f, p.Elevated);
            JBox("Plaza", plaza, 700f, 715f, 30f, 74f, 10f, 20f, p.Elevated);
            JBox("Plaza Wall (left)", plaza, 700f, 770f, 26f, 30f, 10f, 60f, p.Block);
            JBox("Plaza Wall (right)", plaza, 700f, 770f, 74f, 78f, 10f, 60f, p.Block);
            JBox("Finish (-16 m)", plaza, 800f, 860f, 30f, 74f, -26f, -16f, p.Elevated);
            JBox("End Wall", plaza, 857f, 860f, 30f, 74f, -16f, -10f, p.Block);
        }

        /// <summary>
        /// The reality pieces, stage by stage. Names say what each one asks of you. Timings (delay / duration / anticipation,
        /// s) were checked offline against steady runs from 22 to 38 m/s: route pieces are in place before you can reach
        /// them, the crusher needs boosting (a plain 22 m/s run is crushed, about 26 m/s gets through), and the sweeping wall
        /// catches a steady 26-30 m/s run unless you react to its warning.
        /// </summary>
        static void BuildRealityStages(Transform section, Palette p)
        {
            // ---------------- A: the building wakes. Fires at s 40.
            RealityTransformSequence stageA = Stage("Stage A - The Building Wakes", section, J(70f, 0f, 0f), 30f, new Vector3(32f, 40f, 6f));

            RealityChunk slabRight = Piece("A1 Slab Erupts From The Right Wall [steer left]", stageA.transform, J(81f, 7f, 15f));
            Body(slabRight, 70f, 92f, 0f, 14f, 0f, 30f, p);
            OutlineSide(slabRight, 0f, 70f, 92f, 0f, 30f, p);
            Animate(slabRight, JOffset(0f, 14f, 0f), Vector3.zero, Vector3.zero, Vector3.zero, 0.25f, 0.45f, Eruption(), 0.25f, 0.15f, p);

            RealityChunk slabLeft = Piece("A2 Slab Erupts From The Left Wall [steer right]", stageA.transform, J(115f, -7f, 15f));
            Body(slabLeft, 104f, 126f, -14f, 0f, 0f, 30f, p);
            OutlineSide(slabLeft, 0f, 104f, 126f, 0f, 30f, p);
            Animate(slabLeft, JOffset(0f, -14f, 0f), Vector3.zero, Vector3.zero, Vector3.zero, 0.95f, 0.45f, Eruption(), 0.3f, 0.15f, p);

            // The corridor floor, in three 50 m plates hinged on the left wall, swings down into the abyss behind you, one
            // after another: keep moving. (At a steady 22 m/s you are 30+ m past each one when it goes.)
            var floorBehind = new RealityChunk[3];
            float[] collapseAt = { 1.9f, 4.2f, 6.6f };
            for (int i = 0; i < 3; i++)
            {
                float s0 = i * 50f;
                RealityChunk plate = Piece($"A{3 + i} Floor Collapses Behind You ({i + 1}/3) [don't stop]", stageA.transform, J(s0 + 25f, -14f, -1f));
                Body(plate, s0, s0 + 50f, -14f, 13.9f, -1f, 0f, p);
                OutlineFlat(plate, 0f, s0, s0 + 50f, -14f, 13.9f, p);
                Animate(plate, Vector3.zero, Vector3.zero, JOffset(0f, 0f, -40f), new Vector3(90f, 0f, 0f), collapseAt[i], 1.2f, Collapse(), 0.4f, 0.08f, p);
                floorBehind[i] = plate;
            }

            stageA.SetChunks(new[] { slabRight, slabLeft, floorBehind[0], floorBehind[1], floorBehind[2] });

            // ---------------- B: the road ahead peels up into a wall. Fires at s 130.
            RealityTransformSequence stageB = Stage("Stage B - The Road Becomes A Wall", section, J(175f, 0f, 0f), 45f, new Vector3(32f, 40f, 6f));

            // 36 m of road, hinged along its left edge, swings up 90° and slams flat against the left wall (28 m tall). The
            // floor is gone: run on it.
            RealityChunk road = Piece("B1 The Road Peels Up Into A Wall [wall run]", stageB.transform, J(193f, -14f, 0f));
            Body(road, 175f, 211f, -14f, 13.9f, -1f, 0f, p);
            OutlineFlat(road, 0f, 175f, 211f, -14f, 13.9f, p);
            Animate(road, Vector3.zero, Vector3.zero, Vector3.zero, new Vector3(-90f, 0f, 0f), 0.3f, 0.75f, HeavySafe(), 0.3f, 0.08f, p);

            stageB.SetChunks(new[] { road });

            // ---------------- C: the corridor crushes shut. Fires at s 215.
            RealityTransformSequence stageC = Stage("Stage C - The Corridor Crushes Shut", section, J(254f, 0f, 0f), 39f, new Vector3(32f, 50f, 6f));

            // Three 24 m segments, each closed by blocks sliding out of both walls to meet in the middle (16 m tall). Each
            // segment shuts just after a 26 m/s runner clears it: boost. Caught between them, you're crushed.
            var crusher = new RealityChunk[6];
            float[] closeAt = { 0.77f, 1.73f, 2.69f };
            for (int k = 0; k < 3; k++)
            {
                float s0 = 230f + 24f * k;
                RealityChunk left = Piece($"C{2 * k + 1} Crusher {k + 1} From The Left [boost through]", stageC.transform, J(s0 + 12f, -7f, 8f));
                Body(left, s0, s0 + 24f, -14f, 0f, 0f, 16f, p);
                OutlineSide(left, 0f, s0, s0 + 24f, 0f, 16f, p);
                Animate(left, JOffset(0f, -14f, 0f), Vector3.zero, Vector3.zero, Vector3.zero, closeAt[k], 0.9f, AnimationCurve.Linear(0f, 0f, 1f, 1f), 0.35f, 0.1f, p);
                MakeHazard(left, false, p);

                RealityChunk right = Piece($"C{2 * k + 2} Crusher {k + 1} From The Right [boost through]", stageC.transform, J(s0 + 12f, 7f, 8f));
                Body(right, s0, s0 + 24f, 0f, 14f, 0f, 16f, p);
                OutlineSide(right, 0f, s0, s0 + 24f, 0f, 16f, p);
                Animate(right, JOffset(0f, 14f, 0f), Vector3.zero, Vector3.zero, Vector3.zero, closeAt[k], 0.9f, AnimationCurve.Linear(0f, 0f, 1f, 1f), 0.35f, 0.1f, p);
                MakeHazard(right, false, p);

                crusher[2 * k] = left;
                crusher[2 * k + 1] = right;
            }

            stageC.SetChunks(crusher);

            // ---------------- D: the bridge splits apart under you. Fires at s 305.
            RealityTransformSequence stageD = Stage("Stage D - The Bridge Splits", section, J(330f, 0f, 0f), 25f, new Vector3(32f, 40f, 6f));

            // The rest of the bridge breaks into three drifting pieces: 7 m gaps, zig-zagging, 2.5 m down then 2 m up.
            var pieces = new RealityChunk[3];
            Vector3[] drift = { JOffset(7f, -3f, 1.5f), JOffset(14f, 3f, -1f), JOffset(21f, -2f, 1f) };
            for (int i = 0; i < 3; i++)
            {
                float s0 = 330f + 22f * i;
                RealityChunk piece = Piece($"D{i + 1} Bridge Piece {i + 2} Drifts Away [jump]", stageD.transform, J(s0 + 11f, 0f, -1f));
                Body(piece, s0, s0 + 22f, -6f, 6f, -2f, 0f, p);
                OutlineFlat(piece, 0f, s0, s0 + 22f, -6f, 6f, p);
                Animate(piece, Vector3.zero, Vector3.zero, drift[i], Vector3.zero, 0.35f, 1.3f, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f), 0.3f, 0.06f, p);
                pieces[i] = piece;
            }

            // A 14 m red wall waits just left of the third piece, flashing for 0.6 s, then sweeps across it: get past before
            // it crosses (2.4-2.8 s after the stage starts), or after. It kills on contact.
            RealityChunk sweeper = Piece("D4 Wall Sweeps Across The Bridge [LETHAL: time it]", stageD.transform, J(376f, 0f, 7f));
            Body(sweeper, 372f, 380f, -3f, 3f, 0f, 14f, p);
            OutlineEnd(sweeper, 372f, -3f, 3f, 0f, 14f, p);
            Animate(sweeper, JOffset(0f, -22f, 0f), Vector3.zero, JOffset(0f, 40f, 0f), Vector3.zero, 2.12f, 1.2f, AnimationCurve.Linear(0f, 0f, 1f, 1f), 0.6f, 0f, p);
            MakeHazard(sweeper, true, p);

            stageD.SetChunks(new[] { pieces[0], pieces[1], pieces[2], sweeper });

            // ---------------- E: the landing vanishes; the world swings in a new way. Fires at s 380.
            RealityTransformSequence stageE = Stage("Stage E - The Landing Vanishes", section, J(417f, 0f, 0f), 37f, new Vector3(40f, 50f, 6f));

            RealityChunk landing = Piece("E1 The Landing Ahead Drops Away", stageE.transform, J(460f, 0f, -4f));
            Body(landing, 440f, 480f, -10f, 10f, -10f, 2f, p);
            OutlineFlat(landing, 2f, 440f, 480f, -10f, 10f, p);
            Animate(landing, Vector3.zero, Vector3.zero, JOffset(0f, 0f, -60f), new Vector3(0f, 0f, 20f), 0.15f, 0.9f, Collapse(), 0.15f, 0.15f, p);

            // Two 37 m arms on hubs to the right swing down from vertical, each carrying a traversal target to its tip:
            // target 1 out over the gap, target 2 further right. Chain them onto the new route.
            RealityChunk armOne = Piece("E2 Arm Swings Target 1 Into Place [target]", stageE.transform, J(445f, 46f, 10f));
            Body(armOne, 442f, 448f, 43f, 49f, 7f, 13f, p, "Hub");
            Body(armOne, 443.5f, 446.5f, 10f, 46f, 8.5f, 11.5f, p, "Arm");
            Seam(armOne, 443.5f, 10f, 11.5f, 443.5f, 46f, 11.5f, p);
            Seam(armOne, 446.5f, 10f, 11.5f, 446.5f, 46f, 11.5f, p);
            Target("Target 1 (carried by the arm)", armOne.transform, J(445f, 6f, 10f), p);
            Animate(armOne, Vector3.zero, new Vector3(90f, 0f, 0f), Vector3.zero, Vector3.zero, 0.25f, 0.9f, HeavySafe(), 0.2f, 0.1f, p);

            RealityChunk armTwo = Piece("E3 Arm Swings Target 2 Into Place [target]", stageE.transform, J(485f, 70f, 14f));
            Body(armTwo, 482f, 488f, 67f, 73f, 11f, 17f, p, "Hub");
            Body(armTwo, 483.5f, 486.5f, 34f, 70f, 12.5f, 15.5f, p, "Arm");
            Seam(armTwo, 483.5f, 34f, 15.5f, 483.5f, 70f, 15.5f, p);
            Seam(armTwo, 486.5f, 34f, 15.5f, 486.5f, 70f, 15.5f, p);
            Target("Target 2 (carried by the arm)", armTwo.transform, J(485f, 30f, 14f), p);
            Animate(armTwo, Vector3.zero, new Vector3(90f, 0f, 0f), Vector3.zero, Vector3.zero, 0.45f, 0.9f, HeavySafe(), 0.2f, 0.1f, p);

            RealityChunk newRoute = Piece("E4 A New Route Rises From The Abyss (right) [land on it]", stageE.transform, J(535f, 48f, 19f));
            Body(newRoute, 490f, 580f, 30f, 66f, 18f, 20f, p);
            OutlineFlat(newRoute, 20f, 490f, 580f, 30f, 66f, p);
            Animate(newRoute, JOffset(0f, 0f, -100f), Vector3.zero, Vector3.zero, Vector3.zero, 0.6f, 0.8f, Slide(), 0.3f, 0f, p);

            stageE.SetChunks(new[] { landing, armOne, armTwo, newRoute });

            // ---------------- F: the room rolls around you. Fires at s 574, at its door.
            RealityTransformSequence stageF = Stage("Stage F - The Room Rolls", section, J(580f, 48f, 20f), 6f, new Vector3(44f, 40f, 6f));

            // An 85 m room, 22 m inside both ways, rolls 90° around its long axis while you run through it: the left wall
            // becomes the floor (you drop onto it), the floor becomes the right wall, the ceiling the left wall. The big
            // window in the left wall becomes a 30 m pit in the new floor: wall run across on either side.
            RealityChunk room = Piece("F1 The Room Rolls 90° Around You [wall run the pit]", stageF.transform, J(622.5f, 52f, 31f));
            Body(room, 580f, 665f, 39f, 65f, 18f, 20f, p, "Floor");
            Body(room, 580f, 665f, 39f, 65f, 42f, 44f, p, "Ceiling");
            Body(room, 580f, 628f, 39f, 41f, 20f, 42f, p, "Left Wall");
            Body(room, 658f, 665f, 39f, 41f, 20f, 42f, p, "Left Wall");
            Body(room, 580f, 665f, 63f, 65f, 20f, 42f, p, "Right Wall");
            Seam(room, 580f, 41f, 20f, 665f, 41f, 20f, p);
            Seam(room, 580f, 63f, 20f, 665f, 63f, 20f, p);
            Seam(room, 580f, 41f, 42f, 665f, 41f, 42f, p);
            Seam(room, 580f, 63f, 42f, 665f, 63f, 42f, p);
            Seam(room, 628f, 41f, 20f, 628f, 41f, 42f, p);
            Seam(room, 658f, 41f, 20f, 658f, 41f, 42f, p);
            Animate(room, Vector3.zero, Vector3.zero, Vector3.zero, new Vector3(-90f, 0f, 0f), 0.2f, 0.85f, HeavySafe(), 0.2f, 0.1f, p);

            stageF.SetChunks(new[] { room });

            // ---------------- G: the plaza floor opens; the world rebuilds under you as you fall. Fires at s 712.
            RealityTransformSequence stageG = Stage("Stage G - The Floor Opens", section, J(715f, 52f, 20f), 3f, new Vector3(48f, 30f, 4f));

            RealityChunk floorLeft = Piece("G1 Plaza Floor Opens (left) [fall]", stageG.transform, J(740f, 30f, 19f));
            Body(floorLeft, 715f, 765f, 30f, 51.95f, 19f, 20f, p);
            OutlineFlat(floorLeft, 20f, 715f, 765f, 30f, 51.95f, p);
            Animate(floorLeft, Vector3.zero, Vector3.zero, Vector3.zero, new Vector3(90f, 0f, 0f), 0.2f, 0.5f, Retract(), 0.2f, 0.06f, p);

            RealityChunk floorRight = Piece("G2 Plaza Floor Opens (right) [fall]", stageG.transform, J(740f, 74f, 19f));
            Body(floorRight, 715f, 765f, 52.05f, 74f, 19f, 20f, p);
            OutlineFlat(floorRight, 20f, 715f, 765f, 52.05f, 74f, p);
            Animate(floorRight, Vector3.zero, Vector3.zero, Vector3.zero, new Vector3(-90f, 0f, 0f), 0.2f, 0.5f, Retract(), 0.2f, 0.06f, p);

            // Debris pulled together out of the abyss into a 60 x 36 m landing, 36 m down, before you get there. Stage H drops
            // it again through the inner pieces.
            RealityChunk debrisLeft = Piece("G3 Debris Assembles Beneath You (left) [land]", stageG.transform, J(760f, 43f, -18f));
            RealityChunk debrisLeftFalls = Piece("H1 (stage H) Landing Falls Away Behind You (left)", debrisLeft.transform, J(760f, 43f, -18f));
            Body(debrisLeftFalls, 730f, 790f, 34f, 52f, -20f, -16f, p);
            OutlineFlat(debrisLeftFalls, -16f, 730f, 790f, 34f, 52f, p);
            Animate(debrisLeft, JOffset(0f, -30f, -40f), new Vector3(0f, 0f, 35f), Vector3.zero, Vector3.zero, 0.25f, 0.7f, SwingUp(), 0f, 0f, p);
            Animate(debrisLeftFalls, Vector3.zero, Vector3.zero, JOffset(0f, 0f, -60f), new Vector3(0f, 0f, -20f), 0.15f, 1f, Collapse(), 0.15f, 0.08f, p);

            RealityChunk debrisRight = Piece("G4 Debris Assembles Beneath You (right) [land]", stageG.transform, J(760f, 61f, -18f));
            RealityChunk debrisRightFalls = Piece("H2 (stage H) Landing Falls Away Behind You (right)", debrisRight.transform, J(760f, 61f, -18f));
            Body(debrisRightFalls, 730f, 790f, 52f, 70f, -20f, -16f, p);
            OutlineFlat(debrisRightFalls, -16f, 730f, 790f, 52f, 70f, p);
            Animate(debrisRight, JOffset(0f, 30f, -50f), new Vector3(-30f, 0f, 0f), Vector3.zero, Vector3.zero, 0.3f, 0.7f, SwingUp(), 0f, 0f, p);
            Animate(debrisRightFalls, Vector3.zero, Vector3.zero, JOffset(0f, 0f, -60f), new Vector3(0f, 0f, -20f), 0.2f, 1f, Collapse(), 0.15f, 0.08f, p);

            // Glowing red high above the far end of the landing from the moment the floor opens, then slams down onto it just
            // ahead of where you land: a 12 m block in the middle of your run. It kills while it falls.
            RealityChunk crash = Piece("G5 Debris Crashes Onto The Landing [LETHAL: steer around]", stageG.transform, J(773f, 52f, -11f));
            RealityChunk crashFalls = Piece("H3 (stage H) Crashed Debris Falls Away", crash.transform, J(773f, 52f, -11f));
            Body(crashFalls, 768f, 778f, 46f, 58f, -16f, -6f, p);
            OutlineFlat(crashFalls, -6f, 768f, 778f, 46f, 58f, p);
            Animate(crash, JOffset(0f, 0f, 86f), Vector3.zero, Vector3.zero, Vector3.zero, 0.9f, 0.7f, Slam(), 0.5f, 0.2f, p);
            Animate(crashFalls, Vector3.zero, Vector3.zero, JOffset(0f, 0f, -70f), Vector3.zero, 0.15f, 1f, Collapse(), 0.15f, 0.08f, p);
            MakeHazard(crash, true, p);

            RealityChunk fallingPast = Piece("G6 Debris Falls Past You [near miss]", stageG.transform, J(735f, 84f, 40f));
            Body(fallingPast, 730f, 740f, 80f, 88f, 35f, 45f, p);
            OutlineFlat(fallingPast, 35f, 730f, 740f, 80f, 88f, p);
            Animate(fallingPast, Vector3.zero, Vector3.zero, JOffset(0f, 0f, -110f), Vector3.zero, 0.35f, 1.4f, Collapse(), 0f, 0f, p);
            MakeHazard(fallingPast, false, p);

            stageG.SetChunks(new[] { floorLeft, floorRight, debrisLeft, debrisRight, crash, fallingPast });

            // ---------------- H: the landing falls away behind you as you jump to the finish. Fires at s 792.
            RealityTransformSequence stageH = Stage("Stage H - Escape", section, J(800f, 52f, -16f), 8f, new Vector3(48f, 40f, 6f));
            stageH.SetChunks(new[] { debrisLeftFalls, debrisRightFalls, crashFalls });
        }

        // ------------------------------------------------------------------ J helpers

        /// <summary>World position of a point in J's route frame.</summary>
        static Vector3 J(float s, float l, float y)
        {
            return new Vector3(JStartX - s, y, JCentreZ + l);
        }

        /// <summary>World displacement for a move in J's route frame.</summary>
        static Vector3 JOffset(float s, float l, float y)
        {
            return new Vector3(-s, y, l);
        }

        /// <summary>Box (with collider) between two corners in J's route frame.</summary>
        static GameObject JBox(string name, Transform parent, float s0, float s1, float l0, float l1, float y0, float y1, Material material)
        {
            return Box(name, parent, JStartX - s1, JStartX - s0, y0, y1, JCentreZ + l0, JCentreZ + l1, material);
        }

        /// <summary>A stretch of one corridor wall (<paramref name="side"/> -1 = left, 1 = right): 6 m thick outside the 28 m corridor.</summary>
        static void JWall(string name, Transform parent, float side, float s0, float s1, float y0, float y1, Palette p)
        {
            if (side < 0f)
                JBox(name, parent, s0, s1, -20f, -14f, y0, y1, p.Block);
            else
                JBox(name, parent, s0, s1, 14f, 20f, y0, y1, p.Block);
        }

        /// <summary>A respawn zone over a stretch of J (s, l ranges; 350 m tall), restarting at <paramref name="restart"/>.</summary>
        static void Zone(string name, Transform parent, float s0, float s1, float l0, float l1, Transform restart)
        {
            const float yMin = -100f;
            const float yMax = 250f;
            Vector3 a = J(s0, l0, yMin);
            Vector3 b = J(s1, l1, yMax);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = (a + b) * 0.5f;
            var zone = go.AddComponent<RespawnZone>();
            zone.Size = new Vector3(Mathf.Abs(b.x - a.x), yMax - yMin, Mathf.Abs(b.z - a.z));
            zone.RestartPoint = restart;
        }

        /// <summary>
        /// One stage: a sequence and the trigger that starts it. The trigger sits at <paramref name="timedAt"/>, the point the
        /// stage is timed against, and its volume <paramref name="lead"/> metres back along the route
        /// (<paramref name="volume"/> = width across the route, height, depth along it).
        /// </summary>
        static RealityTransformSequence Stage(string name, Transform parent, Vector3 timedAt, float lead, Vector3 volume)
        {
            Transform group = Group(name, parent);
            var sequence = group.gameObject.AddComponent<RealityTransformSequence>();

            var triggerObject = new GameObject($"Trigger (fires {lead:0} m before this point)");
            triggerObject.transform.SetParent(group, false);
            triggerObject.transform.SetPositionAndRotation(timedAt, JFacing);
            var trigger = triggerObject.AddComponent<RealityTrigger>();
            trigger.Sequence = sequence;
            trigger.LeadDistance = lead;
            trigger.VolumeSize = volume;
            return sequence;
        }

        /// <summary>An empty reality piece: its object is the pivot. Its parts are added where they sit in the built pose.</summary>
        static RealityChunk Piece(string name, Transform parent, Vector3 pivot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pivot;
            return go.AddComponent<RealityChunk>();
        }

        /// <summary>Solid part of a reality piece (with box collider), between two corners in J's route frame.</summary>
        static void Body(RealityChunk piece, float s0, float s1, float l0, float l1, float y0, float y1, Palette p, string name = "Body")
        {
            JBox(name, piece.transform, s0, s1, l0, l1, y0, y1, p.RealityBody);
        }

        /// <summary>
        /// Glowing seam (no collider, no shadow) on a reality piece, between two points in J's route frame that differ along
        /// one axis. The piece is still unrotated here, so placing it in world space is all that's needed.
        /// </summary>
        static void Seam(RealityChunk piece, float s0, float l0, float y0, float s1, float l1, float y1, Palette p)
        {
            Vector3 a = J(s0, l0, y0);
            Vector3 b = J(s1, l1, y1);
            var size = new Vector3(Mathf.Abs(b.x - a.x) + SeamSize, Mathf.Abs(b.y - a.y) + SeamSize, Mathf.Abs(b.z - a.z) + SeamSize);
            GameObject seam = Primitive(PrimitiveType.Cube, "Seam", piece.transform, Vector3.zero, size, p.RealityDormant);
            seam.transform.position = (a + b) * 0.5f;
            seam.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Seams around a horizontal face at height <paramref name="y"/>.</summary>
        static void OutlineFlat(RealityChunk piece, float y, float s0, float s1, float l0, float l1, Palette p)
        {
            Seam(piece, s0, l0, y, s1, l0, y, p);
            Seam(piece, s0, l1, y, s1, l1, y, p);
            Seam(piece, s0, l0, y, s0, l1, y, p);
            Seam(piece, s1, l0, y, s1, l1, y, p);
        }

        /// <summary>Seams around a face along the route, at lateral position <paramref name="l"/>.</summary>
        static void OutlineSide(RealityChunk piece, float l, float s0, float s1, float y0, float y1, Palette p)
        {
            Seam(piece, s0, l, y0, s1, l, y0, p);
            Seam(piece, s0, l, y1, s1, l, y1, p);
            Seam(piece, s0, l, y0, s0, l, y1, p);
            Seam(piece, s1, l, y0, s1, l, y1, p);
        }

        /// <summary>Seams around a face across the route, at <paramref name="s"/>.</summary>
        static void OutlineEnd(RealityChunk piece, float s, float l0, float l1, float y0, float y1, Palette p)
        {
            Seam(piece, s, l0, y0, s, l1, y0, p);
            Seam(piece, s, l0, y1, s, l1, y1, p);
            Seam(piece, s, l0, y0, s, l0, y1, p);
            Seam(piece, s, l1, y0, s, l1, y1, p);
        }

        /// <summary>
        /// Sets up a piece's move, from (its built position + <paramref name="fromOffset"/>, <paramref name="fromRotation"/>)
        /// to (its built position + <paramref name="toOffset"/>, <paramref name="toRotation"/>), both in its parent's space.
        /// Its glowing seams are the parts made with the dormant material (an outer piece shares an inner piece's seams).
        /// </summary>
        static void Animate(RealityChunk piece, Vector3 fromOffset, Vector3 fromRotation, Vector3 toOffset, Vector3 toRotation,
            float delay, float duration, AnimationCurve curve, float anticipation, float tremble, Palette p)
        {
            Vector3 built = piece.transform.localPosition;
            piece.Configure(built + fromOffset, fromRotation, built + toOffset, toRotation, duration, curve);
            piece.Delay = delay;
            piece.Anticipation = anticipation;
            piece.Tremble = tremble;
            Renderer[] seams = piece.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterial == p.RealityDormant).ToArray();
            piece.SetLook(seams, p.RealityDormant, p.RealityWarning, p.RealityShifting, p.RealitySettled);
        }

        /// <summary>
        /// Gives a piece (and any piece inside it) the red danger look: dark red body, red seams that flash orange before it
        /// moves. <paramref name="lethal"/>: contact while it moves resets the player. (Anything that pins you crushes you,
        /// lethal or not.) Call after <see cref="Animate"/>.
        /// </summary>
        static void MakeHazard(RealityChunk piece, bool lethal, Palette p)
        {
            piece.IsLethal = lethal;
            foreach (Renderer r in piece.GetComponentsInChildren<Renderer>(true))
            {
                if (r.sharedMaterial == p.RealityBody)
                    r.sharedMaterial = p.HazardBody;
                else if (r.sharedMaterial == p.RealityDormant)
                    r.sharedMaterial = p.HazardDormant;
            }
            foreach (RealityChunk chunk in piece.GetComponentsInChildren<RealityChunk>(true))
            {
                Renderer[] seams = chunk.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterial == p.HazardDormant).ToArray();
                chunk.SetLook(seams, p.HazardDormant, p.HazardWarning, p.HazardMoving, p.HazardDormant);
            }
        }

        // ------------------------------------------------------------------ Motion personalities (pose 0 → 1 over time 0 → 1)

        static Keyframe K(float time, float value, float inTangent, float outTangent)
        {
            return new Keyframe(time, value, inTangent, outTangent);
        }

        /// <summary>Fast eruption: bursts out at full speed, brakes hard, overshoots a little and settles heavily.</summary>
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

        /// <summary>Slide: shoots into place, easing into a firm stop with no bounce.</summary>
        static AnimationCurve Slide()
        {
            return new AnimationCurve(K(0f, 0f, 0f, 2.2f), K(1f, 1f, 0f, 0f));
        }

        /// <summary>Retraction: eases off and pulls away faster and faster.</summary>
        static AnimationCurve Retract()
        {
            return new AnimationCurve(K(0f, 0f, 0f, 0f), K(1f, 1f, 2.2f, 0f));
        }
    }
}
