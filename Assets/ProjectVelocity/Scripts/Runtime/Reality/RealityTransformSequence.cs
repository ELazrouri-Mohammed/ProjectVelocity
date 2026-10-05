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
    /// frame, so the player always moves against this frame's architecture. When a piece moves into the player:
    /// a hazard (<see cref="RealityChunk.IsLethal"/>) resets them to the start of the section; anything else shoves them out
    /// of it the shortest way (blocked, never launched: no speed is added), and if that leaves them pinned between it and
    /// something else, they're crushed and reset too. Resets go through <see cref="VelocityPlayerController.Respawn"/>.
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
        VelocityMotor motor;
        bool[] movedThisFrame;

        /// <summary>Raised when the stage starts playing.</summary>
        public event System.Action<RealityTransformSequence> Started;

        /// <summary>Every enabled sequence. Debug readout.</summary>
        public static IReadOnlyList<RealityTransformSequence> Active => active;

        /// <summary>Why the player was last reset by moving architecture, for a few seconds afterwards; otherwise null. Debug readout.</summary>
        public static string RecentFailure => KinematicResolver.RecentFailure;

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

        /// <summary>The pieces this stage moves.</summary>
        public RealityChunk[] Chunks => chunks;

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
            Started?.Invoke(this);
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

        /// <summary>Every piece straight to its destination, as if the stage had played (restarting past it).</summary>
        public void CompleteInstantly()
        {
            phase = Phase.Complete;
            elapsed = TotalDuration;
            for (int i = 0; chunks != null && i < chunks.Length; i++)
            {
                if (chunks[i] != null)
                    chunks[i].SnapToFinal();
            }
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
                if (ResolvePlayer())
                    return; // the player was reset, and every stage with them (this one included)
            }
            if (done)
                phase = Phase.Complete;
        }

        /// <summary>
        /// Deals with pieces that moved this frame (see <see cref="KinematicResolver"/>): carries a player standing on one,
        /// fails them if a hazard hits them, shoves them out of the way, fails them if they end up pinned. Returns true when the
        /// player failed.
        /// </summary>
        bool ResolvePlayer()
        {
            if (body == null)
            {
                if (player == null)
                    return false;
                body = player.GetComponent<CharacterController>();
                motor = player.GetComponent<VelocityMotor>();
                if (body == null)
                    return false;
            }
            return KinematicResolver.Resolve(player, body, motor, chunks, movedThisFrame, chunks.Length);
        }

        float StartTime(int index)
        {
            return startDelay + index * stagger + chunks[index].Delay;
        }
    }
}
