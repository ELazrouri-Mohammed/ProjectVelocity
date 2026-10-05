using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// One stretch of the slice, from its checkpoint to the next: everything under this object (reality stages, their triggers,
    /// enemies, switches) belongs to it. When the player restarts at checkpoint N, the <see cref="SliceDirector"/> puts every
    /// segment before N in its finished state (stages complete, enemies down) and resets segment N and everything after it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SliceSegment : MonoBehaviour
    {
        [Tooltip("Order along the route: the index of the checkpoint this segment starts at.")]
        [SerializeField, Min(0)] int index;

        RealityTransformSequence[] sequences;
        RealityTrigger[] triggers;
        CombatEnemy[] enemies;

        public int Index
        {
            get => index;
            set => index = Mathf.Max(0, value);
        }

        void Awake()
        {
            Collect();
        }

        void Collect()
        {
            if (sequences != null)
                return;
            sequences = GetComponentsInChildren<RealityTransformSequence>(true);
            triggers = GetComponentsInChildren<RealityTrigger>(true);
            enemies = GetComponentsInChildren<CombatEnemy>(true);
        }

        /// <summary>Everything back to how it starts: stages dormant and armed, enemies alive at their posts.</summary>
        public void ResetSegment()
        {
            Collect();
            foreach (RealityTrigger trigger in triggers)
            {
                if (trigger != null)
                    trigger.Rearm();
            }
            foreach (RealityTransformSequence sequence in sequences)
            {
                if (sequence != null)
                    sequence.ResetToStart();
            }
            foreach (CombatEnemy enemy in enemies)
            {
                if (enemy != null)
                    enemy.Revive();
            }
        }

        /// <summary>Everything as the player left it: stages played to the end, triggers spent, enemies gone.</summary>
        public void CompleteSegment()
        {
            Collect();
            foreach (RealityTrigger trigger in triggers)
            {
                if (trigger != null)
                    trigger.Disarm();
            }
            foreach (RealityTransformSequence sequence in sequences)
            {
                if (sequence != null)
                    sequence.CompleteInstantly();
            }
            foreach (CombatEnemy enemy in enemies)
            {
                if (enemy != null)
                    enemy.Dismiss();
            }
        }
    }
}
