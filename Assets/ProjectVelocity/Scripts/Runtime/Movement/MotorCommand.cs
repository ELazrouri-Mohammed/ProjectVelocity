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

        /// <summary>Lunge to start this frame, or none (default). Requested by <see cref="CombatController"/>.</summary>
        public MotorLunge Lunge;

        /// <summary>Tether to attach this frame, or none (default). Requested by <see cref="TetherController"/>.</summary>
        public MotorTether Tether;

        /// <summary>Let go of the tether this frame (the LINK button was released).</summary>
        public bool ReleaseTether;
    }

    /// <summary>
    /// A tether to attach (see VelocityMotor.Tether.cs). The motor only moves; <see cref="TetherController"/> decides which
    /// anchor and when, and <see cref="TetherTuning"/> carries the feel values for the whole swing.
    /// </summary>
    public struct MotorTether
    {
        public TetherAnchor Anchor;
        public TetherTuning Settings;

        public bool IsRequested => Anchor != null && Settings != null;
    }

    /// <summary>
    /// A short dash that closes in on a point (see VelocityMotor.Lunge.cs). The motor only moves; whoever asks for it
    /// decides what the point is and when it has been reached.
    /// </summary>
    public struct MotorLunge
    {
        /// <summary>World-space point to close in on.</summary>
        public Vector3 Goal;

        /// <summary>Lunge speed (m/s). Moving faster already keeps that speed instead: a lunge never slows you down.</summary>
        public float Speed;

        /// <summary>Longest the lunge may last (s). 0 = no lunge.</summary>
        public float Duration;

        /// <summary>How fast the current direction of travel turns toward the point (degrees per second).</summary>
        public float TurnRate;

        public bool IsRequested => Duration > 0f;
    }
}
