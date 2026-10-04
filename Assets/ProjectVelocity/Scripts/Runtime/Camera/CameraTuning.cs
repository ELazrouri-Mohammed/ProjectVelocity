using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Camera feel settings. Stored as an asset so tweaks made while playing are kept after leaving Play Mode.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Velocity/Camera Tuning", fileName = "CameraTuning")]
    public sealed class CameraTuning : ScriptableObject
    {
        [Header("Framing")]
        [Tooltip("Distance from the character (m).")]
        [Min(0f)] public float distance = 6f;

        [Tooltip("Height of the point the camera orbits around, above the character's feet (m).")]
        public float pivotHeight = 1.6f;

        [Tooltip("Starting downward tilt (degrees).")]
        public float startPitch = 12f;

        [Tooltip("Lowest the camera can look up (degrees, negative = looking up).")]
        [Range(-89f, 0f)] public float minPitch = -30f;

        [Tooltip("Furthest the camera can look down (degrees).")]
        [Range(0f, 89f)] public float maxPitch = 70f;

        [Header("Look")]
        [Tooltip("Mouse sensitivity: degrees of rotation per pixel of mouse movement.")]
        [Min(0f)] public float lookSensitivity = 0.12f;

        [Tooltip("Gamepad right-stick turn speed at full tilt (degrees per second).")]
        [Min(0f)] public float stickLookSpeed = 220f;

        public bool invertY;

        [Header("Follow")]
        [Tooltip("Horizontal follow lag (s). 0 = locked to the character. Tiny values let speed read without feeling sluggish.")]
        [Min(0f)] public float followLag = 0.02f;

        [Tooltip("Vertical follow lag (s). Smooths jumps, landings and ramps.")]
        [Min(0f)] public float verticalFollowLag = 0.07f;

        [Header("Speed Field of View")]
        [Tooltip("Vertical field of view at low speed (degrees).")]
        [Range(20f, 120f)] public float fovMin = 62f;

        [Tooltip("Vertical field of view at high speed (degrees).")]
        [Range(20f, 120f)] public float fovMax = 80f;

        [Tooltip("Speed (m/s) at which the field of view starts widening.")]
        [Min(0f)] public float fovMinSpeed = 8f;

        [Tooltip("Speed (m/s) at which the field of view reaches its maximum.")]
        [Min(0f)] public float fovMaxSpeed = 50f;

        [Tooltip("How quickly the field of view follows speed changes (s).")]
        [Min(0f)] public float fovSmoothTime = 0.18f;

        [Header("Collision")]
        [Tooltip("Radius of the camera's collision probe (m). 0 disables camera collision.")]
        [Min(0f)] public float collisionRadius = 0.25f;

        [Tooltip("Closest the camera may be pushed toward the character by walls (m).")]
        [Min(0f)] public float minDistance = 1f;

        [Tooltip("Layers that block the camera. The player itself sits on Ignore Raycast.")]
        public LayerMask collisionLayers = ~(1 << 2);
    }
}
