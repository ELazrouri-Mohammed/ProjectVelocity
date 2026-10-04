using System.Collections.Generic;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// One stage of reality-changing architecture: plays its <see cref="RealityChunk"/>s once when its
    /// <see cref="RealityTrigger"/> fires. Each piece warns, then moves at its own time (Start Delay + its place in the
    /// order × Stagger + its own Delay), with its own duration and curve, so a stage can be a wave (Stagger) or a
    /// choreography (per-piece Delay). <see cref="ResetToStart"/> snaps every piece back at once.
    /// The world changes around the player: it never touches the camera, the controls or time. Runs before the player each
    /// frame, so the player always moves against this frame's architecture; and if a piece moved into the player, the
    /// player is shoved out of it the shortest way (blocked, never launched: no speed is added).
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

        // Longest step (s) per frame, matching the motor's: after a hitch the architecture doesn't jump ahead of the player.
        const float MaxStep = 0.05f;
        // Extra room (m) left between the player and a piece that shoved them.
        const float ClearanceMargin = 0.02f;

        static readonly List<RealityTransformSequence> active = new List<RealityTransformSequence>();

        [Tooltip("The pieces, in order (the order only matters with Stagger).")]
        [SerializeField] RealityChunk[] chunks;

        [Tooltip("Player kept clear of moving pieces. Found automatically when empty.")]
        [SerializeField] VelocityPlayerController player;

        [Header("Timing")]
        [Tooltip("Seconds between the trigger and the first piece's slot.")]
        [SerializeField, Min(0f)] float startDelay;

        [Tooltip("Seconds between one piece's slot and the next (a wave). 0 = every piece goes by its own Delay.")]
        [SerializeField, Min(0f)] float stagger;

        [Tooltip("Scales every piece's Duration. Below 1 = the whole stage is quicker.")]
        [SerializeField, Min(0.05f)] float durationScale = 1f;

        [Header("Placeholder Feedback")]
        [Tooltip("When the stage starts, every piece that is still waiting lights up for this long (s). 0 = off, so pieces only show themselves when they warn.")]
        [SerializeField, Min(0f)] float wakePulse;

        [Tooltip("A piece stays lit this long (s) after landing, then settles.")]
        [SerializeField, Min(0f)] float landingFlash = 0.25f;

        Phase phase = Phase.Dormant;
        float elapsed;
        CharacterController body;
        bool[] movedThisFrame;

        /// <summary>Every enabled sequence. Debug readout.</summary>
        public static IReadOnlyList<RealityTransformSequence> Active => active;

        public Phase State => phase;

        /// <summary>Seconds since the stage started (stops counting once complete).</summary>
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

        public VelocityPlayerController Player
        {
            get => player;
            set
            {
                player = value;
                body = null;
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

        public float WakePulse
        {
            get => wakePulse;
            set => wakePulse = Mathf.Max(0f, value);
        }

        // Survives play mode without a domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry()
        {
            active.Clear();
        }

        /// <summary>Sets the pieces (used by the movement test builder).</summary>
        public void SetChunks(RealityChunk[] pieces)
        {
            chunks = pieces;
            movedThisFrame = null;
        }

        /// <summary>Starts the stage. Does nothing unless it is dormant: it plays once until reset.</summary>
        [ContextMenu("Play")]
        public void Play()
        {
            if (phase != Phase.Dormant)
                return;
            phase = Phase.Playing;
            elapsed = 0f;
        }

        /// <summary>Every piece back to its initial pose and look at once, ready to play again.</summary>
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
            if (player == null)
                player = FindAnyObjectByType<VelocityPlayerController>();
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
            if (phase != Phase.Playing || chunks == null)
                return;
            if (movedThisFrame == null || movedThisFrame.Length != chunks.Length)
                movedThisFrame = new bool[chunks.Length];

            elapsed += Mathf.Min(Time.deltaTime, MaxStep);
            bool pulse = elapsed < wakePulse;
            bool moved = false;
            bool done = true;
            for (int i = 0; i < chunks.Length; i++)
            {
                RealityChunk chunk = chunks[i];
                movedThisFrame[i] = false;
                if (chunk == null)
                    continue;
                movedThisFrame[i] = chunk.Sample(elapsed - StartTime(i), durationScale, pulse, landingFlash, out bool finished);
                moved |= movedThisFrame[i];
                done &= finished;
            }

            // The player moves later this frame: its collision checks must see the pieces where they are now.
            if (moved)
            {
                Physics.SyncTransforms();
                KeepPlayerClear();
            }
            if (done)
                phase = Phase.Complete;
        }

        /// <summary>
        /// If a piece that moved this frame now overlaps the player, moves the player out of it the shortest way: like being
        /// shoved by a wall. Only the position changes; the motor's speed is left alone, so nothing is ever launched.
        /// </summary>
        void KeepPlayerClear()
        {
            if (body == null)
            {
                if (player == null)
                    return;
                body = player.GetComponent<CharacterController>();
                if (body == null)
                    return;
            }
            if (!body.enabled)
                return;

            Transform bodyTransform = body.transform;
            for (int i = 0; i < chunks.Length; i++)
            {
                if (!movedThisFrame[i])
                    continue;
                Collider[] parts = chunks[i].Colliders;
                for (int j = 0; j < parts.Length; j++)
                {
                    Collider part = parts[j];
                    if (part == null || !part.enabled)
                        continue;
                    Bounds reach = body.bounds;
                    reach.Expand(ClearanceMargin * 2f);
                    if (!part.bounds.Intersects(reach))
                        continue;
                    Transform partTransform = part.transform;
                    if (Physics.ComputePenetration(body, bodyTransform.position, bodyTransform.rotation,
                            part, partTransform.position, partTransform.rotation, out Vector3 direction, out float distance))
                        body.Move(direction * (distance + ClearanceMargin));
                }
            }
        }

        float StartTime(int index)
        {
            return startDelay + index * stagger + chunks[index].Delay;
        }
    }
}
