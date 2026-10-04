using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Every number that shapes how the character moves. Stored as an asset so that tweaks made
    /// while playing are kept after leaving Play Mode.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Velocity/Movement Tuning", fileName = "MovementTuning")]
    public sealed class MovementTuning : ScriptableObject
    {
        [Header("Ground Movement")]
        [Tooltip("Top running speed in metres per second. (An elite human sprinter peaks around 12.)")]
        [Min(0f)] public float maxGroundSpeed = 22f;

        [Tooltip("How quickly you reach top speed (m/s²). 120 gets from standstill to 22 m/s in under 0.2 s.")]
        [Min(0f)] public float groundAcceleration = 120f;

        [Tooltip("How quickly you stop after releasing the movement keys (m/s²).")]
        [Min(0f)] public float groundDeceleration = 80f;

        [Tooltip("How fast your running direction swings toward the input direction (degrees per second). Speed is kept through the turn.")]
        [Min(0f)] public float turnResponsiveness = 900f;

        [Tooltip("Braking strength (m/s²) when pushing roughly opposite to your motion. Makes 180° turns snappy instead of a slow arc.")]
        [Min(0f)] public float reversalBrake = 200f;

        [Tooltip("When above top speed (e.g. after a boost), how fast the extra speed bleeds away on the ground (m/s²). Lower = momentum lasts longer.")]
        [Min(0f)] public float groundMomentumDecay = 18f;

        [Header("Jump & Gravity")]
        [Tooltip("Jump apex height in metres when Jump is held.")]
        [Min(0f)] public float jumpHeight = 3.2f;

        [Tooltip("Gravity in m/s² (Earth is 9.8). High gravity keeps jumps snappy instead of floaty.")]
        [Min(0.01f)] public float gravity = 45f;

        [Tooltip("Gravity multiplier while falling. Above 1 = faster, punchier descents.")]
        [Min(0f)] public float fallGravityMultiplier = 1.5f;

        [Tooltip("Gravity multiplier while still rising after Jump was released early. Tap = short hop, hold = full height.")]
        [Min(1f)] public float jumpReleaseGravityMultiplier = 2.5f;

        [Tooltip("Maximum falling speed (m/s).")]
        [Min(0f)] public float maxFallSpeed = 55f;

        [Tooltip("Grace time (s) after running off an edge during which Jump still works.")]
        [Min(0f)] public float coyoteTime = 0.12f;

        [Tooltip("If Jump is pressed this many seconds before landing, the jump fires on touchdown.")]
        [Min(0f)] public float jumpBufferTime = 0.15f;

        [Header("Air Control")]
        [Tooltip("How quickly you can build speed in the air (m/s²).")]
        [Min(0f)] public float airAcceleration = 60f;

        [Tooltip("How fast you can swing your direction in the air (degrees per second). Speed is kept through the turn.")]
        [Min(0f)] public float airTurnResponsiveness = 360f;

        [Tooltip("Braking strength in the air (m/s²) when pushing against your motion, to line up a landing.")]
        [Min(0f)] public float airBrake = 45f;

        [Tooltip("Maximum horizontal speed you can reach in the air by steering alone (m/s). Faster speed from boosts or running is kept and decays slowly.")]
        [Min(0f)] public float maxAirSpeed = 22f;

        [Tooltip("When above Max Air Speed, how fast the extra speed bleeds away in the air (m/s²). Lower = longer boosted jumps.")]
        [Min(0f)] public float airMomentumDecay = 5f;

        [Header("Boost")]
        [Tooltip("Peak speed of the boost burst (m/s).")]
        [Min(0f)] public float boostSpeed = 52f;

        [Tooltip("How long the burst lasts (s).")]
        [Min(0f)] public float boostDuration = 0.18f;

        [Tooltip("Time between boosts (s), counted from when the boost starts.")]
        [Min(0f)] public float boostCooldown = 0.45f;

        [Tooltip("Speed carried out of the boost (m/s). Anything above top speed bleeds off via Momentum Decay, so the boost flows into the run.")]
        [Min(0f)] public float boostExitSpeed = 32f;

        [Tooltip("How much you can steer during the boost (degrees per second).")]
        [Min(0f)] public float boostSteering = 240f;

        [Tooltip("Boosts allowed per airtime. Resets on landing. 0 = ground-only boost.")]
        [Min(0)] public int maxAirBoosts = 1;

        [Tooltip("When on, a boost started in the air is a flat dash: falling stops and gravity pauses while it lasts.")]
        public bool airBoostHoldsAltitude = true;

        [Header("Ground Detection")]
        [Tooltip("Steepest slope (degrees) you can run on. Steeper surfaces act as walls.")]
        [Range(0f, 89f)] public float maxWalkableSlope = 50f;

        [Tooltip("How far (m) the character may be pulled down to stay glued to ramps and crests at speed.")]
        [Min(0f)] public float groundSnapDistance = 0.6f;

        [Tooltip("Layers that count as ground and walls for the ground check. The player itself sits on Ignore Raycast.")]
        public LayerMask groundLayers = ~(1 << 2);
    }
}
