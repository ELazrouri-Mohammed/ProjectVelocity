using System.Collections.Generic;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Plays a set of <see cref="RealityChunk"/>s once, in order, as a wave: after Start Delay the first piece moves, and each
    /// next one starts Stagger seconds after the one before (plus its own Extra Delay). Started by a
    /// <see cref="RealityTrigger"/>; <see cref="ResetToStart"/> snaps every piece back at once.
    /// The world changes around the player: it never touches the player, the camera, the controls or time.
    /// Runs before the player each frame, so the player always moves against this frame's architecture.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class RealityTransformSequence : MonoBehaviour
    {
        public enum Phase
        {
            Dormant,
            Playing,
            Complete,
        }

        static readonly List<RealityTransformSequence> active = new List<RealityTransformSequence>();

        [Tooltip("The pieces, in the order they move.")]
        [SerializeField] RealityChunk[] chunks;

        [Header("Timing")]
        [Tooltip("Seconds between the trigger and the first piece moving.")]
        [SerializeField, Min(0f)] float startDelay = 0.3f;

        [Tooltip("Seconds between one piece starting and the next (the wave). Each piece can add its own Extra Delay.")]
        [SerializeField, Min(0f)] float stagger = 0.8f;

        [Tooltip("Scales every piece's Duration. Below 1 = the whole transformation is quicker.")]
        [SerializeField, Min(0.05f)] float durationScale = 1f;

        [Header("Placeholder Feedback")]
        [Tooltip("When the sequence starts, every piece lights up for this long (s), so you see the whole route react at once.")]
        [SerializeField, Min(0f)] float wakePulse = 0.3f;

        [Tooltip("A piece stays lit this long (s) after landing in place, then settles.")]
        [SerializeField, Min(0f)] float landingFlash = 0.25f;

        Phase phase = Phase.Dormant;
        float elapsed;

        /// <summary>Every enabled sequence. Debug readout.</summary>
        public static IReadOnlyList<RealityTransformSequence> Active => active;

        public Phase State => phase;

        /// <summary>Seconds since the sequence started (stops counting once complete).</summary>
        public float Elapsed => elapsed;

        /// <summary>Seconds from the start until the last piece has landed.</summary>
        public float TotalDuration
        {
            get
            {
                float end = 0f;
                for (int i = 0; chunks != null && i < chunks.Length; i++)
                {
                    if (chunks[i] != null)
                        end = Mathf.Max(end, StartTime(i) + chunks[i].Duration * durationScale);
                }
                return end;
            }
        }

        public float StartDelay
        {
            get => startDelay;
            set => startDelay = Mathf.Max(0f, value);
        }

        public float Stagger
        {
            get => stagger;
            set => stagger = Mathf.Max(0f, value);
        }

        public float DurationScale
        {
            get => durationScale;
            set => durationScale = Mathf.Max(0.05f, value);
        }

        // Survives play mode without a domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry()
        {
            active.Clear();
        }

        /// <summary>Sets the pieces, in the order they move (used by the movement test builder).</summary>
        public void SetChunks(RealityChunk[] pieces)
        {
            chunks = pieces;
        }

        /// <summary>Starts the transformation. Does nothing unless it is dormant: it plays once until reset.</summary>
        [ContextMenu("Play")]
        public void Play()
        {
            if (phase != Phase.Dormant)
                return;
            phase = Phase.Playing;
            elapsed = 0f;
        }

        /// <summary>Every piece back to its initial pose at once, ready to play again.</summary>
        [ContextMenu("Reset To Start")]
        public void ResetToStart()
        {
            phase = Phase.Dormant;
            elapsed = 0f;
            for (int i = 0; chunks != null && i < chunks.Length; i++)
            {
                if (chunks[i] != null)
                    chunks[i].SnapToInitial();
            }
            // Colliders follow their transforms only at the next physics step: catch them up now.
            Physics.SyncTransforms();
        }

        void Awake()
        {
            // Whatever pose the scene was saved in, play starts from the beginning.
            ResetToStart();
        }

        void OnEnable()
        {
            if (!active.Contains(this))
                active.Add(this);
        }

        void OnDisable()
        {
            active.Remove(this);
        }

        void Update()
        {
            if (phase != Phase.Playing)
                return;

            elapsed += Time.deltaTime;
            bool pulse = elapsed < wakePulse;
            bool moved = false;
            bool done = true;
            for (int i = 0; chunks != null && i < chunks.Length; i++)
            {
                RealityChunk chunk = chunks[i];
                if (chunk == null)
                    continue;
                moved |= chunk.Sample(elapsed - StartTime(i), durationScale, pulse, landingFlash, out bool finished);
                done &= finished;
            }

            // The player moves later this frame: its collision checks must see the pieces where they are now.
            if (moved)
                Physics.SyncTransforms();
            if (done)
                phase = Phase.Complete;
        }

        float StartTime(int index)
        {
            return startDelay + index * stagger + chunks[index].ExtraDelay;
        }
    }
}
