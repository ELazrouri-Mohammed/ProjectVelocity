using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// One frame of device-independent player intent.
    /// Input sources (desktop, touch, gamepad...) produce it; gameplay code only ever consumes it,
    /// so the movement code never knows which device is driving it.
    /// </summary>
    public struct PlayerIntent
    {
        /// <summary>Movement stick: x = right, y = forward (relative to the camera). Magnitude 0..1.</summary>
        public Vector2 Move;

        /// <summary>Pointer-style look delta (mouse or touch pixels this frame). Scaled by camera sensitivity.</summary>
        public Vector2 LookDelta;

        /// <summary>Stick-style look rate (-1..1). Scaled by the camera's stick look speed and frame time.</summary>
        public Vector2 LookRate;

        public bool JumpPressed;
        public bool JumpHeld;
        public bool BoostPressed;
        public bool RespawnPressed;
    }
}
