using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>A piece of architecture that moves by its transform (not physics) and must deal with the player it moves into.</summary>
    public interface IKinematicMover
    {
        /// <summary>Every box that moves with it.</summary>
        BoxCollider[] Boxes { get; }

        /// <summary>A hazard: touching it while it moves kills.</summary>
        bool IsLethal { get; }

        /// <summary>Whether it is in mid-move right now (a hazard only kills while moving).</summary>
        bool IsMoving { get; }

        /// <summary>Where a point that was on it before this frame's move is now.</summary>
        Vector3 CarryPoint(Vector3 point);

        string name { get; }
    }

    /// <summary>
    /// What happens to the player when moving architecture moves this frame. Run by whatever moved it, before the player moves:
    /// 1. Standing on a piece that moved: carried along with it (so a rising, drifting or turning platform takes you with it).
    /// 2. A hazard in mid-move that has pushed into you: hit, fail.
    /// 3. Anything else: shoved out of it the shortest way (position only: no speed is added).
    /// 4. Still inside something after the shoves: pinned between moving architecture and something else. Crushed, fail.
    /// Fails go through <see cref="VelocityPlayerController.Fail"/>. Allocation-free.
    /// </summary>
    public static class KinematicResolver
    {
        // Extra room (m) left between the player and a piece that shoved them.
        const float ClearanceMargin = 0.02f;
        // How far (m) a hazard must have pushed into the player for its hit to count (grazing it doesn't).
        const float HitDepth = 0.03f;
        // How far (m) a piece may still be inside the player after the shoves before it counts as crushing them.
        const float CrushDepth = 0.15f;
        // Shove passes per frame, so a player wedged in a moving corner settles into it instead of being called crushed.
        const int ShovePasses = 3;
        // A carry longer than this (m) in one frame is a snap (a reset), not motion: ignored.
        const float MaxCarry = 4f;

        static string lastFailure;
        static float lastFailureTime = float.NegativeInfinity;

        /// <summary>Why moving architecture last failed the player, for a few seconds afterwards; otherwise null.</summary>
        public static string RecentFailure => Time.time - lastFailureTime < 3f ? lastFailure : null;

        // Survives play mode without a domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            lastFailure = null;
            lastFailureTime = float.NegativeInfinity;
        }

        /// <summary>
        /// Deals with the movers flagged in <paramref name="moved"/> (first <paramref name="count"/> entries). Returns true when the
        /// player failed (and was handed to <see cref="VelocityPlayerController.Fail"/>).
        /// </summary>
        public static bool Resolve(VelocityPlayerController player, CharacterController body, VelocityMotor motor,
            IKinematicMover[] movers, bool[] moved, int count)
        {
            if (player == null || body == null || !body.enabled || player.Frozen)
                return false;

            // 1. Carried by the surface underfoot.
            Collider ground = motor != null ? motor.GroundCollider : null;
            if (ground != null)
            {
                for (int i = 0; i < count; i++)
                {
                    if (!moved[i] || movers[i] == null || !Owns(movers[i], ground))
                        continue;
                    Vector3 feet = body.transform.position;
                    Vector3 carry = movers[i].CarryPoint(feet) - feet;
                    if (carry.sqrMagnitude < MaxCarry * MaxCarry)
                        body.Move(carry);
                    break;
                }
            }

            RealityContact.Capsule(body, out Vector3 top, out Vector3 bottom, out float radius);

            // 2. Hit by a hazard.
            for (int i = 0; i < count; i++)
            {
                IKinematicMover mover = movers[i];
                if (moved[i] && mover != null && mover.IsLethal && mover.IsMoving &&
                    DeepestOverlap(mover, top, bottom, radius, out _, out _) > HitDepth)
                {
                    Fail(player, "hit by " + mover.name);
                    return true;
                }
            }

            // 3. Shoved out of the way.
            bool settled = false;
            for (int pass = 0; pass < ShovePasses && !settled; pass++)
            {
                settled = true;
                for (int i = 0; i < count; i++)
                {
                    if (!moved[i] || movers[i] == null)
                        continue;
                    if (DeepestOverlap(movers[i], top, bottom, radius, out Vector3 direction, out float depth) > 0f)
                    {
                        body.Move(direction * (depth + ClearanceMargin));
                        RealityContact.Capsule(body, out top, out bottom, out radius);
                        settled = false;
                    }
                }
            }
            if (settled)
                return false;

            // 4. Crushed.
            for (int i = 0; i < count; i++)
            {
                if (moved[i] && movers[i] != null && DeepestOverlap(movers[i], top, bottom, radius, out _, out _) > CrushDepth)
                {
                    Fail(player, "crushed by " + movers[i].name);
                    return true;
                }
            }
            return false;
        }

        static bool Owns(IKinematicMover mover, Collider collider)
        {
            BoxCollider[] boxes = mover.Boxes;
            for (int j = 0; j < boxes.Length; j++)
            {
                if (boxes[j] == collider)
                    return true;
            }
            return false;
        }

        /// <summary>The deepest overlap (m, 0 when none) between the capsule and any of a mover's boxes, and the way out of it.</summary>
        public static float DeepestOverlap(IKinematicMover mover, Vector3 top, Vector3 bottom, float radius, out Vector3 direction, out float depth)
        {
            direction = Vector3.zero;
            depth = 0f;
            BoxCollider[] boxes = mover.Boxes;
            for (int j = 0; j < boxes.Length; j++)
            {
                BoxCollider box = boxes[j];
                if (box == null || !box.enabled || !box.gameObject.activeInHierarchy)
                    continue;
                if (RealityContact.Penetration(top, bottom, radius, box, out Vector3 way, out float amount) && amount > depth)
                {
                    depth = amount;
                    direction = way;
                }
            }
            return depth;
        }

        static void Fail(VelocityPlayerController player, string reason)
        {
            lastFailure = reason;
            lastFailureTime = Time.time;
            player.Fail(reason);
        }
    }
}
