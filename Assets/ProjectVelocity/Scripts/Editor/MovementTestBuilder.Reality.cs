using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectVelocity.EditorTools
{
    /// <summary>
    /// Section J: the reality transformation run. A monumental building that rewrites itself around you, in stages, while
    /// you keep moving. See <see cref="BuildRealitySection"/>.
    /// </summary>
    public static partial class MovementTestBuilder
    {
        // J runs west from the main slab's west edge. Its route frame: s = metres along the route (x = JStartX - s),
        // l = metres to the right of its centre line when running along it (z = JCentreZ + l), y = height.
        const float JStartX = -130f;
        const float JCentreZ = -90f;
        // Heights (m): the upper deck after the climb, the path that replaces the betrayed bridge (and the floor the wall
        // turns into), and the finish.
        const float JDeckY = 23f;
        const float JPathY = 21f;
        const float JFinishY = 18f;
        // Thickness of the glowing seams on reality pieces (m).
        const float SeamSize = 0.45f;

        // Triggers face the way the route runs (west).
        static readonly Quaternion JFacing = Quaternion.Euler(0f, 270f, 0f);

        /// <summary>
        /// J: reality transformation, behind the spawn to the right (turn around and run south-west to the pair of red gate
        /// markers at x = -60, then west into the giant corridor). Dark violet-grey pieces with glowing seams are the
        /// architecture that rewrites itself: seams dim cyan while dormant, violet (and trembling) just before they move,
        /// bright cyan while moving, softer once settled. Eight automatic stages (A-G, each fired by crossing a volume on the
        /// route) set it off progressively, close to you; nothing waits for you and nothing stops you.
        ///   A  corridor: a 24 m slab erupts out of the right wall ahead: steer left. (The wall crown and a sky beam shift.)
        ///   B  the floor erupts: a 12 m block and a 2.2 m step burst up across your lane (go right, or hop the step), a block
        ///      slams down over the right lane, the floor behind you falls into the abyss, and the floor ahead folds up into
        ///      a 30° climb that throws you onto the upper deck.
        ///   C  on the climb: a beam sweeps overhead, two blades shoot up beside the deck, a slab bursts from the right wall
        ///      (go left), a door swings shut from the left (go right), a wall panel opens onto a glowing core.
        ///   C2 a hurdle slides across the deck (jump), then the deck ahead collapses: a 19 m gap (boost).
        ///   D  the bridge straight ahead drops away; a path swings up from the abyss on the left (steer left and jump).
        ///   E  a 60 m wall ahead turns over around its own middle into the floor, its rib becoming a wall beside it.
        ///   F  the floor ends, the rib wall goes on over the void: wall run, wall jump; a slab sweeps beneath you, the path
        ///      behind folds away, a giant wall rises.
        ///   G  through the traversal target onto the blue finish, while the wall turns back up and a gate slams behind you.
        /// Falling into the void respawns you; respawning (R / RESET, or the fall) puts every piece back and re-arms every stage.
        /// </summary>
        static void BuildRealitySection(Transform root, Palette p)
        {
            Transform section = Group("J - Reality Transformation", root);
            Pillar(section, new Vector3(-60f, 0f, JCentreZ + 8f), 1f, 6f, p.Marker);
            Pillar(section, new Vector3(-60f, 0f, JCentreZ - 8f), 1f, 6f, p.Marker);

            BuildRealityArchitecture(section, p);
            BuildRealityStages(section, p);

            // Every piece was built where its geometry sits in the built pose; now put each in its starting pose.
            foreach (RealityChunk piece in section.GetComponentsInChildren<RealityChunk>(true))
                piece.SnapToInitial();
        }

        /// <summary>Everything in J that never moves: the corridor, the upper deck, the finish.</summary>
        static void BuildRealityArchitecture(Transform section, Palette p)
        {
            // J1: monumental corridor, 28 m wide between 6 m thick walls that stand 48 m tall (and drop 40 m into the abyss).
            // The gaps in the walls are where pieces come out of them.
            Transform corridor = Group("J1 - Monumental Corridor", section);
            JBox("Entrance Lintel", corridor, 0f, 4f, -14f, 14f, 36f, 48f, p.Block);
            foreach (float s in new[] { 12f, 32f, 52f })
            {
                JBox("Pilaster", corridor, s - 1f, s + 1f, -14f, -12.5f, 0f, 48f, p.Block);
                JBox("Pilaster", corridor, s - 1f, s + 1f, 12.5f, 14f, 0f, 48f, p.Block);
            }

            JWall("Left Wall", corridor, -1f, 0f, 246f, -40f, 48f, p);
            JWall("Left Wall (under the beam window)", corridor, -1f, 246f, 254f, -40f, 30f, p);
            JWall("Left Wall (over the beam window)", corridor, -1f, 246f, 254f, 36f, 48f, p);
            JWall("Left Wall", corridor, -1f, 254f, 305f, -40f, 48f, p);
            JBox("Left Wall (behind the door)", corridor, 305f, 320f, -20f, -17f, -40f, 48f, p.Block);
            JBox("Left Wall (under the door)", corridor, 305f, 320f, -17f, -14f, -40f, JDeckY, p.Block);
            JBox("Left Wall (over the door)", corridor, 305f, 320f, -17f, -14f, 43f, 48f, p.Block);
            JWall("Left Wall", corridor, -1f, 320f, 340f, -40f, 48f, p);
            JWall("Left Wall (under the panel)", corridor, -1f, 340f, 360f, -40f, JDeckY, p);
            JWall("Left Wall", corridor, -1f, 360f, 400f, -40f, 48f, p);

            JWall("Right Wall", corridor, 1f, 0f, 88f, -40f, 48f, p);
            JWall("Right Wall (under the slab)", corridor, 1f, 88f, 112f, -40f, 0f, p);
            JWall("Right Wall (over the slab)", corridor, 1f, 88f, 112f, 30f, 48f, p);
            JWall("Right Wall", corridor, 1f, 112f, 246f, -40f, 48f, p);
            JWall("Right Wall (under the beam window)", corridor, 1f, 246f, 254f, -40f, 30f, p);
            JWall("Right Wall (over the beam window)", corridor, 1f, 246f, 254f, 36f, 48f, p);
            JWall("Right Wall", corridor, 1f, 254f, 280f, -40f, 48f, p);
            JWall("Right Wall (under the slab)", corridor, 1f, 280f, 294f, -40f, JDeckY, p);
            JWall("Right Wall (over the slab)", corridor, 1f, 280f, 294f, 41f, 48f, p);
            JWall("Right Wall", corridor, 1f, 294f, 334f, -40f, 48f, p);
            JWall("Right Wall (under the hurdle slot)", corridor, 1f, 334f, 337f, -40f, JDeckY, p);
            JWall("Right Wall (over the hurdle slot)", corridor, 1f, 334f, 337f, JDeckY + 1.6f, 48f, p);
            JWall("Right Wall", corridor, 1f, 337f, 400f, -40f, 48f, p);

            // Lower floor. The first 80 m (stage B collapses it), the erupting blocks and the plate that folds into the climb
            // are reality pieces; past the climb there is no floor at all, only the abyss.
            JBox("Floor", corridor, 80f, 150f, -14f, 14f, -3f, 0f, p.Floor);
            JBox("Floor (beside the erupting blocks)", corridor, 150f, 166f, 4f, 14f, -3f, 0f, p.Floor);
            JBox("Floor", corridor, 166f, 190f, -14f, 14f, -3f, 0f, p.Floor);

            // J2: the upper deck the climb throws you onto, 23 m up inside the canyon. It narrows where the blades rise
            // beside it, and has a 19 m hole where stage C2 drops its floor.
            Transform deck = Group("J2 - Upper Deck", section);
            // The lip starts as close to the climb's top edge as its swing allows (a 0.76 m slot: narrower than the player).
            JBox("Lip (top of the climb)", deck, 230.6f, 236f, -14f, 14f, JDeckY - 1f, JDeckY, p.Elevated);
            JBox("Deck", deck, 236f, 254f, -14f, 14f, JDeckY - 10f, JDeckY, p.Elevated);
            JBox("Deck (between the blades)", deck, 254f, 278f, -11f, 11f, JDeckY - 10f, JDeckY, p.Elevated);
            JBox("Deck", deck, 278f, 362f, -14f, 14f, JDeckY - 10f, JDeckY, p.Elevated);
            JBox("Deck (after the gap)", deck, 381f, 432f, -14f, 14f, JDeckY - 10f, JDeckY, p.Elevated);
            // What the opening wall panel reveals: impossible architecture glowing inside the wall.
            JBox("Impossible Core", deck, 343f, 357f, -42f, -30f, 22f, 44f, p.RealityWarning);

            // J3: escape. The traversal target past the end of the wall run, and the stable finish below it.
            Transform exit = Group("J3 - Escape", section);
            Target("Target - Escape", exit, J(598f, -20f, 30f), p);
            JBox("Finish (18 m)", exit, 605f, 680f, -34f, 16f, JFinishY - 10f, JFinishY, p.Elevated);
            JBox("End Wall", exit, 677f, 680f, -34f, 16f, JFinishY, JFinishY + 6f, p.Block);
        }

        /// <summary>
        /// The reality pieces, stage by stage. Each piece is tagged: [gameplay] changes the route, [near miss] passes close
        /// but never into the route, [background] is spectacle, [behind you] changes what you have already crossed.
        /// Timings (delay / duration / anticipation, s) are tuned so the route pieces have landed before you can reach them
        /// even at boost-spamming speed (about 38 m/s), while at a plain 22 m/s run you see them happen right ahead of you.
        /// </summary>
        static void BuildRealityStages(Transform section, Palette p)
        {
            // ---------------- A: the building is alive. 38 m before the slab, a normal-looking corridor wakes up.
            RealityTransformSequence stageA = Stage("Stage A - The Building Is Alive", section, J(88f, 0f, 0f), 38f, new Vector3(32f, 40f, 6f));

            RealityChunk wallSlab = Piece("A1 Slab Erupts From The Right Wall [gameplay: steer left]", stageA.transform, J(100f, 7f, 15f));
            Body(wallSlab, 88f, 112f, 0f, 14f, 0f, 30f, p);
            OutlineSide(wallSlab, 0f, 88f, 112f, 0f, 30f, p);
            Animate(wallSlab, JOffset(0f, 14f, 0f), Vector3.zero, Vector3.zero, Vector3.zero, 0.35f, 0.55f, Eruption(), 0.35f, 0.15f, p);

            RealityChunk crown = Piece("A2 Wall Crown Lurches Forward [background]", stageA.transform, J(145f, -17f, 55f));
            Body(crown, 120f, 170f, -22f, -12f, 48f, 62f, p);
            Seam(crown, 120f, -12f, 48f, 170f, -12f, 48f, p);
            Seam(crown, 120f, -12f, 62f, 170f, -12f, 62f, p);
            Animate(crown, Vector3.zero, Vector3.zero, JOffset(30f, 0f, 6f), Vector3.zero, 0.2f, 2f, HeavySafe(), 0.3f, 0.2f, p);

            RealityChunk skyBeam = Piece("A3 Sky Beam Turns Over The Canyon [background]", stageA.transform, J(203f, 0f, 73f));
            Body(skyBeam, 200f, 206f, -45f, 45f, 70f, 76f, p);
            Seam(skyBeam, 200f, -45f, 70f, 200f, 45f, 70f, p);
            Seam(skyBeam, 206f, -45f, 70f, 206f, 45f, 70f, p);
            Animate(skyBeam, Vector3.zero, Vector3.zero, Vector3.zero, new Vector3(0f, 90f, 0f), 0.5f, 2.4f, Heavy(), 0.3f, 0.3f, p);

            stageA.SetChunks(new[] { wallSlab, crown, skyBeam });

            // ---------------- B: the floor attacks, 32 m before the blocks. Then the floor ahead throws you upward.
            RealityTransformSequence stageB = Stage("Stage B - The Floor Attacks", section, J(150f, 0f, 0f), 32f, new Vector3(32f, 40f, 6f));

            RealityChunk bigBlock = Piece("B1 Block Erupts From The Floor (12 m) [gameplay: go right]", stageB.transform, J(158f, -8f, 0f));
            Body(bigBlock, 150f, 166f, -14f, -2f, -14f, 12f, p);
            OutlineFlat(bigBlock, 12f, 150f, 166f, -14f, -2f, p);
            Animate(bigBlock, JOffset(0f, 0f, -12f), Vector3.zero, Vector3.zero, Vector3.zero, 0.25f, 0.4f, Rise(), 0.25f, 0.06f, p);

            RealityChunk step = Piece("B2 Step Erupts From The Floor (2.2 m) [gameplay: or jump onto it]", stageB.transform, J(158f, 1f, 0f));
            Body(step, 150f, 166f, -2f, 4f, -7.8f, 2.2f, p);
            OutlineFlat(step, 2.2f, 150f, 166f, -2f, 4f, p);
            Animate(step, JOffset(0f, 0f, -2.2f), Vector3.zero, Vector3.zero, Vector3.zero, 0.3f, 0.35f, Rise(), 0.3f, 0.05f, p);

            // Stops 8 m above the free lane (3 m above the top of a jump) and 1.5 m clear of the right wall, so it never
            // reaches anyone, running, jumping or wall running.
            RealityChunk hammer = Piece("B3 Block Slams Down Over The Right Lane [near miss: overhead]", stageB.transform, J(150f, 8.75f, 15f));
            Body(hammer, 140f, 160f, 5f, 12.5f, 8f, 22f, p);
            OutlineFlat(hammer, 8f, 140f, 160f, 5f, 12.5f, p);
            Animate(hammer, JOffset(0f, 0f, 42f), Vector3.zero, Vector3.zero, Vector3.zero, 0.45f, 0.45f, Slam(), 0.3f, 0.25f, p);

            // Hinged on its near edge: 46 m of floor tips up to 30° (walkable), its top meeting the lip of the upper deck.
            RealityChunk climb = Piece("B4 Floor Folds Up Into A Climb (30°) [gameplay: run up it]", stageB.transform, J(190f, 0f, 0f));
            Body(climb, 190f, 236f, -14f, 14f, -3f, 0f, p);
            OutlineFlat(climb, 0f, 190f, 236f, -14f, 14f, p);
            Animate(climb, Vector3.zero, Vector3.zero, Vector3.zero, new Vector3(0f, 0f, -30f), 0.8f, 1f, HeavySafe(), 0.35f, 0.06f, p);

            // The first 80 m of floor, split down the middle and hinged on the walls, swings down and falls into the abyss behind
            // you. The halves are 1 m thick with a 0.1 m split, so their inner edges never brush as they start to tip.
            RealityChunk floorBehindLeft = Piece("B5 Floor Behind You Falls Away (left) [behind you]", stageB.transform, J(40f, -14f, -1f));
            Body(floorBehindLeft, 0f, 80f, -14f, -0.05f, -1f, 0f, p);
            Seam(floorBehindLeft, 0f, -0.05f, 0f, 80f, -0.05f, 0f, p);
            Seam(floorBehindLeft, 0f, -14f, 0f, 80f, -14f, 0f, p);
            Animate(floorBehindLeft, Vector3.zero, Vector3.zero, JOffset(0f, 0f, -35f), new Vector3(90f, 0f, 0f), 0.3f, 1.6f, Collapse(), 0.3f, 0.08f, p);

            RealityChunk floorBehindRight = Piece("B6 Floor Behind You Falls Away (right) [behind you]", stageB.transform, J(40f, 14f, -1f));
            Body(floorBehindRight, 0f, 80f, 0.05f, 14f, -1f, 0f, p);
            Seam(floorBehindRight, 0f, 14f, 0f, 80f, 14f, 0f, p);
            Animate(floorBehindRight, Vector3.zero, Vector3.zero, JOffset(0f, 0f, -35f), new Vector3(-90f, 0f, 0f), 0.45f, 1.6f, Collapse(), 0.3f, 0.08f, p);

            stageB.SetChunks(new[] { bigBlock, step, hammer, climb, floorBehindLeft, floorBehindRight });

            // ---------------- C: on the climb. The upper deck rebuilds itself around you.
            RealityTransformSequence stageC = Stage("Stage C - The Building Rebuilds Around You", section, J(236f, 0f, JDeckY), 31f, new Vector3(32f, 50f, 6f));

            // 8 m above the deck (3 m above the top of a jump), through windows in both walls.
            RealityChunk beam = Piece("C1 Beam Sweeps Across Overhead [near miss: overhead]", stageC.transform, J(250f, 0f, 33f));
            Body(beam, 248f, 252f, -20f, 20f, 31f, 35f, p);
            Seam(beam, 248f, -20f, 31f, 248f, 20f, 31f, p);
            Seam(beam, 252f, -20f, 31f, 252f, 20f, 31f, p);
            Animate(beam, JOffset(0f, -60f, 0f), Vector3.zero, JOffset(0f, 60f, 0f), Vector3.zero, 0.6f, 2f, AnimationCurve.Linear(0f, 0f, 1f, 1f), 0.3f, 0f, p);

            // In 3 m slots beside the deck: 1 m clear of its edges, shooting 20 m above it. Walls to run on, too.
            RealityChunk bladeLeft = Piece("C2 Blade Shoots Up Beside The Deck (left) [near miss: beside]", stageC.transform, J(266f, -12.5f, JDeckY));
            Body(bladeLeft, 254f, 278f, -13f, -12f, 3f, 43f, p);
            Seam(bladeLeft, 254f, -12f, 43f, 278f, -12f, 43f, p);
            Seam(bladeLeft, 254f, -13f, 43f, 278f, -13f, 43f, p);
            Seam(bladeLeft, 254f, -12f, JDeckY, 254f, -12f, 43f, p);
            Animate(bladeLeft, JOffset(0f, 0f, -50f), Vector3.zero, Vector3.zero, Vector3.zero, 1.3f, 0.4f, Rise(), 0f, 0f, p);

            RealityChunk bladeRight = Piece("C3 Blade Shoots Up Beside The Deck (right) [near miss: beside]", stageC.transform, J(266f, 12.5f, JDeckY));
            Body(bladeRight, 254f, 278f, 12f, 13f, 3f, 43f, p);
            Seam(bladeRight, 254f, 12f, 43f, 278f, 12f, 43f, p);
            Seam(bladeRight, 254f, 13f, 43f, 278f, 13f, 43f, p);
            Seam(bladeRight, 254f, 12f, JDeckY, 254f, 12f, 43f, p);
            Animate(bladeRight, JOffset(0f, 0f, -50f), Vector3.zero, Vector3.zero, Vector3.zero, 1.35f, 0.4f, Rise(), 0f, 0f, p);

            RealityChunk rightSlab = Piece("C4 Slab Erupts From The Right Wall [gameplay: steer left]", stageC.transform, J(287f, 7.5f, 32f));
            Body(rightSlab, 280f, 294f, 1f, 14f, JDeckY, 41f, p);
            OutlineSide(rightSlab, 1f, 280f, 294f, JDeckY, 41f, p);
            Animate(rightSlab, JOffset(0f, 13f, 0f), Vector3.zero, Vector3.zero, Vector3.zero, 1.2f, 0.5f, Eruption(), 0.4f, 0.15f, p);

            // Hinged on the left wall at its far end, so it swings away from you (anything it catches is pushed forward).
            RealityChunk door = Piece("C5 Door Swings Out Of The Left Wall [gameplay: steer right]", stageC.transform, J(320f, -14f, JDeckY));
            Body(door, 317f, 320f, -14f, 0f, JDeckY, 43f, p);
            OutlineEnd(door, 317f, -14f, 0f, JDeckY, 43f, p);
            Animate(door, Vector3.zero, new Vector3(0f, 90f, 0f), Vector3.zero, Vector3.zero, 1.6f, 0.8f, Sweep(), 0.4f, 0.1f, p);

            RealityChunk panel = Piece("C6 Wall Panel Opens Onto A Glowing Core [background]", stageC.transform, J(350f, -17f, 35.5f));
            Body(panel, 340f, 360f, -20f, -14f, JDeckY, 48f, p);
            OutlineSide(panel, -14f, 340f, 360f, JDeckY, 48f, p);
            Animate(panel, Vector3.zero, Vector3.zero, JOffset(0f, -8f, 30f), Vector3.zero, 1f, 1.6f, Retract(), 0.5f, 0.12f, p);

            stageC.SetChunks(new[] { beam, bladeLeft, bladeRight, rightSlab, door, panel });

            // ---------------- C2: jump, then boost. 32 m before the hurdle.
            RealityTransformSequence stageC2 = Stage("Stage C2 - Jump, Then Boost", section, J(334f, 0f, JDeckY), 32f, new Vector3(32f, 40f, 6f));

            // Full width, 1.6 m tall: too tall to step over, easy to jump. Stops 1 m short of the left wall, so it never pins anyone.
            RealityChunk hurdle = Piece("C7 Hurdle Slides Across The Deck [gameplay: jump]", stageC2.transform, J(335.5f, 0.5f, JDeckY + 0.8f));
            Body(hurdle, 334f, 337f, -13f, 14f, JDeckY, JDeckY + 1.6f, p);
            Seam(hurdle, 334f, -13f, JDeckY + 1.6f, 334f, 14f, JDeckY + 1.6f, p);
            Seam(hurdle, 337f, -13f, JDeckY + 1.6f, 337f, 14f, JDeckY + 1.6f, p);
            Animate(hurdle, JOffset(0f, 28f, 0f), Vector3.zero, Vector3.zero, Vector3.zero, 0.15f, 0.45f, Slide(), 0.15f, 0.1f, p);

            // 19 m of deck, split down the middle and hinged on its outer edges, swings down and falls: too far to jump, not
            // with a boost. Thin halves with a 0.1 m split, like the floor behind you in stage B.
            RealityChunk gapLeft = Piece("C8 Deck Collapses (left) [gameplay: boost across]", stageC2.transform, J(371.5f, -14f, JDeckY - 1f));
            Body(gapLeft, 362f, 381f, -14f, -0.05f, JDeckY - 1f, JDeckY, p);
            Seam(gapLeft, 362f, -0.05f, JDeckY, 381f, -0.05f, JDeckY, p);
            Seam(gapLeft, 362f, -14f, JDeckY, 381f, -14f, JDeckY, p);
            Animate(gapLeft, Vector3.zero, Vector3.zero, JOffset(0f, 0f, -30f), new Vector3(90f, 0f, 0f), 0.5f, 0.7f, Collapse(), 0.3f, 0.08f, p);

            RealityChunk gapRight = Piece("C9 Deck Collapses (right) [gameplay: boost across]", stageC2.transform, J(371.5f, 14f, JDeckY - 1f));
            Body(gapRight, 362f, 381f, 0.05f, 14f, JDeckY - 1f, JDeckY, p);
            Seam(gapRight, 362f, 14f, JDeckY, 381f, 14f, JDeckY, p);
            Animate(gapRight, Vector3.zero, Vector3.zero, JOffset(0f, 0f, -30f), new Vector3(-90f, 0f, 0f), 0.55f, 0.7f, Collapse(), 0.3f, 0.08f, p);

            stageC2.SetChunks(new[] { hurdle, gapLeft, gapRight });

            // ---------------- D: the route betrays you. 45 m before the end of the deck, past the gap.
            RealityTransformSequence stageD = Stage("Stage D - The Route Betrays You", section, J(432f, 0f, JDeckY), 45f, new Vector3(32f, 40f, 6f));

            // Hinged on its near bottom edge, so it swings down away from the deck, never back into it. It stops 1 m short of the
            // tower it seems to lead into, so its end never brushes the tower as it starts to drop.
            RealityChunk bridge = Piece("D1 Bridge Ahead Drops Away [gameplay: don't take it]", stageD.transform, J(432f, 0f, JDeckY - 3f));
            Body(bridge, 432f, 496f, -7f, 7f, JDeckY - 3f, JDeckY, p);
            Seam(bridge, 432f, -7f, JDeckY, 496f, -7f, JDeckY, p);
            Seam(bridge, 432f, 7f, JDeckY, 496f, 7f, JDeckY, p);
            Seam(bridge, 496f, -7f, JDeckY, 496f, 7f, JDeckY, p);
            Animate(bridge, Vector3.zero, Vector3.zero, JOffset(0f, 0f, -45f), new Vector3(0f, 0f, 90f), 0.25f, 0.9f, Collapse(), 0.25f, 0.15f, p);

            // 4 m left of the deck and 2 m lower, alongside its last 20 m (the window to jump across), hanging into the abyss
            // from its outer edge until it swings up (it stays below the route the whole way). Stage F folds it away again,
            // through the inner piece.
            RealityChunk path = Piece("D2 Path Swings Up From The Abyss (left) [gameplay: steer left and jump]", stageD.transform, J(451f, -34f, JPathY));
            RealityChunk pathFolds = Piece("F2 (stage F) Path Folds Back Into The Abyss [behind you]", path.transform, J(451f, -34f, JPathY));
            Body(pathFolds, 412f, 490f, -34f, -18f, JPathY - 3f, JPathY, p);
            Seam(pathFolds, 412f, -34f, JPathY, 490f, -34f, JPathY, p);
            Seam(pathFolds, 412f, -18f, JPathY, 490f, -18f, JPathY, p);
            Animate(path, Vector3.zero, new Vector3(90f, 0f, 0f), Vector3.zero, Vector3.zero, 0.1f, 0.85f, SwingUp(), 0.1f, 0f, p);
            Animate(pathFolds, Vector3.zero, Vector3.zero, JOffset(0f, 0f, -40f), new Vector3(90f, 0f, 0f), 0.2f, 1.2f, Collapse(), 0.2f, 0.1f, p);

            RealityChunk barrier = Piece("D3 Wall Shoots Up Beside The Deck (right) [near miss: beside]", stageD.transform, J(415.5f, 15.7f, 35f));
            Body(barrier, 401f, 430f, 14.2f, 17.2f, 20f, 50f, p);
            OutlineSide(barrier, 14.2f, 401f, 430f, 20f, 50f, p);
            Animate(barrier, JOffset(0f, 0f, -60f), Vector3.zero, Vector3.zero, Vector3.zero, 0.15f, 0.5f, Rise(), 0.15f, 0f, p);

            RealityChunk decoy = Piece("D4 Tower The Bridge Led To Sinks [background]", stageD.transform, J(512f, 0f, 40f));
            Body(decoy, 497f, 527f, -12f, 12f, -60f, 40f, p);
            OutlineEnd(decoy, 497f, -12f, 12f, -60f, 40f, p);
            Animate(decoy, Vector3.zero, Vector3.zero, JOffset(0f, 0f, -120f), Vector3.zero, 0.45f, 1.6f, Retract(), 0.45f, 0.25f, p);

            stageD.SetChunks(new[] { bridge, path, barrier, decoy });

            // ---------------- E: the wall becomes the floor. 70 m before the wall, as you reach the new path.
            RealityTransformSequence stageE = Stage("Stage E - The Wall Becomes The Floor", section, J(490f, -13f, JPathY), 70f, new Vector3(54f, 50f, 8f));

            // A 60 m wall standing across the path (with a 10 m rib on its left and a spire rising to 81 m) turns 90° around
            // its own middle, in mid-air: the top falls away from you, the bottom swings up out of the abyss to meet the end
            // of the path, never above it. Its face becomes the floor and the rib a wall beside it that runs 30 m past the
            // end of the floor. Stage G turns it back up, through the inner piece.
            RealityChunk monolith = Piece("E1 Wall Ahead Turns Into The Floor [gameplay: run onto it]", stageE.transform, J(520f, -26f, JPathY));
            RealityChunk monolithBack = Piece("G1 (stage G) Wall Turns Back Up Behind You [behind you]", monolith.transform, J(520f, -26f, JPathY));
            Body(monolithBack, 490f, 550f, -34f, -16f, JPathY - 4f, JPathY, p, "Face (the floor)");
            Body(monolithBack, 510f, 580f, -36f, -34f, JPathY, JPathY + 10f, p, "Rib (the wall to run on)");
            Seam(monolithBack, 490f, -16f, JPathY, 550f, -16f, JPathY, p);
            Seam(monolithBack, 510f, -34f, JPathY, 580f, -34f, JPathY, p);
            Seam(monolithBack, 510f, -34f, JPathY + 10f, 580f, -34f, JPathY + 10f, p);
            Seam(monolithBack, 510f, -36f, JPathY + 10f, 580f, -36f, JPathY + 10f, p);
            Animate(monolith, Vector3.zero, new Vector3(0f, 0f, -90f), Vector3.zero, Vector3.zero, 0.25f, 1.3f, HeavySafe(), 0.35f, 0.25f, p);
            Animate(monolithBack, Vector3.zero, Vector3.zero, Vector3.zero, new Vector3(0f, 0f, -90f), 0.3f, 1.8f, Heavy(), 0.3f, 0.2f, p);

            RealityChunk columns = Piece("E2 Columns Rise Out Of The Abyss [background]", stageE.transform, J(509f, 64f, 30f));
            foreach (float s in new[] { 470f, 505f, 540f })
            {
                Body(columns, s, s + 8f, 60f, 68f, -20f, 80f, p, "Column");
                Seam(columns, s, 60f, 0f, s, 60f, 80f, p);
            }
            Animate(columns, JOffset(0f, 0f, -120f), Vector3.zero, Vector3.zero, Vector3.zero, 0.6f, 1.8f, Rise(), 0.3f, 0f, p);

            stageE.SetChunks(new[] { monolith, columns });

            // ---------------- F: escape along the wall. 18 m before the end of the floor.
            RealityTransformSequence stageF = Stage("Stage F - Escape Along The Wall", section, J(550f, -26f, JPathY), 18f, new Vector3(40f, 50f, 6f));

            // 8 m under the wall run.
            RealityChunk sweeper = Piece("F1 Slab Sweeps Beneath The Wall Run [near miss: below]", stageF.transform, J(562.5f, 0f, 9.5f));
            Body(sweeper, 545f, 580f, -15f, 15f, 6f, 13f, p);
            Seam(sweeper, 545f, -15f, 13f, 580f, -15f, 13f, p);
            Seam(sweeper, 545f, 15f, 13f, 580f, 15f, 13f, p);
            Animate(sweeper, JOffset(0f, -110f, 0f), Vector3.zero, JOffset(0f, 35f, 0f), Vector3.zero, 0.2f, 1.8f, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f), 0f, 0f, p);

            RealityChunk giantWall = Piece("F3 Giant Wall Rises Ahead On The Right [background]", stageF.transform, J(580f, 55f, 40f));
            Body(giantWall, 520f, 640f, 52f, 58f, -20f, 100f, p);
            OutlineSide(giantWall, 52f, 520f, 640f, -20f, 100f, p);
            Animate(giantWall, JOffset(0f, 0f, -120f), Vector3.zero, Vector3.zero, Vector3.zero, 0.3f, 2.4f, HeavySafe(), 0.4f, 0.3f, p);

            stageF.SetChunks(new[] { sweeper, pathFolds, giantWall });

            // ---------------- G: the world closes behind you, as you pass the target.
            RealityTransformSequence stageG = Stage("Stage G - The World Closes Behind You", section, J(598f, -20f, 30f), 6f, new Vector3(50f, 50f, 8f));

            RealityChunk gate = Piece("G2 Gate Slams Shut Behind You [behind you]", stageG.transform, J(584f, -45f, 25f));
            Body(gate, 582f, 586f, -75f, -15f, 0f, 50f, p);
            OutlineSide(gate, -15f, 582f, 586f, 0f, 50f, p);
            Animate(gate, JOffset(0f, -60f, 0f), Vector3.zero, Vector3.zero, Vector3.zero, 0.15f, 0.6f, Eruption(), 0.15f, 0.1f, p);

            stageG.SetChunks(new[] { monolithBack, gate });
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

        /// <summary>Solid part of a reality piece (with collider), between two corners in J's route frame.</summary>
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

        /// <summary>Violent rise: shoots up, a small overshoot, settles.</summary>
        static AnimationCurve Rise()
        {
            return new AnimationCurve(K(0f, 0f, 0f, 3.2f), K(0.6f, 1.03f, 0f, 0f), K(1f, 1f, 0f, 0f));
        }

        /// <summary>Slam: falls faster and faster, stops dead, kicks back a little and settles.</summary>
        static AnimationCurve Slam()
        {
            return new AnimationCurve(K(0f, 0f, 0f, 0.3f), K(0.72f, 1f, 2.8f, 0f), K(0.84f, 0.985f, 0f, 0f), K(1f, 1f, 0f, 0f));
        }

        /// <summary>Heavy turn: slow to start, gathers speed, a weighted stop that sways just past the end.</summary>
        static AnimationCurve Heavy()
        {
            return new AnimationCurve(K(0f, 0f, 0f, 0f), K(0.62f, 0.72f, 2.3f, 2.3f), K(0.88f, 1.02f, 0f, 0f), K(1f, 1f, 0f, 0f));
        }

        /// <summary>Heavy turn without the sway, for surfaces you may already be on.</summary>
        static AnimationCurve HeavySafe()
        {
            return new AnimationCurve(K(0f, 0f, 0f, 0f), K(0.7f, 0.8f, 1.9f, 1.9f), K(1f, 1f, 0.2f, 0f));
        }

        /// <summary>Collapse: hesitates, then drops away faster and faster.</summary>
        static AnimationCurve Collapse()
        {
            return new AnimationCurve(K(0f, 0f, 0f, 0f), K(0.4f, 0.1f, 0.6f, 0.6f), K(1f, 1f, 2.6f, 0f));
        }

        /// <summary>Sweep: a big swing that runs a little past the end and comes back.</summary>
        static AnimationCurve Sweep()
        {
            return new AnimationCurve(K(0f, 0f, 0f, 0f), K(0.75f, 1.03f, 1f, 0f), K(1f, 1f, 0f, 0f));
        }

        /// <summary>Swing up: fast at first, easing into a firm stop with no bounce (you land on it next).</summary>
        static AnimationCurve SwingUp()
        {
            return new AnimationCurve(K(0f, 0f, 0f, 2f), K(1f, 1f, 0f, 0f));
        }

        /// <summary>Slide: shoots across, easing into a firm stop with no bounce.</summary>
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
