using System.Collections.Generic;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Soft lock for tether anchors, built for thumbs: each frame it picks the one anchor the player most likely means from
    /// where the camera looks, where they're moving, distance, height and line of sight. The selection angle is wide on purpose,
    /// so nobody has to aim on a touch screen. It never moves the camera or the player. Feel values live in Tether Tuning.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TetherTargeting : MonoBehaviour
    {
        const float MovingSpeed = 2f;
        const float MinFlatDistance = 1f;

        [SerializeField] VelocityMotor motor;

        [Tooltip("The view the player looks through (the main camera).")]
        [SerializeField] Transform viewpoint;

        TetherAnchor selected;
        float selectedScore;
        float selectedDistance;

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

        /// <summary>The anchor LINK would hook onto (or is hooked onto), or null.</summary>
        public TetherAnchor Selected => selected;

        /// <summary>How well the selection matches intent: 0 = dead centre, 1 = edge of the selection angle. Lower is better.</summary>
        public float SelectedScore => selectedScore;

        public float SelectedDistance => selectedDistance;

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

        /// <summary>Updates the selection. Called once per frame by <see cref="TetherController"/> before the motor moves.</summary>
        public void Tick(TetherTuning t)
        {
            if (motor == null || t == null)
            {
                Select(null, 0f, 0f);
                return;
            }

            Vector3 eye = motor.BodyCenter;
            TetherAnchor attached = motor.TetherAnchorPoint;
            if (attached != null)
            {
                Select(attached, 0f, Vector3.Distance(eye, attached.Position));
                return;
            }

            bool hasView = viewpoint != null;
            Vector3 travel = motor.PlanarVelocity;
            bool moving = travel.sqrMagnitude > MovingSpeed * MovingSpeed;
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

            TetherAnchor best = null;
            float bestScore = float.MaxValue;
            float bestDistance = 0f;
            float bestRaw = 0f;

            IReadOnlyList<TetherAnchor> anchors = TetherAnchor.Active;
            for (int i = 0; i < anchors.Count; i++)
            {
                TetherAnchor anchor = anchors[i];
                if (anchor == null || !anchor.IsReady)
                    continue;

                Vector3 toAnchor = anchor.Position - eye;
                float distance = toAnchor.magnitude;
                float range = t.range * anchor.RangeMultiplier;
                if (range <= 0f || distance > range || distance < t.minDistance)
                    continue;

                float angle = 0f;
                if (viewWeight > 0f)
                    angle += viewWeight * Vector3.Angle(viewpoint.forward, anchor.Position - viewpoint.position);
                if (travelWeight > 0f)
                {
                    var flat = new Vector3(toAnchor.x, 0f, toAnchor.z);
                    angle += travelWeight * (flat.sqrMagnitude > MinFlatDistance * MinFlatDistance ? Vector3.Angle(travel, flat) : 0f);
                }
                angle /= viewWeight + travelWeight;
                if (angle > t.selectionAngle)
                    continue;

                float raw = angle / t.selectionAngle;
                float score = Mathf.Lerp(raw, distance / range, t.distancePreference);
                if (toAnchor.y > 2f && !anchor.PullsIn)
                    score -= t.abovePreference;
                if (anchor == selected)
                    score -= t.selectionStickiness;
                if (score >= bestScore)
                    continue;

                if (Physics.Linecast(eye, anchor.Position, t.blockingLayers, QueryTriggerInteraction.Ignore))
                    continue;

                best = anchor;
                bestScore = score;
                bestDistance = distance;
                bestRaw = raw;
            }

            Select(best, bestRaw, bestDistance);
        }

        void Select(TetherAnchor anchor, float score, float distance)
        {
            selectedScore = anchor != null ? score : 0f;
            selectedDistance = anchor != null ? distance : 0f;
            if (anchor == selected)
                return;
            if (selected != null)
                selected.SetSelected(false);
            selected = anchor;
            if (selected != null)
                selected.SetSelected(true);
        }
    }
}
