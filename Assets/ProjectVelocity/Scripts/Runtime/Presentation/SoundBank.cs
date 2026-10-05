using System;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>Every sound the slice plays. Add new ones at the end so existing bank entries keep their meaning.</summary>
    public enum SoundId
    {
        Footstep,
        Jump,
        Land,
        LandHard,
        Boost,
        WallRun,
        WallJump,
        Slash,
        SlashHit,
        Kill,
        HeavyKill,
        Deflect,
        PulseFire,
        PulseHit,
        TetherFire,
        TetherConnect,
        TetherRelease,
        TargetLaunch,
        EnemyCharge,
        EnemyFire,
        BoltImpact,
        HoundScreech,
        HoundDash,
        WardenCharge,
        WardenSlam,
        EnemyDeath,
        ShieldBreak,
        ShieldRestore,
        Death,
        Checkpoint,
        Finish,
        WorldWarning,
        WorldRumble,
        WorldImpact,
        Crusher,
        Parry,
        AmbientLoop,
        WindLoop,
    }

    /// <summary>
    /// Which clip plays for each <see cref="SoundId"/>, and how loud. The builder fills empty slots with generated placeholder
    /// sounds and never overwrites a clip you assigned, so replacing a sound is: drag your clip into its slot.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Velocity/Sound Bank", fileName = "SoundBank")]
    public sealed class SoundBank : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public SoundId id;
            public AudioClip clip;
            [Range(0f, 1.5f)] public float volume = 1f;
            [Tooltip("Random pitch variation (+/-), so repeats don't sound mechanical.")]
            [Range(0f, 0.5f)] public float pitchVariance = 0.05f;
        }

        [SerializeField] Entry[] entries = new Entry[0];

        [NonSerialized] Entry[] lookup;

        public Entry[] Entries
        {
            get => entries;
            set
            {
                entries = value;
                lookup = null;
            }
        }

        /// <summary>The entry for a sound, or null when it has none.</summary>
        public Entry Get(SoundId id)
        {
            if (lookup == null)
            {
                int count = Enum.GetValues(typeof(SoundId)).Length;
                lookup = new Entry[count];
                if (entries != null)
                {
                    foreach (Entry e in entries)
                    {
                        if (e == null)
                            continue;
                        int i = (int)e.id;
                        if (i >= 0 && i < count)
                            lookup[i] = e;
                    }
                }
            }
            int index = (int)id;
            return index >= 0 && index < lookup.Length ? lookup[index] : null;
        }

        void OnValidate()
        {
            lookup = null;
        }
    }
}
