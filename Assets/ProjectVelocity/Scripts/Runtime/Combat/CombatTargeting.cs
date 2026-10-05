using System.Collections.Generic;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Soft targeting for the blade. Each frame it picks the one enemy the player most likely means, from where the camera
    /// looks, where they're moving, distance, height and line of sight, so Attack can lunge at it. Only enemies a short way
    /// ahead qualify. No lock-on: it never moves the camera or the player by itself.
    /// Feel values live in Combat Tuning (Target Selection); <see cref="CombatController"/> runs it each frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatTargeting : MonoBehaviour
    {
        // Below this horizontal speed (m/s) there's no direction of travel to go by: the camera alone decides.
        const float MovingSpeed = 2f;
        // An enemy at least this far (m) to the side counts for the travel direction; nearer, it's straight above or below.
        const float MinFlatDistance = 1f;

        [SerializeField] VelocityMotor motor;

        [Tooltip("The view the player looks through (the main camera). Selection favours what's near the centre of it.")]
        [SerializeField] Transform viewpoint;

        CombatEnemy selected;
        float selectedDistance;
        float selectedAngle;
        CombatEnemy pulseSelected;

        public VelocityMotor Motor
        {
            get => motor;
            set => motor = value;
        }

        public Transform Viewpoint
        {
            get => viewpoint;
            set => viewpoint = value;
        }

        /// <summary>The enemy Attack will lunge at (or is lunging at), or null.</summary>
        public CombatEnemy Selected => selected;

        /// <summary>Distance to the selected enemy (m). Debug readout.</summary>
        public float SelectedDistance => selectedDistance;

        /// <summary>Blended camera/travel angle to the selected enemy (degrees). Debug readout.</summary>
        public float SelectedAngle => selectedAngle;

        /// <summary>
        /// The enemy (or switch) the pulse would fire at: beyond lunge range, up to Pulse Range, close to the middle of your
        /// intent. Null while an enemy is selected for the blade. Not highlighted on the enemy itself (the HUD marks it).
        /// </summary>
        public CombatEnemy PulseSelected => pulseSelected;

        void Awake()
        {
            if (motor == null)
                motor = GetComponent<VelocityMotor>();
            if (viewpoint == null && Camera.main != null)
                viewpoint = Camera.main.transform;
        }

        void OnDisable()
        {
            Select(null, 0f, 0f);
            pulseSelected = null;
        }

        /// <summary>
        /// Updates the selection. While a lunge homes in on <paramref name="locked"/>, that enemy stays the selection. With
        /// <paramref name="findPulseTarget"/>, also picks the pulse target when no enemy is selected for the blade.
        /// </summary>
        public void Tick(CombatTuning t, CombatEnemy locked, bool findPulseTarget = false)
        {
            pulseSelected = null;
            if (motor == null || t == null)
            {
                Select(null, 0f, 0f);
                return;
            }

            Vector3 eye = motor.BodyCenter;
            if (locked != null && locked.IsAlive)
            {
                Select(locked, Vector3.Distance(eye, locked.HurtboxCenter), 0f);
                return;
            }

            bool hasView = viewpoint != null;
            Vector3 travel = motor.PlanarVelocity;
            bool moving = travel.sqrMagnitude > MovingSpeed * MovingSpeed;

            // Intent = a weighted blend of the camera's aim and the direction of travel.
            float viewWeight = hasView ? t.cameraWeight : 0f;
            float travelWeight = moving ? t.movementWeight : 0f;
            if (viewWeight + travelWeight <= 0f)
            {
                if (hasView)
                    viewWeight = 1f;
                else if (moving)
                    travelWeight = 1f;
                else
                {
                    Select(null, 0f, 0f);
                    return;
                }
            }

            float range = t.attackRange;
            bool grounded = motor.IsGrounded;
            CombatEnemy best = null;
            float bestScore = float.MaxValue;
            float bestDistance = 0f;
            float bestAngle = 0f;

            IReadOnlyList<CombatEnemy> enemies = CombatEnemy.Active;
            for (int i = 0; i < enemies.Count; i++)
            {
                CombatEnemy enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                Vector3 centre = enemy.HurtboxCenter;
                Vector3 toEnemy = centre - eye;
                float sqrDistance = toEnemy.sqrMagnitude;
                if (range <= 0f || sqrDistance > range * range)
                    continue;

                // On the ground the lunge stays on the ground, so only what the blade reaches from there. In the air, not far
                // above (a lunge is not a lift: jump for high ones), but further below is fine (diving onto it).
                float bladeHeight = t.hitReach + enemy.HurtboxRadius;
                float maxAbove = grounded ? bladeHeight : t.maxHeightAbove;
                float maxBelow = grounded ? bladeHeight : t.maxHeightBelow;
                if (toEnemy.y > maxAbove || -toEnemy.y > maxBelow)
                    continue;

                float distance = Mathf.Sqrt(sqrDistance);
                float angle = 0f;
                if (viewWeight > 0f)
                    angle += viewWeight * Vector3.Angle(viewpoint.forward, centre - viewpoint.position);
                if (travelWeight > 0f)
                {
                    var flat = new Vector3(toEnemy.x, 0f, toEnemy.z);
                    angle += travelWeight * (flat.sqrMagnitude > MinFlatDistance * MinFlatDistance ? Vector3.Angle(travel, flat) : 0f);
                }
                angle /= viewWeight + travelWeight;
                if (angle > t.selectionAngle)
                    continue;

                // Lower is better: well aligned and (by Distance Preference) near. The current pick gets a head start.
                float score = Mathf.Lerp(angle / t.selectionAngle, distance / range, t.distancePreference);
                if (enemy == selected)
                    score -= t.selectionStickiness;
                if (score >= bestScore)
                    continue;

                // Reachable: nothing solid in between (its own body doesn't count).
                if (Blocked(eye, centre, enemy, t.blockingLayers))
                    continue;

                best = enemy;
                bestScore = score;
                bestDistance = distance;
                bestAngle = angle;
            }

            Select(best, bestDistance, bestAngle);
            if (best == null && findPulseTarget)
                pulseSelected = FindPulseTarget(t, eye, viewWeight, travelWeight, travel);
        }

        CombatEnemy FindPulseTarget(CombatTuning t, Vector3 eye, float viewWeight, float travelWeight, Vector3 travel)
        {
            CombatEnemy best = null;
            float bestScore = float.MaxValue;
            float minRange = t.attackRange * 0.5f;
            IReadOnlyList<CombatEnemy> enemies = CombatEnemy.Active;
            for (int i = 0; i < enemies.Count; i++)
            {
                CombatEnemy enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;
                Vector3 centre = enemy.HurtboxCenter;
                Vector3 toEnemy = centre - eye;
                float sqrDistance = toEnemy.sqrMagnitude;
                if (sqrDistance > t.pulseRange * t.pulseRange || sqrDistance < minRange * minRange)
                    continue;

                float angle = 0f;
                if (viewWeight > 0f)
                    angle += viewWeight * Vector3.Angle(viewpoint.forward, centre - viewpoint.position);
                if (travelWeight > 0f)
                {
                    var flat = new Vector3(toEnemy.x, 0f, toEnemy.z);
                    angle += travelWeight * (flat.sqrMagnitude > MinFlatDistance * MinFlatDistance ? Vector3.Angle(travel, flat) : 0f);
                }
                angle /= viewWeight + travelWeight;
                if (angle > t.pulseSelectionAngle)
                    continue;

                float score = angle / t.pulseSelectionAngle + Mathf.Sqrt(sqrDistance) / t.pulseRange * 0.3f;
                if (score >= bestScore)
                    continue;
                if (Blocked(eye, centre, enemy, t.blockingLayers))
                    continue;
                best = enemy;
                bestScore = score;
            }
            return best;
        }

        /// <summary>Whether something other than the enemy's own colliders (a heavy's armour) stands between the eye and it.</summary>
        static bool Blocked(Vector3 eye, Vector3 centre, CombatEnemy enemy, LayerMask layers)
        {
            if (!Physics.Linecast(eye, centre, out RaycastHit hit, layers, QueryTriggerInteraction.Ignore))
                return false;
            return !hit.collider.transform.IsChildOf(enemy.transform);
        }

        void Select(CombatEnemy enemy, float distance, float angle)
        {
            selectedDistance = enemy != null ? distance : 0f;
            selectedAngle = enemy != null ? angle : 0f;
            if (enemy == selected)
                return;

            if (selected != null)
                selected.SetSelected(false);
            selected = enemy;
            if (selected != null)
                selected.SetSelected(true);
        }
    }
}
