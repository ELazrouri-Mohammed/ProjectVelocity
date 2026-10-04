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

        /// <summary>Pointer-style look delta (mouse pixels this frame). Scaled by camera sensitivity.</summary>
        public Vector2 LookDelta;

        /// <summary>Stick-style look rate (-1..1). Scaled by the camera's stick look speed and frame time.</summary>
        public Vector2 LookRate;

        /// <summary>
        /// Look rotation the input source has already converted to degrees this frame (touch drag), so its sensitivity
        /// doesn't depend on screen pixels. x = turn right, y = look up.
        /// </summary>
        public Vector2 LookDegrees;

        public bool JumpPressed;
        public bool JumpHeld;
        public bool BoostPressed;

        /// <summary>Activate the selected traversal target. E on desktop, the ACTION button on touch screens.</summary>
        public bool TargetPressed;

        public bool RespawnPressed;
    }
}
