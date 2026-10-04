using System.Collections.Generic;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Soft lock for traversal targets. Each frame it picks the one target the player most likely means, from where the
    /// camera looks, where they're moving, distance and line of sight, and hands it to the motor when the activate button
    /// is pressed. No aiming: it never moves the camera or the player by itself.
    /// Feel values live in Movement Tuning (Traversal Targets: Selection).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TraversalTargeting : MonoBehaviour
    {
        // Below this horizontal speed (m/s) there's no direction of travel to go by: the camera alone decides.
        const float MovingSpeed = 2f;
        // A target at least this far (m) to the side counts for the travel direction; nearer, it's straight above or below.
        const float MinFlatDistance = 1f;

        [SerializeField] VelocityMotor motor;

        [Tooltip("The view the player looks through (the main camera). Selection favours what's near the centre of it.")]
        [SerializeField] Transform viewpoint;

        TraversalTarget selected;
        float selectedDistance;
        float selectedAngle;

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

        /// <summary>The target the activate button will launch through (or is launching through), or null.</summary>
        public TraversalTarget Selected => selected;

        /// <summary>Distance to the selected target (m). Debug readout.</summary>
        public float SelectedDistance => selectedDistance;

        /// <summary>Blended camera/travel angle to the selected target (degrees). Debug readout.</summary>
        public float SelectedAngle => selectedAngle;

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
        }

        /// <summary>
        /// Called once per frame by the player controller, before the motor moves. Updates the selection, and returns the
        /// target to launch through when <paramref name="activatePressed"/> and one can be used right now; otherwise null.
        /// </summary>
        public TraversalTarget Tick(bool activatePressed)
        {
            UpdateSelection();
            if (!activatePressed || selected == null || motor == null || !motor.CanActivateTarget)
                return null;
            return selected;
        }

        void UpdateSelection()
        {
            if (motor == null)
            {
                Select(null, 0f, 0f);
                return;
            }

            Vector3 eye = motor.BodyCenter;

            // Mid-pull, the target you're flying through stays the only active one.
            TraversalTarget pulling = motor.PullTarget;
            if (pulling != null)
            {
                Select(pulling, Vector3.Distance(eye, pulling.Position), 0f);
                return;
            }

            MovementTuning t = motor.Settings;
            bool hasView = viewpoint != null;
            Vector3 travel = motor.PlanarVelocity;
            bool moving = travel.sqrMagnitude > MovingSpeed * MovingSpeed;

            // Intent = a weighted blend of the camera's aim and the direction of travel.
            float viewWeight = hasView ? t.targetCameraWeight : 0f;
            float travelWeight = moving ? t.targetMovementWeight : 0f;
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

            TraversalTarget best = null;
            float bestScore = float.MaxValue;
            float bestDistance = 0f;
            float bestAngle = 0f;

            IReadOnlyList<TraversalTarget> targets = TraversalTarget.Active;
            for (int i = 0; i < targets.Count; i++)
            {
                TraversalTarget target = targets[i];
                if (target == null || !target.IsReady)
                    continue;

                Vector3 toTarget = target.Position - eye;
                float distance = toTarget.magnitude;
                float range = t.targetDetectionRange * target.RangeMultiplier;
                if (range <= 0f || distance > range || distance < t.targetMinDistance)
                    continue;

                float angle = 0f;
                if (viewWeight > 0f)
                    angle += viewWeight * Vector3.Angle(viewpoint.forward, target.Position - viewpoint.position);
                if (travelWeight > 0f)
                {
                    var flat = new Vector3(toTarget.x, 0f, toTarget.z);
                    angle += travelWeight * (flat.sqrMagnitude > MinFlatDistance * MinFlatDistance ? Vector3.Angle(travel, flat) : 0f);
                }
                angle /= viewWeight + travelWeight;
                if (angle > t.targetSelectionAngle)
                    continue;

                // Lower is better: well aligned and (by Distance Preference) near. The current pick gets a head start.
                float score = Mathf.Lerp(angle / t.targetSelectionAngle, distance / range, t.targetDistancePreference);
                if (target == selected)
                    score -= t.targetSelectionStickiness;
                if (score >= bestScore)
                    continue;

                // Reachable: nothing solid between you and it.
                if (Physics.Linecast(eye, target.Position, t.targetBlockingLayers, QueryTriggerInteraction.Ignore))
                    continue;

                best = target;
                bestScore = score;
                bestDistance = distance;
                bestAngle = angle;
            }

            Select(best, bestDistance, bestAngle);
        }

        void Select(TraversalTarget target, float distance, float angle)
        {
            selectedDistance = target != null ? distance : 0f;
            selectedAngle = target != null ? angle : 0f;
            if (target == selected)
                return;

            if (selected != null)
                selected.SetSelected(false);
            selected = target;
            if (selected != null)
                selected.SetSelected(true);
        }
    }
}
