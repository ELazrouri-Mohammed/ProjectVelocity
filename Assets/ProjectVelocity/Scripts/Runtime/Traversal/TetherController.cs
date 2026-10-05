using System.Collections.Generic;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>What the LINK button does this frame.</summary>
    public struct LinkCommand
    {
        /// <summary>Tether to attach, or none.</summary>
        public MotorTether Attach;

        /// <summary>Let go of the tether.</summary>
        public bool Release;

        /// <summary>Launch through the selected traversal target instead.</summary>
        public bool UseTarget;
    }

    /// <summary>
    /// The LINK button, contextual: pressing it hooks the tether onto the selected anchor, or launches through the selected
    /// traversal target, whichever the player more likely means (closest to the middle of their intent). Holding it keeps the
    /// rope; letting go releases it with all the swing's speed. Zip anchors reel you in and let go on arrival; an Enemy Strike
    /// anchor zips you onto a light enemy and the blade cuts it down on arrival (a movement kill).
    /// The player controller runs it each frame before the motor moves. Feel values live in Tether Tuning.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TetherController : MonoBehaviour
    {
        [Tooltip("Tether tuning asset. Changes made to it in Play Mode are kept when you stop playing.")]
        [SerializeField] TetherTuning tuning;

        [SerializeField] VelocityMotor motor;
        [SerializeField] TetherTargeting targeting;

        [Tooltip("Cuts down the enemy an Enemy Strike zip arrives at. Optional.")]
        [SerializeField] CombatController combat;

        [Tooltip("Where the rope leaves the body (the free hand). Optional: the body centre otherwise.")]
        [SerializeField] Transform hand;

        TetherTuning fallbackTuning;

        public TetherTuning Tuning
        {
            get => tuning;
            set => tuning = value;
        }

        public VelocityMotor Motor
        {
            get => motor;
            set => motor = value;
        }

        public TetherTargeting Targeting
        {
            get => targeting;
            set => targeting = value;
        }

        public CombatController Combat
        {
            get => combat;
            set => combat = value;
        }

        public Transform Hand
        {
            get => hand;
            set => hand = value;
        }

        /// <summary>Where the rope leaves the body.</summary>
        public Vector3 HandPosition => hand != null ? hand.position : motor != null ? motor.BodyCenter : transform.position;

        public TetherTuning Settings
        {
            get
            {
                if (tuning != null)
                    return tuning;
                if (fallbackTuning == null)
                    fallbackTuning = ScriptableObject.CreateInstance<TetherTuning>();
                return fallbackTuning;
            }
        }

        void Awake()
        {
            if (motor == null)
                motor = GetComponent<VelocityMotor>();
            if (targeting == null)
                targeting = GetComponent<TetherTargeting>();
            if (combat == null)
                combat = GetComponent<CombatController>();
        }

        void OnEnable()
        {
            if (motor != null)
                motor.TetherReleased += OnReleased;
        }

        void OnDisable()
        {
            if (motor != null)
                motor.TetherReleased -= OnReleased;
        }

        void OnDestroy()
        {
            if (fallbackTuning != null)
                Destroy(fallbackTuning);
        }

        /// <summary>
        /// Called once per frame by the player controller, before the motor moves. <paramref name="target"/> is the selected
        /// traversal target (or null) and <paramref name="targetScore"/> how well it matches intent (0 = centre, 1 = edge).
        /// </summary>
        public LinkCommand Tick(bool linkPressed, bool linkHeld, TraversalTarget target, float targetScore)
        {
            var command = new LinkCommand();
            if (motor == null)
            {
                command.UseTarget = linkPressed && target != null;
                return command;
            }

            TetherTuning t = Settings;
            if (targeting != null)
                targeting.Tick(t);

            if (motor.IsTethering)
            {
                // Let go of the button, or press it again with another finger: let go of the rope.
                if (!linkHeld || linkPressed)
                    command.Release = true;
                return command;
            }

            if (!linkPressed)
                return command;

            TetherAnchor anchor = targeting != null ? targeting.Selected : null;
            bool anchorWins = anchor != null && motor.CanTether && (target == null || targeting.SelectedScore <= targetScore);
            if (anchorWins)
                command.Attach = new MotorTether { Anchor = anchor, Settings = t };
            else
                command.UseTarget = target != null;
            return command;
        }

        /// <summary>Every anchor ready again (on respawn).</summary>
        public void ResetTether()
        {
            IReadOnlyList<TetherAnchor> anchors = TetherAnchor.Active;
            for (int i = 0; i < anchors.Count; i++)
            {
                if (anchors[i] != null)
                    anchors[i].ResetCooldown();
            }
        }

        void OnReleased(TetherAnchor anchor, VelocityMotor.TetherExit exit)
        {
            if (exit != VelocityMotor.TetherExit.Arrived || anchor == null || anchor.Kind != TetherAnchor.AnchorKind.EnemyStrike)
                return;
            if (combat != null && anchor.StrikeTarget != null && anchor.StrikeTarget.IsAlive)
                combat.StrikeKill(anchor.StrikeTarget);
        }
    }
}
