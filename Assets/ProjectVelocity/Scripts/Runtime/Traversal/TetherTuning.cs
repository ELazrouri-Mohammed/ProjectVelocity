using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Every number that shapes the tether: which anchor the LINK button picks, how the swing carries you and what letting go
    /// gives back. Stored as an asset so tweaks made while playing are kept after leaving Play Mode.
    /// The swing is a controlled arc, not a physics rope: the rope only ever stops you moving away from the anchor, turning that
    /// motion into speed along the arc instead of losing it, so connecting at speed keeps (and rewards) the speed.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Velocity/Tether Tuning", fileName = "TetherTuning")]
    public sealed class TetherTuning : ScriptableObject
    {
        [Header("Selection (soft lock)")]
        [Tooltip("Farthest an anchor can be selected from (m). Each anchor can scale this with its Range Multiplier.")]
        [Min(0f)] public float range = 50f;

        [Tooltip("Anchors closer than this (m) are ignored, so the one you just swung on doesn't grab the selection.")]
        [Min(0f)] public float minDistance = 5f;

        [Tooltip("How far off your intent (degrees) an anchor may be and still be selected. Generous on purpose: touch screens " +
                 "don't aim precisely. Intent blends where the camera looks with where you're moving.")]
        [Range(1f, 90f)] public float selectionAngle = 55f;

        [Tooltip("How much where the camera looks counts toward intent.")]
        [Min(0f)] public float cameraWeight = 0.55f;

        [Tooltip("How much your direction of travel counts toward intent. Ignored while standing still.")]
        [Min(0f)] public float movementWeight = 0.45f;

        [Tooltip("Between two valid anchors, how much the nearer one is preferred over the better-aligned one. 0 = alignment only.")]
        [Range(0f, 1f)] public float distancePreference = 0.25f;

        [Tooltip("Anchors above you score this much better (0-1 scale) than level ones: swinging needs height.")]
        [Range(0f, 1f)] public float abovePreference = 0.15f;

        [Tooltip("How much better another anchor must score before it takes the selection. Stops flicker.")]
        [Range(0f, 1f)] public float selectionStickiness = 0.15f;

        [Tooltip("Layers that block line of sight to an anchor. The player itself sits on Ignore Raycast.")]
        public LayerMask blockingLayers = ~(1 << 2);

        [Header("Swing")]
        [Tooltip("The rope reels in to this share of the distance at the moment you connect, so it catches you at once and turns " +
                 "your speed into the arc instead of letting you drift first.")]
        [Range(0.5f, 1f)] public float lengthFactor = 0.88f;

        [Tooltip("Shortest rope (m).")]
        [Min(1f)] public float minLength = 7f;

        [Tooltip("Longest rope (m). Connecting from further away reels you in to this.")]
        [Min(1f)] public float maxLength = 34f;

        [Tooltip("How fast (m/s) the rope reels in to its length.")]
        [Min(0f)] public float reelSpeed = 32f;

        [Tooltip("Gravity on the swing (m/s²). A little stronger than normal gravity keeps the arc snappy, not floaty.")]
        [Min(0f)] public float gravity = 44f;

        [Tooltip("Connecting slower than this (m/s, horizontal) sets you to it along your heading: FLY → CONNECT → ARC, never stop → hang.")]
        [Min(0f)] public float minSwingSpeed = 26f;

        [Tooltip("Fastest the swing can carry you (m/s).")]
        [Min(0f)] public float maxSwingSpeed = 58f;

        [Tooltip("Share of your speed kept when the rope catches you. 1 = the rope only redirects, never brakes.")]
        [Range(0f, 1f)] public float speedKeep = 1f;

        [Tooltip("Extra push (m/s²) along the arc while it swings downward.")]
        [Min(0f)] public float swingAcceleration = 8f;

        [Tooltip("How fast the stick turns the swing's heading (degrees per second), around the rope.")]
        [Min(0f)] public float steerRate = 110f;

        [Tooltip("Upward speed (m/s) given when you connect from the ground, so the swing starts instead of dragging you.")]
        [Min(0f)] public float groundAttachLift = 8f;

        [Tooltip("A falling catch is cushioned: downward speed at the moment you connect is capped at this (m/s).")]
        [Min(0f)] public float maxCatchFallSpeed = 10f;

        [Tooltip("Longest a swing lasts (s) before the rope lets go by itself.")]
        [Min(0.1f)] public float maxDuration = 3.2f;

        [Tooltip("Once you swing this far above the anchor's height (degrees above horizontal, while still rising), the rope lets " +
                 "go by itself and launches you.")]
        [Range(0f, 90f)] public float autoReleaseAngle = 40f;

        [Tooltip("On the upswing, once your rise slows below this (m/s) the rope launches you from the top of the arc instead of " +
                 "swinging you back. Holding LINK never leaves you hanging.")]
        [Min(0f)] public float apexReleaseSpeed = 3f;

        [Header("Release")]
        [Tooltip("Extra speed (m/s) along your motion when you let go.")]
        [Min(0f)] public float releaseBoost = 5f;

        [Tooltip("Upward pop (m/s) when you let go while not falling fast, so a release flows into flight.")]
        [Min(0f)] public float releaseLift = 4f;

        [Tooltip("Pressing Jump while swinging lets go with at least this upward speed (m/s).")]
        [Min(0f)] public float jumpReleaseUpSpeed = 12f;

        [Tooltip("Connecting gives back one spent air boost, so swing → release → boost works.")]
        public bool refreshesAirBoost = true;

        [Tooltip("After letting go, the next tether can't attach for this long (s).")]
        [Min(0f)] public float reattachCooldown = 0.2f;

        [Header("Zip (pull anchors and enemy strikes)")]
        [Tooltip("Speed (m/s) a zip pulls you along the rope. Arriving faster keeps your speed.")]
        [Min(0f)] public float pullSpeed = 50f;

        [Tooltip("Within this distance (m) of a zip anchor you've arrived: the rope lets go with all your speed.")]
        [Min(0.1f)] public float pullArrivalDistance = 2.6f;

        [Tooltip("Longest a zip lasts (s).")]
        [Min(0.1f)] public float pullMaxDuration = 1.2f;

        [Header("Rope Visual")]
        [Tooltip("Time (s) the rope takes to fly from the hand to the anchor. The swing starts at once; this is only the look.")]
        [Min(0f)] public float fireTime = 0.07f;
    }
}
