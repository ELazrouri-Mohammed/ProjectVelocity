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

        [Header("Portrait Screens")]
        [Tooltip("On a portrait screen, the horizontal field of view (degrees) at low speed. The vertical one is widened to match, " +
                 "so a tall narrow phone still sees enough to the sides. 0 = off (use the vertical values above).")]
        [Range(0f, 120f)] public float portraitHorizontalFovMin;

        [Tooltip("On a portrait screen, the horizontal field of view (degrees) at high speed.")]
        [Range(0f, 120f)] public float portraitHorizontalFovMax;

        [Tooltip("Widest the vertical field of view may get on a portrait screen (degrees).")]
        [Range(20f, 140f)] public float portraitVerticalFovLimit = 110f;

        [Header("Auto Follow (touch screens)")]
        [Tooltip("How strongly the camera swings round behind your direction of travel when you aren't turning it yourself " +
                 "(per second, at full speed). Lets one thumb steer at speed. 0 = off.")]
        [Min(0f)] public float autoFollowStrength;

        [Tooltip("Seconds after you last turned the camera before it starts following by itself.")]
        [Min(0f)] public float autoFollowDelay = 0.5f;

        [Tooltip("Speed (m/s) at which auto follow starts, and the speed at which it reaches full strength.")]
        [Min(0f)] public float autoFollowMinSpeed = 8f;
        [Min(0f)] public float autoFollowFullSpeed = 28f;

        [Tooltip("Never swing round further than this (degrees) by itself: running at the camera doesn't spin it.")]
        [Range(0f, 180f)] public float autoFollowMaxAngle = 140f;

        [Tooltip("Pitch (degrees) the camera settles to while following.")]
        public float autoFollowPitch = 10f;

        [Tooltip("Extra downward pitch (degrees) per m/s of falling speed while falling fast, so you see where you'll land.")]
        [Min(0f)] public float fallLookDown;

        [Tooltip("Most extra downward pitch from falling (degrees).")]
        [Min(0f)] public float maxFallLookDown = 25f;

        [Header("Presentation")]
        [Tooltip("Extra distance (m) while swinging on the tether, so the arc reads.")]
        [Min(0f)] public float tetherDistanceBoost;

        [Tooltip("Roll (degrees) while wall running, tilting the horizon with the run. 0 = off.")]
        [Range(0f, 15f)] public float wallRoll;

        [Tooltip("How quickly (s) impulses (landings, slams) settle.")]
        [Min(0.01f)] public float impulseRecover = 0.18f;
    }
}
