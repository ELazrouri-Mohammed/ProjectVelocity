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
        // Extra room (m) left between the player and a piece that shoved them.
        const float ClearanceMargin = 0.02f;
        // How far (m) a hazard must have pushed into the player for its hit to count (grazing it doesn't).
        const float HitDepth = 0.03f;
        // How far (m) a piece may still be inside the player after the shoves before it counts as crushing them.
        const float CrushDepth = 0.15f;
        // Shove passes per frame, so a player wedged in a moving corner settles into it instead of being called crushed.
        const int ShovePasses = 3;
        // How long (s) the HUD keeps showing why the player was last reset.
        const float FailureShownFor = 3f;

        static readonly List<RealityTransformSequence> active = new List<RealityTransformSequence>();
        static string lastFailure;
        static float lastFailureTime = float.NegativeInfinity;

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

        /// <summary>Why the player was last reset by moving architecture, for a few seconds afterwards; otherwise null. Debug readout.</summary>
        public static string RecentFailure => Time.time - lastFailureTime < FailureShownFor ? lastFailure : null;

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
            lastFailure = null;
            lastFailureTime = float.NegativeInfinity;
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
                if (ResolvePlayer())
                    return; // the player was reset, and every stage with them (this one included)
            }
            if (done)
                phase = Phase.Complete;
        }

        /// <summary>
        /// Deals with pieces that moved into the player this frame. Returns true when the player was reset.
        /// 1. A hazard in mid-move that has pushed into them: hit, reset.
        /// 2. Anything else: shove them out of it the shortest way (position only: the motor's speed is left alone).
        /// 3. Still inside something after the shoves: pinned between moving architecture and something else. Crushed, reset.
        /// </summary>
        bool ResolvePlayer()
        {
            if (body == null)
            {
                if (player == null)
                    return false;
                body = player.GetComponent<CharacterController>();
                if (body == null)
                    return false;
            }
            if (!body.enabled)
                return false;

            RealityContact.Capsule(body, out Vector3 top, out Vector3 bottom, out float radius);
            for (int i = 0; i < chunks.Length; i++)
            {
                if (movedThisFrame[i] && chunks[i].IsLethal && chunks[i].IsMoving &&
                    DeepestOverlap(chunks[i], top, bottom, radius, out _, out _) > HitDepth)
                {
                    Fail("hit by " + chunks[i].name);
                    return true;
                }
            }

            for (int pass = 0; pass < ShovePasses; pass++)
            {
                bool shoved = false;
                for (int i = 0; i < chunks.Length; i++)
                {
                    if (!movedThisFrame[i])
                        continue;
                    if (DeepestOverlap(chunks[i], top, bottom, radius, out Vector3 direction, out float depth) > 0f)
                    {
                        body.Move(direction * (depth + ClearanceMargin));
                        RealityContact.Capsule(body, out top, out bottom, out radius);
                        shoved = true;
                    }
                }
                if (!shoved)
                    return false;
            }

            for (int i = 0; i < chunks.Length; i++)
            {
                if (movedThisFrame[i] && DeepestOverlap(chunks[i], top, bottom, radius, out _, out _) > CrushDepth)
                {
                    Fail("crushed by " + chunks[i].name);
                    return true;
                }
            }
            return false;
        }

        /// <summary>The deepest overlap (m, 0 when none) between the capsule and any of a piece's boxes, and the way out of it.</summary>
        static float DeepestOverlap(RealityChunk chunk, Vector3 top, Vector3 bottom, float radius, out Vector3 direction, out float depth)
        {
            direction = Vector3.zero;
            depth = 0f;
            BoxCollider[] boxes = chunk.Boxes;
            for (int j = 0; j < boxes.Length; j++)
            {
                BoxCollider box = boxes[j];
                if (box == null || !box.enabled)
                    continue;
                if (RealityContact.Penetration(top, bottom, radius, box, out Vector3 way, out float amount) && amount > depth)
                {
                    depth = amount;
                    direction = way;
                }
            }
            return depth;
        }

        void Fail(string reason)
        {
            lastFailure = reason;
            lastFailureTime = Time.time;
            player.Respawn();
        }

        float StartTime(int index)
        {
            return startDelay + index * stagger + chunks[index].Delay;
        }
    }
}
