using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Capsule-against-box overlap for reality pieces: how far a piece's box has pushed into the player's capsule, and which
    /// way to move the capsule to get it out. Plain geometry, so it doesn't depend on what the physics engine supports for
    /// character controllers. Allocation-free.
    /// </summary>
    public static class RealityContact
    {
        // Rounds of alternating projection between the capsule's core segment and the box (converges fast: both are convex).
        const int Iterations = 6;

        /// <summary>The player's capsule in world space (a character controller always stands upright).</summary>
        public static void Capsule(CharacterController body, out Vector3 top, out Vector3 bottom, out float radius)
        {
            Vector3 centre = body.transform.TransformPoint(body.center);
            radius = body.radius;
            float half = Mathf.Max(body.height * 0.5f, radius) - radius;
            top = centre + Vector3.up * half;
            bottom = centre - Vector3.up * half;
        }

        /// <summary>
        /// Whether <paramref name="box"/> overlaps the capsule (core segment <paramref name="top"/>-<paramref name="bottom"/>,
        /// <paramref name="radius"/>), and if so how deep (m) and the world direction that moves the capsule out of it the
        /// shortest way. Assumes the box's object isn't scaled (reality pieces never are).
        /// </summary>
        public static bool Penetration(Vector3 top, Vector3 bottom, float radius, BoxCollider box, out Vector3 direction, out float depth)
        {
            Transform t = box.transform;
            Vector3 a = t.InverseTransformPoint(top) - box.center;
            Vector3 b = t.InverseTransformPoint(bottom) - box.center;
            Vector3 h = box.size * 0.5f;

            // Closest points between the segment and the box.
            Vector3 ab = b - a;
            float abSq = ab.sqrMagnitude;
            Vector3 p = (a + b) * 0.5f;
            Vector3 q = Clamp(p, h);
            for (int i = 0; i < Iterations; i++)
            {
                float k = abSq > 1e-8f ? Mathf.Clamp01(Vector3.Dot(q - a, ab) / abSq) : 0f;
                p = a + ab * k;
                q = Clamp(p, h);
            }

            Vector3 gap = p - q;
            float distance = gap.magnitude;
            if (distance > 1e-4f)
            {
                depth = radius - distance;
                if (depth <= 0f)
                {
                    direction = Vector3.zero;
                    depth = 0f;
                    return false;
                }
                direction = t.TransformDirection(gap / distance);
                return true;
            }

            // The core passes through the box: out through whichever face takes the least travel to clear the whole capsule.
            depth = float.MaxValue;
            Vector3 local = Vector3.zero;
            for (int axis = 0; axis < 3; axis++)
            {
                float outPositive = h[axis] - Mathf.Min(a[axis], b[axis]) + radius;
                float outNegative = Mathf.Max(a[axis], b[axis]) + h[axis] + radius;
                if (outPositive < depth)
                {
                    depth = outPositive;
                    local = Vector3.zero;
                    local[axis] = 1f;
                }
                if (outNegative < depth)
                {
                    depth = outNegative;
                    local = Vector3.zero;
                    local[axis] = -1f;
                }
            }
            direction = t.TransformDirection(local);
            return true;
        }

        static Vector3 Clamp(Vector3 p, Vector3 h)
        {
            return new Vector3(Mathf.Clamp(p.x, -h.x, h.x), Mathf.Clamp(p.y, -h.y, h.y), Mathf.Clamp(p.z, -h.z, h.z));
        }
    }
}
