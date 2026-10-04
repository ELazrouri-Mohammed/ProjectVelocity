using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// What the motor is asked to do this frame, already converted to world space.
    /// </summary>
    public struct MotorCommand
    {
        /// <summary>Desired horizontal movement direction in world space. Magnitude 0..1 (analog amount).</summary>
        public Vector3 MoveDirection;

        /// <summary>Horizontal direction to boost toward when there is no movement input (usually camera forward).</summary>
        public Vector3 FallbackDirection;

        public bool JumpPressed;
        public bool JumpHeld;
        public bool BoostPressed;

        /// <summary>Traversal target to launch through this frame, or null. Chosen by <see cref="TraversalTargeting"/>.</summary>
        public TraversalTarget ActivateTarget;
    }
}
