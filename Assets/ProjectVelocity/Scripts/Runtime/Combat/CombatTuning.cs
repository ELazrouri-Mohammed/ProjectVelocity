using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Every number that shapes the blade attack: reach, soft targeting, the lunge and the kill reward. Stored as an asset so
    /// tweaks made while playing are kept after leaving Play Mode.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Velocity/Combat Tuning", fileName = "CombatTuning")]
    public sealed class CombatTuning : ScriptableObject
    {
        [Header("Attack")]
        [Tooltip("Attack cooldown: time between attacks (s), counted from when one starts.")]
        [Min(0f)] public float attackCooldown = 0.35f;

        [Tooltip("After a kill the next attack is ready this soon (s), so you can cut through enemies in a row.")]
        [Min(0f)] public float killCooldown = 0.1f;

        [Tooltip("Pressing Attack up to this long (s) before it's ready still attacks the moment it is. 0 = off.")]
        [Min(0f)] public float attackBufferTime = 0.1f;

        [Tooltip("Blade reach (m), from the body's centre to an enemy's hurtbox. An enemy this close is cut at once; " +
                 "a selected enemy further away is lunged at first.")]
        [Min(0f)] public float hitReach = 2.2f;

        [Tooltip("How long (s) a swing keeps cutting: any enemy that comes within reach in that time dies too.")]
        [Min(0f)] public float slashActiveTime = 0.12f;

        [Tooltip("How far (degrees) to either side of the attack direction a swing still cuts. 180 = all around you.")]
        [Range(0f, 180f)] public float hitAngle = 100f;

        [Header("Target Selection (soft targeting)")]
        [Tooltip("Attack range: farthest (m) an enemy can be selected and lunged at. Keep it short: no snapping from far away.")]
        [Min(0f)] public float attackRange = 9f;

        [Tooltip("Target selection angle: how far off your intent (degrees) an enemy may be and still be selected. Intent blends " +
                 "where the camera looks with where you're moving (the two weights below).")]
        [Range(1f, 90f)] public float selectionAngle = 35f;

        [Tooltip("Camera weighting: how much where the camera looks counts toward intent.")]
        [Min(0f)] public float cameraWeight = 0.5f;

        [Tooltip("Movement weighting: how much your direction of travel counts toward intent. Ignored while standing still.")]
        [Min(0f)] public float movementWeight = 0.5f;

        [Tooltip("Between two valid enemies, how much the nearer one is preferred over the better-aligned one. " +
                 "0 = alignment only, 1 = distance only.")]
        [Range(0f, 1f)] public float distancePreference = 0.4f;

        [Tooltip("How much better another enemy must score (0-1 scale) before it takes the selection from the current one. " +
                 "Stops the selection flickering, and stops a far enemy stealing it.")]
        [Range(0f, 1f)] public float selectionStickiness = 0.15f;

        [Tooltip("In the air, how far (m) above your body's centre an enemy can be and still be lunged at. A lunge isn't a lift: " +
                 "you have to jump for enemies higher than this. (On the ground the lunge stays on the ground, so only enemies " +
                 "the blade can reach from there count.)")]
        [Min(0f)] public float maxHeightAbove = 3f;

        [Tooltip("In the air, how far (m) below your body's centre an enemy can be and still be lunged at (diving onto it).")]
        [Min(0f)] public float maxHeightBelow = 7f;

        [Tooltip("Layers that block line of sight to an enemy: one behind a wall can't be selected. The player itself sits on Ignore Raycast.")]
        public LayerMask blockingLayers = ~(1 << 2);

        [Header("Lunge")]
        [Tooltip("Lunge amount: speed (m/s) of the dash at a selected enemy beyond blade reach. Already moving faster? " +
                 "You keep your speed: the lunge never slows you down.")]
        [Min(0f)] public float lungeSpeed = 30f;

        [Tooltip("Longest a lunge lasts (s) before giving up and swinging anyway. You keep its speed either way.")]
        [Min(0.01f)] public float lungeMaxDuration = 0.3f;

        [Tooltip("How fast the lunge turns your current direction of travel toward the enemy (degrees per second). " +
                 "Your velocity is turned, never zeroed, and you're never teleported. Lower = a wider curve in.")]
        [Min(0f)] public float lungeTurnRate = 900f;

        [Header("Kill Reward (movement)")]
        [Tooltip("A kill gives back ONE spent air boost. If yours is still available, nothing extra is added.")]
        public bool killRefreshesAirBoost = true;

        [Tooltip("A kill also skips what's left of the boost cooldown, so you can boost straight out of it.")]
        public bool killReadiesBoost = true;

        [Tooltip("Extra speed (m/s) added along your direction of travel after a kill (once per attack). " +
                 "0 = momentum is only kept, never added.")]
        [Min(0f)] public float killSpeedBonus = 3f;

        [Tooltip("Tiny camera feedback on a kill: the field of view widens by this many degrees and eases back. 0 = off. " +
                 "The camera is never turned or locked.")]
        [Range(0f, 10f)] public float killFovKick = 2f;
    }
}
