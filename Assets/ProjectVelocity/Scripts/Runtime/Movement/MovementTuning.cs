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

        [Header("Wall Run")]
        [Tooltip("Horizontal speed (m/s) needed to catch a wall while airborne. Slower than this, walls just block you as before.")]
        [Min(0f)] public float wallRunMinEntrySpeed = 10f;

        [Tooltip("Speed along the wall (m/s) a wall run settles at. Arriving faster keeps the extra speed, which bleeds off via Wall Run Momentum Decay.")]
        [Min(0f)] public float wallRunSpeed = 26f;

        [Tooltip("How quickly a slower wall run builds up to Wall Run Speed (m/s²).")]
        [Min(0f)] public float wallRunAcceleration = 20f;

        [Tooltip("When faster than Wall Run Speed, how fast the extra speed bleeds away on the wall (m/s²). Lower = boosted wall runs stay fast longer.")]
        [Min(0f)] public float wallRunMomentumDecay = 4f;

        [Tooltip("You drop off the wall when your speed along it falls below this (m/s) and you are no longer rising.")]
        [Min(0f)] public float wallRunMinSpeed = 8f;

        [Tooltip("Longest time (s) you can stay on one wall per airtime. Landing or catching a different wall resets it.")]
        [Min(0f)] public float maxWallRunDuration = 1.6f;

        [Tooltip("Gravity while on a wall (m/s²). Well below normal gravity so runs arc instead of drop, but you always sink.")]
        [Min(0f)] public float wallGravity = 18f;

        [Tooltip("Fastest you sink while running on a wall (m/s). Early in a run you barely sink; this limit is reached as the run nears Max Wall Run Duration.")]
        [Min(0f)] public float wallMaxSlideSpeed = 4f;

        [Tooltip("Upward speed (m/s) a wall run starts with when you catch the wall level or falling, so it arcs up first instead of sagging.")]
        [Min(0f)] public float wallRunEntryLift = 4f;

        [Tooltip("Share of the speed you carry into a wall that is redirected (along it or up it) instead of lost on impact. 1 = no loss.")]
        [Range(0f, 1f)] public float wallEntryRedirect = 0.8f;

        [Tooltip("Catching a wall gives back your air boosts, so wall jump → boost chains work.")]
        public bool wallRunRefreshesAirBoosts = true;

        [Header("Wall Climb (upward wall run)")]
        [Tooltip("Hitting a wall steeper than this (degrees, 90 = head-on) turns part of your speed into an upward run. Shallower hits run along the wall.")]
        [Range(0f, 90f)] public float wallClimbMinAngle = 45f;

        [Tooltip("Upward wall-run strength: the fastest upward speed (m/s) a climb can give. How much of it you get depends on how fast you hit the wall.")]
        [Min(0f)] public float wallClimbSpeed = 22f;

        [Tooltip("Upward wall-run limit: seconds of rising per wall (per airtime) that use Wall Gravity. After that normal gravity takes over and the climb dies quickly.")]
        [Min(0f)] public float wallClimbDuration = 0.4f;

        [Header("Wall Steering")]
        [Tooltip("Point the stick at least this many degrees away from the wall to peel off it, keeping your speed. Smaller = easier to leave.")]
        [Range(5f, 90f)] public float wallReleaseAngle = 45f;

        [Tooltip("Braking (m/s²) while pulling the stick back against the run. Drop below Wall Run Min Speed and you fall off.")]
        [Min(0f)] public float wallBrake = 40f;

        [Header("Wall Jump")]
        [Tooltip("Speed (m/s) a wall jump pushes you away from the wall. Your speed along the wall is added on top.")]
        [Min(0f)] public float wallJumpAwayForce = 16f;

        [Tooltip("Upward speed (m/s) of a wall jump. If you were already rising faster, that is kept instead.")]
        [Min(0f)] public float wallJumpUpForce = 16f;

        [Tooltip("Share of your speed along the wall kept through a wall jump. 1 = all of it.")]
        [Range(0f, 1f)] public float wallJumpMomentumKeep = 1f;

        [Tooltip("After a wall jump, steering back toward that wall is damped for this long (s) so the kick carries you clear. Steering away from it is never damped.")]
        [Min(0f)] public float wallJumpCommitTime = 0.3f;

        [Tooltip("Grace time (s) after leaving a wall during which Jump still does a wall jump.")]
        [Min(0f)] public float wallJumpCoyoteTime = 0.2f;

        [Tooltip("Wall jump assist: how far ahead (m, along your travel direction) a wall jump looks for a wall you're steering toward. If the normal jump would carry you over that wall's top, the upward launch is lowered just enough to reach its face. Never raises the jump or changes direction or speed. 0 = off.")]
        [Min(0f)] public float wallJumpAssistRange = 16f;

        [Header("Wall Detection")]
        [Tooltip("How far beyond the body (m) walls are noticed. Larger catches walls earlier at speed; too large feels magnetic.")]
        [Min(0f)] public float wallDetectionDistance = 0.6f;

        [Tooltip("How far (degrees) a surface may lean from vertical and still count as a wall.")]
        [Range(0f, 40f)] public float maxWallTilt = 20f;

        [Tooltip("While falling, walls are only caught at least this high (m) above the ground, so you don't grab a wall just before landing.")]
        [Min(0f)] public float wallMinHeight = 1f;

        [Tooltip("Time (s) before you can catch the same wall again after leaving it. Other walls can be caught immediately.")]
        [Min(0f)] public float wallReattachCooldown = 0.4f;

        [Tooltip("Layers you can wall run on. The player itself sits on Ignore Raycast.")]
        public LayerMask wallRunLayers = ~(1 << 2);

        [Header("Traversal Targets: Selection (soft lock)")]
        [Tooltip("Farthest a traversal target can be selected from (m). Each target can scale this with its Range Multiplier.")]
        [Min(0f)] public float targetDetectionRange = 35f;

        [Tooltip("Targets closer than this (m) are ignored, so a target you're already on top of never grabs the selection.")]
        [Min(0f)] public float targetMinDistance = 3f;

        [Tooltip("Selection angle: how far off your intent (degrees) a target may be and still be selected. Intent blends where the camera looks with where you're moving (the two weights below).")]
        [Range(1f, 90f)] public float targetSelectionAngle = 40f;

        [Tooltip("Camera-direction weighting: how much where the camera looks counts toward intent (measured from the centre of the view).")]
        [Min(0f)] public float targetCameraWeight = 0.6f;

        [Tooltip("Movement-direction weighting: how much your direction of travel counts toward intent. Ignored while standing still.")]
        [Min(0f)] public float targetMovementWeight = 0.4f;

        [Tooltip("Between two valid targets, how much the nearer one is preferred over the better-aligned one. 0 = alignment only, 1 = distance only.")]
        [Range(0f, 1f)] public float targetDistancePreference = 0.3f;

        [Tooltip("How much better another target must score (0-1 scale) before it takes the selection from the current one. Stops the selection flickering between targets.")]
        [Range(0f, 1f)] public float targetSelectionStickiness = 0.15f;

        [Tooltip("Layers that block line of sight to a target: a target behind a wall can't be selected. The player itself sits on Ignore Raycast.")]
        public LayerMask targetBlockingLayers = ~(1 << 2);

        [Header("Traversal Targets: Propulsion")]
        [Tooltip("Propulsion strength: speed (m/s) a target pulls you through it at, and launches you out with. Each target can scale it with its Strength Multiplier. Arriving faster keeps your speed (see Momentum Keep).")]
        [Min(0f)] public float targetPropulsionSpeed = 46f;

        [Tooltip("Momentum preservation: share of your entry speed kept when you arrive faster than Propulsion Speed. 1 = a fast entry is never slowed down; 0 = the target always sets Propulsion Speed.")]
        [Range(0f, 1f)] public float targetMomentumKeep = 1f;

        [Tooltip("How quickly your speed builds toward Propulsion Speed on the way in (m/s²). 0 = instantly. The launch out is always at full speed.")]
        [Min(0f)] public float targetPullAcceleration = 200f;

        [Tooltip("How fast your direction of travel swings toward the target (degrees per second). Your velocity is turned, never zeroed. Lower = a wider, swooping curve in.")]
        [Min(0f)] public float targetRedirectRate = 1080f;

        [Tooltip("Arrival distance (m): within this distance of the target's centre you count as through it and are launched.")]
        [Min(0f)] public float targetArrivalDistance = 1.5f;

        [Tooltip("Pass-through: share of the pull speed carried out the far side. 1 = fly straight through at full speed (slingshot); lower = the target brakes you as you pass.")]
        [Range(0f, 1f)] public float targetExitSpeedKeep = 1f;

        [Tooltip("Upward bias: tilts the launch out of a target toward straight up. 0 = carry straight through, 1 = straight up. Speed is kept; only the direction changes. Each target can add Extra Upward Bias.")]
        [Range(0f, 1f)] public float targetUpwardBias = 0.15f;

        [Tooltip("When on, a launch never points downward: diving into a target comes out level, then Upward Bias applies.")]
        public bool targetLaunchLevelsOut = true;

        [Tooltip("Safety limit (s): if you haven't reached the target by then, the pull lets go and you keep your current speed.")]
        [Min(0.05f)] public float targetMaxPullTime = 1f;

        [Header("Traversal Targets: Chaining")]
        [Tooltip("Air boost refresh: activating a target gives back ONE air boost, so targets sustain long chains. Turn off to test without it.")]
        public bool targetRefreshesAirBoost = true;

        [Tooltip("Reactivation cooldown (s): after leaving a target, the activate button is ignored for this long, so a double press doesn't instantly fire the next one. (Each target also has its own cooldown before it can be reused.)")]
        [Min(0f)] public float targetReactivationCooldown = 0.15f;
    }
}
